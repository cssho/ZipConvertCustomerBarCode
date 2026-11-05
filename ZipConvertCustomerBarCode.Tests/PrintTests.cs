using System.Text.RegularExpressions;

namespace ZipConvertCustomerBarCode.Tests
{
    // 印刷用の実寸
    // http://www.post.japanpost.jp/zipcode/zipmanual/p12.html
    public class PrintTests
    {
        private readonly CustomerBarCode code = BarCode.Create("1000013", "東京都千代田区霞が関", "1丁目3番2号　郵便プラザ503室");

        [Theory]
        // バー67本: (67 - 1) × バーピッチ + バー幅
        [InlineData(10, 79.8, 3.6)]
        [InlineData(8, 63.84, 2.88)]
        [InlineData(11.5, 91.77, 4.14)]
        public void バーコードの大きさはポイントに比例する(double point, double width, double height)
        {
            Assert.Equal(width, code.GetPrintWidth(point), 6);
            Assert.Equal(height, code.GetPrintHeight(point), 6);
        }

        [Theory]
        [InlineData(7.9)]
        [InlineData(11.6)]
        [InlineData(double.NaN)]
        public void ポイントが8から11点5の範囲外なら例外(double point)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetPrintWidth(point));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetPrintHeight(point));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetPrintBars(point));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.ToPrintSvg(point));
        }

        [Fact]
        public void バーの寸法はマニュアルどおり()
        {
            var bars = code.GetPrintBars().ToList();

            Assert.Equal(67, bars.Count);
            Assert.Equal(0, bars[0].X, 6);
            for (int i = 1; i < bars.Count; i++)
            {
                // バーピッチ 1.2mm
                Assert.Equal(1.2, bars[i].X - bars[i - 1].X, 6);
            }
            Assert.All(bars, bar =>
            {
                // バー幅 0.6mm
                Assert.Equal(0.6, bar.Width, 6);
                // ロングバー 3.6mm、セミロングバー 2.4mm、タイミングバー 1.2mm
                var (y, height) = bar.Type switch
                {
                    BarType.Long => (0.0, 3.6),
                    BarType.Ascender => (0.0, 2.4),
                    BarType.Descender => (1.2, 2.4),
                    _ => (1.2, 1.2),
                };
                Assert.Equal(y, bar.Y, 6);
                Assert.Equal(height, bar.Height, 6);
            });
        }

        [Fact]
        public void 印刷用のバーはピクセル単位のバーと同じ種類と並び()
        {
            Assert.Equal(code.GetBars().Select(x => x.Type), code.GetPrintBars().Select(x => x.Type));
        }

        [Fact]
        public void 印刷用SVGはmm単位で余白を含む()
        {
            var svg = code.ToPrintSvg(10, 2);

            Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"83.8mm\" height=\"7.6mm\" viewBox=\"0 0 83.8 7.6\">", svg);
            Assert.EndsWith("</svg>", svg);

            var rects = Regex.Matches(svg, "<rect x=\"([0-9.]+)\" y=\"([0-9.]+)\" width=\"([0-9.]+)\" height=\"([0-9.]+)\"/>").ToList();
            Assert.Equal(67, rects.Count);
            // 最初のバーは余白(2mm)の位置から
            Assert.Equal("2", rects[0].Groups[1].Value);
            Assert.Equal("2", rects[0].Groups[2].Value);
            Assert.Equal("0.6", rects[0].Groups[3].Value);
            Assert.Equal("3.6", rects[0].Groups[4].Value);
        }

        [Fact]
        public void 余白が負なら例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => code.ToPrintSvg(10, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.ToPrintSvg(10, double.NaN));
        }
    }
}
