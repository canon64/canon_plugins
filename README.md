# canon_plugins

動画を見ながら、その動画の音楽リズムに合わせて女性キャラの腰振りを同期させるための
KKS 向けプラグイン群です。

## ダウンロード（最新版）
- GitHub Releases（推奨）:
  - https://github.com/canon64/canon_plugins/releases/latest
- 直接DL（BepInEx一式zip）:
  - https://github.com/canon64/canon_plugins/releases/latest/download/canon_plugins_BepInEx_dropin.zip

## このプラグイン群のメイン用途
- 動画を再生する
- 音楽の BPM に合わせて `右Ctrl` をタップする
- その BPM と波形解析結果を使って、腰振り速度を音楽に追従させる

## 基本的な使い方（最重要）
1. 動画を再生する
2. 曲のテンポに合わせて `右Ctrl` を 5〜10 回タップする
3. タップがズレたと思ったら、もう一度タップして取り直す
4. BPM は曲ごとに保存され、次回以降は自動で再利用される

## 速度反映の仕組み（1/4・1/2・1拍）
- 曲の波形は最初に解析される
- エネルギー量に応じて `1/4`、`1/2`、`1` の拍として扱う
- 反映ルール
  - `1` 拍: そのままの BPM
  - `1/2` 拍: その半分の BPM
  - `1/4` 拍: さらにその半分の BPM（= 1 の 1/4）
- 低・中・高のエネルギー帯で、アニメーション速度を切り替える

## 動画ごとの調整（Threshold）
- 再生スライダーの `▲` を押して詳細パネルを開く
- `Threshold` の `Low` / `High` を調整する
- 調整値は動画ごとに保存できる
- 曲ごとに最適な閾値が違う場合は、動画単位で保存して使い分ける

## フォルダーセーブ / 動画個別セーブ
- `▲` で開く詳細パネルの各セクションに `SaveF` / `SaveV` がある
- `SaveF`（Folder Save）
  - 今選んでいるフォルダー全体の既定値として保存する
  - 同じフォルダー内の動画に共通で使いたい設定向け
- `SaveV`（Video Save）
  - 今再生中の動画ファイル専用設定として保存する
  - その動画だけ挙動を変えたいときに使う
- 読み込み時の優先順
  - 1. 動画個別設定（`SaveV`）
  - 2. フォルダー設定（`SaveF`）
  - 3. どちらも無い場合は通常設定
- 使い分けの目安
  - まず `SaveF` でフォルダー共通の土台を作る
  - 曲ごとにズレる動画だけ `SaveV` で上書きする

## 含まれるプラグイン
- MainGameBlankMapAdd
- MainGameAllPoseMap
- MainGameSpeedLimitBreak
- MainGameBeatSyncSpeed
- MainGameTransformGizmo（MainGameBlankMapAdd の依存）

## ffmpeg 同梱について
- この配布物は `ffmpeg.exe` を同梱します（BeatSync の動画音声解析で使用）。
- 同梱先: `BepInEx/plugins/canon_plugins/_tools/ffmpeg/bin/ffmpeg.exe`
- `MainGameBeatSyncSpeed` は同梱版を優先で探索し、見つからない場合のみ `PATH` を参照します。

## サードパーティライセンス（ffmpeg）
- 同梱 `ffmpeg.exe` は GPLv3 ビルドです。
- ライセンス通知と入手元情報は `BepInEx/plugins/canon_plugins/_tools/ffmpeg/NOTICE.txt` を参照してください。
