using SkiaSharp;
using ZipConvertCustomerBarCode.Skia;

namespace ZipConvertCustomerBarCode.Tests
{
    public class SkiaRenderTests
    {
        [Theory]
        [MemberData(nameof(TestCases.All), MemberType = typeof(TestCases))]
        public void 画像が1x版とピクセル単位で一致する(string zipCode, string address, string _, string imageFile)
        {
            using var expected = SKBitmap.Decode(TestCases.DataPath(imageFile));
            using var actual = BarCode.Create(zipCode, address).ToSKBitmap();

            AssertSamePixels(expected, actual);
        }

        [Fact]
        public void PNGをデコードすると描画結果と一致する()
        {
            var code = BarCode.Create("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");
            using var bitmap = code.ToSKBitmap();
            using var png = SKBitmap.Decode(code.ToPng());

            AssertSamePixels(bitmap, png);
        }

        [Fact]
        public void PNGファイルに出力できる()
        {
            var code = BarCode.Create("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try
            {
                code.SavePng(path, 2);

                using var saved = SKBitmap.Decode(path);
                using var expected = code.ToSKBitmap(2);
                AssertSamePixels(expected, saved);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertSamePixels(SKBitmap expected, SKBitmap actual)
        {
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            for (int y = 0; y < expected.Height; y++)
            {
                for (int x = 0; x < expected.Width; x++)
                {
                    Assert.True(expected.GetPixel(x, y) == actual.GetPixel(x, y), $"({x}, {y}) が一致しない");
                }
            }
        }
    }
}
