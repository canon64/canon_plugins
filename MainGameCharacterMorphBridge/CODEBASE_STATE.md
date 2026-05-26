# MainGameCharacterMorphBridge CODEBASE_STATE

## 概要
- 本編Hシーン中の女性キャラ数値モーフを扱うBepInExプラグイン。
- 初期範囲は `shapeValueBody[]` / `shapeValueFace[]` の数値変更のみ。
- キャラカードPNGは補間先の数値スナップショット取得に使い、髪・服・声・Heroine情報は変更しない。

## 設計
- `Plugin.cs`: 起動、ConfigManager項目、Harmony登録、ログ、JSON保存。
- `Plugin.ConfigDrawers.cs`: ConfigManager用スライダー/ボタン描画。スライダーはドラッグ中に反映し、ログは開始/離した時だけ。
- `Plugin.HScene.cs`: HSceneProc参照、元/カードスナップショット、複数登録カード、数値反映、Animator/MotionIK更新。
- `CharacterMorphBridgeApi.cs`: 外部プラグイン向けpublic API。カードワード指定と秒数指定Blend遷移に対応。
- `SettingsStore.cs`: UTF-8 JSON読み書き。保存先はプラグインDLLと同じフォルダ。

## 運用
- `EnableLogs` 既定値はfalse。
- ログは分岐と実行時のみ。毎フレームログは禁止。
- JSON保存はプラグイン無効化/破棄時に行う。
- Hシーン開始時は既定で元キャラ数値と選択中カード数値を自動取得し、Blend=0で待機する。
- 登録カードは `Selected Card Word` / `Target Card Path` / `Selected Card Trigger Words` を設定して「選択カードを登録/更新」でJSONへ保存する。
