using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// 郵便番号の種類
    /// </summary>
    public enum ZipCodeKind
    {
        /// <summary>町域の郵便番号</summary>
        Town,

        /// <summary>大口事業所の個別郵便番号</summary>
        Business,

        /// <summary>私書箱の個別郵便番号</summary>
        PostOfficeBox
    }

    /// <summary>
    /// 郵便番号データの1町域、または1事業所
    /// </summary>
    public sealed class ZipCodeEntry
    {
        // 町域名の特殊な値
        // http://www.post.japanpost.jp/zipcode/dl/utf-readme.html
        private const string NotListed = "以下に掲載がない場合";
        private const string FollowedByBlockNumber = "の次に番地がくる場合";
        private const string WholeCity = "一円";

        // 郡名を含む市区町村名(「上川郡東神楽町」など。「大和郡山市」は対象外)
        private static readonly Regex DistrictRegex = new Regex("^.+?郡(?<name>.+[町村])$");

        // 事業所名の照合で区別しない会社の種類
        private static readonly string[] CompanyTypes = { "株式会社", "有限会社", "合同会社", "合資会社", "合名会社", "（株）", "(株)", "㈱", "（有）", "(有)", "㈲" };

        // 括弧書き: 超高層ビルの階(「１階」)
        private static readonly Regex FloorRegex = new Regex("^(?<floor>[0-9]+)階$");

        // 括弧書き: 地区名、地区名 + 番地の範囲、地区名 + その他(「稲田」「国見２３～２９８番地」「国見その他」)
        private static readonly Regex AreaRegex = new Regex("^(?<name>[^0-9、・〜～]+?)(?<tail>その他|[0-9].*)?$");

        /// <summary>
        /// 町域を生成する
        /// </summary>
        internal ZipCodeEntry(string zipCode, string prefecture, string city, string town, bool hasChome)
        {
            ZipCode = zipCode;
            Prefecture = prefecture;
            City = city;
            Town = town;
            Kind = ZipCodeKind.Town;
            ParseParenthesis(hasChome);
        }

        /// <summary>
        /// 事業所を生成する
        /// </summary>
        internal ZipCodeEntry(string zipCode, string prefecture, string city, string town, string block, string businessName, ZipCodeKind kind)
        {
            ZipCode = zipCode;
            Prefecture = prefecture;
            City = city;
            Town = town;
            Block = block;
            BusinessName = businessName;
            Kind = kind;
            NormalizedBusinessName = NormalizeBusinessName(businessName);
        }

        /// <summary>郵便番号(半角数字7桁)</summary>
        public string ZipCode { get; }

        /// <summary>都道府県名</summary>
        public string Prefecture { get; }

        /// <summary>市区町村名</summary>
        public string City { get; }

        /// <summary>町域名(郵便番号データの表記のまま)</summary>
        public string Town { get; }

        /// <summary>郵便番号の種類</summary>
        public ZipCodeKind Kind { get; }

        /// <summary>事業所名(大口事業所・私書箱の個別郵便番号の場合。町域の場合は null)</summary>
        public string BusinessName { get; }

        /// <summary>小字名、丁目、番地等(大口事業所・私書箱の個別郵便番号の場合。町域の場合は null)</summary>
        public string Block { get; }

        /// <summary>
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)かどうか
        /// </summary>
        public bool IsRepresentative => Kind == ZipCodeKind.Town && Town == NotListed;

        /// <summary>
        /// 大口事業所・私書箱の個別郵便番号かどうか
        /// </summary>
        public bool IsIndividual => Kind != ZipCodeKind.Town;

        /// <summary>
        /// 町域名の括弧書きの丁目・番地の範囲(「１～７丁目」など)。範囲でない場合は null
        /// </summary>
        internal BlockNumberRange BlockRange { get; private set; }

        /// <summary>
        /// 超高層ビルの階(「霞が関ビル（１階）」の1)。階でない場合は null
        /// </summary>
        internal int? Floor { get; private set; }

        /// <summary>
        /// 超高層ビルの「（地階・階層不明）」かどうか
        /// </summary>
        internal bool IsUnknownFloor { get; private set; }

        /// <summary>
        /// 超高層ビルかどうか(ビル名がわかった場合のみ)
        /// </summary>
        internal bool IsBuilding => NormalizedBuildingName != null;

        /// <summary>
        /// 照合用に正規化したビル名(「霞が関霞が関ビル」の「霞が関ビル」)
        /// </summary>
        internal string NormalizedBuildingName { get; private set; }

        /// <summary>
        /// 照合用に正規化した地区名(「音江町（稲田）」の「稲田」)。地区名でない場合は null
        /// </summary>
        internal string NormalizedArea { get; private set; }

        /// <summary>
        /// 括弧書きが「その他」「次のビルを除く」など(ほかに当てはまらない場合の郵便番号)かどうか
        /// </summary>
        internal bool IsOthers { get; private set; }

        /// <summary>
        /// 照合用に正規化した事業所名
        /// </summary>
        internal string NormalizedBusinessName { get; }

        // ビルの町域名(「霞が関霞が関ビル」の「霞が関」)
        private string buildingTownName;

        /// <summary>
        /// 照合用の町域名(特殊な値の場合は空、括弧書きは除く。超高層ビルはビル名を除く)
        /// </summary>
        internal string GetTownName()
        {
            if (buildingTownName != null) return buildingTownName;
            return GetTownNameWithBuilding();
        }

        /// <summary>
        /// 町域名(特殊な値の場合は空、括弧書きは除く。超高層ビルはビル名を含む)
        /// </summary>
        internal string GetTownNameWithBuilding()
        {
            if (IsIndividual) return Town;

            // 「○○市の次に番地がくる場合」「○○町一円」は町域名がない
            if (Town.EndsWith(FollowedByBlockNumber, StringComparison.Ordinal)) return "";
            // 「一円」という実在の町域名もあるため、市区町村名 + 一円 の場合のみ
            if (Town.Length > WholeCity.Length && Town.EndsWith(WholeCity, StringComparison.Ordinal)
                && City.EndsWith(Town.Substring(0, Town.Length - WholeCity.Length), StringComparison.Ordinal))
            {
                return "";
            }

            // 「北六条東（１～７丁目）」などの括弧書きを除く
            var paren = Town.IndexOf('（');
            return paren >= 0 ? Town.Substring(0, paren) : Town;
        }

        // 照合用に正規化した町域名(住所から郵便番号を探すときに初めて作る)
        private string normalizedTownName;

        /// <summary>
        /// 照合用に正規化した町域名(<see cref="GetTownName"/> を正規化したもの)
        /// </summary>
        internal string NormalizedTownName => normalizedTownName ?? (normalizedTownName = AddressMatcher.Normalize(GetTownName()));

        /// <summary>
        /// 超高層ビルの町域名とビル名を設定する
        /// </summary>
        /// <param name="townName">町域名(「霞が関」)。同じ市区町村にない場合は null</param>
        internal void SetBuildingTown(string townName)
        {
            if (townName == null)
            {
                // ビル名がわからない場合は、解釈できない括弧書きとして扱う
                Floor = null;
                IsUnknownFloor = false;
                return;
            }
            buildingTownName = townName;
            NormalizedBuildingName = AddressMatcher.Normalize(GetTownNameWithBuilding().Substring(townName.Length));
        }

        /// <summary>
        /// 超高層ビルの階の括弧書きかどうか(ビル名を設定する前)
        /// </summary>
        internal bool HasFloor => Floor != null || IsUnknownFloor;

        /// <summary>
        /// 町域名の括弧書きを解析する
        /// </summary>
        /// <param name="hasChome">丁目を有する町域かどうか</param>
        private void ParseParenthesis(bool hasChome)
        {
            var start = Town.IndexOf('（');
            var end = Town.LastIndexOf('）');
            if (start < 0 || end <= start) return;
            var paren = string.Concat(Town.Substring(start + 1, end - start - 1).Select(BarCode.ToHankaku));

            if (paren == "地階・階層不明")
            {
                IsUnknownFloor = true;
                return;
            }
            var floor = FloorRegex.Match(paren);
            if (floor.Success)
            {
                Floor = int.Parse(floor.Groups["floor"].Value, CultureInfo.InvariantCulture);
                return;
            }

            var range = BlockNumberRange.Parse(paren);
            if (range != null)
            {
                BlockRange = AcceptRange(range, hasChome);
                return;
            }

            if (paren == "その他" || paren.Contains("除く"))
            {
                IsOthers = true;
                return;
            }

            var area = AreaRegex.Match(paren);
            if (!area.Success) return;
            var name = area.Groups["name"].Value;
            if (name.StartsWith("大字", StringComparison.Ordinal)) name = name.Substring(2);
            else if (name.StartsWith("字", StringComparison.Ordinal)) name = name.Substring(1);
            if (name.Length == 0 || name.Contains("その他") || name.Contains("除く") || name.Contains("階") || name.Contains("（")) return;

            var tail = area.Groups["tail"].Value;
            if (tail == "その他")
            {
                IsOthers = true;
            }
            else if (tail.Length > 0)
            {
                var areaRange = BlockNumberRange.Parse(tail);
                if (areaRange == null) return;
                BlockRange = AcceptRange(areaRange, hasChome);
            }
            NormalizedArea = AddressMatcher.Normalize(name);
        }

        /// <summary>
        /// 丁目を有する町域の番地の範囲は、住所の最初の数値(丁目)と比べられないため扱わない
        /// </summary>
        private static BlockNumberRange AcceptRange(BlockNumberRange range, bool hasChome)
        {
            return range.IsChome || !hasChome ? range : null;
        }

        /// <summary>
        /// 照合用の事業所名の正規化(空白、全角・半角、「株式会社」「（株）」などの違いは区別しない)
        /// </summary>
        /// <param name="name">事業所名</param>
        /// <returns>正規化した事業所名</returns>
        internal static string NormalizeBusinessName(string name)
        {
            var normalized = AddressMatcher.Normalize(name);
            foreach (var word in CompanyTypes)
            {
                normalized = normalized.Replace(word, "");
            }
            return normalized;
        }

        /// <summary>
        /// 市区町村名までの住所の候補
        /// 都道府県名ありとなし、郡名ありとなし
        /// </summary>
        internal IEnumerable<string> GetCityCandidates()
        {
            yield return Prefecture + City;
            yield return City;

            var district = DistrictRegex.Match(City);
            if (district.Success)
            {
                yield return Prefecture + district.Groups["name"].Value;
                yield return district.Groups["name"].Value;
            }
        }

        /// <summary>
        /// 住所A(町域名までの住所)の候補
        /// 市区町村名までの住所の候補 + 町域名(前に「大字」「字」ありとなし。郵便番号データの町域名には付いていない)
        /// </summary>
        internal IEnumerable<string> GetTownAddressCandidates()
        {
            // 「以下に掲載がない場合」は町域名がわからないため候補なし
            if (IsRepresentative) yield break;

            var town = GetTownName();
            var towns = town.Length == 0 ? new[] { town } : new[] { town, "大字" + town, "字" + town };
            foreach (var city in GetCityCandidates())
            {
                foreach (var t in towns)
                {
                    yield return city + t;
                }
            }
        }
    }

    /// <summary>
    /// 町域名の括弧書きの丁目・番地の範囲(「１～７丁目」「１・３丁目」「１～５００番地」「１００番地以上」など)
    /// </summary>
    internal sealed class BlockNumberRange
    {
        // 数値または数値の範囲を「、」「・」で列挙し、最後に単位
        private static readonly Regex RangeRegex = new Regex(@"^(?<items>[0-9]+([〜～][0-9]+)?([、・][0-9]+([〜～][0-9]+)?)*)(?<unit>丁目|番地|番)(?<compare>以上|以下)?$");

        private readonly (int From, int To)[] ranges;

        private BlockNumberRange((int From, int To)[] ranges, bool isChome)
        {
            this.ranges = ranges;
            IsChome = isChome;
        }

        /// <summary>
        /// 単位が丁目かどうか(丁目でなければ番地)
        /// </summary>
        public bool IsChome { get; }

        /// <summary>
        /// 範囲に含まれるかどうか
        /// </summary>
        public bool Contains(int number) => ranges.Any(x => x.From <= number && number <= x.To);

        /// <summary>
        /// 括弧書きの中身を解析する
        /// </summary>
        /// <param name="text">括弧書きの中身</param>
        /// <returns>範囲。範囲として解析できない場合は null</returns>
        public static BlockNumberRange Parse(string text)
        {
            var match = RangeRegex.Match(string.Concat(text.Select(BarCode.ToHankaku)));
            if (!match.Success) return null;

            var items = match.Groups["items"].Value.Split('、', '・').Select(item =>
            {
                var bounds = item.Split('〜', '～').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                return (From: bounds[0], To: bounds[bounds.Length - 1]);
            }).ToArray();

            switch (match.Groups["compare"].Value)
            {
                case "以上":
                    if (items.Length != 1) return null;
                    items[0].To = int.MaxValue;
                    break;
                case "以下":
                    if (items.Length != 1) return null;
                    items[0].From = 0;
                    break;
            }
            return new BlockNumberRange(items, match.Groups["unit"].Value == "丁目");
        }
    }
}
