using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// 生成済みのカスタマバーコード
    /// 描画ライブラリに依存しないバーの配置情報を持つ
    /// </summary>
    public sealed class CustomerBarCode
    {
        // 1シンボルの幅・高さ(scale=1のときのピクセル数)
        private const int CellWidth = 12;
        private const int CellHeight = 12;

        // バーの幅、バーの間隔
        private const int BarWidth = 2;
        private const int BarPitch = 4;

        // 印刷時の大きさ(ポイント)。マニュアルでは8～11.5ポイント相当を許す
        private const double DefaultPoint = 10;
        private const double MinPoint = 8;
        private const double MaxPoint = 11.5;

        // シンボル→バー配置辞書
        // L:ロングバー、A:セミロングバー(上)、D:セミロングバー(下)、T:タイミングバー、空白:バーなし
        private static readonly Dictionary<string, string> BarPatternDic = new Dictionary<string, string>()
        {
            {"0","LTT"},{"1","LLT"},{"2","LDA"},{"3","DLA"},{"4","LAD"},
            {"5","LTL"},{"6","DAL"},{"7","ALD"},{"8","ADL"},{"9","TLL"},
            {"-","TLT"},{"CC1","DAT"},{"CC2","DTA"},{"CC3","ADT"},{"CC4","TDA"},
            {"CC5","ATD"},{"CC6","TAD"},{"CC7","TTL"},{"CC8","LLL"},
            {"STC"," LD"},{"SPC","DL "}
        };

        internal CustomerBarCode(string zipCode, string addressNumber, bool isRepresentativeZipCode, IEnumerable<string> symbols)
        {
            ZipCode = zipCode;
            AddressNumber = addressNumber;
            IsRepresentativeZipCode = isRepresentativeZipCode;
            // 呼び出し側から書き換えられないよう、コピーを読み取り専用で保持する
            Symbols = new ReadOnlyCollection<string>(symbols.ToArray());
        }

        /// <summary>
        /// 郵便番号(半角数字7桁、ハイフンなし)
        /// </summary>
        public string ZipCode { get; }

        /// <summary>
        /// 住所から抜き出した住所表示番号(マニュアルの「バーコード・データ」、例: 3-20-5B604)
        /// 13けたで切り捨てる前の値で、バーコードに含まれるのは先頭13けた分
        /// </summary>
        public string AddressNumber { get; }

        /// <summary>
        /// 代表の郵便番号かどうか
        /// </summary>
        /// <remarks>
        /// 代表の郵便番号(郵便番号簿の「以下に掲載がない場合」の番号)の郵便物には、カスタマバーコードを付けない。
        /// 郵便番号データ(<see cref="ZipCodeDictionary"/>)を渡して生成した場合は、郵便番号データに基づいて判定する。
        /// 渡さずに生成した場合は、マニュアルの「原則として下2けたが"00"」に従って判定する目安。
        /// http://www.post.japanpost.jp/zipcode/zipmanual/p18.html
        /// </remarks>
        public bool IsRepresentativeZipCode { get; }

        /// <summary>
        /// シンボル列
        /// 開始コード("STC")、郵便番号、住所表示番号、チェックデジット、終了コード("SPC")の順
        /// 住所表示番号の制御コードは"CC1"～"CC8"
        /// </summary>
        public IReadOnlyList<string> Symbols { get; }

        /// <summary>
        /// 画像の幅
        /// </summary>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        public int GetWidth(int scale = 1)
        {
            CheckScale(scale);
            return Symbols.Count * CellWidth * scale;
        }

        /// <summary>
        /// 画像の高さ
        /// </summary>
        /// <param name="scale">拡大率(1で12ピクセル)</param>
        public int GetHeight(int scale = 1)
        {
            CheckScale(scale);
            return CellHeight * scale;
        }

        /// <summary>
        /// バーの配置を返す
        /// 背景は白、バーは黒で描画する想定
        /// </summary>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        /// <returns>バー配置</returns>
        public IEnumerable<Bar> GetBars(int scale = 1)
        {
            CheckScale(scale);
            return Symbols.SelectMany((symbol, i) =>
                BarPatternDic[symbol].Select((type, j) => new { Type = type, X = i * CellWidth + j * BarPitch }))
                .Where(x => x.Type != ' ')
                .Select(x => CreateBar(x.Type, x.X, scale));
        }

        /// <summary>
        /// SVG形式の文字列を返す
        /// </summary>
        /// <param name="scale">拡大率(1で1シンボル12ピクセル)</param>
        /// <returns>SVG</returns>
        public string ToSvg(int scale = 1)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\" shape-rendering=\"crispEdges\">",
                GetWidth(scale), GetHeight(scale));
            sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>");
            foreach (var bar in GetBars(scale))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\"/>",
                    bar.X, bar.Y, bar.Width, bar.Height);
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// 印刷時のバーコードの幅(mm、余白を含まない)
        /// </summary>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        public double GetPrintWidth(double point = DefaultPoint)
        {
            var bars = GetBars().ToList();
            var width = bars[bars.Count - 1].X + BarWidth - bars[0].X;
            return width * MillimetersPerPixel(point);
        }

        /// <summary>
        /// 印刷時のバーコードの高さ(mm、余白を含まない)
        /// </summary>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        public double GetPrintHeight(double point = DefaultPoint)
        {
            return CellHeight * MillimetersPerPixel(point);
        }

        /// <summary>
        /// 印刷時のバーの配置を返す(mm単位、左上の基準は最初のバーの左上)
        /// </summary>
        /// <remarks>
        /// 寸法は郵便番号・バーコードマニュアルに従う(10ポイントのときバー幅0.6mm、バーピッチ1.2mm、ロングバー3.6mm)。
        /// http://www.post.japanpost.jp/zipcode/zipmanual/p12.html
        /// </remarks>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        /// <returns>バー配置</returns>
        public IEnumerable<PrintBar> GetPrintBars(double point = DefaultPoint)
        {
            var mm = MillimetersPerPixel(point);
            var bars = GetBars().ToList();
            var offset = bars[0].X;
            return bars.Select(bar => new PrintBar(bar.Type, (bar.X - offset) * mm, bar.Y * mm, bar.Width * mm, bar.Height * mm));
        }

        /// <summary>
        /// 印刷用のSVG形式の文字列を返す(サイズはmm単位)
        /// </summary>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        /// <param name="margin">上下左右の余白(mm)。マニュアルでは2mm以上の空白が必要</param>
        /// <returns>SVG</returns>
        public string ToPrintSvg(double point = DefaultPoint, double margin = 2)
        {
            if (double.IsNaN(margin) || margin < 0) throw new ArgumentOutOfRangeException(nameof(margin), "余白は0以上で指定してください。");

            var width = GetPrintWidth(point) + margin * 2;
            var height = GetPrintHeight(point) + margin * 2;
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}mm\" height=\"{1}mm\" viewBox=\"0 0 {0} {1}\">",
                Round(width), Round(height));
            sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>");
            foreach (var bar in GetPrintBars(point))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\"/>",
                    Round(bar.X + margin), Round(bar.Y + margin), Round(bar.Width), Round(bar.Height));
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// 1ピクセル(scale=1)あたりのmm
        /// </summary>
        /// <param name="point">大きさ(8～11.5ポイント)</param>
        private static double MillimetersPerPixel(double point)
        {
            if (double.IsNaN(point) || point < MinPoint || point > MaxPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(point), "大きさは8～11.5ポイントで指定してください。");
            }
            // バー幅(2ピクセル)が 0.6mm × ポイント / 10
            return 0.6 * point / 10 / BarWidth;
        }

        /// <summary>
        /// SVG出力用の丸め(浮動小数点の誤差を出力しないため)
        /// </summary>
        private static double Round(double value) => Math.Round(value, 4);

        /// <summary>
        /// バー生成
        /// </summary>
        /// <param name="type">バー種別文字</param>
        /// <param name="x">X座標(scale=1)</param>
        /// <param name="scale">拡大率</param>
        /// <returns>バー</returns>
        private static Bar CreateBar(char type, int x, int scale)
        {
            // 上段・中段・下段を各4ピクセルとして、バー種別ごとの範囲を決める
            switch (type)
            {
                case 'L': return new Bar(BarType.Long, x * scale, 0, BarWidth * scale, 12 * scale);
                case 'A': return new Bar(BarType.Ascender, x * scale, 0, BarWidth * scale, 8 * scale);
                case 'D': return new Bar(BarType.Descender, x * scale, 4 * scale, BarWidth * scale, 8 * scale);
                case 'T': return new Bar(BarType.Timing, x * scale, 4 * scale, BarWidth * scale, 4 * scale);
                default: throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// 拡大率チェック
        /// </summary>
        /// <param name="scale">拡大率</param>
        private static void CheckScale(int scale)
        {
            if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale), "拡大率は1以上で指定してください。");
        }
    }
}
