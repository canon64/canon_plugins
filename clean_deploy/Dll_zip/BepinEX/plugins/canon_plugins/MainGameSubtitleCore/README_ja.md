# MainGameSubtitleCore

## 概要

KKSメインゲーム向け字幕表示の中核プラグインです。  
字幕キュー/表示を担当し、ブリッジ側が使う `SubtitleApi` を提供します。

## 3プラグインセット内での役割

- `MainGameVoiceFaceEventBridge`: 音声/イベント連携
- `MainGameSubtitleEventBridge`: HTTP字幕受信
- `MainGameSubtitleCore`（本プラグイン）: 字幕表示コア

## 同梱ファイル

- `MainGameSubtitleCore.dll`
- `SubtitleCoreSettings.json`
- `SubtitleInputPanelState.json`（ランタイムUI状態）

## 主な機能

- 字幕表示バックエンド制御（`Auto / InformationUI / Overlay`）
- 字幕キュー処理
- 入力パネルUI（任意）
- 入力パネルから外部受信先への転送（任意）

## 転送の既定値（入力パネル -> 外部受信）

- Host: `127.0.0.1`
- Port: `18767`
- Endpoint: `/manual-text`

## 補足

- 主設定ファイルは `SubtitleCoreSettings.json`。
- 設定再読込は Ctrl+R（設定有効時）。
