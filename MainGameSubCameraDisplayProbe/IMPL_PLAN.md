# IMPL_PLAN: SubCameraDisplayProbe 遷移移動 + VoiceFaceEventBridge 連携

## ゴール（ユーザー言葉）
1. サブカメラのプリセット呼び出しを「瞬間移動」から「スーッと動く遷移移動」に変える
2. 遷移時間と動き方の癖（イージング）をUIで設定可能にする（スライダー＋テキスト入力）
3. VoiceFaceEventBridge の「見て」トリガーで、メインカメラとは独立にサブカメラも反応させる
4. VoiceFaceEventBridge にチェックボックスを置いて、サブカメラ連動をON/OFFできるようにする

## 動作仕様
- メインカメラとサブカメラは完全独立。それぞれが「自分のプリセット帳に名前があるか」を見て動く
- Grokテキスト「○○見て」で、両プラグインに **独立に** 「○○」を投げる。各々あれば動く、なければ無視
- VoiceFaceEventBridge の設定 `EnableSubCameraPresetForward` (bool, 既定 false) でサブカメラ転送ON/OFF
- 遷移時間/イージングはサブカメラ側UI設定をそのまま使う（Grokからの個別指定は無し）
- ボーン追従プリセットは遷移中も動的ターゲット再計算する

## 必要材料

### 材料1: MainGameCameraControl の遷移ロジック（参考実装）
- 場所: `F:/kks/work/plugins/canon_plugins/MainGameCameraControl/Plugin.cs:2603-2802`
- ステート: `_transitionActive`, `_transitionFrom`, `_transitionTo`, `_transitionStartTime`, `_transitionDuration`, `_transitionState`
- 関数: `StartCameraTransition`, `UpdateCameraTransition`, `LerpCameraData`, `EvaluateEasing`
- 補間: Pos/Dir/Rot/Fov を `Vector3.Lerp` + `Mathf.LerpAngle` + `Mathf.Lerp`
- イージング: Linear / EaseIn (t²) / EaseOut (1-(1-t)²) / EaseInOut
- ※ Probe は `BaseCameraControl_Ver2.CameraData` ではなく `_cameraAnchorObject.transform` + `_subCamera.fieldOfView` を補間する

### 材料2: SubCameraDisplayProbe のプリセット型
- 場所: `F:/kks/work/plugins/canon_plugins/MainGameSubCameraDisplayProbe/Plugin.Settings.cs:152-176`
- `SubCameraPreset.Name / UseBoneLink / BoneTarget / BoneName / SaveCameraPosition / CameraPosition / CameraRotation / CameraOffsetLocal / LookAtPosition / LookAtOffsetLocal / Fov`

### 材料3: SubCameraDisplayProbe の現行 LoadPreset / ボーン追従
- 場所: `Plugin.Presets.cs:83-154`
- `ApplyNormalPreset` でカメラTransform に瞬間反映
- `ActivateBoneFollow` で `_boneFollowActive` true、`UpdateBoneFollow` で毎フレーム LookAt + camera position
- 遷移後も `_boneFollowActive` を保持して `Update` 内 `UpdateBoneFollow` を継続させる

### 材料4: SubCameraDisplayProbe の現行 ApplyCameraSettings
- 場所: `Plugin.Probe.cs:190-205`
- `_subCamera.fieldOfView`, `nearClipPlane`, `farClipPlane` を毎フレーム反映
- `_boneFollowActive` 中は `_cameraAnchorObject.transform` の position/rotation を上書きしない
- 遷移中も `_boneFollowActive` で同じ判定が走るので、遷移補間値を `_settings` に書き込みながら適用すると競合する → 遷移中は専用の上書きルートを通す（`_transitionActive` チェック）

### 材料5: SubCameraDisplayProbe の現行 Update フロー
- 場所: `Plugin.cs:150-181`
- `UpdateBoneFollow` → `ApplyCameraSettings` → `ApplyDisplaySettings` → `UpdateVrGrab` → `SyncGizmoState` 等の順
- 遷移はこの中の `UpdateBoneFollow` の前に挿入する

### 材料6: SubCameraDisplayProbe の公開API（既存）
- 場所: `Plugin.Presets.cs:463-482` `TryLoadPresetByName(string, out string)`
- 名前が見つからなければ `reason="preset_not_found"` を返す既存仕様
- 新規追加: `TryGetPresetNames(out string[], out string)` — VoiceFaceEventBridge のトリガー検知で必要

### 材料7: VoiceFaceEventBridge の設定追加パターン
- 場所: `Plugin.cs:1389-1393` の `_cfgEnableFacePresetApply` パターンを踏襲
- PluginSettings 側の `[DataMember] public bool EnableFacePresetApply = true;` パターンも `PluginSettings.cs:84` に既存
- ON/OFFのみのチェックボックスは `Config.Bind(section, key, default, description)` で自動的にChecboxになる（追加属性不要）

### 材料8: VoiceFaceEventBridge の HandleCameraPresetCommand
- 場所: `Plugin.cs:7861-7902`
- `TryLoadCameraPresetByNameExternal` → `InvokeMainGameCameraControlApi` でリフレクション呼び出し
- 同パターンで `TryLoadSubCameraPresetByNameExternal` → `InvokeSubCameraDisplayProbeApi` を新設

### 材料9: VoiceFaceEventBridge のトリガー検知
- 場所: `Plugin.cs:7568-7635` `FindCameraPresetTriggerHitsFromText`
- 現状 `TryGetCameraPresetNamesExternal` で MGCC のプリセット名一覧を取得
- 同パターンで `TryGetSubCameraPresetNamesExternal` を新設、サブ用に独立した検知関数 `FindSubCameraPresetTriggerHitsFromText` を追加
- 呼び出し側 `Plugin.cs:3776-3806` で2系統独立に検知＆発火

### 材料10: SubCameraDisplayProbe csproj への参照追加
- VoiceFaceEventBridge.csproj 側に SubCameraDisplayProbe.dll の HintPath を追加（リフレクション呼び出しなので参照は必須ではないが、API公開クラスの型情報用に load 可能パスを通しておく）
- ただし MGCC との結合と同じく **Type.GetType でリフレクション** にするため、実は参照不要。`MainGameSubCameraDisplayProbe.MainGameSubCameraDisplayProbeApi, MainGameSubCameraDisplayProbe` で型解決可能

### 材料11: API公開クラスの追加
- SubCameraDisplayProbe 側に `MainGameSubCameraDisplayProbeApi` 静的クラスを新設（MGCC の `MainGameCameraControlApi` と同形）
- メソッド: `TryLoadPresetByName(string, out string)` + `TryGetPresetNames(out string[], out string)` + `TryLoadPresetByNameWithTransition` (任意)

## 実装ステップ
1. SubCameraDisplayProbe.Settings に `TransitionSeconds` (0-3, 既定 1.0)、`TransitionEasing` (0-3, 既定 0=Linear) 追加
2. SubCameraDisplayProbe に新規 `Plugin.Transition.cs` を作成（StartTransition / UpdateTransition / Easing関数）
3. `Plugin.Presets.cs` の `LoadPreset` を遷移経由に書き換え（duration<=0なら従来通り瞬間）
4. `Plugin.cs` の Update 内に `UpdateTransition()` を呼び出す行を追加（位置: UpdateBoneFollow の前）
5. `Plugin.UI.cs` に遷移時間スライダー＋テキスト入力、イージング選択（4ボタン）を追加
6. `Plugin.Settings.cs` のNormalize/JSONマイグレーション処理に新フィールド追加
7. SubCameraDisplayProbe の末尾に `MainGameSubCameraDisplayProbeApi` 静的クラスを新設、`TryLoadPresetByName` / `TryGetPresetNames` を公開
8. VoiceFaceEventBridge.PluginSettings に `EnableSubCameraPresetForward` (bool, 既定 false) 追加
9. VoiceFaceEventBridge.Plugin.cs に `_cfgEnableSubCameraPresetForward` を追加（Config.Bind）
10. VoiceFaceEventBridge.Plugin.cs に `TryLoadSubCameraPresetByNameExternal` / `TryGetSubCameraPresetNamesExternal` / `InvokeSubCameraDisplayProbeApi` を追加
11. VoiceFaceEventBridge.Plugin.cs の `response_text` 解析（行3776付近）にサブカメラ独立検知を追加（チェックON時のみ）
12. ビルド（SubCameraDisplayProbe → VoiceFaceEventBridge 順）
13. デプロイ（DLLを `F:/kks/BepInEx/plugins/canon_plugins/{プラグイン名}/` にコピー）
14. 入力・処理・出力を確認（ログで遷移開始/終了/転送結果が出るか）
15. git commit（粒度: サブカメラ側遷移、サブカメラ側UI、Bridge側転送、の3つに分けるか一括かは進行時に判断）

## 落とし穴
- 遷移中にボーン追従プリセットを「動的ターゲット」として扱う必要あり。CameraControl の `ResolveTransitionTargetData` 同パターン
- 遷移開始時の "from" は現在の `_cameraAnchorObject.transform` の位置/回転と `_settings.CameraFieldOfView` から取る
- 遷移完了後にボーン追従なら `_boneFollowActive=true` にして以降のフレームでボーン追従を再開
- `ApplyCameraSettings` は遷移中スキップ（`_transitionActive` チェック）。代わりに遷移ステップで直接 transform に書き込む
- `PersistTransformsToSettings` は遷移完了時のみ呼ぶ（途中で呼ぶと開始位置が動的に上書きされる）
- VoiceFaceEventBridge 側のリフレクションは MainGameCameraControl と同パターン、型読み込み失敗時は silently スキップ
- `ScheduleWakeup` 系の sleep は不要、Update ループで時間進行を `Time.unscaledTime` で計測

## 進行管理
- 着手前: この IMPL_PLAN.md で材料が揃ったことを確認 ← **完了**
- 実装後: 各ステップ完了時に状態更新
- ビルド: `dotnet build` を Release 構成で
- デプロイ: 既存 PowerShell スクリプトがあれば確認、なければ手動コピー
- コミット: 機能単位で分割（サブカメラ側遷移/UI、Bridge側転送）
