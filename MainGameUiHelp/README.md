# MainGameUiHelp

canon_plugins のゲーム内 IMGUI 設定に共通の説明ポップアップを表示する。
項目名、チェック、スライダー、隣の数値欄へマウスを置くと、0.3秒後に説明を表示する。
スライダーと数値欄では、その行の説明に現在値と実際の設定範囲を添える。

## 説明データ

- `tools/help_texts.py`: 数値例や効果を含む日本語説明。ディルドの設定項目、Composerのレーン・L1/L2・軌道、カメラ、照明など。
- 各正規ソースの `Config.Bind`、`ConfigDescription`、既存の `tooltip:` も取り込む。
- 初めて各プラグインのUIを観測した時にBepInEx設定の説明と表示名を補完し、定期探索は行わない。
- `ui_inventory.json`: 独自IMGUIの表示名とソース位置。構文解析による配列・条件分岐・共用ヘルパー・設定データの項目も統合する。
- `tools/UiAudit`: 13タブの登録経路と、そのプラグインのUI定義・バー固有部をC#構文から走査する独立した棚卸しツール。
- `embedded_ui_audit.json`・`embedded_help_coverage.json`: ソースのSHA-256付き一覧と、固定表示名・動的ボタンの説明経路の照合結果。未登録・未対応があれば監査とテストを失敗させる。
- `tools/embedded_help_texts.py`: 追加項目の説明と、プリセット名など実行時に変わる項目の操作内容。
- `coverage.json`: 手書き説明、ソース由来説明、単位・表示名に基づく共通説明の内訳。静的ラベル数は実機表示の検証数ではない。
- `help_catalog.json`: 配置する説明データ。DLLの隣に必須。

見出し・状態表示も一覧に含む。説明文が「表示するだけ」の項目と、値を変更する設定は区別する。
動的に生成されるファイル名・プリセット名の全組合せを静的ラベル一覧が網羅するわけではない。

## 表示と入力

実際のUnity 2019.4の管理メソッド `GUI.DoLabel / DoButton / DoToggle / DoTextField / Slider / DoButtonGrid` から、
現在マウスがある行の名前と範囲だけを観測する。対象は `BepInEx/plugins/canon_plugins` 内の呼び出し元。
既存のUIコールバックやスライダーの値、戻り値、`hotControl`、イベントを変更しない。
ポップアップは描画のみで、入力捕捉は各ホストの既存 `UiInputCaptureApi` が引き続き所有する。
PlaybackBar内（展開タブ・選択リストを含む）は説明文と現在値・範囲を既存の説明枠へ渡し、このプラグインの枠は重ねず、表示はバーの「説明」切替に従う。
任意接続APIは `Plugin.BeginHostCapture()`・`TakeHostHelp()`・`EndHostCapture()` で、ホストは描画を `try/finally` で囲んで収集を終了する。
バーでは既存の即時表示・文字サイズを使い、このプラグインの待ち時間・文字サイズ設定はバー外だけに適用する。
現在のスクロール領域で見える部分だけを対象にし、別行のラベルを数値欄へ流用しない。

F1のMainGameUiHelp設定から、有効・表示待ち時間・文字サイズ・枠の幅を変更可能。
新規DLLのため、ゲーム起動中に配置した場合は次の起動から読み込まれる。

## 更新・検証

```powershell
dotnet run --project MainGameUiHelp/tools/UiAudit/UiAudit.csproj -c Release -- .
python MainGameUiHelp/tools/inventory_ui.py
python MainGameUiHelp/tools/build_catalog.py
python MainGameUiHelp/tools/audit_embedded_help.py
dotnet build MainGameUiHelp/MainGameUiHelp.csproj -c Release
dotnet build MainGameBlankMapAdd/MainGameBlankMapAdd.csproj -c Release
dotnet build SimpleAfterimage/SimpleAfterimage.csproj -c Release
dotnet build MainGameVMDPlay/MainGameVMDPlay.csproj -c Release
dotnet run --project MainGameUiHelp/Tests/MainGameUiHelp.Tests.csproj -c Release
```

オフライン検証では説明キーの重複・静的ラベルの欠落、深い一撃の2条件の区別、
複数行ラベルと数値欄の対応、Unity本体の実際の呼び出し経路とHarmony引数を確認する。
この検証は、ゲーム内の見え方・位置・ホバー操作の実機確認を代用しない。
設定データ行と数値欄が離れた表では、共通の `UiHelpReporting.ReportLastControl` で実際の矩形・項目名・値・範囲を表示イベント時だけ渡す。

配置元はこのプロジェクト自身の `bin/Release/net472/MainGameUiHelp.dll` と `help_catalog.json`。
配置先は `F:\kks\BepInEx\plugins\canon_plugins\MainGameUiHelp\`。
今回の拡張タブ対応4DLLとカタログは、ゲーム終了後に `tools/deploy_embedded_help.ps1` でまとめて配置・照合できる。
