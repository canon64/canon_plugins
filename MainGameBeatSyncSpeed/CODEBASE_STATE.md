# MainGameBeatSyncSpeed — CODEBASE_STATE

## 概要
動画マップMod（MainGameBlankMapAdd）で再生中の動画から音声を取得し、
拍ごとの低音エネルギーに合わせて本編Hの速度と強弱モーションを同期するプラグイン。

現行実装は単純な「速度だけのビート同期」ではなく、以下を一体で扱う。
- 動画URL取得と再生時刻ポーリング
- 動画からのWAV自動抽出（`ffmpeg.exe` 利用、手動WAV指定も可）
- BPM単位の拍エネルギー解析
- Low / Mid / High の3段階速度化
- `motionchange` による強弱モーション自動切替
- H内部速度ゲージ/UIの強制反映

## バージョン
- 0.1.0

## ファイル構成
| ファイル | 役割 |
|----------|------|
| Plugin.cs | エントリポイント、Config.Bind、HScene検出、解析トリガ、現在拍強度計算、強弱モーション切替 |
| Plugin.AudioAnalysis.cs | WAV PCM読み込み（16/24/32bit / float）、IIRローパス、拍ごとRMS、3拍移動平均、正規化、ffmpeg抽出 |
| Plugin.VideoRoomBridge.cs | `MainGameBlankMapAdd.Plugin.TryGetMainVideoPlaybackSnapshot` をリフレクション経由で呼び、動画URLも取得 |
| Plugin.TapTempo.cs | `RightCtrl` 連打からBPMを推定し、Configと連携先プラグインへ反映 |
| Patches.cs | `HFlag.WaitSpeedProc*` 乗っ取り、`HSceneProc.Update` / `HSprite.Update` で速度ゲージとUIを強制反映 |

## 動作フロー
1. HSceneProc を検出 → `_insideHScene = true`
2. `Audio.WavFilePath` が設定されていればそれを使い、空なら動画URLを取得して `ffmpeg.exe` でWAV抽出
3. WAVを解析し、BPMごとの拍エネルギー配列 `_beatIntensities[]` と拍長 `_beatDurationSec` を生成
4. 20fpsで動画再生時刻を取得 (`_videoRoomTimeSec`)
5. `再生時刻 / 拍長` から現在拍を引き、正規化エネルギーを `Low / Mid / High` の3段階速度へ変換
6. `SmoothTime` に応じて補間した値を `CurrentIntensity01` に反映
7. Harmony patch が `HFlag.WaitSpeedProc(bool, AnimationCurve)` / `WaitSpeedProcAibu()` を止めて、速度値を `CurrentIntensity01` で上書き
8. `HSceneProc.Update` / `HSprite.Update` postfix でゲージ値とUI表示も追従させる
9. `MotionSwitch.AutoMotionSwitch=true` の場合、High/Low が所定拍数続いた時に `flags.click = motionchange` を1発送る

## Config（F1 ConfigurationManager）
| Section | Key | Default | 説明 |
|---------|-----|---------|------|
| General | Enabled | true | ビートシンク ON/OFF |
| General | ToggleKey | F9 | トグルキー |
| Audio | WavFilePath | "" | 解析するWAVのフルパス。空なら動画から自動抽出 |
| Audio | Bpm | 128 | 曲のBPM |
| Audio | LowPassHz | 150 | IIRローパス周波数(Hz) |
| Speed | AutoThreshold | true | 全拍の33/67パーセンタイルからLow/Mid/High閾値を自動算出 |
| Speed | LowThreshold | 0.3 | `AutoThreshold=false` 時のLow/Mid境界 |
| Speed | HighThreshold | 0.7 | `AutoThreshold=false` 時のMid/High境界 |
| Speed | LowSpeed | 0.25 | 低エネルギー拍の速度値 |
| Speed | MidSpeed | 0.5 | 中エネルギー拍の速度値 |
| Speed | HighSpeed | 1.0 | 高エネルギー拍の速度値 |
| Speed | SmoothTime | 0.5 | 速度変化補間秒数。0で瞬間切替 |
| MotionSwitch | AutoMotionSwitch | true | 強弱モーション自動切替の有効/無効 |
| MotionSwitch | StrongMotionBeats | 4.0 | Highが何拍続いたら強モーションへ切り替えるか |
| MotionSwitch | WeakMotionBeats | 4.0 | Lowが何拍続いたら弱モーションへ切り替えるか |
| Debug | VerboseLog | false | 0.5秒ごとにbeat/intensity/timeをログ出力 |

補足:
- `RightCtrl` のタップテンポ入力で `Audio.Bpm` を更新できる。
- `Audio.WavFilePath` / `Audio.Bpm` / `Audio.LowPassHz` 変更時は解析結果を破棄して再解析する。

## 依存
- MainGameBlankMapAdd（動画時刻取得）: リフレクション経由、なくても起動可
- `ffmpeg.exe`（動画からWAV自動抽出する場合のみ）: PATHに存在すること
- MainGameSpeedLimitBreak: `Plugin.TapTempo.cs` から `ApplyTapBpm` を呼ぶ連携コードあり（なくても起動可）

## 実装修正メモ
- manual の推奨である「`HActionBase.SetAnimatorFloat` のみを触る」設計ではなく、現行実装は `HFlag.WaitSpeedProc*` とゲージ/UI更新まで上書きしている。
- `motionchange` は強/弱の直接指定ではなくトグル命令なので、現行の強弱切替は「現在Animator stateを見て、必要時だけ1発送る」実装になっている。

## 既知リスク
- `motionchange` はトグル命令のため、現在状態判定が外れると意図と逆方向に切り替わる可能性がある。
- `MainGameSpeedLimitBreak` など、H速度系を書き換える別プラグインとは競合しやすい。
- `ffmpeg.exe` が見つからない場合、`Audio.WavFilePath` 未設定では解析が始まらない。
- `RightCtrl` タップテンポはConfig項目化されていない隠し入力で、キー競合の余地がある。

## ビルド & デプロイ
```bash
cd F:/kks/work/plugins/MainGameBeatSyncSpeed
dotnet build -c Release
cp bin/Release/net472/MainGameBeatSyncSpeed.dll "F:/kks/BepInEx/plugins/MainGameBeatSyncSpeed/"
```

## ログ
- `F:/kks/BepInEx/plugins/MainGameBeatSyncSpeed/MainGameBeatSyncSpeed.log`
- VerboseLog=true で beat/intensity/time が 0.5秒ごとに出力される
- 動画ブリッジ接続/切断、WAV抽出、解析完了、強弱モーション切替もここに出力される
