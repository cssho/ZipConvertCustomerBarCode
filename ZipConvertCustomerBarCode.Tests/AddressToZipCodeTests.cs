namespace ZipConvertCustomerBarCode.Tests
{
    // 住所 → 郵便番号
    // TestData/ken_all_sample.csv はテスト用に書いたデータ(内容は郵便番号データと一致しない場合がある)
    public class AddressToZipCodeTests
    {
        private static readonly ZipCodeDictionary Dictionary = ZipCodeDictionary.Load(TestCases.DataPath("ken_all_sample.csv"));

        [Theory]
        [InlineData("東京都千代田区霞が関1丁目3番2号　郵便プラザ503室", "1000013")]
        [InlineData("千代田区一番町5-3", "1020082")]
        [InlineData("東京都 中央区 八丁堀 2丁目1-1", "1040032")]
        // 「ヶ」と「ケ」
        [InlineData("千葉県鎌ヶ谷市右京塚東3丁目-20-5", "2730102")]
        // 「大字」
        [InlineData("岩手県宮古市大字津軽石第二地割原", "0270203")]
        // 丁目の範囲
        [InlineData("北海道札幌市東区北六条東4丁目　郵便センター6号館", "0600906")]
        [InlineData("北海道札幌市東区北六条東８丁目", "0650006")]
        // 町域名の漢数字を算用数字で書く
        [InlineData("札幌市東区北6条東8丁目", "0650006")]
        [InlineData("札幌市東区北六条東十丁目", "0650006")]
        // 番地の範囲と「その他」
        [InlineData("北海道札幌市南区常盤131番地", "0050865")]
        [InlineData("北海道札幌市南区常盤132番地", "0050863")]
        [InlineData("北海道札幌市南区常盤", "0050863")]
        // 丁目の範囲(地区名の町域もある)
        [InlineData("深川市音江町2丁目3", "0741273")]
        // 郡名の省略
        [InlineData("東神楽町ひじり野南1条1丁目", "0711503")]
        [InlineData("北海道上川郡東神楽町ひじり野南一条1丁目", "0711503")]
        // 同名の市区町村(都道府県名なし)は町域名まで一致した方
        [InlineData("府中市宮西町1-1", "1830022")]
        // ○○村一円、○○町の次に番地がくる場合
        [InlineData("沖縄県島尻郡渡名喜村1917", "9013601")]
        [InlineData("茨城県猿島郡境町123", "3060433")]
        // 1つの郵便番号に複数の町域
        [InlineData("北海道テスト市本町北1-2", "9999901")]
        // 町域名が一致しない場合は代表の郵便番号
        [InlineData("東京都千代田区存在しない町1-1", "1000000")]
        public void 住所から郵便番号を探せる(string address, string expected)
        {
            Assert.True(Dictionary.TryGetZipCode(address, out var zipCode));
            Assert.Equal(expected, zipCode);
        }

        [Fact]
        public void 地区名が書かれていなければ決めない()
        {
            // どの地区(稲田、音江、国見)か住所からはわからない(123は「１～２丁目」の範囲外)
            Assert.False(Dictionary.TryGetZipCode("深川市音江町123", out var zipCode));
            Assert.Null(zipCode);
            Assert.Equal(new[] { "0741164", "0741272", "0741276", "0741277" }, Dictionary.FindByAddress("深川市音江町123").Select(x => x.ZipCode).Distinct().OrderBy(x => x));
        }

        [Theory]
        [InlineData("深川市音江町稲田123", "0741276")]
        [InlineData("深川市音江町字音江123", "0741277")]
        // 地区名の後の数値は町域名の「１～２丁目」と比べない
        [InlineData("深川市音江町稲田1", "0741276")]
        // 地区名 + 番地の範囲、地区名 + その他
        [InlineData("深川市音江町国見100番地", "0741164")]
        [InlineData("深川市音江町国見500番地", "0741272")]
        [InlineData("深川市音江町国見", "0741272")]
        public void 地区名で絞り込める(string address, string expected)
        {
            Assert.True(Dictionary.TryGetZipCode(address, out var zipCode));
            Assert.Equal(expected, zipCode);
        }

        [Theory]
        [InlineData("東京都千代田区霞が関3-2-5　霞が関ビル10階", "1006010")]
        [InlineData("東京都千代田区霞が関3丁目2番5号 霞が関ビル 1F", "1006001")]
        // 階がない、地下、データにない階は「（地階・階層不明）」
        [InlineData("東京都千代田区霞が関3-2-5　霞が関ビル", "1006090")]
        [InlineData("東京都千代田区霞が関3-2-5　霞が関ビル地下1階", "1006090")]
        [InlineData("東京都千代田区霞が関3-2-5　霞が関ビル99階", "1006090")]
        // ビル名がなければ「（次のビルを除く）」
        [InlineData("東京都千代田区霞が関1-3-2", "1000013")]
        public void 超高層ビルは階で絞り込める(string address, string expected)
        {
            Assert.True(Dictionary.TryGetZipCode(address, out var zipCode));
            Assert.Equal(expected, zipCode);
        }

        [Theory]
        // 英字の大文字・小文字、全角・半角は区別しない
        [InlineData("東京都千代田区霞が関1-1-1　ABCタワー1階")]
        [InlineData("東京都千代田区霞が関1-1-1　abcタワー1F")]
        [InlineData("東京都千代田区霞が関1-1-1　ａｂｃタワー１ｆ")]
        public void ビル名の英字の大文字小文字は区別しない(string address)
        {
            Assert.True(Dictionary.TryGetZipCode(address, out var zipCode));
            Assert.Equal("1006101", zipCode);
        }

        [Fact]
        public void 同名の市区町村は町域名がより長く一致した方()
        {
            // 「テスト村一円」(北海道)は町域名なしで一致、「本町」(青森県)は町域名まで一致
            Assert.True(Dictionary.TryGetZipCode("テスト村本町1-2", out var zipCode));
            Assert.Equal("9999912", zipCode);

            // 町域名が一致しない場合は、町域名のない「テスト村一円」
            Assert.True(Dictionary.TryGetZipCode("テスト村北町1-2", out zipCode));
            Assert.Equal("9999911", zipCode);
        }

        [Theory]
        [InlineData("大阪府大阪市北区梅田1-1")]
        [InlineData("")]
        public void 見つからない場合は空(string address)
        {
            Assert.Empty(Dictionary.FindByAddress(address));
            Assert.False(Dictionary.TryGetZipCode(address, out var zipCode));
            Assert.Null(zipCode);
        }

        [Fact]
        public void 見つかった郵便番号でバーコードを生成できる()
        {
            const string address = "東京都千代田区一番町5-3";
            Assert.True(Dictionary.TryGetZipCode(address, out var zipCode));

            var code = BarCode.Create(zipCode!, address, Dictionary);

            Assert.Equal("1020082", code.ZipCode);
            Assert.Equal("5-3", code.AddressNumber);
        }

        [Fact]
        public void 引数がnullなら例外()
        {
            Assert.Throws<ArgumentNullException>(() => Dictionary.FindByAddress(null!));
            Assert.Throws<ArgumentNullException>(() => Dictionary.TryGetZipCode(null!, out _));
        }
    }
}
