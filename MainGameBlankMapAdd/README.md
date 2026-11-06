# MainGameBlankMapAdd

MainGameBlankMapAdd builds a video room on the MainGame blank map,
and provides playback-bar based control for video playback, room transform, audio gain, and profile saving.

## 体位の場所

H中のPlaybackBar「マップ」タブで「この体位の場所を調整」を押し、移動・回転ギズモまたはスライダーと数値入力で配置を調整して「この場所を覚える」で保存します。

入室・マップ読込・体位変更ごとにマップのデフォルトH場所を取得し、そのマップ・体位の個別保存があれば個別位置、なければデフォルト位置を使います。デフォルト未指定では今回のマップ開始位置を使います。同じ場所タグでも体位は別保存し、ホテル各客室と受付裏スイートも区別します。

「体位に合わせて自動移動」がONなら入室・体位変更時に個別位置または初期位置を呼び出し、OFFなら自動呼び出しを停止します。

「キャンセル」は編集前の配置へ戻し、体位変更でも未保存の編集を取り消します。「登録を解除」は保存位置だけを削除し、現在位置を動かしません。

既存のH場所一覧は引き続き使えます。配置は既存の移動通知を通してComposer・HipHijackへ伝え、各プラグインの保存データは変更しません。

保存先はプラグイン配置フォルダーの `HLocationPosePlacements.json`（Version 2）です。旧形式の個別保存を維持し、旧共通初期位置は適用しません。明示保存時だけ形式を更新し、旧ファイルをバックアップします。入室・体位切替だけでは書き込みません。旧タグ別ファイルは自動移行・適用しません。

## Environment
- Game: KoikatsuSunshine
- BepInEx: 5.x
- Process: `KoikatsuSunshine`

## Dependencies
- Required: `MainGameTransformGizmo.dll`
- Required in dependency chain: `MainGameLogRelay.dll` (required by TransformGizmo)
- Optional integration:
  - `MainGameBeatSyncSpeed.dll` (BEAT SYNC integration)
  - `MainGameSpeedLimitBreak.dll` (speed-control integration)
  - `MainGameAllPoseMap.dll` (videomap workflow support)

## Installation
1. Place `MainGameBlankMapAdd.dll` in `BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/`.
2. Place required dependencies:
   - `BepInEx/plugins/canon_plugins/MainGameTransformGizmo/MainGameTransformGizmo.dll`
   - `BepInEx/plugins/canon_plugins/MainGameLogRelay/MainGameLogRelay.dll`
3. Start the game, enter MainGame/H scene, and open the videomap.

## Quick Start
1. Move the mouse to the bottom edge of the screen to show the playback bar.
2. Click `フォルダ登録` (Add Folder) and register your video folder.
3. Select folder in `Folder`, then select a file in `Video`.
4. Adjust room transform and audio while playing.
5. Save shared values with `SaveF` and per-video values with `SaveV`.

## Hotkeys
- `Ctrl+P`: Play/Pause video
- `Ctrl+R`: Reload settings
- `Ctrl+D`: Toggle Gizmo edit mode

## Playback Bar Guide

### Fold/Help area
- `▲ / ▼`: Expand/collapse top panel (room transform, AUDIO/SAVE, BEAT SYNC)
- `説明` checkbox: show/hide hover help
- `HipUI` checkbox (to the right of `説明`): toggles `MainGirlHipHijack` UI visibility
- `ClubUI` checkbox: toggles `MainGameClubLights` UI visibility
- `AfterImage` checkbox: enables/disables `SimpleAfterimage`
- `人形` checkbox: enables/disables `MainGameDollMode`
- `体位変更` checkbox: enables/disables pose-change control in `MainGameVoiceFaceEventBridge`
- `状況Auto` checkbox: enables/disables periodic scenario-text sending in `MainGameVoiceFaceEventBridge`
- `状況送信` button: sends the current scenario text once immediately through `MainGameVoiceFaceEventBridge`
- `AutoVoice` checkbox: toggles `MainGameAutoHVoice` UI visibility
- If `MainGirlHipHijack` is not loaded, this toggle is disabled
- Other linked toggles are also disabled when the target plugin is not loaded

### Bottom row (always visible)
- `Play / Pause / Stop`: playback controls
- `|< / >|`: previous/next video
- `1Loop`: loop current video
- `Loop`: wrap to first video at folder end
- `Tiles:n`: tile count switch
- `フォルダ登録`: add playable folder
- `Folder`: select registered folder
- `Video`: select video in folder
- Sliders:
  - `再生位置`: seek position
  - `VOL`: video volume
  - `REV`: reverb intensity
  - `V-REV`: apply reverb to video audio

### Top row (`▲` expanded)
- `SIZE / POSITION / ROTATION`: room scale, position, rotation
- Numeric input and slider operate the same value

#### AUDIO / SAVE
- `Gain`: video audio gain (0.1 to 6.0)
- `RoomF` / `RoomV`: save room layout
- `GainF` / `GainV`: save gain values

#### BEAT SYNC
- Adjust `Enabled / AutoMotion / AutoThreshold / VerboseLog` and related sliders
- Save folder/video values via `SaveF` / `SaveV`
- `AutoMotion / AutoThreshold / VerboseLog` are treated as global linked toggles through `MainGameBeatSyncSpeed`

## Save Behavior (Important)
- Saved files:
  - `MapAddSettings.json` (general settings)
  - `RoomLayoutProfiles.json` (folder/video profile values)
- Priority:
  - If a per-video value exists for a category, per-video wins.
  - Otherwise folder value is applied.
- Main categories:
  - Room layout
  - Video gain
  - BeatSync values

## Main Settings
- `MapAddSettings.json`
  - `EnablePlaybackBar`: enables/disables the playback bar itself
  - `EnableUiHelpPopup`: enables/disables hover help popups for the bar
  - `FolderPlayPath` / `FolderPlayPaths`: current folder-play selection and registered folders
  - `FolderPlayLoop` / `FolderPlaySingleLoop` / `FolderPlaySortMode`: folder playback behavior
  - `VideoAudioGain`: extra gain multiplier for video audio
  - `ApplyReverbToVideoAudio`: applies room reverb to video audio
  - `SyncVoiceSourcesToVideoRoom`: moves H voice sources to the video room position
  - `HttpEnabled` / `HttpPort`: external HTTP control endpoint
  - `WebCamRequestedWidth` / `WebCamRequestedHeight` / `WebCamRequestedFps`: requested webcam capture format
  - `WebCamStatusLogIntervalSec` / `WebCamBlackSampleIntervalSec`: webcam diagnostic log intervals

## Output Files
- `BepInEx/config/com.kks.maingameblankmapadd.cfg`
- `BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/MapAddSettings.json`
- `BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/RoomLayoutProfiles.json`
- `BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/_logs/info.txt`
- `BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/webcam_devices.txt`

## Troubleshooting
通常マップ・ホテルとも「体位別保存 → デフォルト指定H場所 → 今回のマップ開始位置」の順で選び、一度だけ移動します。ホテルの部屋全体の保存位置は自動移動ONでは優先しません。デフォルト変更・解除は次の入室・体位変更から反映します。

ホテルHとAllPoseRoomから呼び出したマップでは、壁なしでも既存のFolder／Video一覧から動画の音声を再生できます。マップ切替・壁の再生成・マップ非表示でも曲と再生位置を維持し、H終了時に停止します。初回再生はAutoPlayOnMapLoad設定に従い、壁なしの曲切替では画面を暗転しません。

H場所の保存・読込・復元ログはBepInEx設定の `[HLocationDiagnostics] Enabled` で切替（既定OFF）。出力先はプラグインフォルダの `_logs/h-location.log`。保存先・読込元・ID・座標・体位別配置の選択理由・適用前後の参加者座標・失敗／中止理由を、連番とフレーム番号で追跡する。OFFでは診断専用の収集・整形・ファイル出力を実行しない。

- Playback bar is not shown:
  - Confirm you are in MainGame/H scene.
  - Move the mouse to the bottom edge.
  - Check `EnablePlaybackBar` is not OFF.
- Video does not play:
  - Check folder registration and `Folder`/`Video` selection.
  - Confirm the folder includes supported video files.
- BEAT SYNC not working:
  - Check `MainGameBeatSyncSpeed.dll` placement and logs.
- Gizmo does not respond:
  - Check `MainGameTransformGizmo.dll` and `MainGameLogRelay.dll` placement.
