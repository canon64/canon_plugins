# MainGameGlow

## 概要
- 本編 / VR 向けのキャラ発光表現プラグインです。
- キャプチャカメラと bloom パイプラインを使って発光したキャラ出力を描画します。

## できること
- キャラ発光用の専用キャプチャ経路構築
- bloom ベースの発光描画
- 本編 / VR 両対応
- 専用ログ出力

## 対象プロセス
- `KoikatsuSunshine`
- `KoikatsuSunshine_VR`

## 主なファイル
- `MainGameGlow.dll`
- `MainGameGlow.log`

## 注意点
- ゲーム制御よりレンダリング効果出力に特化したプラグインです。
- キャプチャと bloom のパイプラインは内部で管理されます。
