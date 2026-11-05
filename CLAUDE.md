# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 言語

- 日本向けのライブラリなので、ユーザーとのやり取り・ドキュメント（README、CLAUDE.md など）・コードコメント・XML ドキュメントコメント・コミットメッセージはすべて日本語で書く。

## 構成・ビルド

- ライブラリは 3 つ（SDK スタイル）。ビルドは `dotnet build ZipConvertCustomerBarCode.sln`。
  - `ZipConvertCustomerBarCode`: コア。`netstandard2.0` と `net8.0`。外部依存を追加しない（描画ライブラリに依存させないのが分割の目的。`System.Memory` も入れない）。
    - `Span<T>` などは `#if NET8_0_OR_GREATER` の中だけで使い、`#else` に `netstandard2.0` 用の処理を書く。`#if` は小さな範囲（メソッドや構造体の中）に閉じ込め、共通にできる部分は共通のメソッドにする。
    - `Span` を使うのは計測で効果があった箇所のみ（住所照合の正規化、CSV の読み込み）。`Span` を使わなくても両方で速くできる場合は、`#if` を使わず共通のコードにする。
  - `ZipConvertCustomerBarCode.Drawing`: System.Drawing.Common による描画（Windows のみ）。
  - `ZipConvertCustomerBarCode.SkiaSharp`: SkiaSharp による描画。名前空間は `SkiaSharp` との衝突を避けるため `ZipConvertCustomerBarCode.Skia`。
- `netstandard2.0` の既定 C# は 7.3。`#if NET8_0_OR_GREATER` の外では `record` や null 許容参照型など新しい構文は使わない。
- バージョン・著作権表示は `Directory.Build.props` にまとめている。
- 公開メンバーには XML ドキュメントコメントを付ける（`GenerateDocumentationFile` 有効のため、ないと警告になる）。

## テスト

- `dotnet test` で実行（xUnit v3、net10.0）。`global.json` で Microsoft.Testing.Platform を指定しているため、`Microsoft.NET.Test.Sdk` や `xunit.runner.visualstudio` は追加しない。
- コアは `#if` で処理が分かれるため、変更したら両方のビルドでテストする: `dotnet test`（net8.0 版）と `dotnet test -p:CoreTargetFramework=netstandard2.0`。`TargetFrameworkTests` が、指定したビルドが実際に使われていることを確認する。
- `TestData/cases.tsv` と `v1-*.png` は 1.x（.NET Framework 版）の出力で、シンボル列と画像がこれとピクセル単位で一致することを確認している。1.x がマニュアルと異なっていたケース（補足ルール対象など）は除いてあり、それらは `SupplementaryRuleTests` でマニュアルの「バーコード・データ」を期待値にしている。出力が変わる修正をした場合は、マニュアルに照らして意図した変更か確認してから期待値を更新する。
- `cases.tsv` はタブ区切り（郵便番号・住所・シンボル列・画像ファイル名）。住所が空の行があるので、シェルで加工するときは連続するタブがまとめられないよう注意する。
- System.Drawing のテストは Windows 以外ではスキップされる。
- `TestData/ken_all_sample.csv` は utf_ken_all.csv の形式で手書きしたテスト用データ（実データの抜粋ではない）。実際の郵便番号データ（約12万件）はリポジトリに含めない。
- `TestData/jigyosyo_sample.csv` は JIGYOSYO.CSV の形式で手書きしたテスト用データで、実データと同じく Shift_JIS・CRLF。編集するときは UTF-8 で書いて `iconv -f UTF-8 -t CP932` で変換する（`.gitattributes` で binary 指定）。
- テストプロジェクトの `SkiaSharp.NativeAssets.Linux.NoDependencies` は CI（Linux）用。ライブラリ本体には追加しない。

## CI・リリース

- `.github/workflows/build.yml`: push・PR で Ubuntu と Windows × コアの両方のビルドをテストし、パッケージを作る。
- リリースは `v2.0.0` のようなタグを push すると、タグのバージョンで3パッケージを nuget.org に公開する（Trusted Publishing。ワークフローのファイル名 `build.yml` と Environment 名 `release` を nuget.org に登録しているので、どちらも名前を変えない）。
- 公開ジョブは GitHub の `release` Environment で動く（`v*` タグのみ、cssho の承認後に実行。Secret `NUGET_USER` もこの Environment にある）。`Directory.Build.props` の `Version` はローカル用で、公開時はタグが優先される。
- パッケージ作成時の検証（`EnablePackageValidation`）で、net8.0 版から netstandard2.0 版の公開 API が欠けているとエラー（CP0002）になる。

## コード規約

- `.cs` ファイルは UTF-8（BOM 付き）・CRLF 改行。どちらも維持する（新規ファイルにも BOM を付ける）。

## バーコード処理

- 住所の変換（`BarCode.cs`）は日本郵便のバーコードマニュアル（http://www.post.japanpost.jp/zipcode/zipmanual/p19.html）に従う。コード中の番号付きコメント（1.〜6.、補足ルール1〜5）はマニュアルの手順番号に対応している。
- マニュアルでは住所を「住所A（町域名まで）」と「住所B（町域名以降）」に分け、住所Bだけから抜き出す。`Create(郵便番号, 住所)` は住所全体を住所Bとして扱うため、町域名に特定文字の前の漢数字を含む住所（一番町、八丁堀など）では誤る。これは既知の制限で、`Create(郵便番号, 住所A, 住所B)` を推奨している。
- バーコードは常に 23 シンボル: スタート + 郵便番号 7 桁 + 住所表示番号 13 枠（不足分は `CC4` で埋める）+ チェックデジット + ストップ。
- バーの形は `CustomerBarCode.cs` の `BarPatternDic` で定義（1 シンボル = 幅 12px に 3 本、x=0/4/8、幅 2px、上・中・下段 各 4px）。この表は旧バージョンの GIF 画像とピクセル単位で一致することを確認済みなので、変更するときは既存出力と比較する。
- 描画パッケージは `GetBars()` の矩形を白地に黒で塗るだけにし、バーコードのロジックはコアに置く。
