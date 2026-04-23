# CODEBASE_STATE: MainGameCharacterAfterimage

- Updated: 2026-03-13
- Project: `F:/kks/work/plugins/MainGameCharacterAfterimage/`
- Build: `dotnet build MainGameCharacterAfterimage.csproj -c Release "/p:KKSDir=F:/kks"`
- Deploy: `F:/kks/BepInEx/plugins/MainGameCharacterAfterimage/MainGameCharacterAfterimage.dll`

## Purpose
- MainGameでキャラのみを毎フレームキャプチャし、残像画像を背景とキャラの間に差し込む。
- 各残像は既定10フレームで透明化し、0になったら自動的に非表示化する。

## Runtime Pipeline
1. 既存のメインカメラをソースとして利用（背景は通常どおり表示）。
2. 中間カメラで残像テクスチャをフルスクリーン描画。
3. 最上位カメラでキャラレイヤーのみ再描画し、キャラを最前面に維持。
4. 別のキャプチャカメラでキャラレイヤーのみ透過RTへ毎フレーム描画。
5. スロットバッファへ保存し、フレーム寿命に応じてアルファ合成。

## Settings
- `MainGameCharacterAfterimageSettings.json`（プラグインフォルダ内）で全パラメータを調整可能。
- 主な項目:
  - `FadeFrames`
  - `MaxAfterimageSlots`
  - `CaptureIntervalFrames`
  - `CharacterLayerNames`
  - `MiddleCameraDepthOffsetMilli`
  - `TopCharacterCameraDepthOffsetMilli`
  - `OverlayTint*`
  - `SourceCamera*`

## Files
- `Plugin.cs`: ライフサイクル、カメラ解決、設定リロード、状態ログ。
- `Plugin.Settings.cs`: JSONロード/保存、バリデーション、バックアップ。
- `LayeredAfterimagePipeline.cs`: 3カメラ構成、毎フレームキャプチャ、残像描画、フェード管理。
- `SimpleFileLogger.cs`: 専用ログファイル出力。
