# IMPL_PLAN: 画像スライドショー Queueモード追加

## 目的
スライドショーの「Latest」チェックを切替ボタン（Latest / Queue）にする。
Queueモードは「次の1枚を控えに持ち、60秒ごとに最新の控えへ進む。控えが無ければ現在の絵を放置（古い絵に戻さない）」。
新プロンプト時にGUIから即表示させるためのHTTP命令を追加する。

## 必要材料（調査済み）

### 1. 既存のモード制御
- 設定 `ImageSlideshowLatestOnly`（bool, DataMember Order=92, 既定false）。PluginSettings.cs:435
- `UpdateImageSlideshow`（Plugin.ImageSlideshow.cs:270）: `!LatestOnly && 時刻 >= _nextImageSlideshowSlideTime` のとき `ShowNextImageSlideshow()`
  → **Latest=自動送りしない / それ以外=自動送りする**。Queueは「それ以外」に乗る
- `ScanImageSlideshowIfNeeded`（同:344,357）: `LatestOnly` のとき `TryShowLatestImageSlideshow()`、それ以外は控え(pending)ロジック
  → 新画像追加時、既に表示中なら `_imageSlideshowPendingPath` に積む（同:377）。**控え=1枚**

### 2. 自動送り本体
- `ShowNextImageSlideshow`（同:445）: 控え(pending)があればそれを表示、無ければソート順で次へ回す
  → **Queueでの変更点はここだけ**：控えが無ければ「放置」（回さない）にする

### 3. UI
- `DrawImageSlideshowSection` 内の「Latest」トグル（同:1700-1705）: `GUI.Toggle(... "Latest")`
  → 切替ボタンに置換

### 4. HTTPサーバー
- `HandleHttpRequest` の switch（Plugin.HttpServer.cs:253-304）。現状: coords/status/play/next/prev。**slideshow制御命令は無い**
- `CommandQueue.Enqueue(...)` でメインスレッド実行
- 状態応答 `SlideshowStatusResponseView`（同:127-179）に `LatestOnly` あり。`CollectImageSlideshowStatusOnMainThread`（同:413）で構築

### 5. DataMember Order
- 最大98。新フィールドは Order=99

## 実装内容

### プラグイン（C#）
1. PluginSettings.cs:
   - `ImageSlideshowPlayMode`（string, Order=99, 既定"Latest"）追加
   - Normalize(): PlayModeが空なら旧LatestOnlyから移行（true→"Latest", false→"Queue"）。"Latest"/"Queue"以外は"Latest"。最後に `ImageSlideshowLatestOnly = (PlayMode=="Latest")` で同期
2. Plugin.ImageSlideshow.cs:
   - `ShowNextImageSlideshow`: 控えが無い場合、Queueモード（!Latest）なら回さず放置（タイマーだけ再スケジュール）
   - UIの「Latest」トグルを Latest/Queue 切替ボタンに変更。押下でPlayMode切替＋保存
   - 外部命令用に「今すぐ最新へ飛べ」= `TryShowLatestImageSlideshow()` + タイマー再スケジュールを呼ぶ口
3. Plugin.HttpServer.cs:
   - `/slideshow/show-latest`（POST）追加 → メインスレッドで最新表示
   - 状態応答に `PlayMode` 追加（LatestOnlyは互換のため残す）

### GUI（Python: human_2_KKS_pipeline）
4. 状態取得に `play_mode`（"PlayMode"）を追加
5. forever loop: Queueモードの時だけ生成制御（控えが無ければ生成・連射しない＝既存pendingチェックがそのまま効く）
6. 新プロンプト生成完了後に `/slideshow/show-latest` を叩く

## 確認（実機）
- Queueにして連射しないか／60秒で進むか／新プロンが即出るか／間に合わない時に放置されるか
