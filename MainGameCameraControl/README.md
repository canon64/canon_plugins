# MainGameCameraControl

デスクトップ向けカメラ制御プラグイン。

## できること
- Hシーンの `CameraControl_Ver2` が持つカメラ状態を保存・呼び出し
- 注視点（`CameraData.Pos`）とカメラオフセット（`CameraData.Dir`）を補間遷移で移動
- 回転（`CameraData.Rot`）とFOV（`CameraData.Fov`）を保存
- 現在のカメラ状態をプリセット保存
- BlankMapAdd のプレイバックバーから UI 表示を切替

## 設定
- `CameraControlSettings.json`

## 連携
- `MainGameBlankMapAdd` から UI 表示を切替できる
- `MainGameVoiceFaceEventBridge` からも将来のコマンド連携を入れやすいように、UI表示の公開APIを用意してある
