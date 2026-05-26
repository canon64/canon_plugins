# IMPL_PLAN: 体位ごとのカメラ位置保存（プリセット内 PoseOverride）

## ゴール（ユーザー言葉）
- 1つのプリセットで「この体位だったらこの位置」を体位ごとに保存・呼び出しできるようにする。
- SubCamera(MainGameSubCameraDisplayProbe) で同等機能があるので、それに倣う。

## 動作仕様（SubCamera 準拠）
- プリセットに `UsePoseOverrides`(bool) と `PoseOverrides[]`(体位別スナップショット配列) を持たせる。
- 体位の識別キー = `HSceneProc.flags.nowAnimationInfo.mode + ":" + id`、表示名 = `nameAnimation`。
- 保存: `体位ごとに保存` ON で「現在を保存」すると `UsePoseOverrides=true` の新規プリセットを作り、現在体位のオーバーライドを1件記録。既存プリセットには「体位を上書き」ボタンで現在体位のスナップショットを追記/更新。
- 呼び出し: プリセット呼び出し時、現在体位に一致するオーバーライドがあればその値で上書きして適用（無ければ基本値）。
- 自動切替: `体位で自動切替` ON のとき、体位が変わったらアクティブプリセットを再適用（=新体位のオーバーライドに切替）。既定 OFF。

## 必要材料（調査済み）

### 材料1: SubCamera の参考実装（最重要）
- `Plugin.PosePresets.cs`: `TryGetCurrentPoseInfo`(`HSceneProc.flags.nowAnimationInfo` mode+id / nameAnimation)、`UpdatePosePresetTracking`(体位変化検知→`ApplyTaggedPoseOverrides`)、auto-apply ガード(`PosePresetAutoApply`)
- `Plugin.Presets.cs`:
  - `ResolveCameraPresetForCurrentPose(preset)`(`Plugin.Presets.cs:271-299`) … 呼び出し時に体位一致オーバーライドで clone 上書き
  - `CapturePoseOverrideToPreset(preset,key,displayName)`(`:301-337`) … 現在カメラを Build*Preset で作って override に格納
  - `FindCameraPoseOverride`(`:339-353`)、`ClonePreset`(`:355-377`)
  - 上書き保存で `UsePoseOverrides` 時は体位上書きに分岐(`:236-243`)
- 型: `SubCameraPreset.UsePoseOverrides/PoseOverrides`、`CameraPoseOverride{Key,DisplayName,SaveCameraPosition,CameraPosition,CameraRotation,CameraOffsetLocal,LookAtPosition,LookAtOffsetLocal,Fov}`（`Plugin.Settings.cs:216-221, 261-282`）

### 材料2: MGCC のプリセット型（拡張対象）
- `PluginSettings.cs:67-119` `CameraPreset`：Name/TargetPosition/CameraDirection/Rotation/Fov/UseBoneLink/BoneTarget/BoneName/LookAtOffsetLocal/UseKsPlugFpvLink/SaveCameraPosition/CameraOffsetLocal/CameraOffsetWorld/RotationOffset/(legacy)
- → ここに `UsePoseOverrides`(bool) と `PoseOverrides`(CameraPoseOverride[]) を追加。新型 `CameraPoseOverride` を MGCC 用に定義（上記 CameraPreset の保存対象フィールド+Key+DisplayName）。

### 材料3: MGCC の保存ロジック
- `SaveCurrentAsPreset()`(`Plugin.cs:909-957`)：`_selectedSaveMode` で `BuildBoneLinkedPreset`/`BuildKsPlugFpvPreset`/通常 を作る
- `BuildBoneLinkedPreset(name,data)`(`:1200-1232`) が設定するフィールドが override スナップショットの内容と一致
- 通常モードのフィールド：TargetPosition=data.Pos / CameraDirection=data.Dir / Rotation=data.Rot / Fov

### 材料4: MGCC の呼び出しロジック
- `LoadPreset(preset)`(`:959-1015`)：先頭で `ResolveCameraPresetForCurrentPose(preset)` を噛ませる。`_activeBoneLinkPresetName`(`:983`) に加えて新規 `_activeCameraPresetName` を保持（自動切替の対象特定用）
- `BuildCameraDataFromPreset`(`:1165-`)・`StartCameraTransition`(`:2698`) は resolved preset をそのまま使えば流用可

### 材料5: ボーン/オフセット ヘルパ（流用）
- `GetPresetBoneLinkOffsetLocal`(`:2166`)、`GetPresetCameraOffsetLocal`(`:2174`)、`HasPresetCameraPosition`(`:2182`)
- override 復元時は CameraPreset のフィールドへ値を差し込む形にすれば既存ヘルパがそのまま効く

### 材料6: 体位検出の前提（MGCC 側で確認済み）
- `HSceneProc` は MGCC でも参照済み（`Plugin.cs:1376`）。`proc.flags.nowAnimationInfo`(public) も SubCamera と同様にアクセス可

### 材料7: 設定の保存パターン（一貫性厳守）
- `_saveBoneCameraPosition`(`Plugin.cs:97`) と同じ「素のboolフィールド + Settings ミラー + UIトグル、Config.Bind無し」方式に倣う（直近フィードバック [[feedback_consistent_settings_pattern]]）
- → `_saveCameraPoseOverrides` / `_posePresetAutoApply` を同方式で追加。`Settings.SaveCameraPoseOverrides` / `Settings.PosePresetAutoApply`(既定false) を `PluginSettings.cs` に追加し、`SyncFromConfig`/`SaveSettings`/Awake 復元に反映

### 材料8: UI 配置
- `Plugin.cs` DrawUiWindow（保存モード `:410-453`、保存ボタン群 `:517-535`、プリセット一覧 `:537-564`）
- 追加：保存モード下に「体位ごとに保存」「体位で自動切替」トグル、現在体位ラベル、保存ボタン群に「体位を上書き」ボタン
- partial クラスなので体位ロジックは新規 `Plugin.PosePresets.cs` に分離（SubCamera と同じ構成）

## 実装ステップ
1. `PluginSettings.cs`: `CameraPreset` に `UsePoseOverrides`/`PoseOverrides` 追加、`CameraPoseOverride` 型新設、`PluginSettings` に `SaveCameraPoseOverrides`/`PosePresetAutoApply`(既定false) 追加
2. `Plugin.cs` フィールド: `_saveCameraPoseOverrides`/`_posePresetAutoApply`/`_activeCameraPresetName`、Awake 復元、`SyncFromConfig`/`SaveSettings` ミラー
3. 新規 `Plugin.PosePresets.cs`: `TryGetCurrentPoseInfo` / `FindPresetByName` / `FindCameraPoseOverride` / `ClonePreset` / `ResolveCameraPresetForCurrentPose` / `CapturePoseOverrideToPreset` / `UpdatePosePresetTracking`
4. `Plugin.cs` `SaveCurrentAsPreset`: `_saveCameraPoseOverrides` 時は `UsePoseOverrides=true` + 現在体位 capture
5. `Plugin.cs` `LoadPreset`: 先頭で `ResolveCameraPresetForCurrentPose`、`_activeCameraPresetName` 設定
6. `Plugin.cs` Update（または LateUpdate）に `UpdatePosePresetTracking()` を追加
7. `Plugin.cs` DrawUiWindow: トグル2つ＋現在体位ラベル＋「体位を上書き」ボタン追加
8. ビルド(Release)→デプロイ→入力/処理/出力確認（ログに pose save/load/auto-apply）→ git commit

## 落とし穴
- `ResolveCameraPresetForCurrentPose` の clone は **Name を保持**（`_activeBoneLinkPresetName`/`_activeCameraPresetName` のトラッキングが崩れないように）
- 自動切替は `Time` ではなく体位キー変化で発火。ギズモ/補間中の再適用は避ける（SubCamera は dragging チェック相当）。MGCC は補間中(`_transitionActive`)なら見送る
- DataContractJsonSerializer は新規フィールドの初期化子が効かない → 既定値は Normalize 不要だが、配列 null を考慮（`PoseOverrides ?? new[0]`）
- override スナップショットは現在の `_selectedSaveMode` に従う（ボーン/ksFPV/通常）。SubCamera 同様 Build*Preset を再利用
- 体位未検出（H外）では capture/auto-apply をスキップ
