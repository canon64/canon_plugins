# MainGameDollMode CODEBASE_STATE

## 目的
- MainGameで「人形モード」を状態化し、目ハイライトを常時OFF（なし）で維持する。
- 他プラグイン（将来はMainGameBlankMapAdd再生バー）からON/OFFできる窓口を提供する。

## 実装要点
- API:
  - `Plugin.IsDollModeEnabled()`
  - `Plugin.SetDollModeEnabled(bool enabled, string source)`
- 動作:
  - ON時: 対象キャラへ `ChaControl.HideEyeHighlight(true)` を適用
  - ON中: 一定間隔で再適用（上書き対策）
  - OFF時: ON前の `hideEyesHighlight` を復元
- 設定:
  - `config.json`（UTF-8）

## ログ
- BepInExログ + `MainGameDollMode.log` に実行結果を出力
- 主要ログ:
  - `[doll-mode] enter/exit`
  - `[doll-mode] apply`
  - `[doll-mode] restore`
  - `[api] set_doll_mode ... result=ok`

## 調査メモ（H挿入Loop融合）
- 日付: 2026-04-21
- 参照実ファイル:
  - `F:/kks/abdata/h/list/01.unity3d`（`AnimationInfo_02`, `al_xxx`）
  - `F:/kks/abdata/h/anim/female/02_01_00.unity3d`（`AnimatorController: khs_f_00`）
- 確定事項:
  - 挿入Loopの処理経路は `HSceneProc.Update -> HSonyu.Proc -> LoopProc -> WaitSpeedProc -> SetAnimatorFloat("speed", ...)`。
  - `AnimationInfo_02` から挿入体位の female controller は `khs_f_00 / khs_f_nXX` 系へ解決できる。
  - `khs_f_base` の `WLoop/SLoop` は BlendTree 構造:
    - ルート軸 `motion`（threshold `0.0/1.0`）で `Loop1` と `Loop2` をブレンド
    - 各枝で軸 `height`（threshold `0.0/0.5/1.0`）により `S/M/L` クリップを選択
  - つまり `WLoop` は `S/M/L × (WLoop1/WLoop2)`、`SLoop` は `S/M/L × (SLoop1/SLoop2)` の合成。
  - 状態遷移テーブルは使わず、`SetPlay("WLoop"/"SLoop")` で状態直指定し、BlendTree内部で合成される。
  - `speed` は BlendTree 軸ではなく state speed パラメータとして再生速度側に効く（重み軸は `motion`）。
  - 体位別 `khs_f_00 / khs_f_nXX` は `S_WLoop1` 等の leaf clip を差し替える実体。
  - `path -9...` の数値は Unity の `PathID`（内部オブジェクトID）で異常値ではない。
