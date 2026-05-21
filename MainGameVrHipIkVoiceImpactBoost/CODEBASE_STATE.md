# MainGameVrHipIkVoiceImpactBoost CODEBASE_STATE

## 目的
- VR本編で `MainGirlHipHijack` の腰中央 BodyIK の「頭方向への進み速度」を監視し、突き入れの瞬間だけ外部ボイス音量ブーストを発火する（戻りの動きは無視）。

## 構成
- `Plugin.cs`: BepInEx入口、設定読込、JSON⇔cfg同期、VRガード、各サービス配線。
- `HipIkMotionDetector.cs`: 腰IK位置と頭ボーン位置から「頭方向への速度」を算出し、しきい値上昇エッジ判定。
- `HipIkTrackingPositionSource.cs`: MainGirlHipHijack の `TryGetBodyIkTrackingPosition` / `TryGetFemaleHeadPosition` をリフレクション呼び出し。
- `VoiceImpactBoostService.cs`: cooldown 判定と `MainGameVoiceFaceEventBridge.PublicApi.TryRequestTransientVolumeBoost` 呼び出し。
- `PluginSettings.cs`: BepInEx `ConfigEntry<T>` ラッパー。ConfigurationManager から全項目操作可。`Changed` イベントを公開。
- `SettingsJsonDto.cs`: JSON シリアライズ用 DTO（マスター形式）。
- `SettingsStore.cs`: JSON DTO のロード/セーブのみ。UTF-8 BOM なし。
- `PluginFileLogger.cs`: プラグインフォルダ内専用ログ。

## 判定ロジック
- 腰位置の毎フレーム差分ベクトルを「腰→頭」の単位ベクトルへ射影し、頭方向への進行距離(m)を取り出す。
- 進行距離が `MinDeltaMeters` 以上 かつ 平滑化済み頭方向速度が `VelocityThresholdMps` 以上で立ち上がりエッジを検出。
- 引き戻し方向(足方向)の動きは射影値がマイナスとなるため自動的に無視。

## 設定
- マスター: `MainGameVrHipIkVoiceImpactBoostSettings.json`（プラグインDLLと同フォルダ、UTF-8 BOMなし）
- GUI: BepInEx ConfigurationManager（F1）から全項目編集可。保存先は `BepInEx\config\com.kks.main.vrhipikvoiceimpactboost.cfg`
- 同期方針:
  - 起動時: JSON → cfg（JSONが無ければ ConfigEntry のデフォルトで JSON を新規作成）
  - 実行中: cfg 変更（ConfigurationManager 操作含む）→ 即 JSON に反映
  - JSON の外部編集は次回起動時に反映（実行中は監視しない）
- 主な調整値: `VelocityThresholdMps`, `MinDeltaMeters`, `SmoothingFactor`, `CooldownMs`, `PeakMultiplier`, `AttackMs`, `HoldMs`, `ReleaseMs`, `SilenceMs`, `Easing`

## 運用メモ
- `MainGameVoiceFaceEventBridge` は VR プロセスでも起動する必要がある。
- 腰位置: `MainGirlHipHijack.Plugin.TryGetBodyIkTrackingPosition` の `body_proxy` を優先し、ない場合は `body_effector_bone` を使う。
- 頭位置: `MainGirlHipHijack.Plugin.TryGetFemaleHeadPosition` で `cf_j_head` ボーンの世界位置を取得（VRグラブ/角度ギズモ/HMD追従が反映後の最終位置）。
