# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 言語

- 日本向けのライブラリなので、ユーザーとのやり取り・ドキュメント（README、CLAUDE.md など）・コードコメント・XML ドキュメントコメント・コミットメッセージ・PR の説明はすべて日本語で書く。

## リポジトリ運用

- コミット・push・PR 作成は頼まれたときだけ行う。master で作業せず、ブランチを切ってから PR にする（依存する変更が続くときは、頼まれたら stacked PR にする）。
- PR のマージはユーザーが行う。「マージした」と言われたら master を pull し、マージ済みのブランチを削除する。

## 構成・ビルド

- ライブラリは 3 つ（SDK スタイル）。ビルドは `dotnet build ZipConvertCustomerBarCode.sln`。
  - `ZipConvertCustomerBarCode`: コア。`netstandard2.0` と `net8.0`。外部依存を追加しない（描画ライブラリに依存させないのが分割の目的。`System.Memory` も入れない）。
    - `Span<T>` などは `#if NET8_0_OR_GREATER` の中だけで使い、`#else` に `netstandard2.0` 用の処理を書く。`#if` は小さな範囲（メソッドや構造体の中）に閉じ込め、共通にできる部分は共通のメソッドにする。
    - `Span` を使うのは計測で効果があった箇所のみ（住所照合の正規化、CSV の読み込み）。`Span` を使わなくても両方で速くできる場合は、`#if` を使わず共通のコードにする。
  - `ZipConvertCustomerBarCode.Drawing`: System.Drawing.Common による描画（Windows のみ）。
  - `ZipConvertCustomerBarCode.SkiaSharp`: SkiaSharp による描画。名前空間は `SkiaSharp` との衝突を避けるため `ZipConvertCustomerBarCode.Skia`。
- `netstandard2.0` の既定 C# は 7.3。`#if NET8_0_OR_GREATER` の外では `record` や null 許容参照型など新しい構文は使わない。
- バージョン・著作権表示・パッケージ情報は `Directory.Build.props` にまとめている。パッケージアイコン `icon.png`（128×128）は、このライブラリで描いたバーコードの先頭4シンボル（スタート + 郵便番号3桁）を縦に伸ばしたもの。
- 公開メンバーには XML ドキュメントコメントを付ける（`GenerateDocumentationFile` 有効のため、ないと警告になる）。CI（`GITHUB_ACTIONS`）では `TreatWarningsAsErrors` で警告をエラーにしている（依存パッケージの脆弱性の警告 NU1901〜NU1904 は除く）。
- `.cs` ファイルは UTF-8（BOM 付き、`.editorconfig` にも記載）。新規ファイルにも BOM を付ける（`.claude/settings.json` のフックが Write / Edit 後に自動で補う）。改行は `core.autocrlf=true` に従う（Windows の作業ツリーでは CRLF、リポジトリ内は LF）。

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
- リリースは `v2.0.0` のようなタグを push すると、タグのバージョンで3パッケージを nuget.org に公開する（Trusted Publishing。ワークフローのファイル名 `build.yml` と Environment 名 `release` を nuget.org に登録しているので、どちらも名前を変えない）。nuget.org 側のポリシーは「新しいバージョンの追加のみ」なので、公開済みのバージョンは出し直せない（修正は次のバージョンで出す）。公開後に `github-release` ジョブがタグの GitHub Release を作る（リリースノートはマージした PR から自動生成、`-` を含むタグはプレリリース）。
- 公開ジョブは GitHub の `release` Environment で動く（`v*` タグのみ、cssho の承認後に実行。Secret `NUGET_USER` もこの Environment にある）。`Directory.Build.props` の `VersionPrefix` はローカルと CI のプレリリース版（`2.0.0-ci.N`）用で、公開時はタグのバージョンが優先される。`VersionPrefix` は `PackageValidationBaselineVersion` 以上にしておく（低いとアセンブリのバージョンが基準より低くなり CP0003 エラー）。
- ワークフローのアクションは `.github/workflows/actions.lock`（`gh actions-lock`）でコミットに固定している。`uses:` を追加・変更したら `gh actions-lock` でロックし直し、`gh actions-lock --verify` で確認してから、ロックファイルとワークフローを一緒にコミットする（ワークフローを編集すると、フックが `--verify-local` で漏れを知らせる）。JSON 出力は `--json=valid,findings` のように `=` でつなぐ（空白区切りだとパスとして解釈される）。
- CI の `verify-lockfile.yml` が PR・push・週1回でロックファイルを検証する。アクションの更新は Dependabot（`.github/dependabot.yml`、既定の cooldown でリリースから3日待つ）が、ロックファイルの作り直しも含めて PR にする。
- パッケージ作成時の検証（`EnablePackageValidation`）で、net8.0 版から netstandard2.0 版の公開 API が欠けているとエラー（CP0002）になる（net8.0 版にだけ API を足すのは検出しない）。
- あわせて、公開済みのバージョン（`Directory.Build.props` の `PackageValidationBaselineVersion`）と比べて公開 API を削除・変更していないかも確認する。破壊的変更をするメジャーバージョンアップでは、この基準を外すか更新する。
- NuGet パッケージの更新も Dependabot が PR にする。`SkiaSharp` とテスト用の `SkiaSharp.NativeAssets.Linux.NoDependencies` はバージョンを揃える必要があるので、グループにまとめて一緒に更新する。
- `build.yml` は、ドキュメントなどビルドに関係しないファイル（`*.md`・`LICENSE`・`.claude/`・`.github/dependabot.yml`）だけの変更では test と pack をスキップする（`changes` ジョブで判定）。`paths-ignore` を使うと必須チェックが報告されず PR をマージできなくなるので使わない。
- master はルールセットで保護している（PR 経由のみ、`build-result`（test・pack の結果をまとめるジョブ）と verify-lockfile の `verify` の成功が必須）。CI のジョブ名を変えるとマージできなくなるので、ルールセットの必須チェックも合わせて変更する。

## バーコード処理

- 住所の変換（`BarCode.cs`）は日本郵便のバーコードマニュアル（http://www.post.japanpost.jp/zipcode/zipmanual/p19.html）に従う。コード中の番号付きコメント（1.〜6.、補足ルール1〜5）はマニュアルの手順番号に対応している。
- マニュアルでは住所を「住所A（町域名まで）」と「住所B（町域名以降）」に分け、住所Bだけから抜き出す。`Create(郵便番号, 住所)` は住所全体を住所Bとして扱うため、町域名に特定文字の前の漢数字を含む住所（一番町、八丁堀など）では誤る。これは既知の制限で、`Create(郵便番号, 住所A, 住所B)` か `ZipCodeDictionary` を渡す `Create` を推奨している。
- バーコードは常に 23 シンボル: スタート + 郵便番号 7 桁 + 住所表示番号 13 枠（不足分は `CC4` で埋める）+ チェックデジット + ストップ。
- バーの形は `CustomerBarCode.cs` の `BarPatternDic` で定義（1 シンボル = 幅 12px に 3 本、x=0/4/8、幅 2px、上・中・下段 各 4px）。この表は旧バージョンの GIF 画像とピクセル単位で一致することを確認済みなので、変更するときは既存出力と比較する。
- 描画パッケージは `GetBars()` の矩形を白地に黒で塗るだけにし、バーコードのロジックはコアに置く。

## 郵便番号データ・住所照合

- `ZipCodeDictionary` は利用者が渡す utf_ken_all.csv（UTF-8）と JIGYOSYO.CSV（Shift_JIS）を読む。Shift_JIS は .NET Core 以降では利用者が `CodePagesEncodingProvider` を登録する必要がある（コアに依存を足さないため、ライブラリ側では登録しない）。読み込み時の都道府県名の検証で、文字コードの取り違えを検出している。
- 住所照合（`AddressMatcher`、`ZipCodeDictionary` の検索）は呼び出し回数が多く、LINQ や `ToUpperInvariant` だけで目に見えて遅くなったことがある。変更したら実データで計測する（英字の大文字化は ASCII のみを手で変換している）。
- 事業所名での検索は、名前の完全一致を優先する。部分一致は誤検出が多かったため、番地なども一致する場合だけ採用する。
