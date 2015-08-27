# ZipConvertCustomerBarCode
郵便番号、住所から郵便物用のカスタマバーコードを生成する
[ZipConvertCustomerBarCode](http://zipccbc.azurewebsites.net/)

## カスタマバーコード
> お客さまがあらかじめ郵便物に印字する場合のバーコードがカスタマバーコードです。カスタマバーコードは、原則として差し出しの必要条件ではなく、料金割引を受けようとする場合に印字するもので、あて名印字と同時に印字できるような仕様としています。

[郵便局 - 郵便番号・バーコードマニュアル](http://www.post.japanpost.jp/zipcode/zipmanual/index.html)

## 機能
### Export
指定したパスにカスタマバーコードを出力する。
```csharp
// 郵便番号,住所,出力先
BarCode.Export("01234567", "とある県とある市とある町 1丁目1の1",@"c:\test.gif");
```
### CreateImage
カスタマバーコードのイメージを返す
```csharp
// 郵便番号,住所
Bitmap img = BarCode.CreateImage("01234567", "とある県とある市とある町 1丁目1の1");
```

## 備考
* 漢数字未対応
* 住所→郵便番号変換辞書検討中（郵便番号の入力が不要に）
