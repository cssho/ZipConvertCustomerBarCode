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

// 郵便番号,住所
CustomerBarCode code = BarCode.Create("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");
```
郵便番号が7桁の数値でない場合は `ArgumentException` になります。

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
* 漢数字未対応
* 住所→郵便番号変換辞書検討中（郵便番号の入力が不要に）
