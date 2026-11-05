using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ZipConvertCustomerBarCode.Drawing;

namespace ZipConvertCustomerBarCode.Tests
{
    // System.Drawing は Windows のみ対応のため、それ以外ではスキップする
    [SupportedOSPlatform("windows6.1")]
    public class DrawingRenderTests
    {
        public DrawingRenderTests()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "System.Drawing は Windows のみ対応");
        }

        [Theory]
        [MemberData(nameof(TestCases.All), MemberType = typeof(TestCases))]
        public void 画像が1x版とピクセル単位で一致する(string zipCode, string address, string _, string imageFile)
        {
            using var expected = new Bitmap(TestCases.DataPath(imageFile));
            using var actual = BarCode.Create(zipCode, address).ToBitmap();

            AssertSamePixels(expected, actual);
        }

        [Fact]
        public void GIFファイルに出力できる()
        {
            var code = BarCode.Create("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            try
            {
                code.Save(path, ImageFormat.Gif, 2);

                using var saved = new Bitmap(path);
                using var expected = code.ToBitmap(2);
                AssertSamePixels(expected, saved);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertSamePixels(Bitmap expected, Bitmap actual)
        {
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            for (int y = 0; y < expected.Height; y++)
            {
                for (int x = 0; x < expected.Width; x++)
                {
                    Assert.True(expected.GetPixel(x, y).ToArgb() == actual.GetPixel(x, y).ToArgb(), $"({x}, {y}) が一致しない");
                }
            }
        }
    }
}
