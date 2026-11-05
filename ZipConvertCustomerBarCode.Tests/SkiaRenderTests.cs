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

        [Fact]
        public void 実寸で描画できる()
        {
            var code = BarCode.Create("1000013", "東京都千代田区霞が関", "1丁目3番2号　郵便プラザ503室");
            using var img = new SKBitmap(1000, 80);
            using (var canvas = new SKCanvas(img))
            {
                canvas.Clear(SKColors.White);
                // 10ピクセル/mm
                code.Draw(canvas, 1, 1, 10);
            }

            PrintAssert.Bars(code, (x, y) => img.GetPixel(x, y) == SKColors.Black, 10, 1);
        }

        [Fact]
        public void PDFに描画できる()
        {
            var code = BarCode.Create("1000013", "東京都千代田区霞が関", "1丁目3番2号　郵便プラザ503室");
            using var stream = new MemoryStream();
            using (var document = SKDocument.CreatePdf(stream))
            {
                // A4
                var canvas = document.BeginPage(210 * CustomerBarCodeSkiaExtensions.PdfUnitsPerMillimeter, 297 * CustomerBarCodeSkiaExtensions.PdfUnitsPerMillimeter);
                code.Draw(canvas, 20, 20, CustomerBarCodeSkiaExtensions.PdfUnitsPerMillimeter);
                document.EndPage();
            }

            Assert.True(stream.Length > 0);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(stream.ToArray(), 0, 4));
        }

        [Fact]
        public void 単位数が0以下なら例外()
        {
            var code = BarCode.Create("1000013", "1-3-2");
            using var img = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(img);

            Assert.Throws<ArgumentOutOfRangeException>(() => code.Draw(canvas, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => code.Draw(canvas, 0, 0, float.NaN));
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
