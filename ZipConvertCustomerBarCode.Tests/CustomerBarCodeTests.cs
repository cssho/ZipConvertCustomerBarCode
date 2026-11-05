using System.Text.RegularExpressions;

namespace ZipConvertCustomerBarCode.Tests
{
    public class CustomerBarCodeTests
    {
        private readonly CustomerBarCode code = BarCode.Create("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");

        [Theory]
        [InlineData(1, 276, 12)]
        [InlineData(3, 828, 36)]
        public void 画像サイズは拡大率に比例する(int scale, int width, int height)
        {
            Assert.Equal(width, code.GetWidth(scale));
            Assert.Equal(height, code.GetHeight(scale));
        }

        [Fact]
        public void 拡大率が1未満なら例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetWidth(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetHeight(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.GetBars(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.ToSvg(0));
        }

        [Fact]
        public void バーは画像の範囲内に収まる()
        {
            foreach (var bar in code.GetBars())
            {
                Assert.InRange(bar.X, 0, code.GetWidth() - bar.Width);
                Assert.InRange(bar.Y, 0, code.GetHeight() - bar.Height);
            }
        }

        [Fact]
        public void バー本数は開始終了コード各2本と他のシンボル各3本()
        {
            Assert.Equal(2 + 21 * 3 + 2, code.GetBars().Count());
        }

        [Fact]
        public void 拡大するとバーの座標と大きさも拡大される()
        {
            var bars1 = code.GetBars(1).ToList();
            var bars2 = code.GetBars(2).ToList();

            Assert.Equal(bars1.Count, bars2.Count);
            for (int i = 0; i < bars1.Count; i++)
            {
                Assert.Equal(bars1[i].Type, bars2[i].Type);
                Assert.Equal(bars1[i].X * 2, bars2[i].X);
                Assert.Equal(bars1[i].Y * 2, bars2[i].Y);
                Assert.Equal(bars1[i].Width * 2, bars2[i].Width);
                Assert.Equal(bars1[i].Height * 2, bars2[i].Height);
            }
        }

        [Theory]
        [InlineData(BarType.Long, 0, 12)]
        [InlineData(BarType.Ascender, 0, 8)]
        [InlineData(BarType.Descender, 4, 8)]
        [InlineData(BarType.Timing, 4, 4)]
        public void バー種別ごとの位置と高さ(BarType type, int y, int height)
        {
            var bars = code.GetBars().Where(x => x.Type == type).ToList();

            Assert.NotEmpty(bars);
            Assert.All(bars, bar =>
            {
                Assert.Equal(y, bar.Y);
                Assert.Equal(height, bar.Height);
                Assert.Equal(2, bar.Width);
            });
        }

        [Fact]
        public void SVGにはサイズと全てのバーが含まれる()
        {
            var svg = code.ToSvg(2);

            Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"552\" height=\"24\"", svg);
            Assert.EndsWith("</svg>", svg);

            var rects = Regex.Matches(svg, "<rect x=\"(\\d+)\" y=\"(\\d+)\" width=\"(\\d+)\" height=\"(\\d+)\"/>")
                .Select(m => (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value)));
            var bars = code.GetBars(2).Select(b => (b.X, b.Y, b.Width, b.Height));
            Assert.Equal(bars, rects);
        }
    }
}
