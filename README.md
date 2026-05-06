# canon_plugins

KoikatsuSunshine 本編向けの BepInEx プラグイン集です。  
本編Hの体位・速度・カメラ・衣装・音声・字幕・演出・IK をまとめて拡張します。

このリポジトリには「単体で遊ぶ機能」と「他プラグインの土台になる機能」が混在しています。  
初見向けには、まず主役プラグインから見るのが分かりやすいです。

## まず何ができるか

- blank マップや全体位マップを追加して、本編Hの遊び方を増やせます。
- H速度を上限突破したり、BPMや動画cueで速度を自動制御できます。
- カメラ位置/FOV/注視点を保存して、プリセット呼び出しや補間遷移ができます。
- 外部テキストやコマンドから、体位変更、着替え、着脱、顔、音声、字幕、カメラを動かせます。
- 残像、グロー、クラブライトなどの映像演出を足せます。
- ヒロインの腰・肩・IK・表情を専用制御できます。

## まず入れるなら

- `MainGameBlankMapAdd`
  - blank マップや動画ルームを使いたい時の入口です。
- `MainGameAllPoseMap`
  - FreeH で全体位を使いたい時の入口です。
- `MainGameSpeedLimitBreak`
  - H速度を大きくいじりたい時の入口です。
- `MainGameCameraControl`
  - カメラプリセットやFOV管理をしたい時の入口です。
- `MainGameVoiceFaceEventBridge`
  - 外部ツールや自然文から本編Hを動かしたい時の入口です。

## プラグイン一覧

### MainGameBlankMapAdd

- できること:
  - 本編マップ一覧へカスタム blank マップを追加します。
  - 追加マップを空白化したり、動画ルームとして使えます。
  - プレイバックバーや外部API経由で他プラグインと連携できます。
- 使い方:
  - `MapAddSettings.json` を調整して、追加マップ番号や表示名、動画ルーム設定を決めます。
  - blank マップを選んで FreeH を始めると、通常マップではなく専用空間で遊べます。
- 向いている用途:
  - 背景を消したい。
  - 壁/床/天井に動画を貼った空間を作りたい。

### MainGameAllPoseMap

- できること:
  - FreeH 用に「全体位を使える専用マップ」を追加します。
  - 進行ロックやカテゴリ制限を受けにくい体位一覧を出せます。
- 使い方:
  - 追加された専用マップを FreeH で選びます。
  - `AllPoseMapSettings.json` で対象カテゴリや特殊ジャンプ抑止を調整できます。
- 向いている用途:
  - 既存マップの制限を気にせず全体位を見たい。

### MainGameSpeedLimitBreak

- できること:
  - 速さゲージ表示はそのままで、実際のHアニメ速度だけ上限突破させます。
  - オート挿入速度の乗っ取り、動画cue連動、体位/衣装/顔/クリック連携まで持っています。
- 使い方:
  - `SpeedLimitBreakSettings.json` で変換元/変換先速度レンジを決めます。
  - 必要なら動画cue JSON を置いて、時刻ごとの自動切り替えも使えます。
- 向いている用途:
  - 速度をもっと上げたい。
  - 動画や演出に合わせて速度・体位・顔を自動化したい。

### MainGameBeatSyncSpeed

- できること:
  - 動画や音声のBPMを基準に H速度をビート同期させます。
  - タップテンポ入力や `MainGameSpeedLimitBreak` 連携にも対応しています。
- 使い方:
  - blank マップ動画再生と組み合わせるか、手動で BPM を入れます。
  - `RightCtrl` タップテンポで BPM 補正もできます。
- 向いている用途:
  - 音楽やMVに合わせてH速度を動かしたい。

### MainGameCameraControl

- できること:
  - Hカメラの位置、注視点、回転、FOV を保存/呼び出しできます。
  - ボーン連動、ksFPV連動、補間遷移つきでカメラプリセットを運用できます。
- 使い方:
  - ゲーム中の UI から現在カメラを保存し、プリセットとして呼び出します。
  - `CameraControlSettings.json` で補間時間やFOV適用などを調整します。
- 向いている用途:
  - 体位ごとに定番カメラを用意したい。
  - 外部連携や自然文トリガーで視点を変えたい。

### MainGameVoiceFaceEventBridge

- できること:
  - named pipe 経由の外部コマンドを受けて、本編Hへ音声・顔・体位・衣装・コーデ・字幕・カメラ指示を流します。
  - `response_text` を使うと、自然文から体位変更、着替え、着脱、カメラ変更、動画再生指示を拾えます。
  - 外部音声再生中のゲーム側ボイス抑止も行えます。
- 使い方:
  - 外部ツールから `kks_voice_face_events` へコマンドを送ります。
  - 直接 `pose` / `clothes` / `coord` / `camera_preset` を送ってもいいし、`response_text` で自然文を送っても使えます。
  - 例:
    - `セーラーに着替えるね`
    - `正常位にするね`
    - `胸カメラにして`
- 向いている用途:
  - Human_2_kks 系の外部AI連携。
  - 音声再生と顔/体位/衣装をひとまとめで動かしたいケース。

### MainGameSubtitleCore

- できること:
  - 本編内に字幕表示レイヤーを出します。
  - 外部リクエストや他プラグインから字幕表示を受けられます。
- 使い方:
  - 単体というより、字幕を出したい他プラグインと組み合わせて使います。

### MainGameSubtitleEventBridge

- できること:
  - イベントや通知音つきで字幕コアへ橋渡しします。
- 使い方:
  - `MainGameSubtitleCore` とセットで使います。
  - 通知音つきのイベント表示を足したい時に向いています。

### MainGameCharacterAfterimage

- できること:
  - キャラだけを毎フレームキャプチャして、残像を背景とキャラの間に挿し込みます。
  - フレーム寿命、残像数、キャプチャ間隔を調整できます。
- 使い方:
  - `MainGameCharacterAfterimageSettings.json` を編集して残像の濃さや寿命を調整します。
- 向いている用途:
  - モーションの勢いを強く見せたい。

### SimpleAfterimage

- できること:
  - 残像演出の軽量版です。
  - プリセット保存、Tint 調整、フェード、速度同期、Glow を持ちます。
- 使い方:
  - `config.json` と `presets.json` を使って見た目を調整します。
- 向いている用途:
  - 残像だけを手早く試したい。

### MainGameGlow

- できること:
  - キャラの発光/グロー演出を追加します。
  - 元カメラを解決し、キャラ専用キャプチャでオーバーレイ描画します。
- 使い方:
  - しきい値、強さ、ブラー量を設定して有効化します。
- 向いている用途:
  - 光り方を強調したMV風の見た目を作りたい。

### MainGameClubLights

- できること:
  - 本編空間へクラブライト演出を追加します。
  - ライトの位置、追従、公転、ビート同期、動画ルーム連携を扱えます。
- 使い方:
  - blank マップ動画ルームやビート同期系と組み合わせると分かりやすいです。
  - UI とギズモで位置調整する運用が前提です。
- 向いている用途:
  - ライブ/MV風演出。

### MainGameDollMode

- できること:
  - 女の子をドール化して、表情や目口の開き、汗、涙、モーション抑制を固定できます。
  - 目オーバーレイ画像も使えます。
- 使い方:
  - `DollMode` を有効化し、目・眉・口・頬・涙・汗・モーション抑制値を設定します。
- 向いている用途:
  - 生っぽい反応を止めて、演出用の固定表情にしたい。

### MainGameFreeHMasturbationMenu

- できること:
  - FreeH のオナニー系メニューや導線を拡張します。
- 使い方:
  - FreeH 側で追加された導線からオナニー系へ入りやすくします。

### MainGameAutoHVoice

- できること:
  - Hボイスを自動/手動で再トリガーします。
  - `Speak Now` 実行や自動発火間隔を UI から調整できます。
- 使い方:
  - UI を開いて `Auto` を有効化し、`Auto Interval` と `Min Spacing` を設定します。
- 向いている用途:
  - ボイスが静かになりやすい場面で、外から補助したい。

### MainGirlHipHijack

- できること:
  - 女の子の腰・体幹・BodyIK を乗っ取って姿勢を強く制御します。
  - HipQuickSetup、VR Grab、男性視点補助、肩補正連携まで入っています。
- 使い方:
  - HipQuickSetup を ON にして、対象ボーンやギズモで姿勢を追い込みます。
  - `MainGameTransformGizmo` と `MainGameUiInputCapture` と一緒に動きます。
- 向いている用途:
  - ポーズを大きく作り込みたい。
  - VR/手動調整で腰位置を触りたい。

### MainGirlShoulderIkStabilizer

- できること:
  - 腕IK時の肩回転を安定化して、肩の暴れを抑えます。
- 使い方:
  - `ShoulderRotationEnabled` や左右ウェイトを調整して使います。
- 向いている用途:
  - 腕のIKを強く使う体位やポーズで肩が破綻する時。

### MainGameAdvIkBridge

- できること:
  - 肩回転補正、FKヒント、呼吸設定などを橋渡しして本編側へ反映します。
- 使い方:
  - 肩補正や呼吸パラメータを設定して、他のIK系プラグインと合わせて使います。

### MainGameLogRelay

- 役割:
  - 各プラグインのログ出力先や出力方式を共通化する土台です。
- 使い方:
  - 単体で遊ぶものではなく、対応プラグインのログ整理のために入れます。

### MainGameUiInputCapture

- 役割:
  - UIドラッグやギズモ操作中に、MainGame の入力競合を防ぐ共通APIです。
- 使い方:
  - 単体で触るものではなく、対応プラグインの操作安定化のために入れます。

### MainGameTransformGizmo

- 役割:
  - 本編内で使う位置/回転ギズモの基盤です。
- 使い方:
  - 単体よりも、HipHijack や ClubLights などの調整UIの土台として使います。

## 依存関係の見方

- 主な基盤:
  - `MainGameLogRelay`
  - `MainGameUiInputCapture`
  - `MainGameTransformGizmo`
- よく連携する組み合わせ:
  - `MainGameBlankMapAdd` + `MainGameBeatSyncSpeed` + `MainGameClubLights`
  - `MainGameCameraControl` + `MainGameVoiceFaceEventBridge`
  - `MainGirlHipHijack` + `MainGirlShoulderIkStabilizer`
  - `MainGameSubtitleCore` + `MainGameSubtitleEventBridge`

## どれから試すか

- blank マップで遊びたい:
  - `MainGameBlankMapAdd`
- 全体位を見たい:
  - `MainGameAllPoseMap`
- H速度を壊したい:
  - `MainGameSpeedLimitBreak`
- 音楽や動画と同期したい:
  - `MainGameBeatSyncSpeed`
- カメラプリセットを作りたい:
  - `MainGameCameraControl`
- 外部AIや自然文で動かしたい:
  - `MainGameVoiceFaceEventBridge`
- 演出を盛りたい:
  - `MainGameCharacterAfterimage`, `SimpleAfterimage`, `MainGameGlow`, `MainGameClubLights`
- 腰や姿勢を手で追い込みたい:
  - `MainGirlHipHijack`
