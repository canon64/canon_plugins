# CODEBASE_STATE: MainGameAllPoseMap

- Updated: 2026-03-10
- Project: `F:/kks/work/plugins/MainGameAllPoseMap`

## 目的

- FreeHで「全体位を使える専用マップ」を1件追加する。
- 対象は追加マップのみ。既存マップの挙動は変えない。

## 実装要点

- `BaseMap.LoadMapInfo` Postfix:
  - `SourceMapNo` を複製し `AddedMapNo` の新規マップとして注入。
- `BaseMap.LoadMapThumbnailInfo` Postfix:
  - 追加マップ用サムネIDを注入（表示名を分離）。
- `HSceneProc.GetCloseCategory` Postfix:
  - 追加マップのFreeH時のみ `useCategorys` を全カテゴリ化。
  - 必要なら仮想HPoint群を生成して `closeHpointData` へ注入。
- `HSceneProc.CreateListAnimationFileName` Postfix:
  - 追加マップのFreeH時のみ `lstUseAnimInfo` をカテゴリ一致で再構築し、
    進行ロックの影響を受けない全体位一覧にする。
- `HSceneProc.LoadSpecialMapStartPosition` Prefix:
  - 追加マップのFreeH時のみ特殊カテゴリの別マップ遷移を抑止可能。

## 設定ファイル

- `AllPoseMapSettings.json`（DLLと同じフォルダ）
- 主要項目:
  - `AddedMapNo`, `SourceMapNo`, `AddedDisplayName`
  - `EnableAllPoseInFreeH`
  - `BypassFreeHProgressLocks`
  - `EnableVirtualPoints`
  - `DisableSpecialMapJump`
  - `CategoriesOverrideCsv`

## ログ

- `MainGameAllPoseMap.log`
- 出力先: `Path.GetDirectoryName(Info.Location)` 配下
