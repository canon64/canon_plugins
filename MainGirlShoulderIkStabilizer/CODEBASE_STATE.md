# CODEBASE_STATE: MainGirlShoulderIkStabilizer

- Updated: 2026-03-29
- Project: `F:/kks/work/plugins/canon_plugins/MainGirlShoulderIkStabilizer`

## 目的
- Hシーン中のメイン女性キャラ肩角度を補正し、腕姿勢の破綻を抑える。

## 動作条件
- `Enabled = true`
- `ShoulderRotationEnabled = true`
- 実行中シーンで対象女性の `FullBodyBipedIK` を取得できること
- `IKPositionWeight > 0` のときのみ実質的に補正が適用される
- 上記より、腕IKが効いている場面で機能する

## 実装概要
- `HSceneProc.LateUpdate` の Postfix で毎フレーム `OnAfterHSceneLateUpdate` を実行
- `ChaControl -> animBody -> FullBodyBipedIK` を解決し、`ShoulderRotator` をアタッチ
- `IKSolver.OnPostUpdate` にフックして肩回転を補正
- `solver.Update()` の再実行は行わず、`IKSolver.OnPostUpdate` 内で肩回転の最終補正のみ適用する

## ConfigurationManager対応（2026-03-12）
- 主要16項目を ConfigManager で変更可能
- 値変更は `SettingChanged` 経由で即時反映（再起動不要）
- パラメータは範囲クランプ済み
- 管理キー:
  - `General`: `Enabled`, `VerboseLog`, `ShoulderRotationEnabled`
  - `Shoulder`: `IndependentShoulders`, `ReverseShoulderL`, `ReverseShoulderR`, `ShoulderWeight`, `ShoulderOffset`, `ShoulderRightWeight`, `ShoulderRightOffset`
  - `ArmState`: `LoweredArmScale`, `RaisedArmStartY`, `RaisedArmFullY`, `RaisedArmScaleMin`
  - `Safety`: `MaxShoulderDeltaAngleDeg`, `MaxSolverBlend`

## 設定ファイル
- JSON: `ShoulderIkStabilizerSettings.json`（プラグインフォルダ）
- BepInEx cfg: 起動後に `BepInEx/config` 配下へ生成
- 実行時は cfg の値が優先され、内部設定へ上書き反映される

## ログ運用（2026-03-29確認）
- ログ出力は `MainGameLogRelay` 経由（`LogRelayApi`）で実施し、Relay が利用不可のときのみ `base.Logger` にフォールバックする
- `com.kks.main.girlshoulderikstabilizer.cfg` の `Logging.EnableLogs = true`（確認時点）
- owner/logKey は `main/MainGirlShoulderIkStabilizer`
- 実ログ出力先: `F:/kks/BepInEx/plugins/canon_plugins/MainGirlShoulderIkStabilizer/log/MainGirlShoulderIkStabilizer.log`
- Relay 既定設定（`MainGameLogRelaySettings.json`）: `Enabled=true`, `DefaultOutputMode=Both`, `FileLayout=PerPlugin`
