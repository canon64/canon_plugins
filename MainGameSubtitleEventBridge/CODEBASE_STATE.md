# MainGameSubtitleEventBridge CODEBASE_STATE

## 概要
- 本編 (`KoikatsuSunshine`) で HTTP 受信した字幕リクエストを `MainGameSubtitleCore.SubtitleApi` にキュー投入するブリッジ。
- 受信時に話者種別推定を行い、任意で通知音（chime）を再生する。

## 現在の仕様
- プラグインID:
  - `GUID`: `com.kks.maingame.subtitleeventbridge`
  - `Name`: `MainGameSubtitleEventBridge`
  - `Version`: `0.1.0`
- 有効プロセス: `KoikatsuSunshine`（`[BepInProcess("KoikatsuSunshine")]`）。
- `MainGameSubtitleCore` へ HardDependency。
- 受信サーバー:
  - `ListenHost`, `ListenPort`, `EndpointPath`
  - 既定値は `0.0.0.0:18766` + `/subtitle-event`
  - `AuthToken` が設定されている場合は `X-Auth-Token` を検証。
- リクエスト:
  - JSON（`text`, `hold_seconds`, `backend`, `display_mode`, `speaker_gender`, `speaker`）
  - `AcceptPlainTextBody=true` なら plain text も受理。
- `Ctrl+R` で設定再読込（`EnableCtrlRReload=true` 時）。
- ログ出力先: `Path.GetDirectoryName(Info.Location)/MainGameSubtitleEventBridge.log`

## 非採用案
- `MainGameSubtitleCore` を介さず独自UIへ直接描画する方式は採用していない。
- WebSocket ではなく軽量な HTTP over TcpListener 実装を採用。

## 依存
- 参照プロジェクト: `../MainGameSubtitleCore/MainGameSubtitleCore.csproj`
- 設定ファイル: `SubtitleEventBridgeSettings.json`
- 補助ファイル: `wave/`（通知音再生用）

## 既知課題
- サーバー処理は軽量実装のため、高負荷時の詳細メトリクスは不足している。
- 話者推定はヒント文字列依存で、曖昧入力時は `Unknown` 扱いになる。

## 次の作業
- リクエスト統計（件数/失敗率/平均処理時間）のログ化。
- 話者推定ルールの設定ファイル化。
