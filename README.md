# ZipConvertCustomerBarCode
郵便番号、住所から郵便物用のカスタマバーコードを生成する
[ZipConvertCustomerBarCode](http://zipccbc.azurewebsites.net/)

## カスタマバーコード
> お客さまがあらかじめ郵便物に印字する場合のバーコードがカスタマバーコードです。カスタマバーコードは、原則として差し出しの必要条件ではなく、料金割引を受けようとする場合に印字するもので、あて名印字と同時に印字できるような仕様としています。

[郵便局 - 郵便番号・バーコードマニュアル](http://www.post.japanpost.jp/zipcode/zipmanual/index.html)

## 構成
すべて .NET Standard 2.0 のライブラリです。描画方法に合わせてパッケージを選んでください。

| プロジェクト | 依存 | 用途 |
| --- | --- | --- |
| `ZipConvertCustomerBarCode` | なし | バーコード生成、バー配置の取得、SVG 出力 |
| `ZipConvertCustomerBarCode.Drawing` | System.Drawing.Common | `Bitmap` として描画（Windows のみ） |
| `ZipConvertCustomerBarCode.SkiaSharp` | SkiaSharp | `SKBitmap` / PNG として描画（クロスプラットフォーム） |

SkiaSharp を Linux で使う場合は、利用側で `SkiaSharp.NativeAssets.Linux` パッケージも参照してください。

## 使い方
### バーコード生成
```csharp
using ZipConvertCustomerBarCode;

// 郵便番号, 住所A(町域名までの住所), 住所B(町域名以降の住所)
CustomerBarCode code = BarCode.Create("1000013", "東京都千代田区霞が関", "1丁目3番2号　郵便プラザ503室");
```
郵便番号が7桁の数値でない場合は `ArgumentException` になります。

住所表示番号は、[郵便番号・バーコードマニュアル](https://www.post.japanpost.jp/zipcode/zipmanual/p19.html)の抜き出しルール（補足ルールを含む）に従って住所Bから抜き出します。
「丁目・番地・号」などの前の漢数字は算用数字に変換し（例：十一丁目 → 11）、「2F」のような階数の F は抜き出しません。

住所を分けずに渡すこともできます。

```csharp
CustomerBarCode code = BarCode.Create("1000013", "東京都千代田区霞が関1丁目3番2号　郵便プラザ503室");
```

ただしこの場合は住所全体を住所Bとして扱うため、町域名に「丁目・番・ノ」などの前の漢数字を含む住所（一番町、八丁堀、三ノ輪 など）では、町域名の漢数字も抜き出されて誤ったバーコードになります。住所A・住所Bに分けて渡すことをおすすめします。

### 郵便番号データを使う
日本郵便の[郵便番号データ（1レコード1行、UTF-8形式）](https://www.post.japanpost.jp/zipcode/dl/utf-zip.html)を読み込むと、郵便番号の町域名で住所を住所A・住所Bに自動で分割できます。住所全体を渡しても、町域名の漢数字（一番町、八丁堀など）を抜き出さなくなります。

データは同梱していないので、`utf_ken_all.zip` をダウンロードして指定してください（zip のままでも、展開した `utf_ken_all.csv` でも読み込めます）。データは毎月更新されます。

```csharp
// 読み込みには時間がかかるので、一度読み込んだものを使い回す
ZipCodeDictionary dictionary = ZipCodeDictionary.Load(@"c:\data\utf_ken_all.zip");

CustomerBarCode code = BarCode.Create("1020082", "東京都千代田区一番町5-3", dictionary);
// code.AddressNumber → "5-3"(郵便番号データなしでは "1-5-3")
```

- `ZipCodeDictionary` は破棄（`Dispose`）不要です。`Load(パス)` はファイルを読み終えると閉じます。`Load(Stream)` は渡された Stream を閉じないので、呼び出し側で破棄してください。
- 読み込んだデータはメモリ上に保持します（全国分で約 24MB、事業所の個別郵便番号データも読み込むと約 44MB）。
- 都道府県名・郡名の省略、町域名の前の「大字」「字」の有無、空白、全角・半角、「ヶ」と「ケ」、漢数字と算用数字（北六条 と 北6条）の違いは区別しません。
- 町域名が一致せず分割できない場合は、住所全体から抜き出します（郵便番号データなしと同じ）。分割できるかどうかは `dictionary.TrySplitAddress()` で確認できます。
- 代表の郵便番号（町域名が「以下に掲載がない場合」）は町域名がわからないため、分割しません。
- `IsRepresentativeZipCode` は郵便番号データに基づいて判定します。郵便番号データにない番号（郵便番号データより新しい番号など）は、下2けたで判定します。
- 従来の Shift_JIS 形式（KEN_ALL.CSV）は、長い町域名が複数行に分かれているため対象外です。
### 事業所の個別郵便番号データを使う
日本郵便の[事業所の個別郵便番号データ](https://www.post.japanpost.jp/zipcode/dl/jigyosyo/index-zip.html)（`jigyosyo.zip`、Shift_JIS）も一緒に読み込むと、大口事業所・私書箱の個別郵便番号を扱えます。

```csharp
// Shift_JIS を使えるようにする(.NET Core / .NET 5 以降。.NET Framework では不要)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

ZipCodeDictionary dictionary = ZipCodeDictionary.Load(@"c:\data\utf_ken_all.zip", @"c:\data\jigyosyo.zip");

// 個別郵便番号のバーコードは、住所表示番号を CC4 で埋める
CustomerBarCode code = BarCode.Create("1008792", "東京都千代田区大手町2-3-1", dictionary);
// code.AddressNumber → ""
```

- [マニュアル p22](https://www.post.japanpost.jp/zipcode/zipmanual/p22.html) に従い、個別郵便番号あての郵便物は個別番号のみをバーコード化し、住所表示番号の 13 けたは CC4 で埋めます。事業所の個別郵便番号データを読み込んでいない場合は、住所から住所表示番号を抜き出してしまうので注意してください。
- `dictionary.Find(郵便番号)` で、事業所名（`BusinessName`）、番地等（`Block`）、種類（`Kind`：大口事業所・私書箱）を取得できます。
- `Encoding.RegisterProvider` を呼ばずに読み込むと `InvalidOperationException` になります。Shift_JIS を自分で用意する場合は `Load(Stream, Stream, Encoding)` を使ってください。

### 住所から郵便番号を探す
郵便番号データを使うと、住所から郵便番号を探せます。

```csharp
if (dictionary.TryGetZipCode("東京都千代田区一番町5-3", out var zipCode))
{
    // zipCode → "1020082"
    CustomerBarCode code = BarCode.Create(zipCode, "東京都千代田区一番町5-3", dictionary);
}

// 候補をすべて取得する(郵便番号が1つに決まらない場合は複数)
IReadOnlyList<ZipCodeEntry> candidates = dictionary.FindByAddress("北海道深川市音江町123");

// 事業所名も渡すと、大口事業所・私書箱の個別郵便番号を探せる(事業所の個別郵便番号データが必要)
dictionary.TryGetZipCode("東京都千代田区大手町2-3-1", "日本郵便株式会社", out var businessZipCode); // → "1008792"
```

- 都道府県名・市区町村名・町域名の順に照合し、最も長く一致した町域を返します。表記の違いは住所の分割と同じく区別しません。
- 同じ町域名で郵便番号が複数ある場合は、郵便番号データの括弧書きで絞り込みます。
  - 超高層ビル（「霞が関ビル（１０階）」など）：住所の町域名より後ろにビル名があれば、その後の階（「10階」「10F」）で選びます。階がない場合、地下の場合、データにない階の場合は「（地階・階層不明）」、ビル名がなければ「（次のビルを除く）」などを使います。
  - 地区名（「音江町（稲田）」「音江町（国見２３〜２９８番地）」など）：町域名の直後（前に「字」があってもよい）の地区名で選びます。
  - 丁目・番地の範囲（「北六条東（１〜７丁目）」「常盤（１〜１３１番地）」など）：町域名（または地区名）の直後の丁目・番地で選びます。範囲に当てはまらない場合は「（その他）」を使います。
- 同名の市区町村（府中市など）は、都道府県名がなければ町域名まで一致した方を使います。
- 町域名が一致しない場合は、マニュアルに従って代表の郵便番号（「以下に掲載がない場合」）を返します（`ZipCodeEntry.IsRepresentative` が `true`）。代表の郵便番号の郵便物にはカスタマバーコードを付けません。
- 事業所名を渡した場合は、住所の市区町村にある事業所から事業所名が一致するもの（空白、全角・半角、「株式会社」「（株）」などの違いは区別しない。一致しなければ一方が他方を含むもの）を探します。同じ事業所名が複数あれば番地等まで一致するものを使い、見つからなければ住所だけで探します。
- `TryGetZipCode` は郵便番号が 1 つに決まった場合だけ `true` を返します。決まらない場合は `FindByAddress` で候補を確認してください。

次の住所は郵便番号を決められないか、正しく探せません。
- 京都市の通り名を含む住所（「烏丸通御池上る二条殿町」など）は、町域名の前に通り名があるため町域名が一致せず、代表の郵便番号になります。
- 地区名が書かれていない住所や、郵便番号データの括弧書きを解釈できない町域は、候補が複数になります。
- 1 つの事業所が複数の個別郵便番号を持つ場合は、候補が複数になります。

全国の郵便番号データ（2026年10月更新）で、データから組み立てた住所で試した結果です。実際の住所の書き方の揺れまでは確かめていません。
- 町域（代表の郵便番号以外の 122,657 町域、「都道府県 + 市区町村 + 町域 + 番地」。超高層ビルは「…番地 + ビル名 + 階」、地区名は「町域 + 地区名 + 番地」）：99.2% で正しい郵便番号に 1 つに決まり、0.7% は候補が複数で正しい郵便番号を含んでいました（残り 0.04% は、組み立てた住所の番地が同じ町域の別の郵便番号の範囲に入ったものなどで、その郵便番号が返ります）。
- 事業所（22,420 件、「データの住所 + 事業所名」）：99.6% で正しい個別郵便番号に 1 つに決まり、残りは候補が複数で正しい個別郵便番号を含んでいました（ほとんどは複数の個別郵便番号を持つ事業所）。

### バーコードの内容
```csharp
string zip = code.ZipCode;             // "1000013"
string number = code.AddressNumber;    // "1-3-2-503"(マニュアルの「バーコード・データ」)
bool rep = code.IsRepresentativeZipCode; // 代表の郵便番号か
```

`AddressNumber` は 13 けたで切り捨てる前の値です。バーコードに含まれるのは先頭 13 けた分です。

代表の郵便番号（郵便番号簿の「以下に掲載がない場合」の番号）の郵便物には、カスタマバーコードを付けないことになっています（[マニュアル p18](https://www.post.japanpost.jp/zipcode/zipmanual/p18.html)）。`IsRepresentativeZipCode` は、郵便番号データを使わない場合はマニュアルの「原則として下2けたが 00」に従って判定する目安です。正確に判定するには郵便番号データを使ってください。

### 印刷用の実寸
[マニュアル p12](https://www.post.japanpost.jp/zipcode/zipmanual/p12.html) の寸法（10 ポイントのときバー幅 0.6mm、バーピッチ 1.2mm、ロングバー 3.6mm）で出力します。大きさは 8〜11.5 ポイントで指定でき、省略時は 10 ポイントです。

```csharp
// mm 単位の SVG(余白は省略時 2mm)
string svg = code.ToPrintSvg(point: 10, margin: 2);

// System.Drawing: 宛名印刷(PrintDocument など)で、用紙の (x, y) mm の位置に描画
code.Draw(e.Graphics, 20, 30);

// SkiaSharp: PDF の (x, y) mm の位置に描画
code.Draw(canvas, 20, 30, CustomerBarCodeSkiaExtensions.PdfUnitsPerMillimeter);

// 独自に描画する場合は mm 単位のバー配置を使う
foreach (var bar in code.GetPrintBars())
{
    // bar.X, bar.Y, bar.Width, bar.Height (mm)
}
```

マニュアル（[p13](https://www.post.japanpost.jp/zipcode/zipmanual/p13.html)）では、バーコードの上下左右に 2mm 以上の空白が必要です。`Draw` はバーだけを描くので、周囲を空けて配置してください。

### SVG（依存ライブラリなし）
```csharp
string svg = code.ToSvg();
```

### System.Drawing
```csharp
using ZipConvertCustomerBarCode.Drawing;

Bitmap img = code.ToBitmap();
code.Save(@"c:\test.gif", ImageFormat.Gif);
```

### SkiaSharp
```csharp
using ZipConvertCustomerBarCode.Skia;

SKBitmap img = code.ToSKBitmap();
byte[] png = code.ToPng();
code.SavePng(@"c:\test.png");
```

### 独自に描画する
`GetBars()` でバーの位置と大きさ（ピクセル単位）が取れるので、任意の描画ライブラリで白地に黒の矩形を描いてください。

```csharp
foreach (var bar in code.GetBars())
{
    // bar.X, bar.Y, bar.Width, bar.Height
}
```

いずれのメソッドも `scale` 引数で拡大できます（1 のとき 276×12 ピクセル）。

## 1.x からの移行
| 1.x | 2.x |
| --- | --- |
| `BarCode.CreateImage(zip, address)` | `BarCode.Create(zip, address).ToBitmap()`（Drawing） |
| `BarCode.Export(zip, address, path)` | `BarCode.Create(zip, address).Save(path, ImageFormat.Gif)`（Drawing） |