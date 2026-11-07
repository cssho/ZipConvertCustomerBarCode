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
    /// 日本郵便の「住所の郵便番号(1レコード1行、UTF-8形式)」(utf_ken_all.csv)と、
    /// 「事業所の個別郵便番号」(JIGYOSYO.CSV)を読み込む
    /// </summary>
    /// <remarks>
    /// データは同梱していないため、利用側でダウンロードして指定する。
    /// https://www.post.japanpost.jp/zipcode/dl/utf-zip.html
    /// https://www.post.japanpost.jp/zipcode/dl/jigyosyo/index-zip.html
    /// 住所の郵便番号の従来の Shift_JIS 形式(KEN_ALL.CSV)は、町域名が複数レコードに分割されている場合があるため対象外。
    /// </remarks>
    public sealed class ZipCodeDictionary
    {
        // 列数
        private const int TownColumnCount = 15;
        private const int BusinessColumnCount = 13;

        // 町域名の前の「大字」「字」(郵便番号データの町域名には付いていない)
        private static readonly string[] TownPrefixes = { "大字", "字" };

        // 都道府県名(文字コードが違うファイルを読み込んでいないかの確認に使う)
        private static readonly HashSet<string> Prefectures = new HashSet<string>
        {
            "北海道", "青森県", "岩手県", "宮城県", "秋田県", "山形県", "福島県",
            "茨城県", "栃木県", "群馬県", "埼玉県", "千葉県", "東京都", "神奈川県",
            "新潟県", "富山県", "石川県", "福井県", "山梨県", "長野県", "岐阜県", "静岡県", "愛知県",
            "三重県", "滋賀県", "京都府", "大阪府", "兵庫県", "奈良県", "和歌山県",
            "鳥取県", "島根県", "岡山県", "広島県", "山口県",
            "徳島県", "香川県", "愛媛県", "高知県",
            "福岡県", "佐賀県", "長崎県", "熊本県", "大分県", "宮崎県", "鹿児島県", "沖縄県"
        };

        // 郵便番号 → 町域・事業所
        private readonly Dictionary<string, ZipCodeEntry[]> entries;

        // 市区町村
        private readonly City[] cities;

        // 市区町村ごとの事業所
        private readonly Dictionary<string, ZipCodeEntry[]> businesses;

        private ZipCodeDictionary(Dictionary<string, ZipCodeEntry[]> entries)
        {
            this.entries = entries;
            var all = entries.Values.SelectMany(x => x).ToList();
            // 事業所だけがある市区町村も、事業所を探せるように含める
            cities = all.GroupBy(CityKey)
                .Select(x => new City(x.Key, x.Where(e => !e.IsIndividual).ToArray(), x.First().GetCityCandidates().Select(AddressMatcher.Normalize).ToArray()))
                .ToArray();
            businesses = all.Where(x => x.IsIndividual)
                .GroupBy(CityKey)
                .ToDictionary(x => x.Key, x => x.ToArray());

            foreach (var city in cities)
            {
                SetBuildingTowns(city.Towns);
            }
        }

        /// <summary>
        /// 郵便番号の件数(事業所の個別郵便番号を含む)
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
            return OpenCsv(path, stream => Load(stream));
        }

        /// <summary>
        /// 郵便番号データと、事業所の個別郵便番号データを読み込む
        /// </summary>
        /// <remarks>
        /// 事業所の個別郵便番号データは Shift_JIS のため、.NET Core / .NET 5 以降では
        /// 事前に Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) を呼び出す。
        /// </remarks>
        /// <param name="path">utf_ken_all.csv、またはそれを含む zip ファイル(utf_ken_all.zip)のパス</param>
        /// <param name="businessPath">JIGYOSYO.CSV、またはそれを含む zip ファイル(jigyosyo.zip)のパス</param>
        /// <returns>郵便番号データ</returns>
        /// <exception cref="InvalidDataException">郵便番号データの形式が正しくない場合</exception>
        /// <exception cref="InvalidOperationException">Shift_JIS が使えない場合</exception>
        public static ZipCodeDictionary Load(string path, string businessPath)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (businessPath == null) throw new ArgumentNullException(nameof(businessPath));

            var encoding = GetShiftJis();
            return OpenCsv(path, stream => OpenCsv(businessPath, businessStream => Load(stream, businessStream, encoding)));
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

            var reader = new EntryReader();
            reader.ReadTowns(stream);
            return reader.Create();
        }

        /// <summary>
        /// 郵便番号データと、事業所の個別郵便番号データを読み込む
        /// </summary>
        /// <param name="stream">utf_ken_all.csv の内容(UTF-8)。読み込み後も閉じないため、呼び出し側で破棄する</param>
        /// <param name="businessStream">JIGYOSYO.CSV の内容。読み込み後も閉じないため、呼び出し側で破棄する</param>
        /// <param name="businessEncoding">JIGYOSYO.CSV の文字コード(Shift_JIS)</param>
        /// <returns>郵便番号データ</returns>
        /// <exception cref="InvalidDataException">郵便番号データの形式が正しくない場合</exception>
        public static ZipCodeDictionary Load(Stream stream, Stream businessStream, Encoding businessEncoding)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (businessStream == null) throw new ArgumentNullException(nameof(businessStream));
            if (businessEncoding == null) throw new ArgumentNullException(nameof(businessEncoding));

            var reader = new EntryReader();
            reader.ReadTowns(stream);
            reader.ReadBusinesses(businessStream, businessEncoding);
            return reader.Create();
        }

        /// <summary>
        /// 郵便番号データに含まれるかどうか
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁。ハイフン・空白・先頭の〒は可)</param>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public bool Contains(string zipCode)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            return FindEntries(BarCode.CheckPostCode(zipCode)) != null;
        }

        /// <summary>
        /// 郵便番号の町域・事業所を返す
        /// </summary>
        /// <param name="zip">郵便番号(半角数字7桁)</param>
        /// <returns>町域・事業所。郵便番号データにない場合は null</returns>
        internal ZipCodeEntry[] FindEntries(string zip)
        {
            return entries.TryGetValue(zip, out var list) ? list : null;
        }

        /// <summary>
        /// 郵便番号の町域・事業所を返す(1つの郵便番号に複数の町域がある場合がある)
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁。ハイフン・空白・先頭の〒は可)</param>
        /// <returns>町域・事業所(郵便番号データにない場合は空)</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public IReadOnlyList<ZipCodeEntry> Find(string zipCode)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            return Array.AsReadOnly(FindEntries(BarCode.CheckPostCode(zipCode)) ?? new ZipCodeEntry[0]);
        }

        /// <summary>
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)かどうか
        /// </summary>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁。ハイフン・空白・先頭の〒は可)</param>
        /// <returns>代表の郵便番号なら true。郵便番号データにない場合は null</returns>
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
        /// 都道府県名・郡名の省略、町域名の前の「大字」「字」の有無、空白、全角・半角、「ヶ」と「ケ」、漢数字と算用数字の違いは区別しない。
        /// 町域名の括弧書き(「北六条東(1～7丁目)」の「(1～7丁目)」など)は除いて照合する。
        /// 代表の郵便番号(町域名が「以下に掲載がない場合」)は町域名がわからないため分割しない。
        /// </remarks>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁。ハイフン・空白・先頭の〒は可)</param>
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
            // (候補の文字列をつなげて作らないよう、都道府県名・市区町村名・町域名の順に照合する)
            var matcher = new AddressMatcher(address);
            var end = -1;
            foreach (var entry in list)
            {
                // 「以下に掲載がない場合」は町域名がわからないため分割しない
                if (entry.IsRepresentative) continue;

                var cityEnd = MatchCityName(entry, matcher);
                if (cityEnd < 0) continue;
                end = Math.Max(end, MatchTownName(entry, matcher, cityEnd, TownStarts(matcher, cityEnd)));
            }
            if (end < 0) return false;

            townAddress = address.Substring(0, end).Trim();
            blockAddress = address.Substring(end);
            return true;
        }

        /// <summary>
        /// 住所から郵便番号の候補を探す
        /// </summary>
        /// <remarks>
        /// 都道府県名・市区町村名・町域名の順に照合し、最も長く一致した町域を返す。
        /// 都道府県名や郡名の省略、町域名の前の「大字」「字」の有無、空白、全角・半角、「ヶ」と「ケ」、漢数字と算用数字の違いは区別しない。
        /// 同じ町域名で郵便番号が複数ある場合は、町域名の括弧書きで絞り込む。
        /// 超高層ビルは住所中のビル名と階(「10階」「10F」)、地区名は町域名の直後の地区名、
        /// 丁目・番地の範囲(「１～７丁目」「１～５００番地」など)は町域名(または地区名)の直後の丁目・番地で絞り込む。
        /// 市区町村名は一致したが町域名が一致しない場合は、「○○町一円」などの町域、なければ代表の郵便番号(以下に掲載がない場合)を返す。
        /// 大口事業所・私書箱の個別郵便番号は返さない(事業所名を指定する <see cref="FindByAddress(string, string)"/> を使う)。
        /// </remarks>
        /// <param name="address">住所</param>
        /// <returns>候補の町域(見つからない場合は空)。郵便番号が1つに決まらない場合は複数</returns>
        public IReadOnlyList<ZipCodeEntry> FindByAddress(string address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            var matcher = new AddressMatcher(address);
            var (matchedCities, cityEnd) = MatchCities(matcher);
            var townStarts = TownStarts(matcher, cityEnd);

            // 同名の市区町村が複数ある場合は、町域名がより長く一致した市区町村を優先する
            // (町域名が一致しない市区町村は代表の郵便番号)
            var found = matchedCities.Select(x => FindTown(x.Towns, matcher, cityEnd, townStarts)).ToList();
            var townMatched = Longest(found, x => x.TownEnd, out _);
            return (townMatched.Count > 0 ? townMatched : found).SelectMany(x => x.Entries).ToList().AsReadOnly();
        }

        /// <summary>
        /// 住所と事業所名から郵便番号の候補を探す
        /// </summary>
        /// <remarks>
        /// 住所の市区町村にある事業所(大口事業所・私書箱の個別郵便番号)から、事業所名が一致するものを探す。
        /// 事業所名の空白、全角・半角、英字の大文字・小文字、「株式会社」「（株）」などの違いは区別しない。
        /// 同じ事業所名が複数ある場合は、番地等まで一致する事業所、なければ町域名まで一致する事業所を優先する。
        /// 事業所名が一致しない場合は、一方が他方を含む事業所のうち、住所が番地等まで一致するものを探す(支店名の省略など)。
        /// 事業所が見つからない場合は、住所だけで探す(<see cref="FindByAddress(string)"/>)。
        /// 事業所の個別郵便番号データを読み込んでいない場合も、住所だけで探す。
        /// </remarks>
        /// <param name="address">住所</param>
        /// <param name="businessName">事業所名</param>
        /// <returns>候補の事業所または町域(見つからない場合は空)。郵便番号が1つに決まらない場合は複数</returns>
        public IReadOnlyList<ZipCodeEntry> FindByAddress(string address, string businessName)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (businessName == null) throw new ArgumentNullException(nameof(businessName));

            var name = ZipCodeEntry.NormalizeBusinessName(businessName);
            if (name.Length > 0)
            {
                var matcher = new AddressMatcher(address);
                var (matchedCities, cityEnd) = MatchCities(matcher);
                var townStarts = TownStarts(matcher, cityEnd);
                var candidates = matchedCities
                    .SelectMany(x => businesses.TryGetValue(x.Key, out var list) ? list : Enumerable.Empty<ZipCodeEntry>())
                    .ToList();

                // 事業所名が一致する事業所
                var exact = candidates.Where(x => x.NormalizedBusinessName == name).ToList();
                if (exact.Count > 0) return SelectBusinesses(exact, matcher, cityEnd, townStarts, false).AsReadOnly();

                // 一方が他方を含む事業所(「田中」と「田中貴金属工業」のような別の事業所を除くため、住所が番地等まで一致するもののみ)
                var partial = candidates.Where(x => x.NormalizedBusinessName.Length > 0
                    && (x.NormalizedBusinessName.Contains(name) || name.Contains(x.NormalizedBusinessName))).ToList();
                var selected = SelectBusinesses(partial, matcher, cityEnd, townStarts, true);
                if (selected.Count > 0) return selected.AsReadOnly();
            }
            return FindByAddress(address);
        }

        /// <summary>
        /// 事業所名が一致した事業所を、住所の町域名・番地等で絞り込む
        /// </summary>
        /// <param name="hits">事業所名が一致した事業所</param>
        /// <param name="matcher">住所</param>
        /// <param name="cityEnd">住所の市区町村名の終わりの位置</param>
        /// <param name="townStarts">町域名の照合を始める位置</param>
        /// <param name="requireBlock">番地等まで一致する事業所のみを返すかどうか</param>
        /// <returns>候補の事業所</returns>
        private static List<ZipCodeEntry> SelectBusinesses(List<ZipCodeEntry> hits, AddressMatcher matcher, int cityEnd, int[] townStarts, bool requireBlock)
        {
            var townHits = hits.Select(x => (Entry: x, End: MatchTownName(x, matcher, cityEnd, townStarts))).Where(x => x.End >= 0).ToList();

            // 番地等が最も長く一致した事業所(「１丁目７－２」より「１丁目７－２（銀座郵便局私書箱第４３３号）」)
            var blockHits = Longest(townHits.Where(x => x.Entry.Block.Length > 0), x => matcher.MatchPrefix(x.Entry.Block, x.End), out _);
            if (blockHits.Count > 0) return blockHits.Select(x => x.Entry).ToList();
            if (requireBlock) return new List<ZipCodeEntry>();

            // 番地等が一致しなければ町域名まで一致する事業所、それもなければ事業所名が一致したすべて
            return townHits.Count > 0 ? townHits.Select(x => x.Entry).ToList() : hits;
        }

        /// <summary>
        /// 住所から郵便番号を探す
        /// </summary>
        /// <param name="address">住所</param>
        /// <param name="zipCode">郵便番号(半角数字7桁)。見つからない場合や1つに決まらない場合は null</param>
        /// <returns>郵便番号が1つに決まった場合は true</returns>
        public bool TryGetZipCode(string address, out string zipCode)
        {
            return TryGetSingleZipCode(FindByAddress(address), out zipCode);
        }

        /// <summary>
        /// 住所と事業所名から郵便番号を探す
        /// </summary>
        /// <param name="address">住所</param>
        /// <param name="businessName">事業所名</param>
        /// <param name="zipCode">郵便番号(半角数字7桁)。見つからない場合や1つに決まらない場合は null</param>
        /// <returns>郵便番号が1つに決まった場合は true</returns>
        public bool TryGetZipCode(string address, string businessName, out string zipCode)
        {
            return TryGetSingleZipCode(FindByAddress(address, businessName), out zipCode);
        }

        /// <summary>
        /// 郵便番号が1つに決まる場合はその郵便番号
        /// </summary>
        private static bool TryGetSingleZipCode(IEnumerable<ZipCodeEntry> found, out string zipCode)
        {
            var zipCodes = found.Select(x => x.ZipCode).Distinct().ToList();
            zipCode = zipCodes.Count == 1 ? zipCodes[0] : null;
            return zipCode != null;
        }

        /// <summary>
        /// 市区町村名が最も長く一致した市区町村(同名の市区町村は都道府県名がなければ複数)
        /// </summary>
        /// <param name="matcher">住所</param>
        /// <returns>市区町村と、住所の市区町村名の終わりの位置</returns>
        private (List<City> Cities, int End) MatchCities(AddressMatcher matcher)
        {
            var matched = Longest(cities, city =>
            {
                // 市区町村の数(約1,900)だけ繰り返すため、LINQ は使わない
                var end = -1;
                foreach (var candidate in city.Candidates)
                {
                    end = Math.Max(end, matcher.MatchNormalizedPrefix(candidate, 0));
                }
                return end;
            }, out var bestEnd);
            return (matched, bestEnd);
        }

        /// <summary>
        /// 住所の先頭が市区町村名まで(都道府県名ありとなし、郡名ありとなし)と一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="entry">町域・事業所</param>
        /// <param name="matcher">住所</param>
        /// <returns>一致した部分の終わりの位置。一致しない場合は -1</returns>
        private static int MatchCityName(ZipCodeEntry entry, AddressMatcher matcher)
        {
            var prefectureEnd = matcher.MatchPrefix(entry.Prefecture, 0);
            var end = -1;
            foreach (var start in prefectureEnd >= 0 ? new[] { 0, prefectureEnd } : new[] { 0 })
            {
                end = Math.Max(end, matcher.MatchPrefix(entry.City, start));
                if (entry.CityWithoutDistrict != null) end = Math.Max(end, matcher.MatchPrefix(entry.CityWithoutDistrict, start));
            }
            return end;
        }

        /// <summary>
        /// 町域名(地区名)の照合を始める位置(後ろに「大字」「字」がある場合はその後ろも)
        /// </summary>
        /// <param name="matcher">住所</param>
        /// <param name="position">位置</param>
        /// <returns>照合を始める位置</returns>
        private static int[] TownStarts(AddressMatcher matcher, int position)
        {
            var starts = new List<int> { position };
            foreach (var prefix in TownPrefixes)
            {
                var start = matcher.MatchNormalizedPrefix(prefix, position);
                if (start >= 0) starts.Add(start);
            }
            return starts.ToArray();
        }

        /// <summary>
        /// 照合を始める位置のいずれかから正規化済みの文字列が一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="matcher">住所</param>
        /// <param name="target">正規化済みの町域名・地区名</param>
        /// <param name="starts">照合を始める位置</param>
        /// <returns>一致した部分の終わりの位置。一致しない場合は -1</returns>
        private static int MatchFrom(AddressMatcher matcher, string target, int[] starts)
        {
            var end = -1;
            foreach (var start in starts)
            {
                end = Math.Max(end, matcher.MatchNormalizedPrefix(target, start));
            }
            return end;
        }

        /// <summary>
        /// 住所の市区町村名の後が町域名と一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="entry">町域・事業所</param>
        /// <param name="matcher">住所</param>
        /// <param name="cityEnd">住所の市区町村名の終わりの位置</param>
        /// <param name="townStarts">町域名の照合を始める位置</param>
        /// <returns>一致した部分の終わりの位置。一致しない場合は -1</returns>
        private static int MatchTownName(ZipCodeEntry entry, AddressMatcher matcher, int cityEnd, int[] townStarts)
        {
            var town = entry.NormalizedTownName;
            return town.Length == 0 ? cityEnd : MatchFrom(matcher, town, townStarts);
        }

        /// <summary>
        /// 市区町村の中から町域を探す
        /// </summary>
        /// <param name="city">市区町村の町域</param>
        /// <param name="matcher">住所</param>
        /// <param name="cityEnd">住所の市区町村名の終わりの位置</param>
        /// <param name="townStarts">町域名の照合を始める位置</param>
        /// <returns>候補の町域と、住所の町域名の終わりの位置(町域名が一致せず代表の郵便番号の場合は -1)</returns>
        private static (IEnumerable<ZipCodeEntry> Entries, int TownEnd) FindTown(ZipCodeEntry[] city, AddressMatcher matcher, int cityEnd, int[] townStarts)
        {
            // 町域名が最も長く一致した町域
            var matched = Longest(city.Where(x => !x.IsRepresentative), x => MatchTownName(x, matcher, cityEnd, townStarts), out var townEnd);

            // 町域名が一致しない場合は代表の郵便番号
            if (matched.Count == 0) return (city.Where(x => x.IsRepresentative), -1);

            return (SelectInTown(matched, matcher, townEnd), townEnd);
        }

        /// <summary>
        /// 一致した部分の終わりの位置が最も大きいもの(同じ位置のものはすべて)を返す
        /// </summary>
        /// <param name="items">候補</param>
        /// <param name="getEnd">一致した部分の終わりの位置(一致しない場合は負)</param>
        /// <param name="bestEnd">最も大きい終わりの位置(一致するものがない場合は -1)</param>
        /// <returns>最も長く一致したもの</returns>
        private static List<T> Longest<T>(IEnumerable<T> items, Func<T, int> getEnd, out int bestEnd)
        {
            bestEnd = -1;
            var result = new List<T>();
            foreach (var item in items)
            {
                var end = getEnd(item);
                if (end < 0 || end < bestEnd) continue;
                if (end > bestEnd)
                {
                    bestEnd = end;
                    result.Clear();
                }
                result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// 同じ町域名の町域を、括弧書き(超高層ビルの階、地区名、丁目・番地の範囲)で絞り込む
        /// </summary>
        /// <param name="matched">町域名が一致した町域</param>
        /// <param name="matcher">住所</param>
        /// <param name="townEnd">住所の町域名の終わりの位置</param>
        /// <returns>候補の町域</returns>
        private static IEnumerable<ZipCodeEntry> SelectInTown(List<ZipCodeEntry> matched, AddressMatcher matcher, int townEnd)
        {
            if (matched.Select(x => x.ZipCode).Distinct().Count() <= 1) return matched;

            // 超高層ビル: 住所の町域名より後ろにビル名があれば、その後の階で選ぶ
            var buildings = matched.Where(x => x.IsBuilding).ToList();
            if (buildings.Count > 0)
            {
                var building = buildings.GroupBy(x => x.NormalizedBuildingName)
                    .Select(x => (Entries: x.ToList(), End: matcher.IndexOfNormalized(x.Key, townEnd)))
                    .Where(x => x.End >= 0)
                    .OrderByDescending(x => x.Entries[0].NormalizedBuildingName.Length)
                    .FirstOrDefault();
                if (building.Entries != null)
                {
                    // 階が書かれていない場合や地下の場合は「（地階・階層不明）」
                    var floor = matcher.FloorAt(building.End);
                    var byFloor = building.Entries.Where(x => floor != null && x.Floor == floor).ToList();
                    if (byFloor.Count > 0) return byFloor;
                    var unknownFloor = building.Entries.Where(x => x.IsUnknownFloor).ToList();
                    return unknownFloor.Count > 0 ? unknownFloor : building.Entries;
                }

                // ビル名がなければビル以外(「（次のビルを除く）」など)
                var others = matched.Where(x => !x.IsBuilding).ToList();
                if (others.Count == 0) return matched;
                matched = others;
            }

            // 地区名: 町域名の直後(前に「大字」「字」があってもよい)の地区名で選ぶ
            var areas = matched.Where(x => x.NormalizedArea != null).ToList();
            if (areas.Count > 0)
            {
                var areaStarts = TownStarts(matcher, townEnd);
                var areaMatched = Longest(areas, x => MatchFrom(matcher, x.NormalizedArea, areaStarts), out var bestAreaEnd);
                if (areaMatched.Count > 0) return SelectByBlockNumber(areaMatched, matcher.LeadingNumber(bestAreaEnd));

                // 地区名が書かれていない場合は、地区名のない町域の範囲・「その他」で選ぶ
                var withoutArea = matched.Where(x => x.NormalizedArea == null).ToList();
                var number = matcher.LeadingNumber(townEnd);
                var inRange = withoutArea.Where(x => number != null && x.BlockRange != null && x.BlockRange.Contains(number.Value)).ToList();
                if (inRange.Count > 0) return inRange;
                var defaults = withoutArea.Where(x => x.IsOthers).ToList();
                if (defaults.Count > 0) return defaults;
                // 決められないため、範囲に含まれない町域を除いてすべて返す
                return matched.Where(x => x.NormalizedArea != null || x.BlockRange == null);
            }

            // 町域名の直後が数値(丁目・番地)の場合のみ、括弧書きの範囲と比べる
            return SelectByBlockNumber(matched, matcher.LeadingNumber(townEnd));
        }

        /// <summary>
        /// 町域名の括弧書きの丁目・番地の範囲で絞り込む
        /// </summary>
        /// <param name="matched">町域名(地区名)が一致した町域</param>
        /// <param name="number">住所の町域名(地区名)の直後の数値(丁目または番地)</param>
        /// <returns>候補の町域</returns>
        private static IEnumerable<ZipCodeEntry> SelectByBlockNumber(List<ZipCodeEntry> matched, int? number)
        {
            if (matched.Select(x => x.ZipCode).Distinct().Count() <= 1) return matched;

            // 括弧書きの範囲(「１～７丁目」など)に含まれる町域
            var inRange = number == null ? new List<ZipCodeEntry>() : matched.Where(x => x.BlockRange != null && x.BlockRange.Contains(number.Value)).ToList();
            if (inRange.Count > 0) return inRange;

            // 範囲に含まれない場合は、範囲のない町域(「その他」「次のビルを除く」、括弧書きなしなど)
            var rest = matched.Where(x => x.BlockRange == null).ToList();
            // 範囲以外の解釈できない括弧書きがある場合は決められないため、それらを含めて返す
            return rest.Count > 0 ? rest : matched;
        }

        /// <summary>
        /// 超高層ビルの町域名(「霞が関霞が関ビル」の「霞が関」)を、同じ市区町村の町域名から探して設定する
        /// </summary>
        /// <param name="city">市区町村の町域</param>
        private static void SetBuildingTowns(ZipCodeEntry[] city)
        {
            var floors = city.Where(x => x.HasFloor).ToList();
            if (floors.Count == 0) return;

            var towns = city.Where(x => !x.HasFloor && !x.IsRepresentative)
                .Select(x => x.GetTownNameWithBuilding())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
            foreach (var entry in floors)
            {
                var name = entry.GetTownNameWithBuilding();
                var town = towns.Where(x => x.Length < name.Length && name.StartsWith(x, StringComparison.Ordinal))
                    .OrderByDescending(x => x.Length)
                    .FirstOrDefault();
                entry.SetBuildingTown(town);
            }
        }

        /// <summary>
        /// 市区町村のキー
        /// </summary>
        private static string CityKey(ZipCodeEntry entry) => entry.Prefecture + "\t" + entry.City;

        /// <summary>
        /// 市区町村(都道府県名・市区町村名が同じ町域)
        /// </summary>
        private sealed class City
        {
            public City(string key, ZipCodeEntry[] towns, string[] candidates)
            {
                Key = key;
                Towns = towns;
                Candidates = candidates;
            }

            /// <summary>都道府県名と市区町村名</summary>
            public string Key { get; }

            /// <summary>町域(事業所は含まない)</summary>
            public ZipCodeEntry[] Towns { get; }

            /// <summary>照合用に正規化した、市区町村名までの住所の候補</summary>
            public string[] Candidates { get; }
        }

        /// <summary>
        /// CSVファイル(zip ファイルの場合は中の最初のCSVファイル)を開く
        /// </summary>
        private static T OpenCsv<T>(string path, Func<Stream, T> read)
        {
            using (var file = File.OpenRead(path))
            {
                if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return read(file);
                }

                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var csv = archive.Entries.FirstOrDefault(x => x.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("zipファイルにCSVファイルがありません。");
                    using (var stream = csv.Open())
                    {
                        return read(stream);
                    }
                }
            }
        }

        /// <summary>
        /// Shift_JIS
        /// </summary>
        private static Encoding GetShiftJis()
        {
            try
            {
                return Encoding.GetEncoding(932);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                throw new InvalidOperationException(
                    "事業所の個別郵便番号データ(Shift_JIS)を読み込めません。.NET Core / .NET 5 以降では、事前に Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) を呼び出してください。", e);
            }
        }

        /// <summary>
        /// 郵便番号データの読み込み
        /// </summary>
        private sealed class EntryReader
        {
            private readonly Dictionary<string, List<ZipCodeEntry>> entries = new Dictionary<string, List<ZipCodeEntry>>();

            // 都道府県名・市区町村名は種類が少ないため、同じ文字列を共有してメモリを減らす
            private readonly Dictionary<string, string> names = new Dictionary<string, string>();

            /// <summary>
            /// utf_ken_all.csv を読み込む
            /// </summary>
            public void ReadTowns(Stream stream)
            {
                const string name = "郵便番号データ";
                const string expected = "UTF-8形式の utf_ken_all.csv";
                ReadLines(stream, new UTF8Encoding(false), (fields, lineNumber, line) =>
                {
                    if (fields.Count != TownColumnCount) throw InvalidFormat(name, lineNumber, "列数が15ではありません", expected);
                    // Shift_JIS のファイルを UTF-8 として読むと置換文字になる
                    if (line.IndexOf((char)0xFFFD) >= 0) throw InvalidFormat(name, lineNumber, "UTF-8 として読めない文字があります", expected);

                    var zipCode = CheckZipCode(fields.Get(2), name, lineNumber, expected);
                    // 7:都道府県名 8:市区町村名 9:町域名(漢字) 12:丁目を有する町域
                    var prefecture = CheckPrefecture(fields.Get(6), name, lineNumber, expected);
                    Add(new ZipCodeEntry(zipCode, prefecture, Share(fields.Get(7)), fields.Get(8), fields.Is(11, "1")));
                });
            }

            /// <summary>
            /// JIGYOSYO.CSV を読み込む
            /// </summary>
            public void ReadBusinesses(Stream stream, Encoding encoding)
            {
                const string name = "事業所の個別郵便番号データ";
                const string expected = "JIGYOSYO.CSV";
                ReadLines(stream, encoding, (fields, lineNumber, line) =>
                {
                    if (fields.Count != BusinessColumnCount) throw InvalidFormat(name, lineNumber, "列数が13ではありません", expected);

                    // 13:修正コード(5は廃止)
                    if (fields.Is(12, "5")) return;

                    var zipCode = CheckZipCode(fields.Get(7), name, lineNumber, expected);
                    // 11:個別番号の種別(0:大口事業所 1:私書箱)
                    ZipCodeKind kind;
                    if (fields.Is(10, "0")) kind = ZipCodeKind.Business;
                    else if (fields.Is(10, "1")) kind = ZipCodeKind.PostOfficeBox;
                    else throw InvalidFormat(name, lineNumber, "個別番号の種別が0か1ではありません", expected);

                    // 3:大口事業所名(漢字) 4:都道府県名 5:市区町村名 6:町域名 7:小字名、丁目、番地等
                    // 文字コードが違う(UTF-8 に変換したファイルなど)と、置換文字にならずに別の文字に化けるため、都道府県名で確認する
                    var prefecture = CheckPrefecture(fields.Get(3), name, lineNumber, expected);
                    Add(new ZipCodeEntry(zipCode, prefecture, Share(fields.Get(4)), fields.Get(5), fields.Get(6), fields.Get(2), kind));
                });
            }

            /// <summary>
            /// 郵便番号データを作る
            /// </summary>
            public ZipCodeDictionary Create()
            {
                return new ZipCodeDictionary(entries.ToDictionary(x => x.Key, x => x.Value.ToArray()));
            }

            private void Add(ZipCodeEntry entry)
            {
                if (!entries.TryGetValue(entry.ZipCode, out var list))
                {
                    list = new List<ZipCodeEntry>();
                    entries.Add(entry.ZipCode, list);
                }
                list.Add(entry);
            }

            private string Share(string value)
            {
                if (names.TryGetValue(value, out var shared)) return shared;
                names.Add(value, value);
                return value;
            }

            /// <summary>
            /// CSVを1行ずつ読み込む(各項目は","区切りで、文字列項目は「"」で囲まれている。項目内に","はない)
            /// </summary>
            private static void ReadLines(Stream stream, Encoding encoding, Action<CsvFields, int, string> read)
            {
#if NET8_0_OR_GREATER
                // 区切り位置は行ごとに使い回す(列数が多すぎる場合を検出できるよう、最大の列数 + 1)
                var ranges = new Range[Math.Max(TownColumnCount, BusinessColumnCount) + 1];
#endif
                using (var reader = new StreamReader(stream, encoding, false, 4096, true))
                {
                    string line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (line.Length == 0) continue;
#if NET8_0_OR_GREATER
                        read(new CsvFields(line, ranges), lineNumber, line);
#else
                        read(new CsvFields(line), lineNumber, line);
#endif
                    }
                }
            }

            private string CheckPrefecture(string prefecture, string name, int lineNumber, string expected)
            {
                if (!Prefectures.Contains(prefecture))
                {
                    throw InvalidFormat(name, lineNumber, "都道府県名が正しくありません。文字コードが違う可能性があります", expected);
                }
                return Share(prefecture);
            }

            private static string CheckZipCode(string zipCode, string name, int lineNumber, string expected)
            {
                if (zipCode.Length != 7 || !zipCode.All(c => c >= '0' && c <= '9'))
                {
                    throw InvalidFormat(name, lineNumber, "郵便番号が7桁の数値ではありません", expected);
                }
                return zipCode;
            }

            /// <summary>
            /// 形式エラー
            /// </summary>
            private static InvalidDataException InvalidFormat(string name, int lineNumber, string reason, string expected)
            {
                return new InvalidDataException($"{name}の{lineNumber}行目の形式が正しくありません({reason})。{expected} を指定してください。");
            }
        }

        /// <summary>
        /// CSVの1行の項目(各項目は","区切りで、文字列項目は「"」で囲まれている。項目内に","はない)
        /// </summary>
        private readonly struct CsvFields
        {
#if NET8_0_OR_GREATER
            // 使う項目だけを文字列にするため、行は区切り位置だけを求めておく
            private readonly string line;
            private readonly Range[] ranges;

            public CsvFields(string line, Range[] ranges)
            {
                this.line = line;
                this.ranges = ranges;
                Count = line.AsSpan().Split(ranges, ',');
            }

            /// <summary>項目数(最大の列数を超える場合は最大の列数 + 1)</summary>
            public int Count { get; }

            /// <summary>項目(「"」は除く)</summary>
            public string Get(int index) => Unquote(line.AsSpan()[ranges[index]]).ToString();

            /// <summary>項目(「"」は除く)が指定した値かどうか</summary>
            public bool Is(int index, string value) => Unquote(line.AsSpan()[ranges[index]]).SequenceEqual(value);

            private static ReadOnlySpan<char> Unquote(ReadOnlySpan<char> field)
            {
                return field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"' ? field.Slice(1, field.Length - 2) : field;
            }
#else
            private readonly string[] fields;

            public CsvFields(string line)
            {
                fields = line.Split(',');
            }

            /// <summary>項目数</summary>
            public int Count => fields.Length;

            /// <summary>項目(「"」は除く)</summary>
            public string Get(int index) => Unquote(fields[index]);

            /// <summary>項目(「"」は除く)が指定した値かどうか</summary>
            public bool Is(int index, string value) => Get(index) == value;

            private static string Unquote(string field)
            {
                return field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"' ? field.Substring(1, field.Length - 2) : field;
            }
#endif
        }
    }
}
