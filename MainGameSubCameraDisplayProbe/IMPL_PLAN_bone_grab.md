# IMPL_PLAN — サブカメラ ボーン関連の改修（掴み角度＋ボタン連動）

対象プラグイン: MainGameSubCameraDisplayProbe
作成日: 2026-05-23
※ 既存 IMPL_PLAN.md（遷移移動＋Bridge連携）は別タスクの完了記録。本ファイルは別案件。

承認済みの2タスク。未解決の小決定はユーザー承認のもと下記で確定：
- (B) 通常プリセット読込時は保存モードを「通常」へ戻す → **やる**
- (2) 再ターゲット時は既存offset維持で新骨へカメラ移動（飛ぶ） → **やる**

---

## タスク2: 掴み中のボーンcamera角度（優先・簡単）

### 仕様（確定）
- ボーンcamera掴み: 位置は手/ギズモ、角度は**ボーン注視ロック**（掴まない）
- 非ボーンcamera掴み: 位置も角度もリジッド追従（掴む）
- VRは既に満たす → **変更しない**
- 通常モード（ギズモ掴み）はボーンcamera角度が凍結 → これを直す

### 原因
`Plugin.cs:176-177` ギズモ掴み中は `cameraDragging=true` で `UpdateBoneFollow` がスキップ→注視回転が更新されず凍結。

### 必要材料（調査済み）
- `Update()` cameraDragging 分岐: `Plugin.cs:170-180`
- `IsHoldingCameraAnchor()`: `Plugin.VR.cs:33-38`（VR掴みとギズモ掴みの区別）
- `_cameraGizmo.IsDragging`: 参照可
- `CaptureActiveBoneCameraOffset()`: `Plugin.Presets.cs:178-186`
- `ApplyHeldBoneCameraLookAtRotation()`: `Plugin.VR.cs:99-112`（VRガード付き、本体を共通化）
- `UpdateBoneFollow()`: `Plugin.Presets.cs:147-166`（注視回転の参照）

### 実装方針
1. 回転のみ注視の共通メソッド `ApplyBoneLookAtRotationOnly()` 新設（VRガード無し）。
2. `ApplyHeldBoneCameraLookAtRotation()` の中身を共通メソッド呼び出しに置換。
3. `Update()` に分岐追加：`_cameraGizmo!=null && _cameraGizmo.IsDragging && !IsHoldingCameraAnchor() && _boneFollowActive && !_transitionActive` のとき
   `CaptureActiveBoneCameraOffset(); ApplyBoneLookAtRotationOnly();` を毎フレーム。
4. 非ボーンは `_boneFollowActive=false` で分岐に入らずギズモが位置+角度を握る（変更不要）。

---

## タスク1: ボーンボタン連動・表示・保存時アクティブ化

### 仕様（確定）
- (A) プリセットボタンに骨名表示（例 `ボーン(胸): cameraA`）
- (B) ボーン連動プリセットがアクティブ化したら保存モード=ボーン＋対象ボーンボタンをその骨へ同期。通常プリセット読込時は保存モード=通常へ戻す。
- (2) 対象ボーンボタンを手動変更したら追従先も切替（既存offset維持でカメラ移動）
- (新) 保存ボタンで作った新規プリセットをアクティブ（赤）にする
- 上書きの基本動作は既存ロジックで充足（変更不要）

### 必要材料（調査済み）
- `BoneTargetLabels={"頭","胸","腰"}`: `Plugin.cs:33`
- `SaveModeLabels={"通常","ボーン"}` / `SaveModeBoneLink=1`: `Plugin.cs:32`, `Presets.cs:11-12`
- `GetPresetDisplayName()`: `Plugin.Presets.cs:488-507`（(A)）
- `ActivateBoneFollow()`: `Plugin.Presets.cs:126-145`（(B)同期点）
- 対象ボーン SelectionGrid: `Plugin.UI.cs:233-241`（(2)変更検知）
- 追従状態フィールド: `Plugin.cs:90-99`
- `ResolveActiveBone()` / `ResolveBoneTarget()` / `GetFemaleBoneCache()`: `Plugin.Presets.cs:539-606`（(2)新骨解決）
- `UpdateBoneFollow()`: `Plugin.Presets.cs:147-166`（(2)移動）
- `SaveCurrentPreset()`: `Plugin.Presets.cs:14-42`（(新)）
- `ApplyTransitionFinal()` 通常分岐: `Plugin.Transition.cs:108-114`（(B)通常戻し点）
- `IsActiveBonePreset()`: `Plugin.Presets.cs:449-460`（赤判定基準）

### 実装方針
1. **(A)** `GetPresetDisplayName`: `UseBoneLink` 接頭辞を `"ボーン(" + BoneTargetLabels[Clamp(BoneTarget,0,2)] + ")"`。
2. **(B)** `ActivateBoneFollow` 末尾: `_settings.SelectedSaveMode=SaveModeBoneLink; _settings.SelectedBoneTarget=_activeBoneTarget;`。
   `ApplyTransitionFinal` 通常分岐: `_settings.SelectedSaveMode=SaveModeNormal;`。
3. **(2)** `DrawPresetUi` 対象ボーンGrid: 変更検知→`_boneFollowActive` 中なら `RetargetActiveBone(new)`（`_activeBoneTarget/_activeBoneName` 更新、offset維持、`UpdateBoneFollow()`）。
4. **(新)** `SaveCurrentPreset` の `SaveSettings()` 前: ボーンなら `_activeBonePresetName=name`、通常なら `_activeCameraPresetName=name`、共通で `_presetNameBuffer=name`。

---

## 実装順序
1. タスク2（独立・簡単）→ ビルド確認
2. タスク1 A→新→B→(2) → ビルド確認
3. デプロイ → 動作確認 → git commit

## デプロイ / コミット
- デプロイ先: `F:/kks/BepInEx/plugins/canon_plugins/MainGameSubCameraDisplayProbe/`（実フォルダ確認）
- push/release が要るなら `F:/kks/work/tools/easy_deploy`
- commit: Claude名義入れない、機能単位

---

## タスク3: 体位ごと保存バグ修正（最優先・実データで確認済み）

### 症状（ユーザー実機）
- 「体位ごとに上書き保存」トグルON、同じカメラ(まんこ)で立位/立ち愛撫を各1回 上書き保存したのに、
  `UsePoseOverrides=false` / `PoseOverrides=[]` で1個も保存されず、同じ位置になる。

### 原因（確定）
- `OverwriteActivePreset` の体位分岐が `preset.UsePoseOverrides`（プリセット個別の旗）だけを見ている
  ([Presets.cs] 体位分岐)。`_settings.SaveCameraPoseOverrides`（トグル）は無視。
- 旗は作成時のトグル値で固定。後からONにする手段が無い → 既存プリセットでは体位保存が永遠に効かない。
- さらに `CameraPoseOverride` はボーン情報(UseBoneLink/BoneTarget/BoneName)を持たない → 体位ごとにボーンを変えられない。
- `SaveActiveBonePresetOffsets` は追従中のFOV/注視いじりを「本体」に書き込む → 本体が体位値で汚染される。

### 修正（3点）
1. **トグル連動**: `OverwriteActivePreset` の体位分岐条件を
   `((_settings.SaveCameraPoseOverrides || preset.UsePoseOverrides) && TryGetCurrentPoseInfo(...))` に変更。
   分岐内で `preset.UsePoseOverrides = true;` を立てて `CapturePoseOverrideToPreset`。既存プリセットでも体位が貯まる。
2. **ボーン保存**: `CameraPoseOverride` に `UseBoneLink/BoneTarget/BoneName` を `[DataMember]` 追加。
   - `CapturePoseOverrideToPreset`: `current` のボーンを pose に保存。
   - `ResolveCameraPresetForCurrentPose`: pose にボーン情報がある時(`!string.IsNullOrEmpty(pose.BoneName)`)だけ resolved のボーンを差し替え（旧データはBoneName無し→本体ボーン維持、後方互換）。
3. **書き戻し先**: `SaveActiveBonePresetOffsets` で現在の体位キーを取得し、アクティブプリセットがその体位の override を持つなら override に書き戻す。無ければ従来通り本体。

### 必要材料（調査済み）
- `OverwriteActivePreset`: `Plugin.Presets.cs`（体位分岐）
- `CapturePoseOverrideToPreset`: `Plugin.Presets.cs:301-337`
- `ResolveCameraPresetForCurrentPose`: `Plugin.Presets.cs:271-299`
- `SaveActiveBonePresetOffsets`: `Plugin.Presets.cs:188-210`
- `CameraPoseOverride` 定義: `Plugin.Settings.cs:261-282`
- `_settings.SaveCameraPoseOverrides` トグル: `Plugin.UI.cs:243`
- `TryGetCurrentPoseInfo` / `FindCameraPoseOverride`: 既存
- 注意: DataContractJsonSerializer は新規フィールド初期化子が効かないが、bool/intの既定(false/0)・string(null)で問題なし。旧JSONは BoneName=null で後方互換。
