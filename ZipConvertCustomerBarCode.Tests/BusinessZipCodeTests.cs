using System.Text;

namespace ZipConvertCustomerBarCode.Tests
{
    // 事業所の個別郵便番号
    // TestData/jigyosyo_sample.csv は JIGYOSYO.CSV の形式(Shift_JIS)で書いたテスト用のデータ
    public class BusinessZipCodeTests
    {
        private static readonly ZipCodeDictionary Dictionary = LoadWithBusinesses();

        private static ZipCodeDictionary LoadWithBusinesses()
        {
            // Shift_JIS を使えるようにする(.NET Core / .NET 5 以降)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return ZipCodeDictionary.Load(TestCases.DataPath("ken_all_sample.csv"), TestCases.DataPath("jigyosyo_sample.csv"));
        }

        [Fact]
        public void 事業所の個別郵便番号データを読み込める()
        {
            // 町域31件 + 事業所7件(廃止の1件は除く)
            Assert.Equal(38, Dictionary.Count);
            Assert.False(Dictionary.Contains("1008799"));
        }

        [Fact]
        public void Streamから読み込める()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var towns = File.OpenRead(TestCases.DataPath("ken_all_sample.csv"));
            using var businesses = File.OpenRead(TestCases.DataPath("jigyosyo_sample.csv"));

            var dictionary = ZipCodeDictionary.Load(towns, businesses, Encoding.GetEncoding(932));

            Assert.Equal(38, dictionary.Count);
            Assert.True(businesses.CanRead);
        }

        [Fact]
        public void 個別郵便番号の事業所を取得できる()
        {
            var business = Assert.Single(Dictionary.Find("1008792"));
            Assert.Equal(ZipCodeKind.Business, business.Kind);
            Assert.True(business.IsIndividual);
            Assert.Equal("日本郵便　株式会社", business.BusinessName);
            Assert.Equal(("東京都", "千代田区", "大手町", "２丁目３－１"), (business.Prefecture, business.City, business.Town, business.Block));
            Assert.False(Dictionary.IsRepresentative("1008792"));

            Assert.Equal(ZipCodeKind.PostOfficeBox, Assert.Single(Dictionary.Find("1008644")).Kind);
            Assert.False(Assert.Single(Dictionary.Find("1000013")).IsIndividual);
        }

        [Theory]
        [InlineData("東京都千代田区大手町2-3-1", "日本郵便株式会社", "1008792")]
        // 空白、全角・半角、「株式会社」と「（株）」
        [InlineData("千代田区大手町２丁目３－１", "日本郵便（株）", "1008792")]
        [InlineData("東京都千代田区大手町2-3-1", "日本郵便", "1008792")]
        // 事業所名の一部
        [InlineData("栃木県宇都宮市清原工業団地20-2", "テスト工業", "3213292")]
        // 同じ事業所名は番地等で選ぶ
        [InlineData("栃木県宇都宮市清原工業団地22-2", "テスト工業株式会社宇都宮事業所", "3213294")]
        [InlineData("東京都千代田区大手町1丁目7-2", "テスト新聞社", "1008077")]
        // 番地等が最も長く一致した事業所(私書箱)
        [InlineData("東京都千代田区大手町1丁目7-2（銀座郵便局私書箱第433号）", "テスト新聞社", "1008644")]
        public void 住所と事業所名から個別郵便番号を探せる(string address, string businessName, string expected)
        {
            Assert.True(Dictionary.TryGetZipCode(address, businessName, out var zipCode));
            Assert.Equal(expected, zipCode);
        }

        [Fact]
        public void 複数の個別番号を持つ事業所は決めない()
        {
            Assert.False(Dictionary.TryGetZipCode("東京都千代田区大手町1丁目1-1", "テスト銀行", out var zipCode));
            Assert.Null(zipCode);
            Assert.Equal(new[] { "1008701", "1008702" }, Dictionary.FindByAddress("東京都千代田区大手町1丁目1-1", "テスト銀行").Select(x => x.ZipCode));
        }

        [Fact]
        public void 事業所名の一部の一致は番地等まで一致する場合のみ()
        {
            // 「テスト」は「テスト工業宇都宮事業所」に含まれるが、番地等が一致しないため別の事業所とみなす
            Assert.DoesNotContain(Dictionary.FindByAddress("栃木県宇都宮市清原工業団地99-9", "テスト"), x => x.IsIndividual);
            Assert.DoesNotContain(Dictionary.FindByAddress("東京都千代田区大手町3-3-3", "テスト"), x => x.IsIndividual);

            // 事業所名が一致する場合は、番地等が一致しなくても候補にする
            Assert.Equal(new[] { "3213292", "3213294" },
                Dictionary.FindByAddress("栃木県宇都宮市清原工業団地99-9", "テスト工業株式会社宇都宮事業所").Select(x => x.ZipCode).OrderBy(x => x));
        }

        [Fact]
        public void 事業所名の英字の大文字小文字は区別しない()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var csv = "13101,\"ｴﾇﾃｲｰﾃｲｰ\",\"ＮＴＴテスト　株式会社\",\"東京都\",\"千代田区\",\"大手町\",\"１丁目５－１\",\"1008116\",\"100  \",\"銀座\",0,0,0\r\n";
            using var towns = File.OpenRead(TestCases.DataPath("ken_all_sample.csv"));
            using var businesses = new MemoryStream(Encoding.GetEncoding(932).GetBytes(csv));
            var dictionary = ZipCodeDictionary.Load(towns, businesses, Encoding.GetEncoding(932));

            Assert.True(dictionary.TryGetZipCode("東京都千代田区大手町1-5-1", "ntt テスト", out var zipCode));
            Assert.Equal("1008116", zipCode);
        }

        [Fact]
        public void 事業所が見つからなければ住所だけで探す()
        {
            Assert.True(Dictionary.TryGetZipCode("東京都千代田区霞が関1-3-2", "存在しない会社", out var zipCode));
            Assert.Equal("1000013", zipCode);

            // 事業所の個別郵便番号データを読み込んでいない場合
            var townsOnly = ZipCodeDictionary.Load(TestCases.DataPath("ken_all_sample.csv"));
            Assert.True(townsOnly.TryGetZipCode("東京都千代田区霞が関1-3-2", "日本郵便", out zipCode));
            Assert.Equal("1000013", zipCode);
        }

        [Fact]
        public void 住所だけでは個別郵便番号を返さない()
        {
            Assert.DoesNotContain(Dictionary.FindByAddress("東京都千代田区大手町2-3-1"), x => x.IsIndividual);
        }

        [Fact]
        public void 個別郵便番号のバーコードは住所表示番号をCC4で埋める()
        {
            // マニュアル p22: 個別番号のみをバーコード化し、住所表示番号の13けたは CC4 で埋める
            var code = BarCode.Create("1008792", "東京都千代田区大手町2-3-1", Dictionary);

            Assert.Equal("", code.AddressNumber);
            Assert.All(code.Symbols.Skip(8).Take(13), x => Assert.Equal("CC4", x));
            Assert.False(code.IsRepresentativeZipCode);

            // 私書箱も同じ
            Assert.Equal("", BarCode.Create("1008644", "東京都千代田区大手町1丁目7-2", Dictionary).AddressNumber);
        }

        [Fact]
        public void 形式が正しくなければ例外()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var towns = File.OpenRead(TestCases.DataPath("ken_all_sample.csv"));
            using var businesses = new MemoryStream(Encoding.ASCII.GetBytes("13101,\"x\",\"x\",\"x\"\r\n"));

            var e = Assert.Throws<InvalidDataException>(() => ZipCodeDictionary.Load(towns, businesses, Encoding.GetEncoding(932)));
            Assert.Contains("事業所の個別郵便番号データの1行目", e.Message);
        }

        [Fact]
        public void 文字コードが違えば例外()
        {
            // UTF-8 に変換した JIGYOSYO.CSV を Shift_JIS として読み込む
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var sjis = Encoding.GetEncoding(932);
            var utf8 = Encoding.UTF8.GetBytes(sjis.GetString(File.ReadAllBytes(TestCases.DataPath("jigyosyo_sample.csv"))));
            using var towns = File.OpenRead(TestCases.DataPath("ken_all_sample.csv"));
            using var businesses = new MemoryStream(utf8);

            var e = Assert.Throws<InvalidDataException>(() => ZipCodeDictionary.Load(towns, businesses, sjis));
            Assert.Contains("都道府県名", e.Message);
        }
    }
}
