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
            try
            {
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
            catch
            {
                // 描画に失敗した場合は生成済みのイメージを破棄する
                img.Dispose();
                throw;
            }
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

        /// <summary>
        /// PDF(SKDocument.CreatePdf)のキャンバスで1mmあたりの単位数(1単位 = 1/72インチ)
        /// </summary>
        public const float PdfUnitsPerMillimeter = 72f / 25.4f;

        /// <summary>
        /// 印刷用の実寸(mm)でカスタマバーコードを描画する
        /// PDF などに、用紙の指定位置に描画するときに使う
        /// </summary>
        /// <remarks>
        /// バーのみを黒で描画する(背景は描画しない)。
        /// マニュアルでは上下左右に2mm以上の空白が必要。
        /// </remarks>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="canvas">描画先</param>
        /// <param name="x">最初のバーの左端のX座標(mm)</param>
        /// <param name="y">バーの上端のY座標(mm)</param>
        /// <param name="unitsPerMillimeter">キャンバスの1mmあたりの単位数(PDF は <see cref="PdfUnitsPerMillimeter"/>)</param>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        public static void Draw(this CustomerBarCode code, SKCanvas canvas, float x, float y, float unitsPerMillimeter, double point = 10)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (!(unitsPerMillimeter > 0)) throw new ArgumentOutOfRangeException(nameof(unitsPerMillimeter), "1mmあたりの単位数は0より大きい値で指定してください。");

            var bars = code.GetPrintBars(point);
            using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false })
            {
                foreach (var bar in bars)
                {
                    canvas.DrawRect(SKRect.Create(
                        (x + (float)bar.X) * unitsPerMillimeter,
                        (y + (float)bar.Y) * unitsPerMillimeter,
                        (float)bar.Width * unitsPerMillimeter,
                        (float)bar.Height * unitsPerMillimeter), paint);
                }
            }
        }
    }
}
