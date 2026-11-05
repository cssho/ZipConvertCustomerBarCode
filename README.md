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
