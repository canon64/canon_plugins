# MainGameBlankMapAdd

MainGame の blank map に動画ルームを構築し、再生バーから動画再生・部屋配置・音量補正・プロファイル保存を行うプラグインです。

## 対応環境
- ゲーム: KoikatsuSunshine
- BepInEx: 5.x
- 必須依存: `MainGameTransformGizmo.dll`
- 任意連携: `MainGameBeatSyncSpeed.dll`（未導入時は BEAT SYNC 欄が `plugin not loaded` 表示）

## 導入
1. `MainGameBlankMapAdd.dll` を `BepInEx/plugins/MainGameBlankMapAdd/` に配置
2. `MainGameTransformGizmo.dll` を `BepInEx/plugins/` 配下に配置
3. ゲーム起動後、MainGame/Hシーンで再生バーを操作

## 基本操作
- `Ctrl+P`: 動画 再生/一時停止
- `Ctrl+R`: 設定再読み込み
- `Ctrl+D`: Gizmo編集モード ON/OFF
- 画面下端にマウスを寄せると再生バーを表示

## 再生バーの見方

### 1) 折りたたみと説明ポップアップ
- `▲ / ▼`: 上段パネル（部屋調整・AUDIO/SAVE・BEAT SYNC）の開閉
- `説明` チェックボックス（`▲` の下）: ON でホバー説明ポップアップ表示、OFF で非表示
- 説明ポップアップは再生バー内の全ボタン/全トグル/全スライダーに対応

### 2) 下段（常時操作）
- `Play / Pause / Stop`: 再生制御
- `|< / >|`: 前後動画へ移動
- `1Loop ON/OFF`: 単曲ループ切替
- `Tiles:n`: 1面あたりの表示タイル数切替
- `フォルダ登録`: 再生対象フォルダを追加
- `Folder:...`: 登録済みフォルダ選択
- `Video:...`: フォルダ内動画選択
- スライダー:
  - `再生位置`: シーク
  - `VOL`: 動画音量
  - `REV`: 残響強度
  - `V-REV`: 動画音への残響適用ON/OFF

### 3) 上段（`▲` 展開時）
- `SIZE / POSITION / ROTATION`: 部屋スケール・座標XYZ・回転XYZ
- 数値入力欄とスライダーは同じ値を操作（どちらも有効）

#### AUDIO / SAVE
- `Gain`: 動画音声の追加ゲイン倍率（0.1〜6.0）
- `RoomF`: 部屋サイズ/座標/回転をフォルダ設定へ保存
- `RoomV`: 部屋サイズ/座標/回転を動画個別設定へ保存
- `GainF`: 動画ゲインをフォルダ設定へ保存
- `GainV`: 動画ゲインを動画個別設定へ保存

#### BEAT SYNC
- `Enabled / AutoMotion / AutoThreshold / VerboseLog` と各種スライダーで調整
- `SaveF`: BeatSync設定をフォルダ設定へ保存
- `SaveV`: BeatSync設定を動画個別設定へ保存

## 保存仕様（重要）
- 保存先ファイル:
  - `MapAddSettings.json`（通常設定）
  - `RoomLayoutProfiles.json`（フォルダ設定/動画個別設定）
- 適用優先順:
  - 同一カテゴリで「動画個別設定」があれば最優先
  - 動画個別設定がなければ「フォルダ設定」を適用
- カテゴリ単位で優先判定:
  - 部屋レイアウト
  - 動画ゲイン
  - BeatSync

## 出力ファイル
- BepInEx設定: `BepInEx/config/com.kks.maingameblankmapadd.cfg`
- ログ: `BepInEx/plugins/MainGameBlankMapAdd/_logs/info.txt`

## トラブルシュート
- 再生バーが出ない:
  - MainGame/Hシーンか確認
  - 画面下端にマウスを移動
  - `EnablePlaybackBar` が OFF になっていないか確認
- BEAT SYNC が使えない:
  - `MainGameBeatSyncSpeed.dll` の導入状態を確認
- ポップアップ説明が出ない:
  - `▲` の下にある `説明` チェックを ON
