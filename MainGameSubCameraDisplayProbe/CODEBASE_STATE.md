# MainGameSubCameraDisplayProbe 状態

## 目的
本編H中に「サブカメラ＋RenderTextureディスプレイQuad」を生成し、デスクトップ／VRどちらでも視聴・撮影できるプローブ。MainGame / VR 両対応。

## ファイル構成（partial class、計 ~3,500行）

| ファイル | 行数 | 役割 |
|---|---|---|
| `Plugin.cs` | 287 | エントリ、Awake/Update/OnGUI、`TryGetUiVisible`/`TrySetUiVisible` 公開API |
| `Plugin.UI.cs` | 390 | OnGUIウィンドウ描画、UI入力奪取連携、スライダー/数値フィールドのドラッグ判定 |
| `Plugin.Probe.cs` | 764 | サブカメラ／Quad生成、TransformGizmo装着、Overlayカメラ、配置（眼の前移動など） |
| `Plugin.Settings.cs` | 509 | JSON永続化（DataContractJsonSerializer）、Normalize、プリセット型定義 |
| `Plugin.Presets.cs` | 515 | カメラプリセット（通常／ボーン追従）、`TryLoadPresetByName` API |
| `Plugin.DisplayPresets.cs` | 134 | ディスプレイ配置プリセット |
| `Plugin.RenderResolution.cs` | 168 | 解像度プリセット（16:9/9:16固定 + カスタム） |
| `Plugin.VR.cs` | 284 | Grip掴み、手元プレビュー、スティックFOV、トリガー写真／長押し動画 |
| `Plugin.Photo.cs` | 81 | PNG保存（`Texture2D.ReadPixels` → `EncodeToPNG`） |
| `Plugin.Video.cs` | 321 | ffmpeg pipe録画（h264_nvenc/hevc_nvenc/libx264） |

## 現在の主要機能
- サブカメラ生成 / RenderTexture / ディスプレイQuad / Overlayカメラ
- F8（既定キー）/ UI 表示切替
- `MainGameUiInputCapture` を使った UI 入力奪取（owner/source = window/slider/gizmo）
- `MainGameTransformGizmo` を使ったカメラ/ディスプレイ移動
- VR Grip によるカメラ/ディスプレイ移動、手元プレビューQuad、スティックでFOV調整
- VRトリガー: 短押し→PNG保存、長押し（既定0.5秒）→ffmpeg動画録画開始/解除で停止
- 解像度プリセット（16:9 360p/720p/1080p、9:16 同等）、カスタムサイズ追加可
- カメラプリセット（通常／ボーン追従の2モード）、ディスプレイプリセット
- ボーン追従: 頭/胸/腰（候補名テーブル `BoneTargetNameCandidates`）

## MainGameCameraControl から借用したロジック（Codex作業の補完部分）
- 保存モードの分類: `通常` / `ボーン`（CameraControl側は + `ksFPV` がある。Probeは前2つだけ採用）
- ボーン候補名テーブル（頭/胸/腰、同一構造）→ `Plugin.cs:43-48`
- 女性キャラ解決 `ResolveMainFemale()`（HSceneProc.lstFemale → ChaControl[] フォールバック）→ `Plugin.Presets.cs:351-378`
- ボーン追従の `LookAtOffsetLocal` / `CameraOffsetLocal` + `Quaternion.LookRotation` 補間 → `Plugin.Presets.cs:135-154`
- `TryLoadPresetByName(string, out string reason)` 公開API（VoiceFaceEventBridge前提のシグネチャ）→ `Plugin.Presets.cs:463-482`
- UI入力奪取の owner/source 二段管理（CameraControl と同パターン）→ `Plugin.UI.cs:352-378`

## Probe独自（CameraControlに無い）
- RenderTexture + 物理Quad表示（CameraControlは本編カメラを直接操作）
- Overlay Camera（Display Layer 0-30 を別カメラで描画する2パス方式）
- VR Grip 掴み移動、手元プレビュー、スティックFOV、トリガー写真/動画
- ffmpeg パイプ動画録画（rgb24生フレームを stdin 流し込み）
- 写真出力: プラグインフォルダ内 `image/`、動画: `video/`
- ffmpeg パスは設定の `VideoFfmpegPath` で `..\\..\\_tools\\ffmpeg\\bin\\ffmpeg.exe`（プラグインフォルダ起点）

## ビルド／デプロイ
- csproj: `MainGameSubCameraDisplayProbe.csproj`（net472、KKSDir=F:\kks）
- 依存DLL: BepInEx, Assembly-CSharp, UnityEngine 各種, MainGameUiInputCapture, MainGameTransformGizmo, VRGIN_OpenXR, Unity.XR.OpenVR
- 出力: `bin/Release/net472/MainGameSubCameraDisplayProbe.dll`
- 配置: `F:/kks/BepInEx/plugins/canon_plugins/MainGameSubCameraDisplayProbe/`
- 設定ファイル: `MainGameSubCameraDisplayProbeSettings.json`（プラグインフォルダ内）
- ログ: `MainGameSubCameraDisplayProbe.log`（プラグインフォルダ内）
- 直近ビルド: 2026-05-10 04:00（DLL配置済み、写真・VR動画録画とも実機動作確認済み）

## 重要な実装ポイント
1. **Awake時はワールドカメラが未取得のことが多い** → 初回 EnsureProbe で `world camera not found` を許容、Update で再試行
2. **Boneフォロー中は Update 内で `_cameraGizmo.IsDragging` チェック後に `UpdateBoneFollow()`** → ギズモ操作中はボーン追従を一時停止
3. **PersistTransformsToSettings** はギズモドラッグ終了時とボーンオフセット変更時に呼ぶ
4. **Display Overlay Camera** は本編カメラの depth+100 で `cullingMask = 1<<DisplayLayer` のみ描画。本編カメラ側からは Layer をマスク除外
5. **VRトリガー判定** は `EVRButtonId.k_EButton_SteamVR_Trigger` と `k_EButton_Axis1` の OR
6. **VR FOV スティック** は `controller.Input.GetAxis().y` をデッドゾーン処理、unscaledDeltaTime で1秒30度（既定）
7. **DisplayWidth は自動計算** `SettingsStore.CalculateDisplayWidth` → `displayHeight * (renderWidth / renderHeight)`

## 未実装・既知の課題（必要なら確認）
- IMPL_PLAN.md が存在しない（CLAUDE.md 鉄則2違反だが、既に実装完了状態のため事後追記の必要性次第）
- 設定ファイルにバックアップが複数残っている（`*.bak_*`、`*.displaypresets-broken-backup.json`）

## 連携プラグイン
- `MainGameUiInputCapture`（HardDependency）
- `MainGameTransformGizmo`（HardDependency）
- VR系（VRGIN_OpenXR / Unity.XR.OpenVR）— `[BepInProcess("KoikatsuSunshine_VR")]` 対応
