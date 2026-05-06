# canon_plugins

`canon_plugins` は、KoikatsuSunshine 本編Hを拡張するための BepInEx プラグイン一式です。  
個別導入の案内ではなく、体位、カメラ、速度、衣装、音声、字幕、演出、IK をまとめて扱うための全体パックです。

## このパックでできること

- blank マップや動画ルームを追加して、本編Hの空間を拡張する
- FreeH で全体位を扱いやすくする
- H速度の上限を拡張し、BPM や動画に同期させる
- カメラ位置、注視点、回転、FOV を保存し、補間つきで切り替える
- 外部テキストやコマンドから、体位、衣装、顔、字幕、カメラ、音声を動かす
- 残像、グロー、照明などの映像演出を追加する
- 腰、肩、BodyIK、姿勢まわりを調整する

## 機能ごとの構成

| 機能 | 主なプラグイン |
|---|---|
| blank マップ / 動画ルーム | `MainGameBlankMapAdd` |
| 全体位 | `MainGameAllPoseMap` |
| H速度拡張 | `MainGameSpeedLimitBreak` |
| BPM / 動画同期 | `MainGameBeatSyncSpeed` |
| カメラ / FOV | `MainGameCameraControl` |
| 外部連携 / 自然文制御 | `MainGameVoiceFaceEventBridge` |
| 字幕 | `MainGameSubtitleCore`, `MainGameSubtitleEventBridge` |
| 残像 / 発光 / 照明演出 | `MainGameCharacterAfterimage`, `SimpleAfterimage`, `MainGameGlow`, `MainGameClubLights` |
| ドール化 / 表情固定 | `MainGameDollMode` |
| 腰 / 姿勢 / BodyIK | `MainGirlHipHijack` |
| 肩IK安定化 | `MainGirlShoulderIkStabilizer` |
| IK橋渡し | `MainGameAdvIkBridge` |

## 基盤プラグイン

以下は単体用途ではなく、他プラグインを支える基盤です。

- `MainGameLogRelay`
- `MainGameUiInputCapture`
- `MainGameTransformGizmo`
