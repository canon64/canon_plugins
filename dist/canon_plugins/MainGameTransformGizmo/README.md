# MainGameTransformGizmo

## 概要
- MainGame 用の Transform Gizmo コンポーネントと API を提供する基盤プラグインです。
- 他プラグインから `TransformGizmoApi.Attach(...)` で利用される前提です。

## 依存関係
- 必須依存プラグインなし
- 対象プロセス: `KoikatsuSunshine`

## 導入
- `MainGameTransformGizmo.dll` を `BepInEx/plugins/MainGameTransformGizmo/` に配置します。

## 基本操作
- 本プラグイン単体に専用UIはありません。
- 利用側プラグインが Gizmo の表示/非表示やドラッグイベントを制御します。

## 設定とログ
- 主要API: `TransformGizmoApi.Attach`, `TransformGizmoApi.TryAttach`
- ログ: `BepInEx/plugins/MainGameTransformGizmo/_logs/info.txt`

## 注意点
- `MainGameBlankMapAdd` などの依存先で必須扱いになるため、同時導入を推奨します。
