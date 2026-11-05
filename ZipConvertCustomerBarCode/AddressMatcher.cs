using System.Linq;

namespace ZipConvertCustomerBarCode
{
    /// <summary>
    /// 住所の前方一致
    /// 空白、全角・半角、「ヶ」と「ケ」の違いは区別しない
    /// </summary>
    internal static class AddressMatcher
    {
        /// <summary>
        /// 住所の先頭が町域名までの住所と一致する場合、一致した部分の長さを返す
        /// </summary>
        /// <param name="address">住所</param>
        /// <param name="prefix">町域名までの住所</param>
        /// <returns>一致した部分の長さ(住所の空白を含む)。一致しない場合は -1</returns>
        public static int MatchPrefix(string address, string prefix)
        {
            int i = 0;
            foreach (var c in prefix.Where(x => !char.IsWhiteSpace(x)))
            {
                while (i < address.Length && char.IsWhiteSpace(address[i])) i++;
                if (i >= address.Length || Normalize(address[i]) != Normalize(c)) return -1;
                i++;
            }
            return i;
        }

        /// <summary>
        /// 照合用の文字の正規化
        /// </summary>
        private static char Normalize(char c)
        {
            switch (c)
            {
                case 'ヶ': return 'ケ';
                case 'ヵ': return 'カ';
                default: return BarCode.ToHankaku(c);
            }
        }
    }
}
