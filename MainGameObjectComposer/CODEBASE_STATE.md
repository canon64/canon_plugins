# CODEBASE_STATE: MainGameObjectComposer

- Updated: 2026-03-10
- Project: `F:/kks/work/plugins/MainGameObjectComposer/`
- Build: `dotnet build MainGameObjectComposer.csproj -c Release "/p:KKSDir=F:/kks"`
- Deploy: `F:/kks/BepInEx/plugins/MainGameObjectComposer/MainGameObjectComposer.dll`

## Purpose
- H scene object manager with parent-child graph editing.
- In-game IMGUI for create/delete/parent/transform editing.
- Snapshot-based Undo/Redo.
- JSON save/load with timestamp backup.
- Separate H scene state window (mode, gauges, animation info, H-point).

## Main Files
- `Plugin.cs`: lifecycle, hotkeys, logging, global state.
- `Plugin.UI.cs`: object manager window and H-state window.
- `Plugin.Objects.cs`: runtime object graph and mutation operations.
- `Plugin.History.cs`: undo/redo snapshot stack.
- `Plugin.Persistence.cs`: settings/layout JSON I/O with backup.
- `Plugin.Runtime.cs`: HSceneProc/HFlag/female resolve and state helpers.

## Runtime Notes
- Targets: `KoikatsuSunshine`, `KoikatsuSunshine_VR`.
- Root object for managed scene objects: `__MainGameObjectComposerRoot`.
- Settings: `ObjectComposerSettings.json` (plugin directory).
- Layout: `ObjectComposerLayout.json` (plugin directory).
- Log: `MainGameObjectComposer.log` (plugin directory).

## H-Scene モーション識別メモ（プリセット用キー設計の根拠）
出典: `F:/kks/work/plugins/canon_plugins/MainGameDollMode/CODEBASE_STATE.md`

- `khs_f_base` の `WLoop` / `SLoop` は BlendTree 構造で、それぞれ **Loop1 / Loop2 の2種類** を内包する。
  - ルート軸 `motion` (threshold 0.0 / 1.0) で `Loop1` ↔ `Loop2` をブレンド
  - 各枝で軸 `height` (threshold 0.0 / 0.5 / 1.0) により `S / M / L` クリップを選択
  - 結果: `WLoop` は `S/M/L × (WLoop1/WLoop2)`、`SLoop` は `S/M/L × (SLoop1/SLoop2)` の合成
- 状態遷移テーブルは使わず、`SetPlay("WLoop"/"SLoop")` で状態直指定し BlendTree 内部で合成される。
- `speed` は state speed パラメータ（再生速度）、`motion` が Loop1/Loop2 ブレンド軸。
- 体位別の `khs_f_00 / khs_f_nXX` は `S_WLoop1` 等の leaf clip を差し替える実体。

### 体位 + モーション識別キー設計
プリセット保存・自動呼び出しの際の識別キーは以下の4成分で構成する：
```
mode / animId / lane / loopIdx
例) Sonyu/3/W/1, Sonyu/3/W/2, Sonyu/3/S/1, Sonyu/3/S/2
```
- `mode`     = `HFlag.mode` (enum 名)
- `animId`   = `HFlag.nowAnimationInfo.id` (int)
- `lane`     = `nowAnimStateName` に `WLoop` 含むなら `W`、`SLoop` 含むなら `S`、それ以外 `O` (絶頂等)
- `loopIdx`  = `HFlag.motion < 0.5 ? 1 : 2`
