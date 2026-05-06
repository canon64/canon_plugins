# CODEBASE_STATE: MainGameFreeHMasturbationMenu

- Updated: 2026-03-10
- Project: `F:/kks/work/plugins/MainGameFreeHMasturbationMenu`

## 目的

- FreeH中の通常系アクション操作に「オナニー」項目を追加する。
- オナニー体位を通常導線から直接選択できるようにする。
- オナニー選択後も通常側体位へ戻れる導線を維持する。

## ファイル構成

- `MainGameFreeHMasturbationMenu.csproj`
  - `net472`
  - `BepInEx`, `Assembly-CSharp`, `UnityEngine.*`, `UnityEngine.UI`, `Unity.TextMeshPro` 参照
- `Plugin.cs`
  - BepInExエントリ
  - H中UIのアクションボタンを動的追加（`オナニー`）
  - `lstUseAnimInfo[3]` から体位選択して `selectAnimationListInfo + ClickKind.actionChange` を送信
  - マスターベーション中の `voiceWait/click` 補正
  - 専用ログ出力 (`MainGameFreeHMasturbationMenu.log`)
- `PluginSettings.cs`
  - JSON設定定義
- `SettingsStore.cs`
  - `MainGameFreeHMasturbationMenuSettings.json` 読み書き

## 設定JSON

- `Enabled`: 全体ON/OFF
- `FreeHOnly`: FreeH時のみ動作
- `ButtonText`: 追加ボタン表示名
- `ButtonOffsetX` / `ButtonOffsetY`: 追加ボタンのUI位置オフセット
- `TemplateButtonIndex`: 見た目コピー元ボタンindex
- `AnchorButtonIndex`: 位置基準ボタンindex
- `CycleMasturbationPoses`: クリックごとにオナニー体位を巡回
- `StartFromCurrentWhenInMasturbation`: 現在オナニー体位の次から巡回
- `KeepActionMenuVisibleInMasturbation`: オナニー中もアクションメニュー表示維持
- `AutoRecoverTransitionFromMasturbation`: `voiceWait/click` 補正で遷移復帰
- `VerboseLog`: 詳細ログON/OFF

## デプロイ先

- `F:/kks/BepInEx/plugins/MainGameFreeHMasturbationMenu/MainGameFreeHMasturbationMenu.dll`
- `F:/kks/BepInEx/plugins/MainGameFreeHMasturbationMenu/MainGameFreeHMasturbationMenuSettings.json`
- `F:/kks/BepInEx/plugins/MainGameFreeHMasturbationMenu/MainGameFreeHMasturbationMenu.log`
