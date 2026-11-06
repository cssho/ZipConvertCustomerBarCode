# セキュリティポリシー

## サポートしているバージョン

セキュリティ上の修正は、最新の 2.x 系にのみ行います。

| バージョン | サポート |
|---|---|
| 2.x（最新） | ✅ |
| 1.x（.NET Framework 版） | ❌ |

対象のパッケージは `ZipConvertCustomerBarCode`・`ZipConvertCustomerBarCode.Drawing`・`ZipConvertCustomerBarCode.SkiaSharp` です。

## 脆弱性の報告

脆弱性を見つけた場合は、公開の Issue には書かず、GitHub の非公開の報告機能から連絡してください。

- [脆弱性を報告する](https://github.com/cssho/ZipConvertCustomerBarCode/security/advisories/new)（リポジトリの Security タブ →「Report a vulnerability」）

報告には、次の内容を含めてもらえると確認が早くなります。

- 影響を受けるパッケージとバージョン
- 再現手順（入力した郵便番号・住所・CSV ファイルなど）
- 想定される影響

個人で保守しているため対応には時間がかかることがありますが、内容を確認して返信します。修正したバージョンを公開したあと、必要に応じて GitHub Security Advisory で公表します。

## 対象外

住所からバーコードへの変換結果が日本郵便のマニュアルと異なる、といった不具合はセキュリティの問題ではないので、通常の Issue で報告してください。

依存しているパッケージ（SkiaSharp、System.Drawing.Common など）自体の脆弱性は、それぞれの提供元に報告してください。このリポジトリでは Dependabot で更新しています。
