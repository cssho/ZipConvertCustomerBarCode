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

        // 郵便番号 → 町域・事業所
        private readonly Dictionary<string, ZipCodeEntry[]> entries;

        // 市区町村(都道府県名・市区町村名が同じ町域)ごとの町域と、照合用に正規化した市区町村名までの住所の候補
        private readonly (string Key, ZipCodeEntry[] Towns, string[] Candidates)[] cities;

        // 市区町村ごとの事業所
        private readonly Dictionary<string, ZipCodeEntry[]> businesses;

        private ZipCodeDictionary(Dictionary<string, ZipCodeEntry[]> entries)
        {
            this.entries = entries;
            var all = entries.Values.SelectMany(x => x).ToList();
            // 事業所だけがある市区町村も、事業所を探せるように含める
            cities = all.GroupBy(CityKey)
                .Select(x => (x.Key, x.Where(e => !e.IsIndividual).ToArray(), x.First().GetCityCandidates().Select(AddressMatcher.Normalize).ToArray()))
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
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
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
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
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
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
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
            var matcher = new AddressMatcher(address);
            var end = list.SelectMany(x => x.GetTownAddressCandidates())
                .Select(x => matcher.MatchPrefix(x))
                .DefaultIfEmpty(-1)
                .Max();
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

            // 同名の市区町村が複数ある場合は、町域名まで一致した市区町村を優先する
            var found = matchedCities.Select(x => FindTown(x.Towns, matcher, cityEnd)).ToList();
            if (found.Any(x => x.TownMatched))
            {
                found = found.Where(x => x.TownMatched).ToList();
            }
            return found.SelectMany(x => x.Entries).ToList().AsReadOnly();
        }

        /// <summary>
        /// 住所と事業所名から郵便番号の候補を探す
        /// </summary>
        /// <remarks>
        /// 住所の市区町村にある事業所(大口事業所・私書箱の個別郵便番号)から、事業所名が一致するものを探す。
        /// 事業所名の空白、全角・半角、「株式会社」「（株）」などの違いは区別せず、一致しない場合は一方が他方を含むものを探す。
        /// 同じ事業所名が複数ある場合は、番地等まで一致する事業所、なければ町域名まで一致する事業所を優先する。
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
                var candidates = matchedCities
                    .SelectMany(x => businesses.TryGetValue(x.Key, out var list) ? list : Enumerable.Empty<ZipCodeEntry>())
                    .ToList();

                // 事業所名が一致する事業所、なければ一方が他方を含む事業所
                var hits = candidates.Where(x => x.NormalizedBusinessName == name).ToList();
                if (hits.Count == 0)
                {
                    hits = candidates.Where(x => x.NormalizedBusinessName.Length > 0
                        && (x.NormalizedBusinessName.Contains(name) || name.Contains(x.NormalizedBusinessName))).ToList();
                }

                if (hits.Count > 0)
                {
                    // 同じ事業所名が複数ある場合は、番地等まで一致する事業所、なければ町域名まで一致する事業所を優先する
                    var townHits = hits.Select(x => (Entry: x, End: MatchTownName(x, matcher, cityEnd))).Where(x => x.End >= 0).ToList();
                    // 番地等が最も長く一致した事業所(「１丁目７－２」より「１丁目７－２（銀座郵便局私書箱第４３３号）」)
                    var blockHits = townHits.Select(x => (x.Entry, End: matcher.MatchPrefix(x.Entry.Block, x.End))).Where(x => x.End >= 0).ToList();
                    if (blockHits.Count > 0)
                    {
                        var longest = blockHits.Max(x => x.End);
                        return blockHits.Where(x => x.End == longest).Select(x => x.Entry).ToList().AsReadOnly();
                    }
                    return (townHits.Count > 0 ? townHits.Select(x => x.Entry).ToList() : hits).AsReadOnly();
                }
            }
            return FindByAddress(address);
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
        private (List<(string Key, ZipCodeEntry[] Towns, string[] Candidates)> Cities, int End) MatchCities(AddressMatcher matcher)
        {
            var bestEnd = -1;
            var matched = new List<(string Key, ZipCodeEntry[] Towns, string[] Candidates)>();
            foreach (var city in cities)
            {
                // 市区町村の数(約1,900)だけ繰り返すため、LINQ は使わない
                var end = -1;
                foreach (var candidate in city.Candidates)
                {
                    end = Math.Max(end, matcher.MatchNormalizedPrefix(candidate, 0));
                }
                if (end < 0 || end < bestEnd) continue;
                if (end > bestEnd)
                {
                    bestEnd = end;
                    matched.Clear();
                }
                matched.Add(city);
            }
            return (matched, bestEnd);
        }

        /// <summary>
        /// 住所の市区町村名の後が町域名と一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="entry">町域・事業所</param>
        /// <param name="matcher">住所</param>
        /// <param name="cityEnd">住所の市区町村名の終わりの位置</param>
        /// <returns>一致した部分の終わりの位置。一致しない場合は -1</returns>
        private static int MatchTownName(ZipCodeEntry entry, AddressMatcher matcher, int cityEnd)
        {
            var town = entry.NormalizedTownName;
            if (town.Length == 0) return cityEnd;

            var end = matcher.MatchNormalizedPrefix(town, cityEnd);
            foreach (var prefix in TownPrefixes)
            {
                var start = matcher.MatchNormalizedPrefix(prefix, cityEnd);
                if (start >= 0) end = Math.Max(end, matcher.MatchNormalizedPrefix(town, start));
            }
            return end;
        }

        /// <summary>
        /// 市区町村の中から町域を探す
        /// </summary>
        /// <param name="city">市区町村の町域</param>
        /// <param name="matcher">住所</param>
        /// <param name="cityEnd">住所の市区町村名の終わりの位置</param>
        /// <returns>候補の町域と、町域名(または「○○町一円」など)が一致したかどうか</returns>
        private static (IEnumerable<ZipCodeEntry> Entries, bool TownMatched) FindTown(ZipCodeEntry[] city, AddressMatcher matcher, int cityEnd)
        {
            // 町域名が最も長く一致した町域
            var bestTownEnd = -1;
            var matched = new List<ZipCodeEntry>();
            foreach (var entry in city)
            {
                if (entry.IsRepresentative) continue;

                var end = MatchTownName(entry, matcher, cityEnd);
                if (end < 0 || end < bestTownEnd) continue;
                if (end > bestTownEnd)
                {
                    bestTownEnd = end;
                    matched.Clear();
                }
                matched.Add(entry);
            }

            // 町域名が一致しない場合は代表の郵便番号
            if (matched.Count == 0) return (city.Where(x => x.IsRepresentative), false);

            return (SelectInTown(matched, matcher, bestTownEnd), true);
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
                var starts = new[] { townEnd, matcher.MatchNormalizedPrefix("大字", townEnd), matcher.MatchNormalizedPrefix("字", townEnd) };
                var bestAreaEnd = -1;
                var areaMatched = new List<ZipCodeEntry>();
                foreach (var entry in areas)
                {
                    var end = starts.Where(x => x >= 0).Max(x => matcher.MatchNormalizedPrefix(entry.NormalizedArea, x));
                    if (end < 0 || end < bestAreaEnd) continue;
                    if (end > bestAreaEnd)
                    {
                        bestAreaEnd = end;
                        areaMatched.Clear();
                    }
                    areaMatched.Add(entry);
                }
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
                    if (fields.Length != TownColumnCount) throw InvalidFormat(name, lineNumber, "列数が15ではありません", expected);
                    // Shift_JIS のファイルを UTF-8 として読むと置換文字になる
                    if (line.IndexOf((char)0xFFFD) >= 0) throw InvalidFormat(name, lineNumber, "UTF-8 として読めない文字があります", expected);

                    var zipCode = CheckZipCode(Unquote(fields[2]), name, lineNumber, expected);
                    // 7:都道府県名 8:市区町村名 9:町域名(漢字) 12:丁目を有する町域
                    Add(new ZipCodeEntry(zipCode, Share(Unquote(fields[6])), Share(Unquote(fields[7])), Unquote(fields[8]), fields[11] == "1"));
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
                    if (fields.Length != BusinessColumnCount) throw InvalidFormat(name, lineNumber, "列数が13ではありません", expected);

                    // 13:修正コード(5は廃止)
                    if (fields[12] == "5") return;

                    var zipCode = CheckZipCode(Unquote(fields[7]), name, lineNumber, expected);
                    // 11:個別番号の種別(0:大口事業所 1:私書箱)
                    ZipCodeKind kind;
                    switch (fields[10])
                    {
                        case "0": kind = ZipCodeKind.Business; break;
                        case "1": kind = ZipCodeKind.PostOfficeBox; break;
                        default: throw InvalidFormat(name, lineNumber, "個別番号の種別が0か1ではありません", expected);
                    }
                    // 3:大口事業所名(漢字) 4:都道府県名 5:市区町村名 6:町域名 7:小字名、丁目、番地等
                    Add(new ZipCodeEntry(zipCode, Share(Unquote(fields[3])), Share(Unquote(fields[4])), Unquote(fields[5]), Unquote(fields[6]), Unquote(fields[2]), kind));
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
            private static void ReadLines(Stream stream, Encoding encoding, Action<string[], int, string> read)
            {
                using (var reader = new StreamReader(stream, encoding, false, 4096, true))
                {
                    string line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (line.Length == 0) continue;
                        read(line.Split(','), lineNumber, line);
                    }
                }
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
            /// 「"」で囲まれた項目から「"」を除く
            /// </summary>
            private static string Unquote(string field)
            {
                return field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"' ? field.Substring(1, field.Length - 2) : field;
            }

            /// <summary>
            /// 形式エラー
            /// </summary>
            private static InvalidDataException InvalidFormat(string name, int lineNumber, string reason, string expected)
            {
                return new InvalidDataException($"{name}の{lineNumber}行目の形式が正しくありません({reason})。{expected} を指定してください。");
            }
        }
    }
}
