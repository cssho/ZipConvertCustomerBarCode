# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 言語

- 日本向けのライブラリなので、ユーザーとのやり取り・ドキュメント（README、CLAUDE.md など）・コードコメント・XML ドキュメントコメント・コミットメッセージはすべて日本語で書く。

## ビルド

- プロジェクトは旧形式（非 SDK スタイル）の csproj で、ターゲットは .NET Framework 4.0（`LibExecuter` は 4.0 Client Profile）。`dotnet build` ではビルドできないため、Visual Studio の MSBuild を使う:
  `"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" ZipConvertCustomerBarCode.sln -nologo -v:m`
- 現在この環境では .NET Framework 4.0 の参照アセンブリ（Developer Pack）がないため、MSB3644 でビルドが失敗する。その場合はエラーを報告し、ユーザーの指示なしに `TargetFrameworkVersion` を変更しない。
- コードは .NET Framework 4.0 の API の範囲で書く（`IReadOnlyList` や `Task.Run` などは使えない）。

## テスト

- テストプロジェクトはない。`LibExecuter` はサンプル住所で `BarCode.CreateImage` を呼ぶだけのコンソールアプリで、結果は手動で確認する。

## コード規約

- `.cs` ファイルは UTF-8（BOM 付き）・CRLF 改行。どちらも維持する。

## バーコード処理（`ZipConvertCustomerBarCode/BarCode.cs`）

- 住所の変換は日本郵便のバーコードマニュアル（http://www.post.japanpost.jp/zipcode/zipmanual/p19.html）に従う。コード中の番号付きコメント（1.〜6.）はマニュアルの手順番号に対応している。
- バーコードは常に 23 シンボル: スタート + 郵便番号 7 桁 + 住所表示番号 13 枠（不足分は `CC4` で埋める）+ チェックデジット + ストップ。各シンボルは幅 12px の GIF なので画像サイズは 276×12。シンボル数を変える場合は `new Bitmap(276, 12)` も合わせて変更する。
- シンボル画像は `ZipConvertCustomerBarCode/Resources/*.gif`。`ImageKeyDic` が各シンボルをリソース名（`Num0`、`Hyphen`、`CC1`、`Start`、`End` など）に対応付けている。リソース名はファイル名ではなく `Properties/Resources.resx` と `Resources.Designer.cs` で定義されているため、画像を追加するときは両方を更新する。
- 住所中の漢数字には未対応（README に記載の既知の制限）。
