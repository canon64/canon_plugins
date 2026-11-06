# MainGameSubtitleEventBridge

## 概要

KKSメインゲーム用のHTTP字幕ブリッジです。  
HTTPで受信した字幕リクエストを `SubtitleApi` 経由で `MainGameSubtitleCore` に渡します。

## 依存関係

必須依存:
- `MainGameSubtitleCore` が先に読み込まれている必要があります。

## 同梱ファイル

- `MainGameSubtitleEventBridge.dll`
- `SubtitleEventBridgeSettings.json`
- `chime/`（通知音）

## 既定エンドポイント

- Host: `0.0.0.0`
- Port: `18766`
- Endpoint: `/subtitle-event`

## リクエスト形式（POST JSON）

主なフィールド:
- `text`（必須）
- `hold_seconds`（任意）
- `backend`（任意）
- `display_mode`（任意）
- `speaker_gender` / `speaker`（任意）

## 補足

- `AuthToken` 設定でトークン認証を有効化できます。
- 設定再読込は Ctrl+R（設定有効時）。
