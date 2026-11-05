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

        internal CustomerBarCode(IEnumerable<string> symbols)
        {
            // 呼び出し側から書き換えられないよう、コピーを読み取り専用で保持する
            Symbols = new ReadOnlyCollection<string>(symbols.ToArray());
        }

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
