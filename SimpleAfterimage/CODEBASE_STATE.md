# SimpleAfterimage 状態

## 目的
本編 / VR 両対応のキャラ残像プラグイン。Charaレイヤーだけを別カメラでキャプチャして循環バッファに保存し、寿命に応じて透明度を下げながら本編画面に重ねる。

## ファイル構成（partial class）

| ファイル | 行数 | 役割 |
|---|---|---|
| `Plugin.cs` | 185 | 統括。属性、定数、フィールド宣言、`Instance`、`Awake` / `Update`（プリセット保留処理） / `LateUpdate` / `OnDestroy` |
| `Plugin.Config.cs` | 495 | 設定バインド + `ApplyConfig` + `config.json` 読み書き + プリセット保存・読込 + JSONヘルパ + `PresetData` 内部クラス |
| `Plugin.Capture.cs` | 132 | キャプチャカメラ生成 + スロット管理 (`CreateRT` / `ReleaseSlots`) + `ResolveCamera` + `SyncOverlayDrawer` + `GetHSceneSpeedIntensity` + `CaptureEndOfFrame` |
| `Plugin.Bloom.cs` | 246 | Glowパイプライン (`EnsureCaptureGlowPipeline` / `FindPostProcessLayerTemplate` / `ResolvePostProcessResources` / `ApplyCaptureGlowSettings` / `CleanupCaptureGlowPipeline` / `IsCaptureGlowRequested`) + `CaptureGlowVolumeLayer` 定数 + `PostProcessLayerResourcesField` リフレクション |
| `Plugin.Overlay.cs` | 81 | `OverlayDrawer` 内部クラス + `AgeThenBuildDrawList` + `ApplyCurve` + `DrawOnPostRender` |
| `Plugin.Logging.cs` | 42 | `LogPlugin` + `LogGlowDecision` |

## 動作フロー

1. **Awake**: ログパス確定 → プリセット読込 → 設定バインド → JSON読込 → 適用 → JSON書出
2. **Update**: プリセット保留アクション（保存/読込）処理
3. **LateUpdate**:
    - 画面サイズ変動検知で再構築
    - HScene速さ同期（有効時）
    - `ResolveCamera` → `SyncOverlayDrawer`
    - `_frameCounter++` & 間隔判定 → `CaptureEndOfFrame` 起動
    - `AgeThenBuildDrawList` で寿命管理＆描画リスト構築
4. **CaptureEndOfFrame** (コルーチン): `WaitForEndOfFrame` 後、`_captureCamera.CopyFrom(src)` → Bloom 設定 → cullingMaskを Charaレイヤーに → `_slots[_writeIndex]` に Render → 寿命書込
5. **OverlayDrawer.OnPostRender**: 本編カメラ描画完了後、`DrawOnPostRender` で複数スロットを `Graphics.DrawTexture` で重ねる

## 主要な内部要素

- **循環バッファ**: `_slots[]` (RenderTexture配列) + `_life[]` 寿命カウンタ + `_writeIndex` 書込位置
- **描画キュー**: `_drawSlots[]` + `_drawAlpha[]` + `_drawCount`（`AgeThenBuildDrawList` で毎フレーム再構築）
- **Bloomパイプライン**: PostProcessLayer + Volume + Profile + Bloom（`CaptureGlowVolumeLayer = 31` 専用）
- **HScene速さ取得**: `HSceneProc.flags.speedCalc / speedMaxBody` で 0..1 の強度

## 設定（5カテゴリ）

| カテゴリ | 項目 |
|---|---|
| 01.一般 | 有効 / 詳細ログ |
| 02.キャプチャ | 残像寿命フレーム / 同時残像数 / キャプチャ間隔 / 画面解像度を使う / 幅 / 高さ / キャラレイヤー名 |
| 03.オーバーレイ | 色R/G/B/A / 残像アルファ倍率 / フェードカーブ / キャラ前面に表示 / グロー閾値 / 強さ / ぼかし% |
| 04.元カメラ | Camera.main優先 / カメラ名フィルタ / カメラ候補フォールバック |
| 05.プリセット | プリセット名 / プリセット操作 / 速さ同期有効 / 速さ最小時FadeFrames / 速さ最大時FadeFrames |

## 永続化ファイル
- `config.json` - 設定値（cfg と双方向でないことに注意、現状はBepInEx cfg がプライマリ、起動時に JSON 読込→cfg 上書き、SettingChanged で JSON 書出）
- `presets.json` - プリセット辞書
- `SimpleAfterimage.log` - 専用ログ
- `BepInEx/config/com.kks.maingame.simpleafterimage.cfg` - BepInEx標準cfg

## 他canon_pluginsとの連携
- 直接の依存: なし
- VRGIN.Core 参照（VR モード判定用）
- **次のステップで MainGameGlow と連携予定**: Plugin.Bloom.cs を `MainGameGlowApi` 呼び出しに差し替え、4モード（Afterimage / Glow / Afterimage+Glow / Glow-only Afterimage）切替を追加

## ビルド・配置
- csproj: `SimpleAfterimage.csproj`（net472）
- 出力: `bin/Release/net472/SimpleAfterimage.dll`
- 配置先: `F:/kks/BepInEx/plugins/canon_plugins/SimpleAfterimage/`

## 変更履歴
- 2026-05-10: 単一ファイル `Plugin.cs` (1133行) を partial class 6ファイルに分割（責務ごと）。動作・設定キーは変更なし。
