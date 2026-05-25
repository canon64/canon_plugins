# IMPL_PLAN — ClubLights フォルダ機能

対象: MainGameClubLights
作成: 2026-05-23

## 改訂（2026-05-24）: 単一フォルダ→多フォルダ
当初「全ライトを1個の強制フォルダ」に実装したが、要件は**複数フォルダを自由に作成し、ライトを任意で割り当てる**だった。以下に作り直し：
- `LightFolder`（Id/Name/Pos/Rot/ShowHandle/HandleSize）を複数 `Folders` に保持
- `LightInstanceSettings.FolderId`（空=未所属=従来通りワールド座標で単体）
- 所属ライトはそのフォルダGOの子（フォルダ基準ローカル）、未所属はワールド
- 割り当ては `SetParent(worldPositionStays:true)` で**位置を保ったまま**（原点集約を防止）
- フォルダごとにハンドル＋ギズモ＋VR掴み。掴むと所属ライトのみ追従
- UI: フォルダ追加/名前/ハンドルトグル/削除、ライト行に所属フォルダ選択（サイクル）
- 体位スナップショットは Folders＋Lights を丸ごと保存/復元


## ゴール（ユーザー確定）
1. ライトをまとめる「フォルダ」（親オブジェクト）を作る
2. 全ライトをフォルダにぶら下げる（親子化、composerと同パターン）
3. フォルダごと掴んで全ライト一括移動（通常ギズモ＋VR）
4. フォルダの掴みハンドルは表示/非表示トグル（実体が無いので見える球マーカー）
5. 体位ごとに「丸ごとスナップショット」保存（フォルダ位置＋全ライト全設定）

## 参考実装
- composer の親子化: `MainGameObjectComposer/Plugin.Objects.cs:160-242`
  - 親 root の下に wrapper を `SetParent(parent, false)` → `localPosition/localEulerAngles` で配置
  - **位置は親基準のローカル座標で持つ**
- MGCC の体位保存（正しい設計）: `MainGameCameraControl/Plugin.PosePresets.cs`
  - 専用ボタンで `UsePoseOverrides=true` 強制、スナップショット方式、auto-apply

## 現状把握（ClubLights）
- 各ライト= 独立GO `ClubLight_{id}`、共通の親なし、ワールド座標（`WorldPos`↔`transform.position` 双方向同期）
- 生成: `Plugin.Lights.cs:CreateLightEntry`（`OnHSceneStart`→`BuildLightObjects`）
- 同期: `Plugin.Lights.cs:SyncFreeGizmoPositions`（`LateUpdate`、free光のworld pos→WorldPos）
- 個別ギズモ: `TryAttachGizmo`（`MainGameTransformGizmo`）
- 個別VR掴み: `Plugin.VRLightGrab.cs`（コントローラー近傍のライトを掴む）
- FollowCamera/公転はworld座標を毎フレーム計算
- ライフサイクル: `Plugin.cs` Update/LateUpdate/OnHSceneStart/End/DestroyAllLights

## 設計（確定）
- **フォルダ= 親GO `ClubLightsFolder`**。world transform を設定に保存（`FolderPosX/Y/Z`, `FolderRotX/Y/Z`）。既定 identity（=従来互換: local==world）。
- **全ライトをフォルダにぶら下げる**: `CreateLightEntry` で `go.transform.SetParent(folder, false)`。
- **`WorldPos` をフォルダ基準ローカルとして再解釈**:
  - free光: `localPosition = (WorldPosX,Y,Z)`
  - `SyncFreeGizmoPositions`: `WorldPos = go.transform.localPosition`（worldではなくlocal）
  - `AddLight`: spawn(world) → `folder.InverseTransformPoint(spawn)` を WorldPos に
  - FollowCamera/公転は引き続きworld座標で計算（フォルダ追従しない=仕様）。公転中心はworldのまま。
- **掴みハンドル**: folder原点に球マーカー（`ShowFolderHandle` でトグル）。これがギズモ/VR掴みのアンカー。
- **通常ギズモ**: folder GO に `TransformGizmo` をアタッチ。`ShowFolderHandle` と連動表示。
- **VR掴み**: `UpdateVRLightGrab` にフォルダ掴みを追加（ハンドル近傍をGripで掴む）。folderを動かすと子が追従。
- **体位スナップショット**: `List<LightPoseSnapshot>{ Key, DisplayName, FolderPos/Rot, List<LightInstanceSettings> }`。
  - 「体位を上書き」ボタンで現在の丸ごとを保存（MGCC方式: 常に有効化）。
  - `PosePresetAutoApply` ONで体位変化時に復元（DestroyAllLights→Lights差し替え→BuildLightObjects）。
  - `TryGetCurrentPoseInfo` は MGCC/サブカメラと同じ実装を移植。

## 必要材料（調査済み）
| 材料 | 場所 |
|---|---|
| ライト生成/破棄/同期/ギズモ | `Plugin.Lights.cs`（CreateLightEntry/DestroyAllLights/SyncFreeGizmoPositions/TryAttachGizmo/OnGizmoDragStateChanged） |
| VR掴み | `Plugin.VRLightGrab.cs` |
| ライフサイクル | `Plugin.cs`（OnHSceneStart/End, Update, LateUpdate, GetReferencePosition） |
| データ定義 | `ClubLightsSettings.cs` |
| 親子化パターン | `MainGameObjectComposer/Plugin.Objects.cs:160-242` |
| 体位保存パターン | `MainGameCameraControl/Plugin.PosePresets.cs` |
| ギズモAPI | `TransformGizmoApi.TryAttach` |
| UI | `Plugin.UI.cs` |

## 実装ステップ（段階）
### Stage 1: フォルダ＋親子化＋掴み＋ハンドル
1. `ClubLightsSettings` に Folder系フィールド追加（FolderPos/Rot, ShowFolderHandle）
2. 新規 `Plugin.Folder.cs`: folder GO生成/破棄、ハンドル球、ギズモアタッチ、VR掴み、位置同期
3. `CreateLightEntry`: folderにSetParent＋localPosition
4. `SyncFreeGizmoPositions`: localPosition基準に変更
5. `AddLight`: spawnをfolderローカルに変換
6. `OnHSceneStart`/`DestroyAllLights`: folder生成/破棄を組み込む
7. `UpdateVRLightGrab`: folder掴みを追加
8. UI: フォルダ節（ハンドル表示トグル、位置リセット等）
9. build/deploy/commit

### Stage 2: 体位ごと丸ごとスナップショット
1. `LightPoseSnapshot` 型＋設定リスト追加
2. `Plugin.PosePresets.cs` 新規: TryGetCurrentPoseInfo/Capture/ApplySnapshot/AutoApplyトラッキング
3. UI: 「体位を上書き」「体位で自動切替」＋一覧
4. build/deploy/commit

## 注意
- 既存JSON互換: Folder既定identityでlocal==world、既存配置を維持
- FollowCamera/公転はフォルダ非追従（仕様）。必要なら後で公転中心ローカル化を検討
- ハンドルOFF時はギズモ・球とも非表示（掴めなくする）
- デプロイ先: `F:/kks/BepInEx/plugins/canon_plugins/MainGameClubLights/`
- commit: Claude名義入れない
