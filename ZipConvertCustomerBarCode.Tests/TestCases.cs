namespace ZipConvertCustomerBarCode.Tests
{
    /// <summary>
    /// TestData/cases.tsv のテストケース
    /// 期待値(シンボル列・画像)は 1.x (.NET Framework 版)の出力
    /// </summary>
    public static class TestCases
    {
        public static string DataPath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

        /// <summary>
        /// 郵便番号, 住所, 期待するシンボル列(空白区切り), 期待する画像ファイル名
        /// </summary>
        public static TheoryData<string, string, string, string> All()
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var line in File.ReadAllLines(DataPath("cases.tsv")))
            {
                var p = line.Split('\t');
                data.Add(p[0], p[1], p[2], p[3]);
            }
            return data;
        }
    }
}
