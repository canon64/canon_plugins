# MainGameBlankMapAdd

## 概要
MainGame の blank map に動画ルームを追加し、H中に動画再生・配置・音声設定を行うプラグインです。
現在はフォルダー再生を基本運用とし、再生バーからフォルダー/動画の個別設定を保存できます。

## 導入
1. `MainGameBlankMapAdd.dll` を `BepInEx/plugins/MainGameBlankMapAdd/` に配置します。
2. 依存として `MainGameTransformGizmo.dll` を有効化してください。
3. ゲームを起動し、Hシーンで動画マップ（既定: map 900）に入ります。

## 基本操作
- `Ctrl+P`: 動画再生/停止
- `Ctrl+R`: 設定再読込
- `Ctrl+D`: Gizmo編集モード切替
- 再生バー: 画面下端にマウスを寄せると表示

## フォルダー再生の使い方
1. 再生バーの `フォルダ登録` ボタンでフォルダを追加します。
2. `Folder` ドロップダウンで再生対象フォルダを選びます。
3. `Video` ドロップダウンで動画を直接選択できます。
4. `1Loop` で単曲ループ、`Loop` でフォルダ末尾時の先頭戻りを制御します。

## 保存仕様（利用者向け）
- `SaveF`: フォルダ設定として保存
- `SaveV`: 動画個別設定として保存
- 読み込み順は「フォルダ設定 → 動画個別設定」です。
- 動画個別設定がある項目は動画側が優先されます。

保存対象の主な項目:
- ROOM: サイズ/位置/回転
- AUDIO: 動画ゲイン、残響反映
- BEAT SYNC: Enabled、Threshold、MotionSwitch など

## 設定ファイルとログ
- `BepInEx/plugins/MainGameBlankMapAdd/MapAddSettings.json`
- `BepInEx/plugins/MainGameBlankMapAdd/RoomLayoutProfiles.json`
- `BepInEx/plugins/MainGameBlankMapAdd/_logs/info.txt`

## トラブル時
- 再生バーが出ない: `EnablePlaybackBar=true` を確認
- フォルダ動画が出ない: 対象フォルダに動画拡張子ファイルがあるか確認
- BEAT SYNC連携しない: `MainGameBeatSyncSpeed.dll` の配置とログを確認