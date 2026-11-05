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

### バーコードの内容
```csharp
string zip = code.ZipCode;             // "1000013"
string number = code.AddressNumber;    // "1-3-2-503"(マニュアルの「バーコード・データ」)
bool rep = code.IsRepresentativeZipCode; // 代表の郵便番号か
```

`AddressNumber` は 13 けたで切り捨てる前の値です。バーコードに含まれるのは先頭 13 けた分です。

代表の郵便番号（郵便番号簿の「以下に掲載がない場合」の番号）の郵便物には、カスタマバーコードを付けないことになっています（[マニュアル p18](https://www.post.japanpost.jp/zipcode/zipmanual/p18.html)）。`IsRepresentativeZipCode` は、マニュアルの「原則として下2けたが 00」に従って判定します。

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

## 備考
* 住所→郵便番号変換辞書検討中（郵便番号の入力が不要に）
