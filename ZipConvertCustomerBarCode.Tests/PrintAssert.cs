namespace ZipConvertCustomerBarCode.Tests
{
    public static class PrintAssert
    {
        /// <summary>
        /// 実寸で描画した画像に、バーが正しい位置・形で描かれていることを確認する
        /// バーの中心が黒、バーの間とバーのない段が白であることを点で調べる
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <param name="isBlack">ピクセルが黒かどうか</param>
        /// <param name="pixelsPerMillimeter">1mmあたりのピクセル数</param>
        /// <param name="originMillimeters">描画位置(X、Y共通、mm)</param>
        public static void Bars(CustomerBarCode code, Func<int, int, bool> isBlack, double pixelsPerMillimeter, double originMillimeters)
        {
            bool At(double x, double y) =>
                isBlack((int)((originMillimeters + x) * pixelsPerMillimeter), (int)((originMillimeters + y) * pixelsPerMillimeter));

            var bars = code.GetPrintBars().ToList();
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                var centerX = bar.X + bar.Width / 2;
                Assert.True(At(centerX, bar.Y + bar.Height / 2), $"{i}本目のバーが描画されていない");

                // 上段(0～1.2mm)、下段(2.4～3.6mm)
                var hasTop = bar.Type == BarType.Long || bar.Type == BarType.Ascender;
                var hasBottom = bar.Type == BarType.Long || bar.Type == BarType.Descender;
                Assert.Equal(hasTop, At(centerX, 0.6));
                Assert.Equal(hasBottom, At(centerX, 3.0));

                // バーの間(スペース0.6mm)
                if (i < bars.Count - 1)
                {
                    Assert.False(At(bar.X + 0.9, 1.8), $"{i}本目のバーの右が空いていない");
                }
            }
            // 描画位置より左上は空白
            Assert.False(At(-0.3, 1.8));
        }
    }
}
