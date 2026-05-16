# CODEBASE_STATE: MainGameSpeedLimitBreak

- Updated: 2026-03-02
- Project: `F:/kks/work/plugins/MainGameSpeedLimitBreak`

## 目的

- 速さゲージの上限表示（従来どおり）を保ったまま、
  実アニメ速度のみ上限突破させる。

## ファイル構成

- `MainGameSpeedLimitBreak.csproj`
  - `net472`
  - `BepInEx`, `0Harmony`, `Assembly-CSharp`, `UnityEngine.*` 参照
- `Plugin.cs`
  - BepInExエントリ
  - 専用ログ出力 (`MainGameSpeedLimitBreak.log`)
  - JSONロード、`Ctrl+R` 再読込
  - HScene内判定キャッシュ
- `Patches.cs`
  - `HActionBase.SetAnimatorFloat(string,float,bool,bool)` Prefix
- `PluginSettings.cs`
  - 全パラメータJSON化
- `SettingsStore.cs`
  - `SpeedLimitBreakSettings.json` 読み書き

## 設定JSON

- `Enabled`: 全体ON/OFF
- `AffectsSpeed`: `speed` への適用
- `AffectsSpeedBody`: `speedBody` への適用
- `ApplyOnlyInsideHScene`: Hシーン内のみ適用
- `IgnoreValuesBelowSourceMin`: 下限未満を素通し
- `SourceMinSpeed`, `SourceMaxSpeed`: 変換元レンジ
- `TargetMinSpeed`, `TargetMaxSpeed`: 変換先レンジ
- `VerboseLog`, `LogIntervalSec`: ログ制御

## デプロイ先

- `F:/kks/BepInEx/plugins/MainGameSpeedLimitBreak/MainGameSpeedLimitBreak.dll`
- `F:/kks/BepInEx/plugins/MainGameSpeedLimitBreak/SpeedLimitBreakSettings.json`

## 既知制約

- ゲージ表示は `HFlag` 系を触っていないため従来ロジックのまま。
- 高速域のモーション破綻は体位依存で発生し得る。

## 2026-03-02 追加（動画時刻cueでプリセット呼び出し）

- `PluginSettings.cs`
  - 追加設定:
    - `EnableVideoTimeSpeedCues`
    - `VideoTimeCuesResetOnLoop`
    - `VideoTimeSpeedCues[]`
      - `TimeSec`
      - `PresetName`
      - `Enabled`
      - `TriggerOnce`
- `Plugin.VideoCues.cs`（新規）
  - 動画時刻（`MainGameBlankMapAdd` ブリッジ）を監視し、cue到達でプリセットを自動適用。
  - 同フレームで複数cue到達時は最も遅い時刻のcueを優先適用。
  - 動画巻き戻り検出時のリセット挙動を設定で切替。
- `Plugin.Preset.cs`
  - `ApplyPreset` に内部オーバーロードを追加し、
    cue経由適用時は設定ファイル保存をスキップ可能にした（無駄なディスク書込を回避）。

## 2026-03-02 追加（正常位オート完全乗っ取り）

- `Patches.cs`
  - `HFlag.WaitSpeedProc(bool, AnimationCurve)` Prefix を追加。
  - 条件一致時は元メソッドをスキップし、プラグイン側で `speed/speedCalc` を確定。
- `Plugin.cs`
  - `TryHijackWaitSpeedProc(...)` を追加。
  - 対象モード（通常`sonyu`、必要時`sonyu3P`/`sonyu3PMMF`）かつオート時に、
    `speedCalc` と `speed` を上書きして `WaitSpeedProc` の更新を置換。
  - `voice.speedMotion` の閾値更新も同時に再現。
- `PluginSettings.cs`
  - 追加設定:
    - `EnableAutoSonyuHijack`
    - `AutoSonyuHijackRequireAutoLock`
    - `AutoSonyuHijackAlsoSonyu3P`
    - `AutoSonyuHijackAlsoSonyu3PMMF`
    - `AutoSonyuHijackUseSourceMax`
    - `AutoSonyuHijackFixedSourceSpeed`

## 次の修正方針（基準値/反映値の分離）

- 状態
  - 現在は「基準BPM（換算係数）」と「反映BPM（適用値）」がUI/保存名/保存データで一部混在している。
  - `最小へ適用` が基準値更新経路に入っており、名称と挙動が一致していない。

- 目標モデル（4値分離）
  - `BaseMinBpm`: 速度1基準（換算）
  - `BaseMaxBpm`: 速度3基準（換算）
  - `AppliedMinBpm`: 反映用下限BPM（UI・保存用）
  - `AppliedMaxBpm`: 反映用上限BPM（UI・保存用）

- UI方針
  - 「基準値」ブロックと「反映値」ブロックを明確に分離する。
  - `最大へ適用` 実行時は `AppliedMaxBpm` を更新し、同時に `AppliedMinBpm = AppliedMaxBpm / 4` を自動セットする。
  - `最小へ適用` は `AppliedMinBpm` のみ更新し、基準値は変更しない。

- 保存方針
  - プリセットは「アニメ別フォルダ」でグルーピング保存する。
  - 1件の保存データに以下を保持する:
    - `AnimationName`
    - `Folder`
    - `BaseMinBpm` / `BaseMaxBpm`
    - `AppliedMinBpm` / `AppliedMaxBpm`
  - 保存名は必ず `体位名 AppliedMaxBpm-AppliedMinBpm`（例: `正常位 140-35`）を使う。
  - 保存名のBPMは基準値ではなく反映値を使う。

- 呼び出し方針
  - 呼び出し時は「基準値復元 → 反映値復元 → Target速度反映」の順で適用する。
  - これにより、換算係数と実適用値の責務を明確化する。

## 2026-03-02 追加（動画cueアクション拡張）

- `Plugin.VideoCueActions.cs`（新規）
  - cue発火時に以下を順次適用:
    - FaceDB: `hface_dict.db` から検索して `FaceListCtrl.SetFace(...)`
    - 体位: `lstUseAnimInfo` から `TaiiId/TaiiName/TaiiMode` 検索し `flags.selectAnimationListInfo` へ投入
    - コーデ: `ChaControl.ChangeCoordinateTypeAndReload(...)`
    - 衣装部位着脱: `ChaControl.SetClothesState(kind, state, next:true)`
    - クリック: `flags.click = (HFlag.ClickKind)`（`motionchange` で強弱トグル可能）
- `Plugin.VideoCues.cs`
  - `PresetName` 空cueでもアクションのみ実行可能に変更。
  - 1 cue実行ログに `actionsApplied` を追加。
- `VideoCueStore.cs`
  - sanitize時に新規アクションキーを保持。
  - `PresetName` なしでも、アクションキーがあれば有効cueとして保存。
- `MainGameSpeedLimitBreak.csproj`
  - 参照追加:
    - `Mono.Data.Sqlite.dll`
    - `Sirenix.Serialization.dll`
- サンプル:
  - `SpeedTimeline.sample.actions.2min.json` を追加（2分、表情/体位/コーデ/着脱/強弱の例）。

## 2026-03-04 追加（Timeline OFF時hijack完全停止 + BPM個別Disable）

- 背景
  - ConfigManagerで `SpeedTimeline` を `Disabled` にしても、`EnableAutoSonyuHijack` が有効なまま
    `WaitSpeedProc` 経路の `speed/speedCalc` が固定されるケースがあった。

- 仕様変更
  - `EnableVideoTimeSpeedCues == false` の間は `TryHijackWaitSpeedProc` を常に無効化。
    - これにより `SpeedTimeline Disabled` で速度ゲージhijackを完全停止。
  - BPM反映（`HActionBase.SetAnimatorFloat` の remap）は新フラグ `EnableBpmSpeedRemap` で独立制御。

- 追加設定
  - `PluginSettings.cs`
    - `EnableBpmSpeedRemap`（default: true）
  - ConfigManager (`Plugin.UI.cs`)
    - `Behavior / 00 Enable BPM Remap`

## 2026-03-05 追記（GaugeTransitionSec / GaugeEasing）

- 実装状況
  - `GaugeTransitionSec` による「数秒かけて目標ゲージへ到達」は実装済み。
  - 対象は timeline cue の `GaugePos01` / `GaugeSpeed13`。
  - `GaugeTransitionSec <= 0` は即時反映。

- イージング
  - `GaugeEasing` で指定可能:
    - `linear`
    - `easeInQuad`
    - `easeOutQuad`
    - `easeInOutQuad`
    - `easeInSine`
    - `easeOutSine`
    - `easeInOutSine`
    - `smoothStep`
  - 不明な値は `linear` にフォールバック。

- 実行条件（重要）
  - 次の条件を満たすときだけ補間が進行する:
    - `EnableVideoTimeSpeedCues == true`
    - timeline が空でない
    - 動画ブリッジが利用可能かつ準備完了
    - 動画が再生中
  - `SpeedTimeline Disabled` 時、または動画非再生時は補間は進行しない（hijack停止仕様）。

- 更新方式
  - 補間値は `UpdateVideoTimeSpeedCues()` 内の `UpdateTimelineGaugeTransition()` で毎フレーム更新し、
    `_timelineGaugeOverride01` に適用される。

## 2026-04-04 メモ（オナニーBPM測定）

- 観測メモ（ユーザー測定値）
  - オナニーモーション 1段階目:
    - 最小 `15.72`
    - 最大 `99.03`
  - オナニーのイキモーション:
    - 最小 `39.82`
    - 最大 `247.42`

- 注意点
  - 上記値は、`MainGameSpeedLimitBreak` のリマップ適用後の値である可能性が高い。
  - その場合、バニラ基準値ではないため、単純に基準BPMへ反映しても拍が一致しないケースがある。
  - バニラ切り分け時は `ForceVanillaSpeed=ON`（必要なら `Enable BPM Remap=OFF` 併用）で再測定する。

## 2026-04-04 追記（ユーザー再確定値）

- 方針
  - 以降のオナニー基準値は、ユーザー手動測定値を正として扱う。
  - 第一段階の最小値は「既に取得済みの最小値」を採用する。
  - 直近ログは「第一段階を最大値で回している確認ログ」として扱う。

- オナニー基準BPM（再確定）
  - 第一段階:
    - 最小 `15.72`
    - 最大 `99.03`
  - 第二段階:
    - 最小 `23.69`
    - 最大 `148.38`
  - イキモーション:
    - 最小 `39.82`
    - 最大 `272.66`（`247.42` ではなく手動測定値を採用）

- 直近ログ解釈メモ
  - `mode=masturbation` の `speedCalc=1` 連続は「第一段階の最大運転中」。
  - 同区間ログだけでは第一段階の最小値確定は行わない。

- 今回作業ログで確認できたこと（リアルタイム追記）
  - 現在の実行ログでも、第一段階を最大で回している区間を確認。
  - 根拠（`HSceneProc.Update.Postfix mode=masturbation speedCalc=1 speed=1` 連続）:
    - `07:05:42.701`
    - `07:05:43.204`
    - `07:05:43.706`
    - `07:05:44.223`
    - `07:05:44.718`
    - `07:05:45.234`
    - `07:05:45.753`
    - `07:05:46.259`
  - 判定:
    - この区間は「第一段階の最大値確認ログ」として扱う。

- モーション名ログの解釈メモ（2026-04-04）
  - `nowInfo.name=床オナニー` は体位/選択名（上位名）。
  - `clip(name=M_WLoop, len=1.333, w=0.374)` の `M_WLoop` が実再生クリップ名（下位モーション名）。
  - `len=1.333` はクリップ基準長（speed=1時の1周秒数）。
  - `w=0.374` はブレンド重み（1.0単独再生、1未満は他クリップ混在）。
  - 段階判定は `clip名` だけでなく `state hash` と `speedCalc` も併用する。

- 今回ログで追加確定した事実（2026-04-04）
  - 対象区間は `nowInfo(id=6, mode=masturbation, name=床オナニー)` / `clip=M_WLoop` のまま推移。
  - 同一クリップのまま `speedCalc` が `1 -> 0.3 -> 0` に低下する遷移を確認。
    - `07:09:00.766` 付近: `speedCalc=1`
    - `07:09:44.068` 付近: `speedCalc=0.3`
    - `07:09:44.585` 以降: `speedCalc=0`
  - `speedCalc=0` でも `loop-changed` は継続し、実再生は停止していない。
  - `mast-trace` では同区間で `animSpeed=0.35` を確認。
  - 解釈:
    - この系統では `speedCalc`（ゲージ）と `animSpeed`（実再生速度）は一致しない。
    - BPM基準の実効判定は `speedCalc` 単独では不十分で、`animSpeed` と `loop/norm` 進行を同時参照する必要がある。

- 今回ログで追加確定した事実（第二段階 最小値 / 2026-04-04）
  - 第二段階クリップへの遷移を確認:
    - `07:27:54.686` `transition ... clip=M_MLoop1 ... animSpeed=2.2 flags(... speedCalc=1 ...)`
  - 第二段階の減速遷移を確認:
    - `07:27:56.456` `HSceneProc.Update.Postfix ... speedCalc=0.75 speed=0.75`
    - `07:27:56.973` `HSceneProc.Update.Postfix ... speedCalc=0 speed=0`
  - 第二段階の最小運転として観測できた値:
    - `clip=M_MLoop1`
    - `speedCalc=0`
    - `animSpeed=0.35`
    - 代表ログ: `07:27:56.856`（snapshot）, `07:27:56.973`（patch）
  - 最小運転中の挙動:
    - `loop-changed` は継続（例: `07:27:57.570`, `07:28:00.104`, `07:29:07.021`）
    - クリップ重み `w` は変動するが、`animSpeed=0.35` は維持される
  - 追加判定メモ:
    - 今回ログには `speedCalc=0.3` ブロックは存在しない（該当なし）
    - したがって、この実行分の「第二段階最小値」は `speedCalc=0 / animSpeed=0.35` として扱う。

- 今回ログで追加確定した事実（第二段階 最大値 / 2026-04-04）
  - 第二段階最大運転の継続を確認:
    - `clip=M_MLoop1` 区間で `speedCalc=1 speed=1` が連続
    - `animSpeed=2.2` が連続
  - 代表ログ:
    - `07:30:44.515` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:30:50.142` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:31:00.346` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:31:11.558` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
  - 代表スナップショット:
    - `07:30:44.509` `clip=M_MLoop1 ... animSpeed=2.2 ... loop=98`
    - `07:30:59.832` `clip=M_MLoop1 ... animSpeed=2.2 ... loop=136`
    - `07:31:08.987` `clip=M_MLoop1 ... animSpeed=2.2 ... loop=158`
    - `07:31:11.549` `clip=M_MLoop1 ... animSpeed=2.2 ... loop=165`
  - 判定:
    - この最新実行分では、第二段階最大値は `speedCalc=1 / animSpeed=2.2` として安定維持されている。
    - 同区間では `speedCalc=0.75` / `0` への落ち込みは観測されていない。

- 今回ログで追加確定した事実（第一段階 最小値 / 2026-04-04 07:35台）
  - 対象は第一段階クリップ `M_WLoop`。
  - 第一段階の最大運転（同一実行内）:
    - `07:35:25.643` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:35:27.659` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `animSpeed=2.2`（例: `07:35:25.639`, `07:35:27.655`）
  - 第一段階の減速遷移（同一クリップ内）:
    - `07:35:29.190` `speedCalc=0.85` / `animSpeed=1.923`
    - `07:35:29.693` `speedCalc=0.45` / `animSpeed=1.183`
    - `07:35:30.209` `speedCalc=0` / `animSpeed=0.35`
  - 第一段階の最小運転として観測できた値:
    - `clip=M_WLoop`
    - `speedCalc=0`
    - `animSpeed=0.35`
    - 代表ログ: `07:35:30.205`（snapshot）, `07:35:30.209`（patch）
  - 最小運転の継続確認:
    - `07:35:33.769`, `07:35:41.924`, `07:35:49.540`, `07:36:00.241`, `07:36:05.310` まで `speedCalc=0 speed=0` を連続確認
    - `loop` は `16 -> 25` まで増加（停止せず再生継続）
  - 付随して確認できた遷移:
    - `M_OLoop -> M_Orgasm -> M_Orgasm_A -> M_Orgasm_B -> M_WLoop` の遷移後に第一段階の速度制御が行われている
  - 判定:
    - この実行分では、第一段階最小値はログ根拠付きで `speedCalc=0 / animSpeed=0.35` として確定扱い。

- 今回ログで追加確定した事実（第一段階 最大値 / 2026-04-04 07:38台）
  - 対象は第一段階クリップ `M_WLoop`。
  - 直前の最小運転区間を確認:
    - `07:37:54.797` `HSceneProc.Update.Postfix ... speedCalc=0 speed=0`
    - `07:38:10.072` `HSceneProc.Update.Postfix ... speedCalc=0 speed=0`
    - `animSpeed=0.35`（例: `07:37:54.791`, `07:38:10.068`）
    - `loop` は `53 -> 58` まで進行（停止なし）
  - 第一段階の最大運転区間を確認:
    - `07:38:25.340` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:38:38.589` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:38:50.766` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `07:38:58.407` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
    - `animSpeed=2.2`（例: `07:38:25.452`, `07:38:38.585`, `07:38:58.396`）
  - 最大運転中の継続性:
    - `loop` は `66 -> 121` まで増加
    - `clip=M_WLoop` のまま維持
    - 当該区間で `speedCalc=0` / `0.45` / `0.85` への落ち込みは観測なし
  - 判定:
    - この最新実行分では、第一段階最大値は `speedCalc=1 / animSpeed=2.2` として安定維持されている。
    - 同時に、直前に第一段階最小値 (`speedCalc=0 / animSpeed=0.35`) 区間が存在することも確認済み。

- 今回ログで追加確定した事実（イキモーション最小値 / 2026-04-04 07:40〜07:41）
  - 観測区間:
    - `07:40:24.945` 〜 `07:41:22.497`（この範囲で継続確認）
  - モード/体位情報:
    - `mode=masturbation`
    - `nowInfo(id=6, mode=masturbation, name=床オナニー)`
  - 実再生クリップ:
    - `clip(name=M_SLoop, len=1.333, w=0.374)`（snapshot行で継続確認）
    - この観測区間では `M_SLoop` 以外への遷移は未観測
  - 速度関連（最小値運転の成立条件）:
    - `HSceneProc.Update.Postfix mode=masturbation speedCalc=0 speed=0` が連続
    - `mast-trace snapshot` 側で `animSpeed=0.35` が連続
    - 代表ログ:
      - `07:40:37.647` `... speedCalc=0 speed=0`
      - `07:41:13.350` `... clip=M_SLoop ... animSpeed=0.35 ...`
      - `07:41:22.497` `... speedCalc=0 speed=0`
  - ループ進行（停止していないことの根拠）:
    - `loop-changed` が継続発生
    - 例: `loop=41`（07:40:37台）→ `loop=65`（07:41:13台）→ `loop=71`（07:41:22.815）
    - `norm` も単調増加（例: `41.029` → `65.128` → `71.006`）
  - 併発ログ:
    - `HSprite.Update.Postfix` / `HSceneProc.Update.Postfix` が同時周期で出力
    - `[timeline] skip src=hsprite-update reason=force-vanilla`
    - `[timeline] skip src=hscene-update reason=force-vanilla`
    - 上記 `force-vanilla` スキップが定期的に発生している
  - 判定:
    - 今回の実行分で「イキモーション最小値」は `clip=M_SLoop` 上で
      `speedCalc=0 / speed=0 / animSpeed=0.35` として安定継続している。
    - 再生停止ではなく、低速でループ継続している。

- 今回ログで追加確定した事実（イキモーション最大値 / 2026-04-04 07:43台）
  - 観測対象:
    - `mode=masturbation`
    - `nowInfo(id=6, mode=masturbation, name=床オナニー)`
    - `clip(name=M_SLoop, len=1.333, w=0.374)`（観測中はクリップ遷移なし）
  - 最大化前の最小運転継続:
    - `07:42:04.738` 〜 `07:43:08.906` で `speedCalc=0 speed=0` が連続
    - 同区間 `animSpeed=0.35`（snapshot）
    - `loop` は `~119 -> 140` まで進行（停止なし）
  - 最小→最大の立ち上がり遷移（同一クリップ内）:
    - `07:43:09.315` `animSpeed=1.183`, `speedCalc=0.45`
    - `07:43:09.419` `animSpeed=1.738`, `speedCalc=0.75`
    - `07:43:09.424` `HSceneProc.Update.Postfix ... speedCalc=0.75 speed=0.75`
    - `07:43:09.620` `animSpeed=2.2`, `speedCalc=1`
    - `07:43:09.939` `HSceneProc.Update.Postfix ... speedCalc=1 speed=1`
  - 最大運転の継続:
    - `07:43:09.939` 以降、`speedCalc=1 speed=1` が連続（少なくとも `07:43:55.239` まで）
    - 同区間の `mast-trace snapshot` は `animSpeed=2.2` を維持
    - 代表ログ:
      - `07:43:21.177` `... speedCalc=1 speed=1`
      - `07:43:33.927` `... speedCalc=1 speed=1`
      - `07:43:45.576` `... speedCalc=1 speed=1`
      - `07:43:55.239` `... speedCalc=1 speed=1`
  - ループ進行（最大運転中）:
    - `loop=142`（07:43:09.620）から `loop=330`（07:43:55.201）まで増加
    - `norm` も単調増加（例: `143.343` -> `271.492` -> `330.223`）
  - 併発ログ:
    - `timeline skip ... reason=force-vanilla` は最大運転中も定期的に出力
      - 例: `07:43:35.349`, `07:43:41.398`, `07:43:48.136`, `07:43:54.384`
  - 判定:
    - この実行分の「イキモーション最大値」は `M_SLoop` 上で
      `speedCalc=1 / speed=1 / animSpeed=2.2` として安定継続している。
    - 立ち上がりは `0 -> 0.45 -> 0.75 -> 1.0` の段階遷移が確認できる。

- 今回ログで追加確定した事実（ログ全体のモーション抽出 / 2026-04-04）
  - `clip(name=...)` で確認できたモーション一覧（重複除外）:
    - `M_Idle`
    - `M_WLoop`
    - `M_SLoop`
    - `M_MLoop1`
    - `M_MLoop2`
    - `M_OLoop`
    - `M_Orgasm`
    - `M_Orgasm_A`
    - `M_Orgasm_B`
  - `nowInfo(...name=...)` で確認できた上位名（重複除外）:
    - `立ち愛撫`
    - `床オナニー`

- 今回ログで追加確定した事実（末尾側の絶頂遷移 / 2026-04-04）
  - 末尾区間で `M_SLoop` 最大運転が継続（`animSpeed=2.2`, `speedCalc=1`）
  - `07:44:18.230` で `M_SLoop -> M_OLoop` に遷移
    - 根拠: `transition ... clip(name=M_OLoop...)`
  - `07:44:26.382` で `M_OLoop -> M_Orgasm` に遷移
    - 根拠: `transition ... clip(name=M_Orgasm...)`
  - `M_Orgasm` 区間では減速値が段階的に出現
    - 例: `speedCalc=0.65` -> `0.2` -> `0.1`
    - 対応 `animSpeed` 例: `1.553` -> `0.72` -> `0.535`
  - 判定:
    - 末尾側に絶頂モーション遷移が明確に存在する。
    - 遷移順は `M_SLoop -> M_OLoop -> M_Orgasm` で確認済み。
  - `M_OLoop` 区間の詳細（追加メモ）:
    - `07:44:18.230` に `M_SLoop -> M_OLoop` 遷移
    - 遷移直後は `speedCalc=1 / speed=1 / animSpeed=2.2` の最大運転を維持
    - `loop` は `0 -> 26` まで増加（再生継続）
    - 終端で減速開始:
      - `07:44:26.149` `speedCalc=0.85`, `animSpeed=1.923`
      - `07:44:26.335` `speedCalc=0.65`, `animSpeed=1.553`
    - `07:44:26.382` に `M_OLoop -> M_Orgasm` 遷移

- 第一弾回 BPM実測値（ユーザー確定値 / 2026-04-04）
  - 最小値: `15.8`
  - 最大値: `97.8`
  - 補足（線形換算式）:
    - `BPM = 15.8 + (97.8 - 15.8) * speedCalc`

- 第二弾回 BPM実測値（ユーザー確定値 / 2026-04-04）
  - 最小値: `23.6`
  - 最大値: `149.6`
  - 補足（線形換算式）:
    - `BPM = 23.6 + (149.6 - 23.6) * speedCalc`

- イキモーション BPM実測値（ユーザー確定値 / 2026-04-04）
  - 最小値: `39.0`
  - 最大値: `269.0`
  - 補足（線形換算式）:
    - `BPM = 39.0 + (269.0 - 39.0) * speedCalc`

- OLoop BPM実測値（ユーザー確定値 / 2026-04-04）
  - 最小値: `31.7`
  - 最大値: `430`
  - 補足（線形換算式）:
    - `BPM = 31.7 + (430 - 31.7) * speedCalc`
