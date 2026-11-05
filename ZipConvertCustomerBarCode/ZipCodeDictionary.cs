using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// 郵便番号データ
    /// 日本郵便の「住所の郵便番号(1レコード1行、UTF-8形式)」(utf_ken_all.csv)を読み込む
    /// </summary>
    /// <remarks>
    /// データは同梱していないため、利用側でダウンロードして指定する。
    /// https://www.post.japanpost.jp/zipcode/dl/utf-zip.html
    /// 従来の Shift_JIS 形式(KEN_ALL.CSV)は、町域名が複数レコードに分割されている場合があるため対象外。
    /// 大口事業所の個別郵便番号は含まれない。
    /// </remarks>
    public sealed class ZipCodeDictionary
    {
        // 列数
        private const int ColumnCount = 15;

        private readonly Dictionary<string, ZipCodeEntry[]> entries;

        private ZipCodeDictionary(Dictionary<string, ZipCodeEntry[]> entries)
        {
            this.entries = entries;
        }

        /// <summary>
        /// 郵便番号の件数
        /// </summary>
        public int Count => entries.Count;

        /// <summary>
        /// 郵便番号データを読み込む
        /// </summary>
        /// <param name="path">utf_ken_all.csv、またはそれを含む zip ファイル(utf_ken_all.zip)のパス</param>
        /// <returns>郵便番号データ</returns>
        /// <exception cref="InvalidDataException">郵便番号データの形式が正しくない場合</exception>
        public static ZipCodeDictionary Load(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            using (var file = File.OpenRead(path))
            {
                if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return Load(file);
                }

                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var csv = archive.Entries.FirstOrDefault(x => x.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("zipファイルにCSVファイルがありません。");
                    using (var stream = csv.Open())
                    {
                        return Load(stream);
                    }
                }
            }
        }

        /// <summary>
        /// 郵便番号データを読み込む
        /// </summary>
        /// <param name="stream">utf_ken_all.csv の内容(UTF-8)。読み込み後も閉じないため、呼び出し側で破棄する</param>
        /// <returns>郵便番号データ</returns>
        /// <exception cref="InvalidDataException">郵便番号データの形式が正しくない場合</exception>
        public static ZipCodeDictionary Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var entries = new Dictionary<string, List<ZipCodeEntry>>();
            // 都道府県名・市区町村名は種類が少ないため、同じ文字列を共有してメモリを減らす
            var names = new Dictionary<string, string>();
            string Share(string name)
            {
                if (names.TryGetValue(name, out var shared)) return shared;
                names.Add(name, name);
                return name;
            }

            using (var reader = new StreamReader(stream, new UTF8Encoding(false), true, 4096, true))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (line.Length == 0) continue;

                    // 各項目は","区切りで、文字列項目は「"」で囲まれている(項目内に","はない)
                    var fields = line.Split(',');
                    if (fields.Length != ColumnCount)
                    {
                        throw InvalidFormat(lineNumber, "列数が15ではありません");
                    }
                    // Shift_JIS のファイルを UTF-8 として読むと置換文字になる
                    if (line.IndexOf('�') >= 0)
                    {
                        throw InvalidFormat(lineNumber, "UTF-8 として読めない文字があります");
                    }

                    var zipCode = Unquote(fields[2]);
                    if (zipCode.Length != 7 || !zipCode.All(c => c >= '0' && c <= '9'))
                    {
                        throw InvalidFormat(lineNumber, "郵便番号が7桁の数値ではありません");
                    }

                    // 7:都道府県名 8:市区町村名 9:町域名(漢字)
                    var entry = new ZipCodeEntry(zipCode, Share(Unquote(fields[6])), Share(Unquote(fields[7])), Unquote(fields[8]));
                    if (!entries.TryGetValue(zipCode, out var list))
                    {
                        list = new List<ZipCodeEntry>();
                        entries.Add(zipCode, list);
                    }
                    list.Add(entry);
                }
            }
            return new ZipCodeDictionary(entries.ToDictionary(x => x.Key, x => x.Value.ToArray()));
        }

        /// <summary>
        /// 郵便番号データに含まれるかどうか
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public bool Contains(string zipCode)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            return FindEntries(BarCode.CheckPostCode(zipCode)) != null;
        }

        /// <summary>
        /// 郵便番号の町域を返す
        /// </summary>
        /// <param name="zip">郵便番号(半角数字7桁)</param>
        /// <returns>町域。郵便番号データにない場合は null</returns>
        internal ZipCodeEntry[] FindEntries(string zip)
        {
            return entries.TryGetValue(zip, out var list) ? list : null;
        }

        /// <summary>
        /// 郵便番号の町域を返す(1つの郵便番号に複数の町域がある場合がある)
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <returns>町域(郵便番号データにない場合は空)</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public IReadOnlyList<ZipCodeEntry> Find(string zipCode)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            return Array.AsReadOnly(FindEntries(BarCode.CheckPostCode(zipCode)) ?? new ZipCodeEntry[0]);
        }

        /// <summary>
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)かどうか
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <returns>代表の郵便番号なら true。郵便番号データにない場合(事業所の個別番号など)は null</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public bool? IsRepresentative(string zipCode)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            var list = FindEntries(BarCode.CheckPostCode(zipCode));
            if (list == null) return null;
            return list.Any(x => x.IsRepresentative);
        }

        /// <summary>
        /// 郵便番号の町域名で、住所を住所A(町域名まで)と住所B(町域名以降)に分割する
        /// </summary>
        /// <remarks>
        /// 都道府県名の省略、町域名の前の「大字」「字」の有無、空白、全角・半角、「ヶ」と「ケ」の違いは区別しない。
        /// 町域名の括弧書き(「北六条東(1～7丁目)」の「(1～7丁目)」など)は除いて照合する。
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)は町域名がわからないため分割しない。
        /// </remarks>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <param name="address">住所</param>
        /// <param name="townAddress">住所A(分割できない場合は null)</param>
        /// <param name="blockAddress">住所B(分割できない場合は null)</param>
        /// <returns>分割できた場合は true。郵便番号データにない場合、代表の郵便番号の場合、町域名が一致しない場合は false</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public bool TrySplitAddress(string zipCode, string address, out string townAddress, out string blockAddress)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            if (address == null) throw new ArgumentNullException(nameof(address));

            var list = FindEntries(BarCode.CheckPostCode(zipCode));
            if (list == null)
            {
                townAddress = null;
                blockAddress = null;
                return false;
            }
            return TrySplitAddress(list, address, out townAddress, out blockAddress);
        }

        /// <summary>
        /// 町域名で住所を分割する
        /// </summary>
        /// <param name="list">郵便番号の町域</param>
        /// <param name="address">住所</param>
        /// <param name="townAddress">住所A(分割できない場合は null)</param>
        /// <param name="blockAddress">住所B(分割できない場合は null)</param>
        /// <returns>分割できた場合は true</returns>
        internal static bool TrySplitAddress(ZipCodeEntry[] list, string address, out string townAddress, out string blockAddress)
        {
            townAddress = null;
            blockAddress = null;

            // 最も長く一致した町域名で分割する
            var end = list.SelectMany(x => x.GetTownAddressCandidates())
                .Select(x => AddressMatcher.MatchPrefix(address, x))
                .DefaultIfEmpty(-1)
                .Max();
            if (end < 0) return false;

            townAddress = address.Substring(0, end).Trim();
            blockAddress = address.Substring(end);
            return true;
        }

        /// <summary>
        /// 「"」で囲まれた項目から「"」を除く
        /// </summary>
        private static string Unquote(string field)
        {
            return field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"' ? field.Substring(1, field.Length - 2) : field;
        }

        /// <summary>
        /// 形式エラー
        /// </summary>
        private static InvalidDataException InvalidFormat(int lineNumber, string reason)
        {
            return new InvalidDataException($"郵便番号データの{lineNumber}行目の形式が正しくありません({reason})。UTF-8形式の utf_ken_all.csv を指定してください。");
        }
    }

    /// <summary>
    /// 郵便番号データの1町域
    /// </summary>
    public sealed class ZipCodeEntry
    {
        // 町域名の特殊な値
        // http://www.post.japanpost.jp/zipcode/dl/utf-readme.html
        private const string NotListed = "以下に掲載がない場合";
        private const string FollowedByBlockNumber = "の次に番地がくる場合";
        private const string WholeCity = "一円";

        internal ZipCodeEntry(string zipCode, string prefecture, string city, string town)
        {
            ZipCode = zipCode;
            Prefecture = prefecture;
            City = city;
            Town = town;
        }

        /// <summary>郵便番号(半角数字7桁)</summary>
        public string ZipCode { get; }

        /// <summary>都道府県名</summary>
        public string Prefecture { get; }

        /// <summary>市区町村名</summary>
        public string City { get; }

        /// <summary>町域名(郵便番号データの表記のまま)</summary>
        public string Town { get; }

        /// <summary>
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)かどうか
        /// </summary>
        public bool IsRepresentative => Town == NotListed;

        /// <summary>
        /// 照合用の町域名(特殊な値の場合は空、括弧書きは除く)
        /// </summary>
        internal string GetTownName()
        {
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

        /// <summary>
        /// 住所A(町域名までの住所)の候補
        /// 都道府県名ありとなし、町域名の前に「大字」「字」ありとなし(郵便番号データの町域名には付いていない)
        /// </summary>
        internal IEnumerable<string> GetTownAddressCandidates()
        {
            // 「以下に掲載がない場合」は町域名がわからないため候補なし
            if (IsRepresentative) yield break;

            var town = GetTownName();
            var towns = town.Length == 0 ? new[] { town } : new[] { town, "大字" + town, "字" + town };
            foreach (var t in towns)
            {
                yield return Prefecture + City + t;
                yield return City + t;
            }
        }
    }
}
