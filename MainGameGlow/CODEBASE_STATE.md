# MainGameGlow 状態

## 目的
本編 / VR 両対応の **キャラ発光（グロー）エフェクト** プラグイン。Charaレイヤーだけを切り出して Bloom を当て、本編カメラの画面にオーバーレイ合成する。

## ファイル構成（partial class）

| ファイル | 行数 | 役割 |
|---|---|---|
| `Plugin.cs` | 157 | 統括。属性、定数、フィールド宣言、`Awake` / `Update` / `LateUpdate` / `OnDestroy` / `CaptureGlowEndOfFrame` コルーチン |
| `Plugin.Config.cs` | 270 | 設定バインド全 (`SetupConfig` / `Bind` ヘルパ / `BuildConfigManagerAttributes` / `ApplyConfig`) |
| `Plugin.Capture.cs` | 118 | キャプチャカメラ (`SetupCaptureCamera` / `CreateRT` / `ReleaseCaptureRt` / `ResolveCamera` / `IsGlowRequested` / `CaptureGlow`) |
| `Plugin.Bloom.cs` | 247 | グロー (`EnsureCaptureGlowPipeline` / `FindPostProcessLayerTemplate` / `ResolvePostProcessResources` / `ApplyCaptureGlowSettings` / `CleanupCaptureGlowPipeline`) + `CaptureGlowVolumeLayer` 定数 + `PostProcessLayerResourcesField` リフレクション |
| `Plugin.Overlay.cs` | 78 | オーバーレイ (`OverlayDrawer` 内部クラス / `SyncOverlayDrawer` / `DrawOverlay`) |
| `Plugin.Logging.cs` | 44 | ログ (`LogPlugin` / `LogGlowDecision`) |

## 動作フロー

1. **Awake**: 設定バインド → `ApplyConfig`（RenderTexture 生成、キャプチャカメラ生成）
2. **Update（毎フレーム）**: 解像度変動検知で再構築、`ResolveCamera` で本編メインカメラ特定、`SyncOverlayDrawer` でカメラに `OverlayDrawer` を attach
3. **LateUpdate**: `WaitForEndOfFrame` 後に `CaptureGlow` を起動（コルーチン）
4. **CaptureGlow**: 本編カメラの位置/姿勢を `_captureCamera` にコピー → cullingMask=Charaレイヤーだけ → `_captureRt` に描画 → `EnsureCaptureGlowPipeline` で Bloom 設定 → `_captureCamera.Render`
5. **OverlayDrawer.OnPostRender**: 本編カメラ描画完了後に `DrawOverlay` で `Graphics.DrawTexture` を使い `_captureRt` を Tint+Alpha で画面に上書き合成

## 主要な内部要素

- **キャプチャ用専用Layer**: `CaptureGlowVolumeLayer = 31` (PostProcessVolume の trigger 専用に使う)
- **PostProcessLayerResourcesField**: `m_Resources` private フィールドへのリフレクション参照（PostProcessLayer のテンプレート流用用）

## 設定（5カテゴリ）

| カテゴリ | 項目 |
|---|---|
| 01. General | Enabled / Verbose Log |
| 02. Capture | Use Screen Size / Capture Width / Height / Character Layer Name |
| 03. Glow | Glow Threshold / Strength / Blur Percent |
| 04. Overlay | Tint R / G / B / A / Overlay Alpha |
| 05. Source Camera | Prefer Camera.main / Camera Name Filter / Fallback Index |

## 他canon_pluginsとの連携
- 完全に独立。HardDependency / SoftDependency なし
- 本編カメラの上に効果を重ねる単発プラグイン

## ビルド・配置
- csproj: `MainGameGlow.csproj`（net472）
- 出力: `bin/Release/MainGameGlow.dll`
- 配置先: `F:/kks/BepInEx/plugins/canon_plugins/MainGameGlow/`

## 変更履歴
- 2026-05-10: 単一ファイル `Plugin.cs` (869行) を partial class 6ファイルに分割（責務ごと）。動作・設定キーは変更なし。
