# MainGameAllPoseMap

## 概要
- FreeH で全ポーズを扱える専用マップを追加するプラグインです。
- 仮想Hポイントの生成やカテゴリ制限の緩和を行います。

## 依存関係
- 必須依存プラグインなし
- 対象プロセス: `KoikatsuSunshine`, `KoikatsuSunshine_VR`

## 導入
- `MainGameAllPoseMap.dll` を `BepInEx/plugins/MainGameAllPoseMap/` に配置します。

## 基本操作
- 本プラグインは常駐型です。基本は設定ファイルで動作を制御します。
- 追加されるマップ番号や表示名は設定から変更できます。

## 設定とログ
- JSON設定: `AllPoseMapSettings.json`
- ログ: `BepInEx/plugins/MainGameAllPoseMap/MainGameAllPoseMap.log`

## 注意点
- 既存の map 番号と `AddedMapNo` が重複しないようにしてください。
- `EnableVirtualPoints` を有効化すると仮想ポイントが追加され、体位選択の幅が広がります。
