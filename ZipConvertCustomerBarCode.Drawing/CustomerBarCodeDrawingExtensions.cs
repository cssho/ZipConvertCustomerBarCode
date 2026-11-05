using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ZipConvertCustomerBarCode.Drawing
{
    /// <summary>
    /// System.Drawing によるカスタマバーコードの描画
    /// </summary>
    public static class CustomerBarCodeDrawingExtensions
    {
        /// <summary>
        /// カスタマバーコードのイメージを返す
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        /// <returns>バーコードイメージ</returns>
        public static Bitmap ToBitmap(this CustomerBarCode code, int scale = 1)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));

            var img = new Bitmap(code.GetWidth(scale), code.GetHeight(scale));
            try
            {
                using (var gr = Graphics.FromImage(img))
                {
                    gr.Clear(Color.White);
                    foreach (var bar in code.GetBars(scale))
                    {
                        gr.FillRectangle(Brushes.Black, bar.X, bar.Y, bar.Width, bar.Height);
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
        /// 指定したパスにカスタマバーコードを出力する
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="outputPath">出力先</param>
        /// <param name="format">画像形式</param>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        public static void Save(this CustomerBarCode code, string outputPath, ImageFormat format, int scale = 1)
        {
            using (var img = code.ToBitmap(scale))
            {
                img.Save(outputPath, format);
            }
        }

        /// <summary>
        /// 印刷用の実寸(mm)でカスタマバーコードを描画する
        /// 宛名印刷(PrintDocument など)で、用紙の指定位置に描画するときに使う
        /// </summary>
        /// <remarks>
        /// バーのみを黒で描画する(背景は描画しない)。
        /// マニュアルでは上下左右に2mm以上の空白が必要。
        /// </remarks>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="graphics">描画先</param>
        /// <param name="x">最初のバーの左端のX座標(mm)</param>
        /// <param name="y">バーの上端のY座標(mm)</param>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        public static void Draw(this CustomerBarCode code, Graphics graphics, float x, float y, double point = 10)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (graphics == null) throw new ArgumentNullException(nameof(graphics));

            var bars = code.GetPrintBars(point);
            var pageUnit = graphics.PageUnit;
            var pageScale = graphics.PageScale;
            try
            {
                graphics.PageUnit = GraphicsUnit.Millimeter;
                graphics.PageScale = 1;
                foreach (var bar in bars)
                {
                    graphics.FillRectangle(Brushes.Black, x + (float)bar.X, y + (float)bar.Y, (float)bar.Width, (float)bar.Height);
                }
            }
            finally
            {
                graphics.PageUnit = pageUnit;
                graphics.PageScale = pageScale;
            }
        }
    }
}
