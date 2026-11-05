using System.Text;

namespace ZipConvertCustomerBarCode.Tests
{
    // 抜き出しの基本ルール・補足ルール
    // 期待値はマニュアルの「バーコード・データ」
    // http://www.post.japanpost.jp/zipcode/zipmanual/p19.html
    public class SupplementaryRuleTests
    {
        [Theory]
        // 基本ルール(p17、p19)
        [InlineData("1000013", "東京都千代田区霞が関1丁目3番2号　郵便プラザ503室", "1-3-2-503")]
        [InlineData("2630023", "千葉市稲毛区緑町3丁目30-8　郵便ビル403号", "3-30-8-403")]
        [InlineData("2730102", "千葉県鎌ヶ谷市右京塚　東3丁目-20-5　郵便・A&bコーポB604号", "3-20-5B604")]
        // 補足ルール1: 特定文字の前の漢数字は算用数字に変換する
        [InlineData("1980036", "東京都青梅市河辺町十一丁目六番地一号　郵便タワー601", "11-6-1-601")]
        [InlineData("0800831", "北海道帯広市稲田町南七線　西28", "7-28")]
        // 補足ルール2: 算用数字に続く"F"は抜き出さない
        [InlineData("3170055", "茨城県日立市宮田町6丁目7-14　ABCビル2F", "6-7-14-2")]
        // 補足ルール3: "F"以降に抜き出す文字があれば"F"はハイフンにする
        [InlineData("3170055", "茨城県日立市宮田町6丁目7-14　ABCビル2F201号室", "6-7-14-2-201")]
        // 補足ルール4: アルファベット前後のハイフンは取り除く
        [InlineData("6500046", "神戸市中央区港島中町9丁目7-6　郵便シティA棟1F1号", "9-7-6A1-1")]
        // 補足ルール5: その結果2文字以上連続したアルファベットはそのまま
        [InlineData("6230011", "京都府綾部市青野町綾部6-7　LプラザB106", "6-7LB106")]
        // 13けたで切り捨て(p20)
        [InlineData("0640804", "札幌市中央区南四条西29丁目1524-23　第2郵便ハウス501", "29-1524-23-2-")]
        public void マニュアルの例どおりに抜き出す(string zipCode, string address, string expected)
        {
            var code = BarCode.Create(zipCode, address);

            Assert.Equal(expected, ToBarCodeData(code));
            AssertCheckDigit(code);
        }

        [Theory]
        [InlineData("二〇五号", "205")]
        [InlineData("十番地", "10")]
        [InlineData("十一丁目", "11")]
        [InlineData("二十三番", "23")]
        [InlineData("百二番地", "102")]
        [InlineData("千二百三十四番", "1234")]
        [InlineData("三ノ二番", "3-2")]
        [InlineData("一の三号", "1-3")]
        [InlineData("第二地割", "2")]
        [InlineData("四丁", "4")]
        // 特定文字の前でない漢数字は抜き出さない
        [InlineData("北六条東4丁目", "4")]
        public void 漢数字を算用数字に変換する(string address, string expected)
        {
            var code = BarCode.Create("1234567", address);

            Assert.Equal(expected, ToBarCodeData(code));
        }

        [Theory]
        [InlineData("2F", "2")]
        [InlineData("2F201", "2-201")]
        [InlineData("2F-3", "2-3")]
        // 数字に続かない"F"、2文字以上連続する"F"は対象外
        [InlineData("F棟2", "F2")]
        [InlineData("2FG棟3", "2-3")]
        public void 数字に続くFの扱い(string address, string expected)
        {
            var code = BarCode.Create("1234567", address);

            Assert.Equal(expected, ToBarCodeData(code));
        }

        [Fact]
        public void 住所Bだけから抜き出す()
        {
            // 町域名に特定文字の前の漢数字を含む
            Assert.Equal("5-3", ToBarCodeData(BarCode.Create("1020082", "東京都千代田区一番町", "5-3")));
            Assert.Equal("2-1-1", ToBarCodeData(BarCode.Create("1040032", "東京都中央区八丁堀", "2丁目1-1")));
            Assert.Equal("1-2", ToBarCodeData(BarCode.Create("1100011", "東京都台東区三ノ輪", "1丁目2")));
        }

        [Fact]
        public void 住所Bが住所Aで始まる場合は住所Aを取り除く()
        {
            var code = BarCode.Create("1040032", "東京都中央区八丁堀", "東京都中央区八丁堀2丁目1-1");

            Assert.Equal("2-1-1", ToBarCodeData(code));
        }

        [Fact]
        public void 住所全体を渡すと町域名の漢数字も抜き出される()
        {
            // 2引数版の既知の制限(XMLコメントとREADMEに記載)
            var code = BarCode.Create("1020082", "東京都千代田区一番町5-3");

            Assert.Equal("1-5-3", ToBarCodeData(code));
        }

        [Fact]
        public void 住所Aと住所Bがnullなら例外()
        {
            Assert.Throws<ArgumentNullException>(() => BarCode.Create("1020082", null!, "5-3"));
            Assert.Throws<ArgumentNullException>(() => BarCode.Create("1020082", "東京都千代田区一番町", (string)null!));
        }

        // 住所表示番号のシンボルをバーコード・データの文字列に戻す(CC4 の埋め草は除く)
        private static string ToBarCodeData(CustomerBarCode code)
        {
            var symbols = code.Symbols.Skip(8).Take(13).ToList();
            var sb = new StringBuilder();
            for (int i = 0; i < symbols.Count && symbols[i] != "CC4"; i++)
            {
                var s = symbols[i];
                if (s.StartsWith("CC"))
                {
                    // 英字用制御コード + 数字でアルファベット1文字
                    var control = s[2] - '1';
                    var digit = symbols[++i][0] - '0';
                    sb.Append((char)('A' + control * 10 + digit));
                }
                else
                {
                    sb.Append(s);
                }
            }
            return sb.ToString();
        }

        // 郵便番号・住所表示番号・チェックデジットの合計が19の倍数
        private static void AssertCheckDigit(CustomerBarCode code)
        {
            var sum = code.Symbols.Skip(1).Take(21).Sum(s =>
                s == "-" ? 10 : s.StartsWith("CC") ? 10 + (s[2] - '0') : s[0] - '0');
            Assert.Equal(0, sum % 19);
        }
    }
}
