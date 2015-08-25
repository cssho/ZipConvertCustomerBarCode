using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.RegularExpressions;
using ZipConvertCustomerBarCode.Properties;

namespace ZipConvertCustomerBarCode
{
    public static class BarCode
    {
        // 開始コード
        private static readonly string startCode = "STC";

        // 終了コード
        private static readonly string endCode = "SPC";

        // 全角→半角辞書
        private static readonly Dictionary<char, char> HankakuDic = new Dictionary<char, char>() {
            {'１','1'},{'２','2'},{'３','3'},{'４','4'},{'５','5'},
            {'６','6'},{'７','7'},{'８','8'},{'９','9'},{'０','0'},
            {'Ａ','A'},{'Ｂ','B'},{'Ｃ','C'},{'Ｄ','D'},{'Ｅ','E'},
            {'Ｆ','F'},{'Ｇ','G'},{'Ｈ','H'},{'Ｉ','I'},{'Ｊ','J'},
            {'Ｋ','K'},{'Ｌ','L'},{'Ｍ','M'},{'Ｎ','N'},{'Ｏ','O'},
            {'Ｐ','P'},{'Ｑ','Q'},{'Ｒ','R'},{'Ｓ','S'},{'Ｔ','T'},
            {'Ｕ','U'},{'Ｖ','V'},{'Ｗ','W'},{'Ｘ','X'},{'Ｙ','Y'},
            {'Ｚ','Z'},
            {'ａ','a'},{'ｂ','b'},{'ｃ','c'},{'ｄ','d'},{'ｅ','e'},
            {'ｆ','f'},{'ｇ','g'},{'ｈ','h'},{'ｉ','i'},{'ｊ','j'},
            {'ｋ','k'},{'ｌ','l'},{'ｍ','m'},{'ｎ','n'},{'ｏ','o'},
            {'ｐ','p'},{'ｑ','q'},{'ｒ','r'},{'ｓ','s'},{'ｔ','t'},
            {'ｕ','u'},{'ｖ','v'},{'ｗ','w'},{'ｘ','x'},{'ｙ','y'},
            {'ｚ','z'},
            {'　',' '},{'‐','-'}
        };

        // 文字→バーコード辞書
        private static readonly Dictionary<string, string[]> BarcodeCharDic = new Dictionary<string, string[]>()
        {
            {"A",new string[]{"CC1","0"}},{"B",new string[]{"CC1","1"}},{"C",new string[]{"CC1","2"}},
            {"D",new string[]{"CC1","3"}},{"E",new string[]{"CC1","4"}},{"F",new string[]{"CC1","5"}},
            {"G",new string[]{"CC1","6"}},{"H",new string[]{"CC1","7"}},{"I",new string[]{"CC1","8"}},
            {"J",new string[]{"CC1","9"}},{"K",new string[]{"CC2","0"}},{"L",new string[]{"CC2","1"}},
            {"M",new string[]{"CC2","2"}},{"N",new string[]{"CC2","3"}},{"O",new string[]{"CC2","4"}},
            {"P",new string[]{"CC2","5"}},{"Q",new string[]{"CC2","6"}},{"R",new string[]{"CC2","7"}},
            {"S",new string[]{"CC2","8"}},{"T",new string[]{"CC2","9"}},{"U",new string[]{"CC3","0"}},
            {"V",new string[]{"CC3","1"}},{"W",new string[]{"CC3","2"}},{"X",new string[]{"CC3","3"}},
            {"Y",new string[]{"CC3","4"}},{"Z",new string[]{"CC3","5"}},{"0",new string[]{"0"}}
            ,{"1",new string[]{"1"}},{"2",new string[]{"2"}},{"3",new string[]{"3"}},{"4",new string[]{"4"}}
            ,{"5",new string[]{"5"}},{"6",new string[]{"6"}},{"7",new string[]{"7"}},{"8",new string[]{"8"}}
            ,{"9",new string[]{"9"}},{"-",new string[]{"-"}}
        };

        // チェックデジット計算用辞書
        private static readonly Dictionary<string, int> CheckSumDic = new Dictionary<string, int>()
        {
            {"0",0},{"1",1},{"2",2},{"3",3},{"4",4},{"5",5},{"6",6},{"7",7},{"8",8},{"9",9},
            {"-",10},{"CC1",11},{"CC2",12},{"CC3",13},{"CC4",14},{"CC5",15},{"CC6",16},{"CC7",17},{"CC8",18}
        };

        // 画像リソースキー辞書
        private static readonly Dictionary<string, string> ImageKeyDic = new Dictionary<string, string>()
        {
            {"0","Num0"},{"1","Num1"},{"2","Num2"},{"3","Num3"},{"4","Num4"},{"5","Num5"},
            {"6","Num6"},{"7","Num7"},{"8","Num8"},{"9","Num9"},{"-","Hyphen"},{"CC1","CC1"},
            {"CC2","CC2"},{"CC3","CC3"},{"CC4","CC4"},{"CC5","CC5"},{"CC6","CC6"},{"CC7","CC7"},
            {"CC8","CC8"},{"STC","Start"},{"SPC","End"}
        };

        // アルファベット
        private static readonly string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// 指定したパスにカスタマバーコードを出力する。
        /// </summary>
        /// <param name="postCode">郵便番号(ハイフンなし半角数字7桁)</param>
        /// <param name="zip">住所</param>
        /// <param name="outputPath">出力先</param>
        public static void Export(string postCode, string zip, string outputPath)
        {
            var code = ConvertCustomerBarCode(zip, CheckPostCode(postCode));
            using (var img = CreateImage(code))
            {
                img.Save(outputPath, ImageFormat.Gif);
            }
        }

        /// <summary>
        /// カスタマバーコードのイメージを返す
        /// </summary>
        /// <param name="postCode">郵便番号(ハイフンなし半角数字7桁)</param>
        /// <param name="zip">住所</param>
        /// <returns>バーコードイメージ</returns>
        public static Bitmap CreateImage(string postCode, string zip)
        {
            var code = ConvertCustomerBarCode(zip, CheckPostCode(postCode));
            var img = CreateImage(code);
            return img;
        }

        /// <summary>
        /// 郵便番号チェック
        /// ハイフンを除いて数値7桁であること
        /// </summary>
        /// <param name="postCode">郵便番号</param>
        /// <returns>郵便番号配列</returns>
        private static IEnumerable<string> CheckPostCode(string postCode)
        {
            // 全角→半角
            postCode = string.Join("", postCode.Select(n => (HankakuDic.ContainsKey(n) ? HankakuDic[n] : n)));

            // ハイフン除去
            postCode = postCode.Replace("-", "");

            if (!Regex.IsMatch(postCode, @"\A\d{7}\Z"))
            {
                throw new ArgumentException("郵便番号は7桁の数値で入力してください。");
            }

            return postCode.Select(x => x.ToString());

        }

        /// <summary>
        /// イメージ生成
        /// </summary>
        /// <param name="code">カスタマバーコード</param>
        /// <returns>バーコードイメージ</returns>
        private static Bitmap CreateImage(IEnumerable<string> code)
        {
            var img = new Bitmap(276, 12);
            var rmgr = Resources.ResourceManager;
            using (var gr = Graphics.FromImage(img))
            {
                foreach (var c in code.Select((x, i) => new { Value = x, Index = i }))
                {
                    gr.DrawImage((Image)rmgr.GetObject(ImageKeyDic[c.Value]), c.Index * 12, 0);
                }
            }
            return img;
        }

        /// <summary>
        /// カスタマバーコード変換
        /// </summary>
        /// <param name="zip">住所</param>
        /// <param name="postCode">郵便番号</param>
        /// <returns></returns>
        private static IEnumerable<string> ConvertCustomerBarCode(string zip, IEnumerable<string> postCode)
        {
            yield return startCode;

            // 全角→半角
            zip = string.Join("", zip.Select(n => (HankakuDic.ContainsKey(n) ? HankakuDic[n] : n)));

            foreach (var pc in postCode) yield return pc;

            // http://www.post.japanpost.jp/zipcode/zipmanual/p19.html
            // 1. まず、データ内にあるアルファベットの小文字は大文字に置き換えます。
            zip = zip.ToUpper();

            // 2. 同様に、データ内にある"&"等の下記の文字は取り除き、後ろのデータを詰めます。
            //「&」(アンパサンド)、「/」(スラッシュ)、「・」(中グロ)、「.」(ピリオド)
            zip = string.Join("", zip.Split('&', '＆', '/', '／', '･', '・', '.', '．'));

            // 3. 1および2で整理したデータから、算用数字、ハイフンおよび連続していないアルファベット1文字を必要な文字情報として抜き出します。
            // 4. 次に抜き出された文字の前にある下記の文字等は、ハイフン1文字に置き換えます。
            // 「漢字」、「かな文字」、「カタカナ文字」、「漢数字」、「ブランク」、「2文字以上連続したアルファベット文字」
            zip = Regex.Replace(zip, "[^0-9A-Z\\-]|[A-Z]{2,}", "-");

            // ハイフン処理
            zip = TrimHyphen(zip);

            var customerCode = ConvertCCode(zip);
            foreach (var cd in customerCode) yield return cd;

            yield return CalcCheckDigit(postCode, customerCode);

            yield return endCode;
        }

        /// <summary>
        /// ハイフン処理
        /// </summary>
        /// <param name="target">処理対象文字列</param>
        /// <returns>処理後文字列</returns>
        private static string TrimHyphen(string target)
        {
            // 5. 4の置き換えで、ハイフンが連続する場合は1つにまとめます。
            target = Regex.Replace(target, "\\-{2,}", "-");

            //アルファベット前後のハイフンを除去
            var tmp = target.ToCharArray();
            for (int i = 0; i < target.Length; i++)
            {
                if (Alphabet.Contains(target[i].ToString()))
                {
                    if (i > 0 && target[i - 1] == '-') tmp[i - 1] = '@';
                    if (i < target.Length - 1 && target[i + 1] == '-') tmp[i + 1] = '@';
                }
            }
            target = (new string(tmp)).Replace("@", "");

            // 6. 最後に、先頭がハイフンの場合は取り除きます。            
            return target.Trim('-');
        }

        /// <summary>
        /// カスタマコード変換
        /// </summary>
        /// <param name="zip">住所</param>
        /// <returns>カスタマバーコード</returns>
        private static string[] ConvertCCode(string zip)
        {
            int idx = 0;
            var zipDisplayCode = Enumerable.Repeat("CC4", 13).ToArray();
            foreach (var c in zip.Select(x => x.ToString()).Take(13))
            {
                var cc = BarcodeCharDic[c];
                if (cc.Length == 1)
                {
                    // 数値、ハイフン
                    zipDisplayCode[idx] = cc[0];
                }
                else
                {
                    // アルファベット
                    zipDisplayCode[idx] = cc[0];
                    zipDisplayCode[idx + 1] = cc[1];
                    idx++;
                }
                idx++;
            }
            return zipDisplayCode;
        }

        /// <summary>
        /// チェックデジット計算
        /// </summary>
        /// <param name="postCode">郵便番号</param>
        /// <param name="zipDisplayCode">住所表示番号</param>
        /// <returns>チェックデジット</returns>
        private static string CalcCheckDigit(IEnumerable<string> postCode, string[] zipDisplayCode)
        {
            // 郵便番号の各値と住所表示番号の各値の合計＋CD=19の倍数
            var sum = postCode.Concat(zipDisplayCode)
                .Select(x => CheckSumDic[x]).Sum();

            if (sum % 19 == 0) return "0";

            var mod = sum / 19;
            var checkD = (mod + 1) * 19 - sum;
            return CheckSumDic.Where(x => x.Value == checkD).First().Key;


        }
    }
}
