namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// バーの種類
    /// </summary>
    public enum BarType
    {
        /// <summary>ロングバー(上段～下段)</summary>
        Long,

        /// <summary>セミロングバー(上段～中段)</summary>
        Ascender,

        /// <summary>セミロングバー(中段～下段)</summary>
        Descender,

        /// <summary>タイミングバー(中段のみ)</summary>
        Timing
    }

    /// <summary>
    /// バー1本の配置(ミリメートル単位、印刷用)
    /// </summary>
    public readonly struct PrintBar
    {
        /// <summary>
        /// バーを生成する
        /// </summary>
        /// <param name="type">バーの種類</param>
        /// <param name="x">X座標(mm)</param>
        /// <param name="y">Y座標(mm)</param>
        /// <param name="width">幅(mm)</param>
        /// <param name="height">高さ(mm)</param>
        public PrintBar(BarType type, double x, double y, double width, double height)
        {
            Type = type;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>バーの種類</summary>
        public BarType Type { get; }

        /// <summary>X座標(mm)</summary>
        public double X { get; }

        /// <summary>Y座標(mm)</summary>
        public double Y { get; }

        /// <summary>幅(mm)</summary>
        public double Width { get; }

        /// <summary>高さ(mm)</summary>
        public double Height { get; }
    }

    /// <summary>
    /// バー1本の配置(ピクセル単位)
    /// </summary>
    public readonly struct Bar
    {
        /// <summary>
        /// バーを生成する
        /// </summary>
        /// <param name="type">バーの種類</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        public Bar(BarType type, int x, int y, int width, int height)
        {
            Type = type;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>バーの種類</summary>
        public BarType Type { get; }

        /// <summary>X座標</summary>
        public int X { get; }

        /// <summary>Y座標</summary>
        public int Y { get; }

        /// <summary>幅</summary>
        public int Width { get; }

        /// <summary>高さ</summary>
        public int Height { get; }
    }
}
