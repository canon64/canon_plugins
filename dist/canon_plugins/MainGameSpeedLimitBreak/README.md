# MainGameSpeedLimitBreak

## 概要
- H中の速度制御を BPM ベースで拡張するプラグインです。
- BPMプリセット、動画タイムライン連動、UI操作、ホットキー操作に対応します。

## 依存関係
- 必須依存プラグインなし
- 対象プロセス: `KoikatsuSunshine`, `KoikatsuSunshine_VR`
- 任意連携: `MainGameBlankMapAdd`（動画時間スナップショット連携）

## 導入
- `MainGameSpeedLimitBreak.dll` を `BepInEx/plugins/MainGameSpeedLimitBreak/` に配置します。

## 基本操作
- 既定ホットキー: `LeftAlt+S`（BPM UIの表示/非表示）
- プリセット切替ホットキーは Config で任意設定できます。
- 動画タイムライン機能を使う場合は `SpeedTimeline.json` を指定します。

## 設定とログ
- JSON設定: `SpeedLimitBreakSettings.json`
- BepInEx設定: `BepInEx/config/com.kks.maingame.speedlimitbreak.cfg`
- ログ: `BepInEx/plugins/MainGameSpeedLimitBreak/MainGameSpeedLimitBreak.log`
- 参考テンプレート: `SpeedTimeline.annotated.template.jsonc`

## 注意点
- 動画タイムライン連動は `EnableVideoTimeSpeedCues` を有効化して使用します。
- `MainGameBlankMapAdd` が未導入でも通常の BPM 速度制御は使用できます。
