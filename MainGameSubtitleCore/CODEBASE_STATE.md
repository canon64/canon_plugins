# MainGameSubtitleCore CODEBASE_STATE

## 概要
- 本編 (`KoikatsuSunshine`) で `F6` 押下時に `ActionGame.InformationUI` 経由でテスト字幕を表示する。
- 表示テキスト、表示モード、表示秒数、キーは JSON 設定で変更可能。
- 外部プラグイン向けに公開 API を実装:
  - `MainGameSubtitleCore.SubtitleApi`
  - `TryShow(string text, float holdSeconds = -1f)`
  - `TryShow(SubtitleRequest request, out string reason)`
  - `SubtitleBackend` (`Auto` / `InformationUI` / `Overlay`)
  - 呼び出しは内部キューに積まれ、Unity メインスレッドで描画される。

## 設定
- 設定ファイル: `SubtitleCoreSettings.json`
- 保存先: `Path.GetDirectoryName(Info.Location)`（プラグインDLLと同じディレクトリ）
- 主なキー:
  - `Enabled`
  - `TriggerKey`
  - `DisplayBackend`
  - `DisplayMode` (`Normal` / `Topic`)
  - `TestText`
  - `HoldSeconds`
  - `OverlayUseConversationFont`
  - `OverlayShadowEnabled`
  - `OverlayShadowColor`
  - `OverlayShadowOffsetX`
  - `OverlayShadowOffsetY`
  - `EnableCtrlRReload`

## ビルド&配置
- ビルド:
  - `dotnet build F:/kks/work/plugins/MainGameSubtitleCore/MainGameSubtitleCore.csproj -c Release`
- 配置先:
  - `F:/kks/BepInEx/plugins/MainGameSubtitleCore/MainGameSubtitleCore.dll`

## 残課題
- `InformationUI` の表示位置はゲーム側プレハブ依存。固定下部字幕UIが必要なら別Canvas実装を追加する。

## 外部呼び出し例
```csharp
using MainGameSubtitleCore;

SubtitleApi.TryShow("外部プラグインからの字幕");

var req = new SubtitleRequest
{
    Text = "Overlay 指定字幕",
    Backend = SubtitleBackend.Overlay,
    HoldSeconds = 2.5f
};
SubtitleApi.TryShow(req, out var reason);
```

