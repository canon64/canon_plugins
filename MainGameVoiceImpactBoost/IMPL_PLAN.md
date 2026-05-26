# MainGameVoiceImpactBoost — 実装計画 (v0.2 設計)

## 目的
本編H中、**PregnancyPlusBridge の腹ボコピーク** をトリガーにして、
再生中の SBV2 外部ボイス (VoiceFaceEventBridge 経由) の音量を
一瞬カーブで持ち上げる。

## v0.1 からの変更
- 旧: 男女股間距離をポーリングして edge 検出で発火
- 新: `MainGamePregnancyPlusBridge.Plugin.OnBellyBokoPeak` イベント購読
- 理由:
  - 腹ボコピーク = PregnancyPlusBridge が belly モーフ計算の中で「距離極小→反転」を既に判定済み
  - エッジ検出を二重実装する必要がなくなる
  - PregnancyPlus 側の精度に追従できる

## 仕様

### トリガー
- `MainGamePregnancyPlusBridge.Plugin.OnBellyBokoPeak += Handler` を Awake で購読
- ハンドラ引数: `float normalized` (0..1) — ピーク時の腹ボコ強度
- OnDestroy で必ず解除

### Cooldown / Mode
- Cooldown (ms) — 連続発火抑制
- TriggerMode:
  - `Always`: 毎ピーク発火
  - `BeatSyncGated`: BeatSync 強度 >= 閾値時のみ発火
  - `OneShot`: 1H シーン中 1 回のみ
- HScene 検知は PregnancyPlusBridge 側にお任せ (イベント自体が H 中のみ発火する想定)
- OneShot のリセットは別途必要: イベント購読中はリセット契機がないので
  「N秒間ピーク無し」をリセットとする、または HSceneProc 監視

### Envelope
- `peakMultiplier`, `attackMs`, `holdMs`, `releaseMs`
- `MainGameVoiceFaceEventBridge.PublicApi.TryRequestTransientVolumeBoost(...)` に渡す
- 既存実装そのまま

### 強度連動 (将来拡張)
- v0.2 では `normalized` を無視、固定 peakMultiplier
- v0.3 で `peakMultiplier = lerp(1.0, configMax, normalized)` 検討

### 設定 (BepInEx cfg、既存とほぼ同じ)
- `Trigger`
  - `Mode` Always | BeatSyncGated | OneShot (デフォ Always)
  - `BeatSyncMinIntensity` 0.7
  - `CooldownMs` 0
  - `MinIntensity` 0.5 (腹ボコ強度がこの値未満なら発火しない)
- `Envelope`
  - `PeakMultiplier` 2.0
  - `AttackMs` 20
  - `HoldMs` 30
  - `ReleaseMs` 200
- `Diagnostic`
  - `VerboseLog` false

### HSceneProc 監視 (OneShot リセット用)
- HSceneProc が null → not null になった時点で `_oneShotFiredThisScene = false`
- Update で 1 秒に 1 回 FindObjectOfType<HSceneProc>() 程度の頻度で十分

## 依存
- `MainGamePregnancyPlusBridge` (HardDependency相当、OnBellyBokoPeak event)
- `MainGameVoiceFaceEventBridge` (HardDependency相当、PublicApi.TryRequestTransientVolumeBoost)
- `MainGameBeatSyncSpeed` (Optional、リフレクション)

## 必要材料 (済)
| # | 項目 | 場所 |
|---|------|------|
| 1 | OnBellyBokoPeak event | `MainGamePregnancyPlusBridge\Plugin.PublicApi.cs:18` |
| 2 | TryRequestTransientVolumeBoost API | `MainGameVoiceFaceEventBridge\PublicApi.cs:20` |
| 3 | BeatSync 強度リフレクション | 既存実装に同じ |
| 4 | HSceneProc 検出 | `UnityEngine.Object.FindObjectOfType<HSceneProc>()` |

## ビルド
- net472, KKSDir=F:\kks
- 既存 csproj 流用
- 出力: `bin/Release/net472/MainGameVoiceImpactBoost.dll`
- 配置: `F:/kks/BepInEx/plugins/canon_plugins/MainGameVoiceImpactBoost/`
