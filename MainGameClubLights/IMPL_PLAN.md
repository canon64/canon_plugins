# MainGameClubLights 実装計画

## 概要
HSceneでスポットライト複数をHMD追従・演出制御するプラグイン。
ビートシンク連携・動画ルーム連携・元ライト制御・プリセット管理。

## 必要材料

| 材料 | 状態 | 備考 |
|------|------|------|
| BepInEx 5.x 基盤 | ✅ | 他プラグインと同じ構成 |
| UnityEngine.Light / SpotLight | ✅ | 標準Unity。AddComponent<Light>で生成 |
| HMD位置取得 | ✅ | OpenVR.Compositor.GetLastPoses（VRDevicePoseレイヤー参照）|
| VR非使用時の位置 | ✅ | 女キャラ or 固定座標にフォールバック |
| BeatSync CurrentIntensity01 | ✅ | MainGameBeatSyncSpeed.Plugin.CurrentIntensity01（static float）|
| BeatSync Low/Mid/High判定 | ✅ | intensity と low/highThreshold をリフレクションで取得 |
| 動画切り替えイベント | ✅ | MainGameBlankMapAdd.Plugin.OnVideoLoaded（static Action<string>）|
| 元ライト取得 | ✅ | FindObjectsOfType<Light>()、起動時キャッシュ |
| 設定JSON保存 | ✅ | SettingsStore相当をJson.NETなしで System.Text.Json or JsonUtility で実装 |
| プリセット保存 | ✅ | プリセットごとにJSONファイル |
| VR有無判定 | ✅ | VR.Active (VRGIN) |

## アーキテクチャ

### データ構造

```
ClubLightsSettings
  ├ List<LightInstanceSettings>  // 追加されたライト一覧
  ├ List<LightPreset>            // プリセット一覧
  ├ Dictionary<string, string>   // videoPath → presetId
  └ NativeLightSettings          // 元ライト制御設定

LightInstanceSettings
  ├ string Id
  ├ bool Enabled
  ├ Vector3 HmdOffset            // HMDからのオフセット
  ├ float Intensity
  ├ float Range
  ├ float SpotAngle
  ├ Color Color
  ├ bool OrbitEnabled            // HMD周囲を回転
  ├ float OrbitRadius
  ├ float OrbitSpeed             // deg/sec
  ├ float OrbitAngle             // 現在角度（runtime）
  ├ RainbowSettings Rainbow
  ├ StrobeSettings Strobe
  └ BeatPresetAssignment Beat    // Low/Mid/High各プリセットID

RainbowSettings
  ├ bool Enabled
  └ float CycleSpeed             // Hue/sec (0-1)

StrobeSettings
  ├ bool Enabled
  ├ float FrequencyHz
  └ float DutyRatio              // ON比率 0-1

BeatPresetAssignment
  ├ string LowPresetId
  ├ string MidPresetId
  └ string HighPresetId

LightPreset
  ├ string Id
  ├ string Name
  ├ float Intensity
  ├ float SpotAngle
  ├ Color Color
  ├ RainbowSettings Rainbow
  └ StrobeSettings Strobe

NativeLightSettings
  ├ bool OverrideEnabled
  ├ float IntensityScale
  ├ BeatPresetAssignment Beat
  └ string PresetId
```

### ファイル構成

```
Plugin.cs                    // Awake/OnDestroy/Update/OnGUI
Plugin.Lights.cs             // ライト生成・更新・エフェクト処理
Plugin.BeatSync.cs           // BeatSync連携・Low/Mid/High判定
Plugin.VideoRoomBridge.cs    // 動画切り替えイベント購読・プリセット自動適用
Plugin.NativeLights.cs       // 元ライト取得・制御
Plugin.UI.cs                 // IMGUI
Plugin.Presets.cs            // プリセット保存・読み込み
ClubLightsSettings.cs        // 設定モデル
SettingsStore.cs             // JSON保存/読み込み
SimpleFileLogger.cs          // ログ
```

## 実装順序

1. csproj + Plugin.cs 基盤
2. ClubLightsSettings + SettingsStore
3. Plugin.Lights.cs（生成・エフェクト）
4. Plugin.UI.cs（追加ボタン・各ライト設定）
5. Plugin.BeatSync.cs（Low/Mid/High判定）
6. Plugin.Presets.cs（プリセット保存/読み込み）
7. Plugin.VideoRoomBridge.cs（自動適用）
8. Plugin.NativeLights.cs（元ライト制御）

## 注意事項
- HScene外でも動作させるか要確認（現状: HScene限定でよい）
- VR未使用時は女キャラ頭上などに固定配置
- ライト数上限: 設定なし（ユーザー責任）
- フォワードレンダリングなのでライト数は少なく使うことを推奨（UI警告表示）
