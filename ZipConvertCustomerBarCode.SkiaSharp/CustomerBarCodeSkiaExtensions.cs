using System;
using System.IO;
using SkiaSharp;

namespace ZipConvertCustomerBarCode.Skia
{
    /// <summary>
    /// SkiaSharp によるカスタマバーコードの描画
    /// </summary>
    public static class CustomerBarCodeSkiaExtensions
    {
        /// <summary>
        /// カスタマバーコードのイメージを返す
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        /// <returns>バーコードイメージ</returns>
        public static SKBitmap ToSKBitmap(this CustomerBarCode code, int scale = 1)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));

            var img = new SKBitmap(code.GetWidth(scale), code.GetHeight(scale));
            using (var canvas = new SKCanvas(img))
            using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false })
            {
                canvas.Clear(SKColors.White);
                foreach (var bar in code.GetBars(scale))
                {
                    canvas.DrawRect(SKRect.Create(bar.X, bar.Y, bar.Width, bar.Height), paint);
                }
            }
            return img;
        }

        /// <summary>
        /// カスタマバーコードをPNG形式のバイト列で返す
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        /// <returns>PNGデータ</returns>
        public static byte[] ToPng(this CustomerBarCode code, int scale = 1)
        {
            using (var img = code.ToSKBitmap(scale))
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }

        /// <summary>
        /// 指定したパスにカスタマバーコードをPNG形式で出力する
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="outputPath">出力先</param>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        public static void SavePng(this CustomerBarCode code, string outputPath, int scale = 1)
        {
            File.WriteAllBytes(outputPath, code.ToPng(scale));
        }
    }
}
