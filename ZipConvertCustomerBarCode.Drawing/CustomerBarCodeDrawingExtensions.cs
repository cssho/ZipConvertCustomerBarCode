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
    }
}
