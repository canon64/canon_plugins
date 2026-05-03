## 今からやる内容（仮）

- フォルダーを複数登録できるようにする（`FolderPlayPaths` リスト化）
- フォルダ設定と動画個別設定を保存できるようにする
- 動画個別設定は加算的に適用されるようにする
- 再生バーUIの重なりを解消し、左にフォルダ/動画情報、右に再生時間/ボリュームを配置する
- 再生スライダーとボリュームスライダーを同一行に配置し、時間/VOL数値を各スライダー左隣に置く
- 残響の強さ（REV）スライダーを同一行へ追加する
- 再生スライダー開始位置を右へ20px寄せ、再生時間ラベルもその左隣へ合わせる
- 再生スライダーをさらに右へ約50px寄せる
- 残響スライダーを左へ70px寄せる
- 残響スライダーが効くように実反映ロジックを修正する
- 動画情報（左カラム）を左端から右へ寄せる
- 動画情報行と下段スライダー行の間隔を確保し、重なりを解消する
- ※作業後にこのセクションを削除する

---

# 動画連動リズムシステム 実装計画

作成日: 2026-03-01
更新日: 2026-03-01

---

## 概要

KKS 本編 HScene で動画を再生しながら、音楽のビートに合わせて
リズムバー・ボイス・表情・IK・感度ゲージを連動させるシステム。

---

## 全体構成

```
[Windows エディタ (PyQt6)]  F:/kks/work/gui/KoreoEditor/
  a.mp4 を読み込んで波形表示
  BPM + 区間 + ヒットバーを配置
        ↓ 保存
  a.events.json

[KKS ゲーム内]
  MainGameBlankMapAdd
    a.mp4 → VideoPlayer → 映像+音声再生

  MainGameKoreoSync（新規）
    a.events.json を読む
    VideoPlayer.clockTime を唯一の時計として使う
        ↓ 毎フレーム
    currentBeat = clockTime * bpm / 60 （+ beatOffsetSec）
    Unity Canvas にリズムバーをスクロール描画
        ↓ プレイヤー入力
    clockTime で判定（Perfect / Miss）
        ↓
    ボイス再生 / 表情変更 / 感度ゲージ増減 / IK ポーズ
```

---

## 同期の仕組み（確定）

**VideoPlayer.clockTime が唯一の時計。**

- フレームレートが落ちても clockTime はオーディオクロック駆動で正確に進む
- バー描画・入力判定どちらも clockTime を参照する
- 入力タイミング判定: `diff = videoPlayer.clockTime - event.centerTimeSec`
- 一時停止: clockTime が止まる → バーも止まる → 判定も止まる
  - ゲーム側追加処理は `if (videoPlayer.isPaused) return;` の1行のみ

---

## ビートオフセット

動画の先頭（0秒）が必ずしも拍の頭とは限らない。
`a.events.json` に `beatOffsetSec` を持たせ、
ゲーム側はサンプル計算時に加算する。

```csharp
double currentBeat = (videoPlayer.clockTime - beatOffsetSec) * bpm / 60.0;
```

エディター側もグリッド描画時に同じオフセットを使う。

---

## a.events.json フォーマット

```json
{
  "bpm": 128,
  "sampleRate": 44100,
  "beatOffsetSec": 0.0,
  "sections": [
    { "name": "A", "startBeat": 0,  "endBeat": 16, "bpm": 0 },
    { "name": "B", "startBeat": 16, "endBeat": 32, "bpm": 0 }
  ],
  "events": [
    {
      "startBeat": 4.0,
      "endBeat": 4.5,
      "section": "A",
      "action": "ik_pose_1",
      "voiceSuccess": "v_appeal_01",
      "voiceFail": "v_pain_01",
      "gaugeAmount": 10.0,
      "faceSuccess": "face_pleasure",
      "faceFail": "face_surprise"
    }
  ]
}
```

---

## 実装ステップ

### Step 1: MainGameBlankMapAdd 動画再生 ✅ 完了

- 動画面生成（Quad / 球体）
- 音声付き VideoPlayer
- リバーブゾーン（女性キャラ追従）
- ギズモ UI（位置・回転編集）

---

### Step 2: KoreoEditor（エディタ）← 進行中

**場所**: `F:/kks/work/gui/KoreoEditor/`
**詳細**: `CODEBASE_STATE.md` 参照

#### 実装済み
- 動画/音声読み込み・波形表示
- BPM グリッド（区間ごと個別 BPM 可）
- ヒットバー配置（左ドラッグ）・消去（右ドラッグ）・範囲選択
- 区間設定テーブル（動的追加・ドラッグ移動）
- OverviewWidget（ミニマップ）
- QMediaPlayer 再生・シーク
- a.events.json 出力
- プラグインシステム（入口のみ）

#### 残り作業
- ビートオフセット フィールド追加 → JSON に `beatOffsetSec` 出力
- イベントプロパティ設定 UI（action / voice / gauge / face）

---

### Step 3: MainGameKoreoSync プラグイン（新規）

**場所**: `F:/kks/work/plugins/MainGameKoreoSync/`

#### 起動時
- `a.events.json` を読み込む
- BPM・beatOffsetSec・セクション・イベントリストを保持
- Unity Canvas を生成してリズムバー描画の準備

#### 毎フレーム Update
```csharp
if (videoPlayer.isPaused) return;

double currentBeat = (videoPlayer.clockTime - beatOffsetSec) * bpm / 60.0;

foreach (var ev in events) {
    // バーの画面上 X 位置を currentBeat から計算してスクロール描画
    // ヒットウィンドウ内なら入力受付
}
```

#### 入力判定
```csharp
float diff = (float)(videoPlayer.clockTime - ev.centerTimeSec);
// |diff| < 0.05s → Perfect
// |diff| < 0.12s → Good
// それ以外      → Miss
```

#### 判定後処理

| 結果 | 処理 |
|------|------|
| Perfect / Good | ゲージ +gaugeAmount、成功ボイス、IK ポーズ、成功表情 |
| Miss | 失敗ボイス、失敗表情 |

#### 区間連動
- `currentBeat` が各 Section の範囲に入ったら OnSectionChange
- 区間ごとに IK 動作速度・表情ベースラインを切り替え

---

### Step 4: 既存プラグイン連携

| 機能 | 連携先 |
|------|--------|
| IK 位置・ポーズ | `MainGirlHipsIkHijack` |
| 感度ゲージ | 本編ゲージ API |
| ボイス再生 | `Manager.Voice` |
| 表情変更 | `FaceControl` / `FaceBlendShape` |

---

## ファイル配置（完成時）

```
F:/kks/BepInEx/plugins/
  MainGameBlankMapAdd/
    MainGameBlankMapAdd.dll
    MapAddSettings.json
    musicmp4/
      a.mp4
      a.events.json       ← KoreoEditor で作成
  MainGameKoreoSync/
    MainGameKoreoSync.dll
    KoreoSyncSettings.json

F:/kks/work/
  plugins/
    MainGameBlankMapAdd/  ← ソース（Step 1 完了）
    MainGameKoreoSync/    ← ソース（Step 3 で作成）
  gui/
    KoreoEditor/          ← Python アプリ（Step 2 進行中）
```

---

## 現状

- Step 1: ✅ 完了
- Step 2: 🔧 進行中（基本機能実装済み、プロパティ設定 UI が残り）
- Step 3: ⬜ 未着手
- Step 4: ⬜ 未着手

---

## 統合プリセット保存 + マイリスト機能（構想）

### 概要

動画マップに「プリセット保存/呼び出し」と「マイリスト再生」を追加する。
プリセット1件 = 動画1曲分の全設定。マイリスト = プリセットの順序付きリスト。

---

### プリセット JSON 構造

```json
{
  "presetName": "track01",
  "videoMap": {
    "videoPath": "F:/videos/foo.mp4",
    "roomShape": "Sphere",
    "panelsPerSide": 1,
    "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale":    { "x": 1.0, "y": 1.0, "z": 1.0 }
  },
  "speedLimitBreak": {
    "enabled": true,
    "sourceMinSpeed": 1.0,
    "targetMinSpeed": 0.8115796,
    "sourceMaxSpeed": 1.0,
    "targetMaxSpeed": 2.0,
    "useTimeline": false,
    "timelinePath": ""
  },
  "beatSync": {
    "enabled": true,
    "bpm": 120,
    "lowThreshold": 0.3,
    "highThreshold": 0.7,
    "lowIntensity": 0.25,
    "midIntensity": 0.55,
    "highIntensity": 1.0,
    "smoothTime": 0.5,
    "autoMotionSwitch": true,
    "strongMotionBeats": 4.0,
    "weakMotionBeats": 4.0
  }
}
```

保存先: `BepInEx/plugins/MainGameBlankMapAdd/presets/{presetName}.json`

---

### マイリスト JSON 構造

```json
{
  "listName": "mylist01",
  "tracks": [
    "track01",
    "track02",
    "track03"
  ],
  "loopAll": false
}
```

保存先: `BepInEx/plugins/MainGameBlankMapAdd/playlists/{listName}.json`

マイリスト再生時の動作:
1. tracks[0] のプリセットを読み込んで動画再生開始
2. 動画終端（VideoPlayer.isPlaying が false になった時点）を検知
3. tracks[1] へ自動切り替え（プリセット全適用 → 動画再生）
4. 全曲終了後: loopAll=true なら先頭に戻る、false なら停止

---

### プラグイン構成方針（確定）

**統合プラグイン `MainGameVideoSuite`（新規）がオーケストレーターになる。**
既存3プラグインは触らない（APIを公開するだけ）。

```
MainGameBlankMapAdd     ← 変更なし
MainGameSpeedLimitBreak ← GetSettings/ApplySettings のみ追加
MainGameBeatSyncSpeed   ← GetSettings/ApplySettings のみ追加

MainGameVideoSuite（新規）
  ├── プリセット保存/呼び出し
  ├── マイリスト管理・再生
  ├── 動画終端検知 → 次トラック自動切り替え
  └── GUI（プリセット・マイリスト UI）
```

メリット:
- 既存プラグインへの影響が最小限
- VideoSuite だけ抜けば元の状態に戻る
- 責務がはっきり分かれる

各プラグインに追加が必要なもの:
- `SpeedLimitBreakApi.GetSettings() / ApplySettings()`
- `BeatSyncApi.GetSettings() / ApplySettings()`

---

### UI 配置（案）

MainGameVideoSuite の独立ウィンドウ:
- プリセット名入力 + 「保存」ボタン
- プリセット一覧（クリックで呼び出し、右クリックで削除）
- マイリスト選択 + 「再生」ボタン
- マイリスト編集（プリセットを並べ替え）

---

### 実装ステップ（未着手）

1. SpeedLimitBreak に GetSettings/ApplySettings API 追加
2. BeatSync に GetSettings/ApplySettings API 追加
3. MainGameVideoSuite 新規プロジェクト作成
4. プリセット保存/呼び出しロジック実装
5. マイリスト管理ロジック実装
6. 動画終端検知 → 次トラック自動切り替え
7. GUI 実装（プリセット・マイリスト UI）
