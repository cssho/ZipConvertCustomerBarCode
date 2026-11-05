using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// カスタマバーコード生成
    /// </summary>
    public static class BarCode
    {
        // 開始コード
        private static readonly string startCode = "STC";

        // 終了コード
        private static readonly string endCode = "SPC";

        // 全角→半角辞書
        private static readonly Dictionary<char, char> HankakuDic = new Dictionary<char, char>() {
            {'１','1'},{'２','2'},{'３','3'},{'４','4'},{'５','5'},
            {'６','6'},{'７','7'},{'８','8'},{'９','9'},{'０','0'},
            {'Ａ','A'},{'Ｂ','B'},{'Ｃ','C'},{'Ｄ','D'},{'Ｅ','E'},
            {'Ｆ','F'},{'Ｇ','G'},{'Ｈ','H'},{'Ｉ','I'},{'Ｊ','J'},
            {'Ｋ','K'},{'Ｌ','L'},{'Ｍ','M'},{'Ｎ','N'},{'Ｏ','O'},
            {'Ｐ','P'},{'Ｑ','Q'},{'Ｒ','R'},{'Ｓ','S'},{'Ｔ','T'},
            {'Ｕ','U'},{'Ｖ','V'},{'Ｗ','W'},{'Ｘ','X'},{'Ｙ','Y'},
            {'Ｚ','Z'},
            {'ａ','a'},{'ｂ','b'},{'ｃ','c'},{'ｄ','d'},{'ｅ','e'},
            {'ｆ','f'},{'ｇ','g'},{'ｈ','h'},{'ｉ','i'},{'ｊ','j'},
            {'ｋ','k'},{'ｌ','l'},{'ｍ','m'},{'ｎ','n'},{'ｏ','o'},
            {'ｐ','p'},{'ｑ','q'},{'ｒ','r'},{'ｓ','s'},{'ｔ','t'},
            {'ｕ','u'},{'ｖ','v'},{'ｗ','w'},{'ｘ','x'},{'ｙ','y'},
            {'ｚ','z'},
            {'　',' '},{'‐','-'},{'－','-'}
        };

        // 文字→バーコード辞書
        private static readonly Dictionary<string, string[]> BarcodeCharDic = new Dictionary<string, string[]>()
        {
            {"A",new string[]{"CC1","0"}},{"B",new string[]{"CC1","1"}},{"C",new string[]{"CC1","2"}},
            {"D",new string[]{"CC1","3"}},{"E",new string[]{"CC1","4"}},{"F",new string[]{"CC1","5"}},
            {"G",new string[]{"CC1","6"}},{"H",new string[]{"CC1","7"}},{"I",new string[]{"CC1","8"}},
            {"J",new string[]{"CC1","9"}},{"K",new string[]{"CC2","0"}},{"L",new string[]{"CC2","1"}},
            {"M",new string[]{"CC2","2"}},{"N",new string[]{"CC2","3"}},{"O",new string[]{"CC2","4"}},
            {"P",new string[]{"CC2","5"}},{"Q",new string[]{"CC2","6"}},{"R",new string[]{"CC2","7"}},
            {"S",new string[]{"CC2","8"}},{"T",new string[]{"CC2","9"}},{"U",new string[]{"CC3","0"}},
            {"V",new string[]{"CC3","1"}},{"W",new string[]{"CC3","2"}},{"X",new string[]{"CC3","3"}},
            {"Y",new string[]{"CC3","4"}},{"Z",new string[]{"CC3","5"}},{"0",new string[]{"0"}}
            ,{"1",new string[]{"1"}},{"2",new string[]{"2"}},{"3",new string[]{"3"}},{"4",new string[]{"4"}}
            ,{"5",new string[]{"5"}},{"6",new string[]{"6"}},{"7",new string[]{"7"}},{"8",new string[]{"8"}}
            ,{"9",new string[]{"9"}},{"-",new string[]{"-"}}
        };

        // チェックデジット計算用辞書
        private static readonly Dictionary<string, int> CheckSumDic = new Dictionary<string, int>()
        {
            {"0",0},{"1",1},{"2",2},{"3",3},{"4",4},{"5",5},{"6",6},{"7",7},{"8",8},{"9",9},
            {"-",10},{"CC1",11},{"CC2",12},{"CC3",13},{"CC4",14},{"CC5",15},{"CC6",16},{"CC7",17},{"CC8",18}
        };

        // アルファベット
        private static readonly string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // 漢数字(位取りなし)
        private static readonly string KanjiDigits = "〇一二三四五六七八九";

        // 漢数字(位取りを含む)
        internal static readonly string KanjiNumerals = KanjiDigits + "十百千";

        // 特定文字の前にある漢数字
        private static readonly Regex KanjiNumberRegex = new Regex("[" + KanjiNumerals + "]+(?=丁目|丁|番地|番|号|地割|線|の|ノ)");

        /// <summary>
        /// カスタマバーコードを生成する
        /// 住所Aと住所Bに分けて渡すオーバーロードの使用を推奨
        /// </summary>
        /// <remarks>
        /// 住所全体を町域名以降の住所(住所B)として扱う。
        /// 町域名に「特定文字(丁目、番、ノ など)の前の漢数字」を含む住所(一番町、八丁堀、三ノ輪 など)では、
        /// 町域名の漢数字も住所表示番号として抜き出されるため、誤ったバーコードになる。
        /// </remarks>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <param name="address">住所</param>
        /// <returns>カスタマバーコード</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public static CustomerBarCode Create(string zipCode, string address)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            if (address == null) throw new ArgumentNullException(nameof(address));

            var zip = CheckPostCode(zipCode);
            return CreateCore(zip, address, IsRepresentativeByRule(zip));
        }

        /// <summary>
        /// 郵便番号データを使ってカスタマバーコードを生成する
        /// </summary>
        /// <remarks>
        /// 郵便番号データの町域名で住所を住所A(町域名まで)と住所B(町域名以降)に分割し、住所Bから住所表示番号を抜き出す。
        /// 分割できない場合(町域名の表記が一致しない場合など)は、住所全体を住所Bとして扱う。
        /// <see cref="CustomerBarCode.IsRepresentativeZipCode"/> は郵便番号データに基づいて判定する。
        /// 大口事業所・私書箱の個別郵便番号(事業所の個別郵便番号データを読み込んだ場合)は、マニュアルに従い
        /// 個別番号のみをバーコード化し、住所表示番号は抜き出さない(13けたすべて CC4)。
        /// </remarks>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <param name="address">住所</param>
        /// <param name="dictionary">郵便番号データ</param>
        /// <returns>カスタマバーコード</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public static CustomerBarCode Create(string zipCode, string address, ZipCodeDictionary dictionary)
        {
            if (zipCode == null) throw new ArgumentNullException(nameof(zipCode));
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            var zip = CheckPostCode(zipCode);
            var entries = dictionary.FindEntries(zip);
            if (entries == null)
            {
                // 郵便番号データにない番号(事業所の個別番号、郵便番号データより新しい番号など)は
                // 住所全体を住所Bとし、代表の郵便番号は下2けたで判定する
                return CreateCore(zip, address, IsRepresentativeByRule(zip));
            }

            // 大口事業所・私書箱の個別郵便番号は、個別番号のみをバーコード化し、住所表示番号の13けたは CC4 で埋める
            // http://www.post.japanpost.jp/zipcode/zipmanual/p22.html
            if (entries.Any(x => x.IsIndividual))
            {
                return CreateCore(zip, "", false);
            }

            if (ZipCodeDictionary.TrySplitAddress(entries, address, out _, out var blockAddress))
            {
                address = blockAddress;
            }
            return CreateCore(zip, address, entries.Any(x => x.IsRepresentative));
        }

        /// <summary>
        /// 代表の郵便番号の判定(原則として下2けたが"00")
        /// </summary>
        /// <param name="zip">郵便番号(半角数字7桁)</param>
        private static bool IsRepresentativeByRule(string zip)
        {
            return zip.EndsWith("00", StringComparison.Ordinal);
        }

        /// <summary>
        /// カスタマバーコード生成
        /// </summary>
        /// <param name="zip">郵便番号(半角数字7桁)</param>
        /// <param name="address">住所B</param>
        /// <param name="isRepresentative">代表の郵便番号かどうか</param>
        /// <returns>カスタマバーコード</returns>
        private static CustomerBarCode CreateCore(string zip, string address, bool isRepresentative)
        {
            var addressNumber = ExtractAddressNumber(address);
            var customerCode = ConvertCCode(addressNumber);

            // スタートコード + 郵便番号 + 住所表示番号 + チェックデジット + ストップコード
            var symbols = new[] { startCode }
                .Concat(zip.Select(x => x.ToString()))
                .Concat(customerCode)
                .Concat(new[] { CalcCheckDigit(zip.Select(x => x.ToString()), customerCode), endCode });
            return new CustomerBarCode(zip, addressNumber, isRepresentative, symbols);
        }

        /// <summary>
        /// カスタマバーコードを生成する
        /// </summary>
        /// <remarks>
        /// 住所表示番号は住所Bからのみ抜き出す。
        /// 住所Bが住所Aで始まる場合(住所全体が渡された場合)は、住所Aの部分を取り除いてから抜き出す。
        /// 住所Aとの比較では、空白、全角・半角、「ヶ」と「ケ」、漢数字と算用数字の違いは区別しない。
        /// http://www.post.japanpost.jp/zipcode/zipmanual/p17.html
        /// </remarks>
        /// <param name="zipCode">郵便番号(半角・全角数字7桁、ハイフンは可)</param>
        /// <param name="townAddress">住所A: 町域名までの住所(例: 東京都千代田区霞が関)</param>
        /// <param name="blockAddress">住所B: 町域名以降の住所(例: 1丁目3番2号 郵便プラザ503室)</param>
        /// <returns>カスタマバーコード</returns>
        /// <exception cref="ArgumentException">郵便番号が7桁の数値でない場合</exception>
        public static CustomerBarCode Create(string zipCode, string townAddress, string blockAddress)
        {
            if (townAddress == null) throw new ArgumentNullException(nameof(townAddress));
            if (blockAddress == null) throw new ArgumentNullException(nameof(blockAddress));

            var end = new AddressMatcher(blockAddress).MatchPrefix(townAddress);
            if (end > 0)
            {
                blockAddress = blockAddress.Substring(end);
            }
            return Create(zipCode, blockAddress);
        }

        /// <summary>
        /// 郵便番号チェック
        /// ハイフンを除いて数値7桁であること
        /// </summary>
        /// <param name="zipCode">郵便番号</param>
        /// <returns>郵便番号(半角数字7桁)</returns>
        internal static string CheckPostCode(string zipCode)
        {
            // 全角→半角にし、ハイフンを除いて半角数字(0～9 のみ。全角以外の Unicode 数字や改行は不可)が7桁であること
            var digits = new char[7];
            var count = 0;
            foreach (var c in zipCode)
            {
                var hankaku = ToHankaku(c);
                if (hankaku == '-') continue;
                if (hankaku < '0' || hankaku > '9' || count == digits.Length) throw InvalidPostCode();
                digits[count++] = hankaku;
            }
            if (count != digits.Length) throw InvalidPostCode();

            return new string(digits);
        }

        private static ArgumentException InvalidPostCode()
        {
            return new ArgumentException("郵便番号は7桁の数値で入力してください。");
        }

        /// <summary>
        /// 全角→半角(数字、アルファベット、空白、ハイフン)
        /// </summary>
        /// <param name="c">文字</param>
        /// <returns>変換後の文字</returns>
        internal static char ToHankaku(char c)
        {
            return HankakuDic.TryGetValue(c, out var hankaku) ? hankaku : c;
        }

        /// <summary>
        /// 住所表示番号の抜き出し
        /// </summary>
        /// <param name="address">住所</param>
        /// <returns>住所表示番号(13けたで切り捨てる前)</returns>
        private static string ExtractAddressNumber(string address)
        {
            // 全角→半角
            address = string.Join("", address.Select(ToHankaku));

            // http://www.post.japanpost.jp/zipcode/zipmanual/p19.html
            // 1. まず、データ内にあるアルファベットの小文字は大文字に置き換えます。
            // 実行環境のカルチャによらず変換する(tr-TR では i が İ になるため)
            address = address.ToUpperInvariant();

            // 2. 同様に、データ内にある"&"等の下記の文字は取り除き、後ろのデータを詰めます。
            //「&」(アンパサンド)、「/」(スラッシュ)、「・」(中グロ)、「.」(ピリオド)
            address = string.Join("", address.Split('&', '＆', '/', '／', '･', '・', '.', '．'));

            // 補足ルール
            // 1. 漢数字が下記の特定文字の前にある場合は抜き出し対象とし、算用数字に変換して抜き出します。
            // "丁目" "丁" "番地" "番" "号" "地割" "線" "の" "ノ"
            address = KanjiNumberRegex.Replace(address, m => KanjiToNumber(m.Value));

            // 2. 連続していないアルファベット1文字は抜き出し対象となりますが、算用数字に続くアルファベット1文字"F"に限っては抜き出し対象としません。
            // 3. "F"以降のデータに抜き出し対象となる文字がある場合、"F"はハイフン1文字に置き換えます。
            // (抜き出し対象外の文字として、4の処理でハイフンに置き換わる)
            address = Regex.Replace(address, "(?<=[0-9])F(?![A-Z])", "-");

            // 3. 1および2で整理したデータから、算用数字、ハイフンおよび連続していないアルファベット1文字を必要な文字情報として抜き出します。
            // 4. 次に抜き出された文字の前にある下記の文字等は、ハイフン1文字に置き換えます。
            // 「漢字」、「かな文字」、「カタカナ文字」、「漢数字」、「ブランク」、「2文字以上連続したアルファベット文字」
            address = Regex.Replace(address, "[^0-9A-Z\\-]|[A-Z]{2,}", "-");

            // ハイフン処理
            return TrimHyphen(address);
        }

        /// <summary>
        /// 漢数字を算用数字に変換する
        /// 「十一」のような位取りありと、「二〇五」のような位取りなしの両方に対応
        /// </summary>
        /// <param name="kanji">漢数字</param>
        /// <returns>算用数字</returns>
        internal static string KanjiToNumber(string kanji)
        {
            return KanjiToNumber(kanji, 0, kanji.Length);
        }

        /// <summary>
        /// 文字列の一部の漢数字を算用数字に変換する
        /// </summary>
        /// <param name="value">文字列</param>
        /// <param name="start">漢数字の開始位置</param>
        /// <param name="length">漢数字の長さ</param>
        /// <returns>算用数字</returns>
        internal static string KanjiToNumber(string value, int start, int length)
        {
            if (!HasKanjiUnit(value, start, length))
            {
                var digits = new char[length];
                for (int i = 0; i < length; i++) digits[i] = KanjiDigit(value[start + i]);
                return new string(digits);
            }
            return KanjiValue(value, start, length).ToString(CultureInfo.InvariantCulture);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// 文字列の一部の漢数字を算用数字に変換して書き込む
        /// </summary>
        /// <param name="value">文字列</param>
        /// <param name="start">漢数字の開始位置</param>
        /// <param name="length">漢数字の長さ</param>
        /// <param name="destination">書き込み先(漢数字の長さの4倍あれば足りる)</param>
        /// <returns>書き込んだ文字数</returns>
        internal static int WriteKanjiNumber(string value, int start, int length, Span<char> destination)
        {
            if (!HasKanjiUnit(value, start, length))
            {
                for (int i = 0; i < length; i++) destination[i] = KanjiDigit(value[start + i]);
                return length;
            }
            KanjiValue(value, start, length).TryFormat(destination, out var written, default, CultureInfo.InvariantCulture);
            return written;
        }
#endif

        // 位取りの漢数字
        private static readonly char[] KanjiUnits = { '十', '百', '千' };

        /// <summary>
        /// 位取り(十、百、千)を含むかどうか(含まない場合は「二〇五」のように1文字ずつ数字にする)
        /// </summary>
        private static bool HasKanjiUnit(string value, int start, int length)
        {
            return value.IndexOfAny(KanjiUnits, start, length) >= 0;
        }

        /// <summary>
        /// 位取りなしの漢数字1文字を算用数字にする
        /// </summary>
        private static char KanjiDigit(char c)
        {
            return (char)('0' + KanjiDigits.IndexOf(c));
        }

        /// <summary>
        /// 位取りを含む漢数字(「十一」「千二百三十四」)の値
        /// </summary>
        private static int KanjiValue(string value, int start, int length)
        {
            int total = 0, current = 0;
            for (int i = start; i < start + length; i++)
            {
                var c = value[i];
                var digit = KanjiDigits.IndexOf(c);
                if (digit >= 0)
                {
                    current = digit;
                    continue;
                }
                var unit = c == '十' ? 10 : c == '百' ? 100 : 1000;
                total += (current == 0 ? 1 : current) * unit;
                current = 0;
            }
            return total + current;
        }

        /// <summary>
        /// ハイフン処理
        /// </summary>
        /// <remarks>
        /// 補足ルール5: アルファベット前後のハイフンを取り除いた結果、
        /// 2文字以上の連続したアルファベット文字が残った場合はそのままにする(例: 6-7-L-B106 → 6-7LB106)
        /// </remarks>
        /// <param name="target">処理対象文字列</param>
        /// <returns>処理後文字列</returns>
        private static string TrimHyphen(string target)
        {
            // 5. 4の置き換えで、ハイフンが連続する場合は1つにまとめます。
            target = Regex.Replace(target, "\\-{2,}", "-");

            // 補足ルール4: アルファベット前後のハイフンを除去
            var tmp = target.ToCharArray();
            for (int i = 0; i < target.Length; i++)
            {
                if (Alphabet.Contains(target[i].ToString()))
                {
                    if (i > 0 && target[i - 1] == '-') tmp[i - 1] = '@';
                    if (i < target.Length - 1 && target[i + 1] == '-') tmp[i + 1] = '@';
                }
            }
            target = (new string(tmp)).Replace("@", "");

            // 6. 最後に、先頭がハイフンの場合は取り除きます。
            return target.Trim('-');
        }

        /// <summary>
        /// カスタマコード変換
        /// </summary>
        /// <param name="address">住所</param>
        /// <returns>カスタマバーコード</returns>
        private static string[] ConvertCCode(string address)
        {
            // 数値、ハイフンは1けた、アルファベットは英字用制御コードと数字の2けたに変換し、
            // 13けたに満たない場合は CC4 で埋める
            // http://www.post.japanpost.jp/zipcode/zipmanual/p20.html
            // 合計けた数が20けた(住所表示番号は13けた)を超えた場合、以降の文字については切り捨てます。
            // その際、20～21けた目がアルファベット文字となった場合、20けた目の英字用制御(CC1、CC2、CC3)は残して以降は切り捨てます。
            return address.SelectMany(c => BarcodeCharDic[c.ToString()])
                .Concat(Enumerable.Repeat("CC4", 13))
                .Take(13)
                .ToArray();
        }

        /// <summary>
        /// チェックデジット計算
        /// </summary>
        /// <param name="zipCode">郵便番号</param>
        /// <param name="customerCode">住所表示番号</param>
        /// <returns>チェックデジット</returns>
        private static string CalcCheckDigit(IEnumerable<string> zipCode, string[] customerCode)
        {
            // 郵便番号の各値と住所表示番号の各値の合計＋CD=19の倍数
            var sum = zipCode.Concat(customerCode)
                .Select(x => CheckSumDic[x]).Sum();

            if (sum % 19 == 0) return "0";

            var mod = sum / 19;
            var checkD = (mod + 1) * 19 - sum;
            return CheckSumDic.Where(x => x.Value == checkD).First().Key;


        }
    }
}
