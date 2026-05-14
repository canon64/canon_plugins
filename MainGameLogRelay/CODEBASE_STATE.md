# CODEBASE_STATE: MainGameLogRelay

- Updated: 2026-03-20
- Project: `F:/kks/work/plugins/MainGameLogRelay/`
- Phase: 仕様合意済み（実装前）
- Deploy: 未実施（今回デプロイしない）

## 概要

- 各プラグインから共通API経由でログを出すためのログリレープラグイン。
- 今回は `MainGameLogRelay` のみ作業対象。既存プラグイン本体は変更しない。

## 合意済み仕様

- 共通API:
  - 各プラグインは `LogRelayApi` 呼び出しだけでログ出力可能にする。
- 出力先モード（owner単位）:
  - `FileOnly`（専用ログのみ）
  - `BepInExOnly`（BepInExターミナルのみ）
  - `Both`（両方）
- レベル:
  - `Debug / Info / Warning / Error`
  - 判定は「全体デフォルト + owner個別上書き」
- owner単位の有効/無効:
  - ownerごとに `Enabled / Disabled` を設定できる。
  - `Disabled` の owner は出力しない。
- 専用ログ出力先:
  - `F:/kks/BepInEx/plugins/canon_plugins/log/` 配下
  - owner単位でファイル分離（混在させない）
- ファイル名:
  - `owner -> logKey` を設定で指定可能（優先）
  - `logKey` 未指定時は owner から自動生成
  - `logKey` はサブフォルダ対応（`/` 許可）
    - 例: `main/MainGirlHipHijack` -> `log/main/MainGirlHipHijack.log`
- パス安全化:
  - `..` や絶対パスを拒否する。
- 起動時リセット:
  - 専用ログは起動時リセット既定ON。

## 現在状態

- ディレクトリ作成済み。
- `MainGameLogRelay.csproj` は作成済み。
- 上記仕様は合意済み、実装はこれから開始。
