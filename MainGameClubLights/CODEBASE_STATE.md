# MainGameClubLights CODEBASE_STATE

## 目的
HScene中にスポットライトを複数追加・配置し、ビートシンク・レインボー・ストロボ等のエフェクトを付与するプラグイン。

## 実装概要
- プロセス: `KoikatsuSunshine` / `KoikatsuSunshine_VR`
- 依存: `MainGameTransformGizmo`（SoftDependency）
- 依存: `MainGameBlankMapAdd`（SoftDependency、動画連携）
- 依存: `MainGameBeatSyncSeed`（SoftDependency、ビートシンク）
- 依存: `MainGameUiInputCapture`（SoftDependency）
- 設定: `ClubLightsSettings.json`
- ログ: `MainGameClubLights.log`

## ファイル構成
| ファイル | 役割 |
|---|---|
| `Plugin.cs` | Awake/Update/LateUpdate/OnGUI、HScene検出、設定バリデーション |
| `Plugin.Lights.cs` | ライトエントリ生成・破棄・毎フレーム更新・ギズモ管理 |
| `Plugin.UI.cs` | ImGUI ウィンドウ（ライト一覧・プリセット・動画連携・元ライト・ビート閾値） |
| `Plugin.NativeLights.cs` | シーン元ライトのキャッシュ・強度スケール・ビート連動 |
| `Plugin.Presets.cs` | プリセット保存・適用・削除・動画連携 |
| `Plugin.Beat.cs` | BeatSyncからの強度取得・ゾーン分類（Low/Mid/High） |
| `Plugin.VRLightGrab.cs` | VRコントローラーによるライト掴み移動 |
| `Plugin.InputCapture.cs` | UIドラッグ中の入力奪取 |
| `ClubLightsSettings.cs` | 全設定データクラス |

## ライト位置管理（重要）
- `WorldPosX/Y/Z`: 自由配置時のワールド座標。**常に go.transform.position と同期**（SyncFreeGizmoPositions で毎LateUpdateに無条件で書く）
- `RevolutionCenterX/Y/Z`: 公転の中心座標。公転ONにした瞬間の現在位置を保存。WorldPosとは別管理。
- `FollowCamera=true` 時: ギズモをデタッチ。位置はカメラ追従で毎フレーム計算。
- `RevolutionEnabled=true` 時（自由配置）: ギズモをデタッチ。RevolutionCenterを中心に周回。
- 状態変化時に位置を保存・復元する処理は不要（WorldPos常時同期により解決済み）。

## ライティングシステムの調査結果（2026-03-18）
- KKSキャラクター（髪・服）のシェーダー（Koikano/hair_main_sun、KKUTS等）は**ForwardAddパスを持たない**ものが多い
- 一部の服マテリアルはForwardAddに対応しており、カスタムスポットライトが当たる
- シーン内の `UBER_applyLightForDeferred` インスタンスはHSceneに**存在しない**（count=0）
- `_WorldSpaceLightPosCustom` はKoikanoシェーダーの主照明計算には**使われていない**
- キャラクターの照明は `CameraBase/Camera/Directional Light`（cullingMask=1024 = layer10専用）1灯のみ
- → 詳細: `work/analysis/kks_lighting_system.md`

## 既知の制限
- 髪・服の全シェーダーへの照明反映は不可（Koikanoシェーダーの構造的制限）
- ForwardAdd対応素材のみカスタムスポットライトが当たる
- `innerSpotAngle` はKoikanoキャラクターには効かない（ForwardAdd非対応のため）

## ビート連動
- `BeatSyncSeed` プラグインから毎フレーム強度値（0-1）を取得
- Low/Mid/High ゾーンに分類し、各ライトのプリセットを切り替え

## 元ライト制御
- HScene開始時にシーン内のライトをキャッシュ（自分が生成したライトは除外）
- 強度スケール・ビート連動によるプリセット適用が可能
- HScene終了時に元の強度に復元

## 2026-03-18 主な変更
- 公転・FollowCamera切り替え時の位置が飛ぶバグ修正（WorldPos常時同期・RevolutionCenter分離）
- RendererDiagタイミング修正（lstFemale が揃ってからUpdate内で実行）
- 強度ループデフォルト値変更（0.5〜1.0 / 0.3Hz）
- 主光源機能を調査の結果**削除**（`_WorldSpaceLightPosCustom`はKoikanoシェーダーに無効と判明）
- Disabled時にマーカー・矢印・ギズモも非表示にするよう修正
