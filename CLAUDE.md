# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 言語

- 日本向けのライブラリなので、ユーザーとのやり取り・ドキュメント（README、CLAUDE.md など）・コードコメント・XML ドキュメントコメント・コミットメッセージはすべて日本語で書く。

## 構成・ビルド

- ライブラリは 3 つ、すべて `netstandard2.0`（SDK スタイル）。ビルドは `dotnet build ZipConvertCustomerBarCode.sln`。
  - `ZipConvertCustomerBarCode`: コア。外部依存を追加しない（描画ライブラリに依存させないのが分割の目的）。
  - `ZipConvertCustomerBarCode.Drawing`: System.Drawing.Common による描画（Windows のみ）。
  - `ZipConvertCustomerBarCode.SkiaSharp`: SkiaSharp による描画。名前空間は `SkiaSharp` との衝突を避けるため `ZipConvertCustomerBarCode.Skia`。
- `netstandard2.0` の既定 C# は 7.3。`record` や null 許容参照型など新しい構文は使わない。
- バージョン・著作権表示は `Directory.Build.props` にまとめている。
- 公開メンバーには XML ドキュメントコメントを付ける（`GenerateDocumentationFile` 有効のため、ないと警告になる）。

## テスト

- `dotnet test` で実行（xUnit v3、net10.0）。`global.json` で Microsoft.Testing.Platform を指定しているため、`Microsoft.NET.Test.Sdk` や `xunit.runner.visualstudio` は追加しない。
- `TestData/cases.tsv` と `v1-*.png` は 1.x（.NET Framework 版）の出力で、シンボル列と画像がこれとピクセル単位で一致することを確認している。出力が変わる修正をした場合は、意図した変更か確認してから期待値を更新する。
- `cases.tsv` はタブ区切り（郵便番号・住所・シンボル列・画像ファイル名）。住所が空の行があるので、シェルで加工するときは連続するタブがまとめられないよう注意する。
- System.Drawing のテストは Windows 以外ではスキップされる。

## コード規約

- `.cs` ファイルは UTF-8（BOM 付き）・CRLF 改行。どちらも維持する（新規ファイルにも BOM を付ける）。

## バーコード処理

- 住所の変換（`BarCode.cs`）は日本郵便のバーコードマニュアル（http://www.post.japanpost.jp/zipcode/zipmanual/p19.html）に従う。コード中の番号付きコメント（1.〜6.）はマニュアルの手順番号に対応している。
- バーコードは常に 23 シンボル: スタート + 郵便番号 7 桁 + 住所表示番号 13 枠（不足分は `CC4` で埋める）+ チェックデジット + ストップ。
- バーの形は `CustomerBarCode.cs` の `BarPatternDic` で定義（1 シンボル = 幅 12px に 3 本、x=0/4/8、幅 2px、上・中・下段 各 4px）。この表は旧バージョンの GIF 画像とピクセル単位で一致することを確認済みなので、変更するときは既存出力と比較する。
- 描画パッケージは `GetBars()` の矩形を白地に黒で塗るだけにし、バーコードのロジックはコアに置く。
- 住所中の漢数字には未対応（README に記載の既知の制限）。
