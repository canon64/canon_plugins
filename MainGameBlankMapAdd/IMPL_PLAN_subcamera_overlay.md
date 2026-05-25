# IMPL_PLAN: 動画ルームのVideoSurfaceにサブカメラ映像を半透明オーバーレイ

## ゴール（ユーザー言葉）
動画ルームの動画ディスプレイの上に、サブカメラの映像を半透明で重ねたい。動画自体は止めずに上に乗せる。動画ルームのサイズに連動。

## 動作仕様（Phase 1）
- BlankMapAdd の動画ルーム内、選択した VideoSurface（front/back/floor 等）の **すぐ手前** に半透明Quadを生成
- Quadは VideoSurface の Transform（位置/回転/スケール）に毎フレーム追従
- Quadのマテリアルに SubCameraDisplayProbe の RenderTexture を貼る + α透明度を設定値で適用
- VideoSurface の法線方向に微小オフセット（z-fighting防止）
- BlankMapAddのプレイバックバー詳細項目に新セクション「SUBCAMERA OVERLAY」を追加
  - チェックボックス: ON/OFF
  - ドロップダウン: 対象 VideoSurface 選択（VideoRoomRoot の子から動的列挙）
  - スライダー＋テキスト入力: 透明度 0-1
- 設定はBlankMapAdd側JSONに永続化

## Phase 2（後回し、今回は実装しない）
- 拍信号取得（MainGameBeatSyncSpeed からリフレクション、MGCC 同様）
- SubCameraDisplayProbe側に拍FOV連動を移植
- BlankMapAddのオーバーレイ透明度に拍連動（Min/Max/反転）

---

# Phase 2 実装計画（拍連動FOV + 拍連動透明度）

## ゴール
- サブカメラのFOVをBPMに合わせて拡縮（MGCCと同じ仕組み）
- オーバーレイ透明度をBPMに合わせて Min↔Max で振動（反転モード対応）
- 両者は独立にON/OFF可能

## 必要材料

### 材料P2-1: MGCCの拍連動実装
- 場所: `MainGameCameraControl/Plugin.cs:1650-1936`
- リフレクション型: `MainGameBeatSyncSpeed.Plugin`
- 取得フィールド: `_currentZoneRawTarget` (Instance.NonPublic, float)、`_cfgBpm`/`_cfgLowIntensity`/`_cfgMidIntensity`/`_cfgHighIntensity` (ConfigEntry)
- ゾーン判定: rawTargetが Low/Mid/High Intensity のどれに最も近いかで判定
- 拍周波数: `Hz = (BPM/60) * zoneSpeed01`
- パルス: `phase01 = Repeat(Time.unscaledTime * Hz, 1)`、`pulse01 = Sin(phase01 * π)`
- FOV倍率: `multiplier = Lerp(1, ZoomMultiplier, pulse01)`、`zoomedFov = baseFov / multiplier`
- 拍停止時の戻し補間（0.5秒で multiplier=1 に戻る）

### 材料P2-2: SubCameraDisplayProbe側 拍FOV
- 反映先: `_subCamera.fieldOfView`
- 設定追加: `BeatFovEnabled` (bool, 既定 false)、`BeatFovZoomMultiplier` (float 1-3, 既定 1.5)
- 適用タイミング: `UpdateCameraTransition` が走っていない時、`ApplyCameraSettings` の代わりに拍補正後のFOVを反映
- 既存の `_settings.CameraFieldOfView` をbaseFovとして使う

### 材料P2-3: BlankMapAdd側 拍透明度
- 反映先: `ApplyOverlayOpacity()` の直前で透明度値を拍補正
- 設定追加:
  - `OverlayBeatOpacityEnabled` (bool, 既定 false)
  - `OverlayBeatOpacityMin` (float 0-1, 既定 0.1)
  - `OverlayBeatOpacityMax` (float 0-1, 既定 1.0)
  - `OverlayBeatOpacityInverted` (bool, 既定 false)
- 計算: 反転OFF時 `opacity = Lerp(Min, Max, pulse01)`、反転ON時 `opacity = Lerp(Max, Min, pulse01)`
- 既存の `_settings.OverlayOpacity` は使わなくなる（拍ON時のみ。OFF時は従来通り使用）

## 実装ステップ（Phase 2）

### A. SubCameraDisplayProbe 側
1. `Plugin.Settings.cs` に `BeatFovEnabled` (bool)、`BeatFovZoomMultiplier` (float) 追加 + マイグレーション + クランプ
2. 新規 `Plugin.BeatSync.cs`: リフレクション (Plugin型/Instance/_currentZoneRawTarget/_cfgBpm/_cfgLowIntensity/_cfgMidIntensity/_cfgHighIntensity) + `TryGetBeatPulse01(out float)` + `ResolveBeatZoomedFov(baseFov)`
3. `Plugin.cs` の Update で拍FOVが有効なら `_subCamera.fieldOfView` を `ResolveBeatZoomedFov(_settings.CameraFieldOfView)` で書き換え
4. `Plugin.UI.cs` のカメラ設定セクションに「拍FOV」チェック + 「ズーム倍率」スライダー追加

### B. BlankMapAdd 側
5. `PluginSettings.cs` に `OverlayBeatOpacityEnabled` (bool)、`OverlayBeatOpacityMin` (float)、`OverlayBeatOpacityMax` (float)、`OverlayBeatOpacityInverted` (bool) 追加 + マイグレーション
6. `Plugin.SubCameraOverlay.cs` に拍信号取得 (リフレクション同パターン) + `ResolveOverlayOpacityWithBeat()` を追加
7. `UpdateSubCameraOverlay` の透明度反映で `ResolveOverlayOpacityWithBeat()` を使う
8. UI セクション高さを 60 → 100 に増やす（バー高さ定数 346 → 386）
9. `DrawSubCameraOverlaySection` に2段目を追加: BeatOp チェック / Min スライダー / Max スライダー / Inverted チェック

### C. ビルド・デプロイ・コミット
10. 両プラグインビルド
11. デプロイ
12. 実機確認
13. コミット

## 落とし穴
- 拍信号未取得時（MainGameBeatSyncSpeed未ロード or 信号停止）は multiplier=1 / pulse=0 を返す
- BPMが0や負の値の時はsilently OFF扱い
- 拍ONの時、透明度スライダー(`OverlayOpacity`)は未使用 → UI上はグレーアウトすると親切
- バー高さ定数(`PlaybackBarMinHeightExpandedPx`)を増やすことを忘れない
- 拍停止時のFOV戻しは Phase 1 の MGCC 実装に倣って 0.5秒線形補間（細部省略可）
- リフレクションは1回キャッシュして毎フレーム再探索しない

## 役割分担
- **SubCameraDisplayProbe**: RenderTexture を提供する公開API 1個追加するだけ
- **BlankMapAdd**: オーバーレイQuadのライフサイクル全管理、UI追加、リフレクション呼び出し

## 必要材料

### 材料1: BlankMapAdd の VideoSurface 構造
- 親: `__BlankMapVideoRoom` GameObject (`_videoRoomRoot`)
- 子: `VideoSurface_<surfaceName>` Quad 複数（front/back/left/right/floor/ceiling 等）
- 場所: `Plugin.VideoSurface.cs:158` の `CreateVideoSurface`、`Plugin.VideoRoom.cs:325` で呼び出し
- 各Quad: PrimitiveType.Quad、`MeshRenderer.sharedMaterial.mainTexture` に動画

### 材料2: BlankMapAdd の VideoRoomRoot 取得
- フィールド `_videoRoomRoot` (GameObject) は Plugin partial 内で利用可能
- 子の VideoSurface 列挙: `_videoRoomRoot.transform.Find("VideoSurface_xxx")` または `GetChildren` 走査

### 材料3: BlankMapAdd の PluginSettings 構造
- 場所: `PluginSettings.cs`
- `[DataContract]` クラス、`[DataMember]` で永続化
- `OnDeserialized` で旧設定マイグレーション

### 材料4: BlankMapAdd の PlaybackBar 詳細エリア
- 場所: `Plugin.PlaybackBar.cs:715-1066` (`_playbackRoomControlsExpanded` で展開)
- 既存セクション: SIZE/POSITION/ROTATION/AUDIO・SAVE（layoutSectionH=64） + BEAT SYNC（integrationSectionH=146）
- 高さ計算: `controlsTotalH = layoutSectionH + controlsGapY (4) + integrationSectionH = 214`
- 追加箇所: BEAT SYNC の下に新セクション「SUBCAMERA OVERLAY」を1段追加
- `controlsTotalH` を新セクション分（例: 60-70px）増やす

### 材料5: SubCameraDisplayProbe の RenderTexture 取得経路
- フィールド: `Plugin._renderTexture` (RenderTexture)
- 既存 API クラス: `MainGameSubCameraDisplayProbeApi`（既存：TryLoadPresetByName/TryGetPresetNames/TryGet/SetUiVisible）
- 新規追加 API: `TryGetRenderTexture(out RenderTexture rt, out string reason)`
- 内部ヘルパー: `Plugin.TryGetRenderTextureInternalApi` を Plugin.cs か Plugin.Probe.cs に追加

### 材料6: 半透明シェーダー候補（KKS Unity 環境）
- 候補1: `Unlit/Transparent`（標準、α対応）
- 候補2: `Standard` を Transparent モードに切替（既存 `CreateDisplayMaterial` パターン）
- 候補3: `Mobile/Particles/Alpha Blended`
- Phase 1 で実機確認、効かない場合は別候補に切替

### 材料7: リフレクション API 呼び出しパターン
- 既存サンプル: `MainGameVoiceFaceEventBridge/Plugin.cs:7892-` の `InvokeMainGameCameraControlApi` 同パターン
- 型解決: `Type.GetType("MainGameSubCameraDisplayProbe.MainGameSubCameraDisplayProbeApi, MainGameSubCameraDisplayProbe", throwOnError: false)`
- メソッド: `apiType.GetMethod("TryGetRenderTexture", BindingFlags.Public | BindingFlags.Static)`
- out引数で `RenderTexture` を受ける

## 実装ステップ（Phase 1）

### A. SubCameraDisplayProbe 側
1. `Plugin.Presets.cs`（または Plugin.Probe.cs）に `TryGetRenderTextureInternalApi(out RenderTexture, out string)` を追加
2. `Plugin.Api.cs` に `TryGetRenderTexture(out RenderTexture, out string)` を追加

### B. BlankMapAdd 側
3. `PluginSettings.cs` に追加:
   - `OverlayEnabled` (bool, 既定 false)
   - `OverlayTargetSurface` (string, 既定 "front")
   - `OverlayOpacity` (float, 既定 0.5)
   - `OnDeserialized` でクランプ・既定値補正
4. 新規 `Plugin.SubCameraOverlay.cs` を作成:
   - `_overlayQuad` (GameObject), `_overlayMaterial` (Material), `_overlayRenderTexture` (RenderTexture cached)
   - `EnsureOverlayQuad()`: Quad生成（VideoRoomRoot配下）、マテリアル設定
   - `DestroyOverlayQuad()`: 破棄
   - `UpdateOverlay()`: 毎フレーム呼ぶ — 設定変化検知、対象VideoSurface追従、透明度反映
   - `TryGetSubCameraRenderTextureExternal(out RenderTexture, out string)`: リフレクション呼び出し
   - VideoSurface名一覧取得 `EnumerateVideoSurfaceNames()`: VideoRoomRootの子からprefix一致で抽出
5. `Plugin.cs` または既存 partial の Update から `UpdateOverlay()` を呼ぶ
6. `Plugin.PlaybackBar.cs` UI 追加:
   - `controlsTotalH` 計算修正（オーバーレイセクション分加算）
   - 新セクション「SUBCAMERA OVERLAY」を BEAT SYNC の下に描画
   - チェックボックス: `_settings.OverlayEnabled`
   - ドロップダウン: `_settings.OverlayTargetSurface`（既存 dropdown パターン流用）
   - スライダー＋数値入力: `_settings.OverlayOpacity`

### C. ビルド・デプロイ・コミット
7. SubCameraDisplayProbe → BlankMapAdd 順にビルド（依存はリフレクションのみだが、API側を先）
8. BepInExへデプロイ
9. 実機確認（実装後、ユーザーに依頼）
10. git commit

## 落とし穴
- VideoSurface のローカルZ方向の手前にオフセットしないとz-fightingで点滅する。法線方向（VideoSurface のローカル -Z 方向 = カメラに向く側）に +0.001m〜+0.005m
- VideoSurface の `localScale` は (size.x, size.y, 1) なので、オーバーレイQuadは同じscaleにする
- VideoSurface の rotation も継承する（局所座標系で同方向を向かせる）
- 動画ルーム未生成時はオーバーレイ非表示
- VideoRoom 再生成（マップ再ロード）時はオーバーレイQuadも作り直し → `_videoRoomRoot` 変化を検知
- マテリアルの Shader 選択: KKS の Unity で `Shader.Find` で取れるものを試す（Phase 1 実機確認）
- リフレクションでRenderTexture取得失敗時はオーバーレイ非表示にして無効ログ
- ドロップダウンUI: BlankMapAdd既存のドロップダウン（folder/video）パターンに揃える

## 進行管理
- 実装後ステップごとに状態更新
- 実機確認は Phase 1 完了時にユーザー依頼
- コミットは Phase 1 をひとまとめでOK（CLAUDE.md鉄則: ビルド＆デプロイ成功後即git commit）
- Phase 2 は別プラン（IMPL_PLAN_subcamera_overlay_beat.md として別途）
