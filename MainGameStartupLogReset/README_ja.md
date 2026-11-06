# MainGameStartupLogReset

BepInEx 5のPreloaderで、canon_pluginsの前回起動分のログを削除する。
各プラグインのログ設定に関係なく、通常のプラグイン読込より前に一度だけ実行する。
ゲームのアセンブリは変更しない。今回の起動で新たに書かれたログは保持する。

## ビルド・配置

`dotnet build MainGameStartupLogReset/MainGameStartupLogReset.csproj -c Release`

ゲーム・VR版・CharaStudioが終了した状態で、自プロジェクトの
`bin/Release/net472/MainGameStartupLogReset.dll` だけを
`F:/kks/BepInEx/patchers/canon_plugins/MainGameStartupLogReset/` に配置する。
通常の `plugins` 配下には置かない。依存DLLはBepInEx/coreの既存DLLを利用する。

## 対象

- LogCleaner.csの65所有元フォルダ（64ソースプラグイン＋CoordCatalog）の `.log` と `.log.*`。下位の旧診断フォルダも対象。
- LogRelayの共有ログと `com/log`、`unknown_owner/log` の出力。
- BlankMapAddとStudioItemSummonの `_logs/info.txt` と数値世代。
- UnityResourceDiagnosticsの `_logs/resource_diagnostics.txt`、`_logs/bake_mesh_trace.txt` と数値世代。
- VrScreenEffectProbeの診断TXT。BlankMapAddの `webcam_devices.txt`。
- MilkSprayの `_logs/milk-performance*.csv`。
- VoiceFaceEventBridgeの `receive-timing/receive-*.csv`。
- BepInEx直下 `clothes-load-probe/trace-*.csv`。

設定・プリセット・画像・一般のTXT/CSV・BepInEx共通ログは対象外。
ディレクトリやファイルのリンク（reparse point）は辿らず、削除もしない。
読込・削除失敗は対象パスと理由をBepInEx共通ログへ警告し、他の対象は継続する。
共通ログの `Startup log cleanup: deleted=... failed=... linked=...` で結果を確認できる。

## 検証

`dotnet run --project MainGameStartupLogReset/Tests/MainGameStartupLogReset.Tests.csproj -c Release`

実際のBepInEx 5ローダーによる再帰検出・Initialize実行・一度だけの清掃も検証する場合:

```powershell
dotnet build MainGameStartupLogReset/Tests/LoaderProbe/LoaderProbe.csproj -c Release
& ./MainGameStartupLogReset/Tests/LoaderProbe/bin/Release/net472/LoaderProbe.exe F:/kks/BepInEx/core
```

両テストの一時データは、各テスト実行ファイルと同じ場所の `fixtures/` に作成する。
このチェックアウトでは `F:/kks/work/plugins/canon_plugins/MainGameStartupLogReset/Tests/` 配下の
`bin/Release/<framework>/fixtures/` となる。Windows標準のTEMPフォルダは使用しない。
LoaderProbeはそのテスト用フォルダをBepInExのルートとして設定し、ゲームのアセンブリを渡さない。
DLLのロードで一時ファイルがロックされる場合は `LOADER_PROBE_TEMP=` のパスを出力する。
プローブ終了後にそのテスト用フォルダを削除できる。

テスト用の一時フォルダだけを使用し、実際のゲーム配置先は清掃しない。
ゲーム内の次回起動確認はユーザーが行う。
