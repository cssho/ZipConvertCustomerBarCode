using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// 住所の照合
    /// 空白、全角・半角、英字の大文字・小文字、「ヶ」と「ケ」、漢数字と算用数字(六条 と 6条)の違いは区別しない
    /// </summary>
    internal sealed class AddressMatcher
    {
        // 漢数字(BarCode.KanjiToNumber で変換できるもの)
        private static readonly string KanjiNumerals = BarCode.KanjiNumerals;

        // 階数(地下は除く)
        private static readonly Regex FloorRegex = new Regex(@"\G(?<floor>[0-9]+)(階|[Ff])");

        // 照合用に正規化した住所と、正規化後の各文字に対応する元の住所の位置(その文字の直後)
        private readonly string normalized;
        private readonly int[] ends;

        /// <summary>
        /// 照合する住所を正規化する
        /// </summary>
        /// <param name="address">住所</param>
        public AddressMatcher(string address)
        {
            var sb = new StringBuilder(address.Length);
            var list = new List<int>(address.Length);
            Normalize(address, sb, list);
            normalized = sb.ToString();
            ends = list.ToArray();
        }

        /// <summary>
        /// 住所の先頭が町域名までの住所と一致する場合、一致した部分の長さを返す
        /// </summary>
        /// <param name="prefix">町域名までの住所</param>
        /// <returns>一致した部分の長さ(元の住所での長さ)。一致しない場合は -1</returns>
        public int MatchPrefix(string prefix)
        {
            return MatchPrefix(prefix, 0);
        }

        /// <summary>
        /// 住所の指定位置以降が町域名と一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="prefix">町域名など</param>
        /// <param name="start">照合を始める位置(元の住所での位置)</param>
        /// <returns>一致した部分の終わり(元の住所での位置)。一致しない場合は -1</returns>
        public int MatchPrefix(string prefix, int start)
        {
#if NET8_0_OR_GREATER
            // 正規化した文字列を作らず、スタック上で正規化して比べる(漢数字は1文字が最大4文字になる)
            var size = prefix.Length * 4;
            Span<char> buffer = size <= 256 ? stackalloc char[size] : new char[size];
            return MatchNormalizedPrefix(buffer.Slice(0, NormalizeTo(prefix, buffer)), start);
#else
            return MatchNormalizedPrefix(Normalize(prefix), start);
#endif
        }

        /// <summary>
        /// 住所の指定位置以降が、正規化済みの町域名などと一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="target">正規化済みの町域名など(<see cref="Normalize(string)"/>)</param>
        /// <param name="start">照合を始める位置(元の住所での位置)</param>
        /// <returns>一致した部分の終わり(元の住所での位置)。一致しない場合は -1</returns>
        public int MatchNormalizedPrefix(string target, int start)
        {
#if NET8_0_OR_GREATER
            return MatchNormalizedPrefix(target.AsSpan(), start);
#else
            var offset = NormalizedIndex(start);
            if (target.Length == 0) return start;
            if (normalized.Length - offset < target.Length || string.CompareOrdinal(normalized, offset, target, 0, target.Length) != 0) return -1;
            return MatchEnd(offset, target.Length);
#endif
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// 住所の指定位置以降が、正規化済みの町域名などと一致する場合、一致した部分の終わりの位置を返す
        /// </summary>
        /// <param name="target">正規化済みの町域名など</param>
        /// <param name="start">照合を始める位置(元の住所での位置)</param>
        /// <returns>一致した部分の終わり(元の住所での位置)。一致しない場合は -1</returns>
        public int MatchNormalizedPrefix(ReadOnlySpan<char> target, int start)
        {
            var offset = NormalizedIndex(start);
            if (target.Length == 0) return start;
            if (!normalized.AsSpan(offset).StartsWith(target, StringComparison.Ordinal)) return -1;
            return MatchEnd(offset, target.Length);
        }
#endif

        /// <summary>
        /// 正規化後の住所で一致した部分の終わりを、元の住所での位置に変換する
        /// </summary>
        /// <param name="offset">一致した部分の開始位置(正規化後)</param>
        /// <param name="length">一致した部分の長さ(正規化後)</param>
        /// <returns>一致した部分の終わり(元の住所での位置)。漢数字の途中で終わる場合は -1</returns>
        private int MatchEnd(int offset, int length)
        {
            // 漢数字の途中で終わる場合(「二十」の「2」だけが一致した場合など)は一致としない
            var last = offset + length - 1;
            if (last + 1 < normalized.Length && ends[last + 1] == ends[last]) return -1;
            return ends[last];
        }

        /// <summary>
        /// 住所の指定位置以降で、正規化済みの文字列(ビル名など)が現れる場合、その終わりの位置を返す
        /// </summary>
        /// <param name="target">正規化済みの文字列(<see cref="Normalize(string)"/>)</param>
        /// <param name="start">探し始める位置(元の住所での位置)</param>
        /// <returns>見つかった文字列の終わり(元の住所での位置)。見つからない場合は -1</returns>
        public int IndexOfNormalized(string target, int start)
        {
            if (target.Length == 0) return -1;
            var index = normalized.IndexOf(target, NormalizedIndex(start), StringComparison.Ordinal);
            return index < 0 ? -1 : ends[index + target.Length - 1];
        }

        /// <summary>
        /// 指定位置(空白は除く)から始まる階数を返す(「10階」「10F」)
        /// </summary>
        /// <param name="start">位置(元の住所での位置)</param>
        /// <returns>階数。地下(「地下1階」「B1F」)や、階数が書かれていない場合は null</returns>
        public int? FloorAt(int start)
        {
            var match = FloorRegex.Match(normalized, NormalizedIndex(start));
            if (!match.Success) return null;
            return int.TryParse(match.Groups["floor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var floor) ? floor : (int?)null;
        }

        /// <summary>
        /// 指定位置(空白は除く)から始まる数値を返す
        /// </summary>
        /// <param name="start">位置(元の住所での位置)</param>
        /// <returns>数値。指定位置が数値で始まらない場合は null</returns>
        public int? LeadingNumber(int start)
        {
            int i = NormalizedIndex(start);
            if (i >= normalized.Length || !IsDigit(normalized[i])) return null;

            long value = 0;
            for (; i < normalized.Length && IsDigit(normalized[i]); i++)
            {
                value = value * 10 + (normalized[i] - '0');
                if (value > int.MaxValue) return null;
            }
            return (int)value;
        }

        /// <summary>
        /// 住所を照合用に正規化する
        /// </summary>
        /// <param name="value">住所</param>
        /// <returns>正規化した住所</returns>
        public static string Normalize(string value)
        {
            var sb = new StringBuilder(value.Length);
            Normalize(value, sb, null);
            return sb.ToString();
        }

        /// <summary>
        /// 住所を照合用に正規化する
        /// </summary>
        /// <param name="value">住所</param>
        /// <param name="sb">正規化した住所の出力先</param>
        /// <param name="ends">正規化後の各文字に対応する元の位置(その文字の直後)の出力先</param>
        private static void Normalize(string value, StringBuilder sb, List<int> ends)
        {
            int i = 0;
            while (i < value.Length)
            {
                var c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // 漢数字の並びは算用数字に変換する
                var kanjiEnd = KanjiRunEnd(value, i);
                if (kanjiEnd > i)
                {
                    foreach (var d in BarCode.KanjiToNumber(value, i, kanjiEnd - i))
                    {
                        sb.Append(d);
                        ends?.Add(kanjiEnd);
                    }
                    i = kanjiEnd;
                    continue;
                }

                sb.Append(NormalizeChar(c));
                ends?.Add(i + 1);
                i++;
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// 住所を照合用に正規化して書き込む(<see cref="Normalize(string)"/> の文字列を作らない版)
        /// </summary>
        /// <param name="value">住所</param>
        /// <param name="destination">書き込み先(住所の長さの4倍あれば足りる)</param>
        /// <returns>書き込んだ文字数</returns>
        private static int NormalizeTo(string value, Span<char> destination)
        {
            int i = 0, written = 0;
            while (i < value.Length)
            {
                var c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // 漢数字の並びは算用数字に変換する
                var kanjiEnd = KanjiRunEnd(value, i);
                if (kanjiEnd > i)
                {
                    written += BarCode.WriteKanjiNumber(value, i, kanjiEnd - i, destination.Slice(written));
                    i = kanjiEnd;
                    continue;
                }

                destination[written++] = NormalizeChar(c);
                i++;
            }
            return written;
        }
#endif

        /// <summary>
        /// 指定位置から漢数字が続く場合、その終わりの位置を返す
        /// </summary>
        /// <returns>漢数字の並びの終わりの位置。漢数字で始まらない場合は指定位置</returns>
        private static int KanjiRunEnd(string value, int start)
        {
            int end = start;
            while (end < value.Length && KanjiNumerals.IndexOf(value[end]) >= 0) end++;
            return end;
        }

        /// <summary>
        /// 元の住所での位置を正規化後の位置に変換する
        /// </summary>
        private int NormalizedIndex(int start)
        {
            int i = 0;
            while (i < ends.Length && ends[i] <= start) i++;
            return i;
        }

        /// <summary>
        /// 照合用の文字の正規化(全角→半角、英字は大文字、「ヶ」→「ケ」)
        /// </summary>
        private static char NormalizeChar(char c)
        {
            switch (c)
            {
                case 'ヶ': return 'ケ';
                case 'ヵ': return 'カ';
                default:
                    // 全角英字は ToHankaku で半角になるため、大文字にするのは半角の a～z のみ
                    // (char.ToUpperInvariant は住所の分割で1件あたり約1μs遅くなるため使わない)
                    var hankaku = BarCode.ToHankaku(c);
                    return hankaku >= 'a' && hankaku <= 'z' ? (char)(hankaku - 'a' + 'A') : hankaku;
            }
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
