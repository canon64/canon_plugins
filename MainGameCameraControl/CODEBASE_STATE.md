# CODEBASE_STATE: MainGameCameraControl

## 目的
- Hシーン向けのカメラ保存・呼び出し・補間制御を行う。
- `CameraControl_Ver2` の `CameraData.Pos/Dir/Rot/Fov` を保存し、呼び出し時は滑らかに遷移させる。

## 現在の実装
- `Plugin.cs`
  - `HFlag.ctrlCamera` から `CameraControl_Ver2` を取得する。
  - `CameraData.Pos` を注視点、`CameraData.Dir` をカメラオフセットとして毎フレーム補間更新する。
  - `OnGUI` でプリセット保存/呼び出し UI を出す。
  - `TryGetUiVisible` / `TrySetUiVisible` を公開して外部連携しやすくしている。
- `PluginSettings.cs`
  - UI表示、補間速度、`TargetPosition/CameraDirection/Rotation/Fov`、保存済みプリセットを保持する。
- `SettingsStore.cs`
  - `CameraControlSettings.json` を読み書きする。

## マウスジェスチャ
- ボーン連動中に「左ボタン押下中＋右クリック」でボーン連動を解除する（UIの「解除」と同処理 `ClearBoneLinkActiveState()`）。
- 検知は `Update()` → `HandleBoneReleaseGesture()`。UIウィンドウ上ポインタ／ウィンドウドラッグ中は誤発火させない。
- 設定 `BoneReleaseByRightClick`（bool, 既定 true, Config セクション `Input`）でON/OFF。

## 連携予定
- `MainGameBlankMapAdd` のプレイバックバーから UI の表示/非表示を切替。
- `MainGameVoiceFaceEventBridge` からのテキスト/コマンド連携を後から足せる構造にする。
