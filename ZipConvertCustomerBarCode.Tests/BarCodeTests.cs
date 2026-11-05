using System.Globalization;

namespace ZipConvertCustomerBarCode.Tests
{
    public class BarCodeTests
    {
        [Theory]
        [MemberData(nameof(TestCases.All), MemberType = typeof(TestCases))]
        public void シンボル列が1x版と一致する(string zipCode, string address, string expected, string _)
        {
            var code = BarCode.Create(zipCode, address);

            Assert.Equal(expected.Split(' '), code.Symbols);
        }

        // 住所表示番号は13けたまで。13けた目がアルファベットの場合は英字用制御コードのみ残す
        // http://www.post.japanpost.jp/zipcode/zipmanual/p20.html
        [Theory]
        // マニュアル p20 の例
        [InlineData("9100067", "福井県福井市新田塚3丁目80-25　J1ビル2-B", "STC 9 1 0 0 0 6 7 3 - 8 0 - 2 5 CC1 9 1 - 2 CC1 9 SPC")]
        // アルファベットで13けたを超える
        [InlineData("1234567", "A1B2C3D4E5F6G7", "STC 1 2 3 4 5 6 7 CC1 0 1 CC1 1 2 CC1 2 3 CC1 3 4 CC1 CC5 SPC")]
        [InlineData("1234567", "123456789012A", "STC 1 2 3 4 5 6 7 1 2 3 4 5 6 7 8 9 0 1 2 CC1 8 SPC")]
        // アルファベットが12～13けた目にちょうど収まる
        [InlineData("1234567", "12345678901A", "STC 1 2 3 4 5 6 7 1 2 3 4 5 6 7 8 9 0 1 CC1 0 - SPC")]
        public void 住所表示番号は13けたで切り捨てる(string zipCode, string address, string expected)
        {
            var code = BarCode.Create(zipCode, address);

            Assert.Equal(expected.Split(' '), code.Symbols);
        }

        [Theory]
        [InlineData("0140113")]
        [InlineData("014-0113")]
        [InlineData("０１４０１１３")]
        [InlineData("０１４‐０１１３")]
        [InlineData("０１４－０１１３")]
        public void 郵便番号は全角とハイフンを受け付ける(string zipCode)
        {
            var code = BarCode.Create(zipCode, "添60-1");

            Assert.Equal(new[] { "0", "1", "4", "0", "1", "1", "3" }, code.Symbols.Skip(1).Take(7));
        }

        [Theory]
        [InlineData("")]
        [InlineData("014011")]
        [InlineData("01401130")]
        [InlineData("014011A")]
        [InlineData("014 0113")]
        [InlineData("0140113\n")]
        // アラビア・インド数字
        [InlineData("٠١٤٠١١٣")]
        public void 郵便番号が7桁の数値でなければ例外(string zipCode)
        {
            Assert.Throws<ArgumentException>(() => BarCode.Create(zipCode, "添60-1"));
        }

        [Fact]
        public void 住所の大文字変換は実行環境のカルチャによらない()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                // tr-TR では "i".ToUpper() が "İ" になる
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

                var code = BarCode.Create("1234567", "1-2i");

                Assert.Equal(new[] { "1", "-", "2", "CC1", "8" }, code.Symbols.Skip(8).Take(5));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void シンボル列は書き換えられない()
        {
            var code = BarCode.Create("0140113", "添60-1");

            Assert.False(code.Symbols is string[]);
            var list = Assert.IsAssignableFrom<IList<string>>(code.Symbols);
            Assert.Throws<NotSupportedException>(() => list[1] = "X");
        }

        [Fact]
        public void 引数がnullなら例外()
        {
            Assert.Throws<ArgumentNullException>(() => BarCode.Create(null!, "添60-1"));
            Assert.Throws<ArgumentNullException>(() => BarCode.Create("0140113", null!));
        }

        [Fact]
        public void 常に23シンボルで開始コードと終了コードで挟まれる()
        {
            var code = BarCode.Create("0140113", "");

            Assert.Equal(23, code.Symbols.Count);
            Assert.Equal("STC", code.Symbols[0]);
            Assert.Equal("SPC", code.Symbols[22]);
        }
    }
}
