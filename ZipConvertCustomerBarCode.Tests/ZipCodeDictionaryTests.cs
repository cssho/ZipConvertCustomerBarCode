using System.IO.Compression;
using System.Text;

namespace ZipConvertCustomerBarCode.Tests
{
    // TestData/ken_all_sample.csv は utf_ken_all.csv の形式で書いたテスト用のデータ
    // (実在の住所をもとにしているが、内容は郵便番号データと一致しない場合がある。999で始まる番号は架空)
    public class ZipCodeDictionaryTests
    {
        private static readonly ZipCodeDictionary Dictionary = ZipCodeDictionary.Load(TestCases.DataPath("ken_all_sample.csv"));

        [Fact]
        public void CSVファイルを読み込める()
        {
            Assert.Equal(31, Dictionary.Count);
        }

        [Fact]
        public void zipファイルのまま読み込める()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
            try
            {
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(TestCases.DataPath("ken_all_sample.csv"), "utf_ken_all.csv");
                }

                Assert.Equal(31, ZipCodeDictionary.Load(path).Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Streamから読み込める()
        {
            using var stream = File.OpenRead(TestCases.DataPath("ken_all_sample.csv"));

            Assert.Equal(31, ZipCodeDictionary.Load(stream).Count);
            // 渡された Stream は閉じない
            Assert.True(stream.CanRead);
        }

        [Fact]
        public void 列数が違えば例外()
        {
            var csv = "13101,\"100  \",\"1000013\",\"東京都\",\"千代田区\",\"霞が関\"\r\n";

            var e = Assert.Throws<InvalidDataException>(() => Load(Encoding.UTF8.GetBytes(csv)));
            Assert.Contains("1行目", e.Message);
        }

        [Fact]
        public void UTF8でなければ例外()
        {
            // Shift_JIS の「東京都」
            var sjis = new byte[] { 0x93, 0x8C, 0x8B, 0x9E, 0x93, 0x73 };
            var line = Encoding.ASCII.GetBytes("13101,\"100  \",\"1000013\",\"x\",\"x\",\"x\",\"")
                .Concat(sjis)
                .Concat(Encoding.ASCII.GetBytes("\",\"x\",\"x\",0,0,1,0,0,0\r\n"))
                .ToArray();

            Assert.Throws<InvalidDataException>(() => Load(line));
        }

        [Fact]
        public void 郵便番号が7桁の数値でなければ例外()
        {
            var csv = "13101,\"100  \",\"100001\",\"x\",\"x\",\"x\",\"東京都\",\"千代田区\",\"霞が関\",0,0,1,0,0,0\r\n";

            Assert.Throws<InvalidDataException>(() => Load(Encoding.UTF8.GetBytes(csv)));
        }

        [Theory]
        [InlineData("1000000", true)]
        [InlineData("100-0013", false)]
        // 郵便番号データにない(事業所の個別番号など)
        [InlineData("1008798", null)]
        public void 代表の郵便番号を判定できる(string zipCode, bool? expected)
        {
            Assert.Equal(expected, Dictionary.IsRepresentative(zipCode));
        }

        [Fact]
        public void 郵便番号の町域を取得できる()
        {
            Assert.True(Dictionary.Contains("１０００－０１３"));
            Assert.False(Dictionary.Contains("1008798"));

            var entry = Assert.Single(Dictionary.Find("1000013"));
            Assert.Equal(("1000013", "東京都", "千代田区", "霞が関（次のビルを除く）"), (entry.ZipCode, entry.Prefecture, entry.City, entry.Town));
            Assert.Equal(new[] { "本町", "本町北" }, Dictionary.Find("9999901").Select(x => x.Town));
            Assert.Empty(Dictionary.Find("1008798"));
        }

        [Theory]
        [InlineData("1020082", "東京都千代田区一番町5-3", "東京都千代田区一番町", "5-3")]
        // 都道府県名の省略
        [InlineData("1020082", "千代田区一番町5-3", "千代田区一番町", "5-3")]
        // 空白
        [InlineData("1020082", "東京都 千代田区 一番町 5-3", "東京都 千代田区 一番町", " 5-3")]
        // ヶ と ケ
        [InlineData("2730102", "千葉県鎌ヶ谷市右京塚　東3丁目-20-5", "千葉県鎌ヶ谷市右京塚", "　東3丁目-20-5")]
        // 全角・半角
        [InlineData("9999902", "北海道テスト市西2条3丁目", "北海道テスト市西2条", "3丁目")]
        // 漢数字と算用数字
        [InlineData("0650006", "札幌市東区北6条東8丁目", "札幌市東区北6条東", "8丁目")]
        // 郡名の省略
        [InlineData("0711503", "東神楽町ひじり野南一条1丁目", "東神楽町ひじり野南一条", "1丁目")]
        // 町域名の括弧書き
        [InlineData("0600906", "北海道札幌市東区北六条東4丁目　郵便センター6号館", "北海道札幌市東区北六条東", "4丁目　郵便センター6号館")]
        // 町域名の前の「大字」「字」
        [InlineData("0270203", "岩手県宮古市大字津軽石第二地割原　郵便番号CD-1", "岩手県宮古市大字津軽石", "第二地割原　郵便番号CD-1")]
        [InlineData("0270203", "宮古市字津軽石10", "宮古市字津軽石", "10")]
        [InlineData("0270203", "岩手県宮古市津軽石10", "岩手県宮古市津軽石", "10")]
        // ○○村一円
        [InlineData("9013601", "沖縄県島尻郡渡名喜村1917", "沖縄県島尻郡渡名喜村", "1917")]
        // ○○町の次に番地がくる場合
        [InlineData("3060433", "茨城県猿島郡境町123", "茨城県猿島郡境町", "123")]
        // 町域名が「一円」
        [InlineData("5220341", "滋賀県犬上郡多賀町一円12", "滋賀県犬上郡多賀町一円", "12")]
        // 1つの郵便番号に複数の町域がある場合は長く一致した方
        [InlineData("9999901", "北海道テスト市本町北1-2", "北海道テスト市本町北", "1-2")]
        [InlineData("9999901", "テスト市本町1-2", "テスト市本町", "1-2")]
        public void 住所を町域名で分割できる(string zipCode, string address, string town, string block)
        {
            Assert.True(Dictionary.TrySplitAddress(zipCode, address, out var townAddress, out var blockAddress));
            Assert.Equal(town, townAddress);
            Assert.Equal(block, blockAddress);
        }

        [Theory]
        // 郵便番号データにない
        [InlineData("1008798", "東京都千代田区丸の内2-7-2")]
        // 町域名が一致しない
        [InlineData("1020082", "大阪府大阪市北区梅田1-1")]
        // 代表の郵便番号(以下に掲載がない場合)は町域名がわからない
        [InlineData("1000000", "東京都千代田区一番町5-3")]
        public void 分割できない場合はfalse(string zipCode, string address)
        {
            Assert.False(Dictionary.TrySplitAddress(zipCode, address, out var townAddress, out var blockAddress));
            Assert.Null(townAddress);
            Assert.Null(blockAddress);
        }

        [Theory]
        [InlineData("1020082", "東京都千代田区一番町5-3", "5-3")]
        [InlineData("1040032", "東京都中央区八丁堀2丁目1-1", "2-1-1")]
        [InlineData("1100011", "台東区三ノ輪1丁目2", "1-2")]
        public void 郵便番号データを使うと町域名の漢数字を抜き出さない(string zipCode, string address, string expected)
        {
            Assert.Equal(expected, BarCode.Create(zipCode, address, Dictionary).AddressNumber);
            // 郵便番号データなしでは町域名の漢数字も抜き出される
            Assert.NotEqual(expected, BarCode.Create(zipCode, address).AddressNumber);
        }

        [Fact]
        public void 分割できない場合は住所全体から抜き出す()
        {
            var withDictionary = BarCode.Create("1020082", "大阪府大阪市北区梅田1-1", Dictionary);
            var withoutDictionary = BarCode.Create("1020082", "大阪府大阪市北区梅田1-1");

            Assert.Equal(withoutDictionary.Symbols, withDictionary.Symbols);
        }

        [Theory]
        [InlineData("1000000", true)]
        [InlineData("1000013", false)]
        // 郵便番号データにない場合(郵便番号データが古い場合など)は下2けたで判定する
        [InlineData("3510100", true)]
        [InlineData("1008798", false)]
        public void 郵便番号データで代表の郵便番号を判定する(string zipCode, bool expected)
        {
            Assert.Equal(expected, BarCode.Create(zipCode, "1-1", Dictionary).IsRepresentativeZipCode);
        }

        [Fact]
        public void 代表の郵便番号では住所を分割しない()
        {
            var code = BarCode.Create("1000000", "東京都千代田区一番町5-3", Dictionary);

            Assert.Equal(BarCode.Create("1000000", "東京都千代田区一番町5-3").Symbols, code.Symbols);
            Assert.True(code.IsRepresentativeZipCode);
        }

        [Fact]
        public void 郵便番号データなしでは下2けたで判定する()
        {
            Assert.True(BarCode.Create("3510100", "1-1").IsRepresentativeZipCode);
        }

        [Fact]
        public void 引数がnullなら例外()
        {
            Assert.Throws<ArgumentNullException>(() => ZipCodeDictionary.Load((string)null!));
            Assert.Throws<ArgumentNullException>(() => ZipCodeDictionary.Load((Stream)null!));
            Assert.Throws<ArgumentNullException>(() => BarCode.Create("1020082", "一番町5-3", (ZipCodeDictionary)null!));
            Assert.Throws<ArgumentNullException>(() => Dictionary.TrySplitAddress("1020082", null!, out _, out _));
        }

        private static ZipCodeDictionary Load(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return ZipCodeDictionary.Load(stream);
        }
    }
}
