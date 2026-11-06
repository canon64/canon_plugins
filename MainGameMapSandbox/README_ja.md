# MainGameMapSandbox

AllPoseRoomの追加マップへ共通ランタイムを接続するBepInExホストです。

同じフォルダに `MapEnvironmentRuntime.dll`、`Content/map-environment.bundle`、`Content/parameters.json` を配置します。

プレイバックバーの「マップ」にある「自然な空・昼夜の検証マップ」から使います。

解除・退室時は共通ランタイムが元の空・光・環境色・霧を復元します。

この時間システムはksPlugの時間経過設定から独立しています。
