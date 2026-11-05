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

        [Theory]
        [InlineData("0140113")]
        [InlineData("014-0113")]
        [InlineData("０１４０１１３")]
        [InlineData("０１４‐０１１３")]
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
        public void 郵便番号が7桁の数値でなければ例外(string zipCode)
        {
            Assert.Throws<ArgumentException>(() => BarCode.Create(zipCode, "添60-1"));
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
