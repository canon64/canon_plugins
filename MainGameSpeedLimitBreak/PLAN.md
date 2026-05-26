# MainGameSpeedLimitBreak PLAN

## 目的
- DLL側で `FaceListCtrl.facelib` に自作表情を追加し、`SpeedTimeline` から既存表情と同じ手順で呼び出せるようにする。

## ゴール
- `SetFace(faceId, voiceKind, action)` が自作表情IDでも成功する。
- 既存の `FaceDb...` 条件指定（`FaceDbChara`, `FaceDbVoiceKind`, `FaceDbAction`）で自作表情を選択できる。
- 既存IDと衝突しない運用を固定化する。

## 前提
- `hface_dict.db` は検索用DBであり、実際の適用元は `FaceListCtrl.facelib`。
- 新規表情を使うには、DB編集だけでなく `facelib` 側へ行追加が必要。

## 実装方針（DLL）
1. **自作表情定義ファイルを追加**
- 例: `BepInEx/plugins/MainGameSpeedLimitBreak/CustomFaces.json`
- 1件あたり:
  - `chara`（例: `c43`）
  - `voiceKind`（0/1/2）
  - `action`（0,1,...）
  - `faceId`（推奨: 9000以上）
  - 表情パラメータ（眉/目/口/涙/頬/ハイライト/瞬き/揺れ等）

2. **適用タイミングを固定**
- `facelib` 生成後にのみ追加する。
- Hシーン初期化直後（`HSceneProc` 側の初期化後）で1回注入する。
- 再初期化やシーン遷移で辞書が作り直される場合は再注入する。

3. **ID衝突回避**
- 注入前に `faceId` の既存有無を確認。
- 既存があれば:
  - `上書き禁止` をデフォルト
  - 設定でのみ上書き許可
- ログへ `added / skipped(collision)` を出す。

4. **FaceDB検索との接続**
- 既存 `FaceDb...` ルートをそのまま使うため、検索DBに自作行を持たせるか、DLL側に補助検索を追加する。
- 第一段は運用簡略化のため、`FaceDbFileId/FaceDbFaceId` 直指定でも呼べる経路を残す。

5. **ログ/診断**
- 専用ログに以下を出す:
  - 注入件数
  - 衝突件数
  - 実際に `SetFace` に通った `chara/voiceKind/action/faceId`

## 検証計画
1. `CustomFaces.json` に1件だけ追加（`faceId=9001`）
2. タイムラインで該当 `FaceDb...` 条件を指定
3. ログで `SetFace` 成功を確認
4. 衝突テスト（既存IDを指定）で `skipped(collision)` を確認

## リスク
- 注入タイミングが早いと `facelib` 未生成で失敗する。
- シーン遷移で辞書再生成された場合、再注入しないと消える。
- `voiceKind/action` の組み合わせが不一致だと `SetFace` が失敗する。

## 次アクション
- 実装前に `CustomFaces.json` の厳密スキーマ（必須/任意）を確定。
- その後、注入ポイントを1箇所に固定して実装。

## 保留タスク（強弱の直接指定）
- 本日見送り。次回対応する。
- 要件:
  - タイムラインで「強にする」「弱にする」を直接指定できるようにする。
  - 既存の `motionchange`（トグル）は互換維持する。
- 実装方針:
  1. GUI (`KoreoEditor`) の `ClickKind` 候補に `motionStrong` / `motionWeak` を追加。
  2. ゲーム側 (`MainGameSpeedLimitBreak`) の `TryApplyCueClick` で上記2つを独自解釈。
  3. 現在状態（`flags.voice.speedMotion`）を見て、目標と異なる時だけ `motionchange` を1回送る。
  4. 同一状態なら何もしない（無駄なトグルを避ける）。
- 受け入れ条件:
  - `motionStrong` 指定時、結果が必ず強状態になる。
  - `motionWeak` 指定時、結果が必ず弱状態になる。
  - 連続再生時に不要な反転が起きない。
