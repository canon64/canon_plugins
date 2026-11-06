# canon_gate 全体配置結果 2026-10-28

04:48 JST、既存配置の51対象プラグインと canon_gate 2.3.0、計52DLLを更新した。MainGameUiHelpの既存 `help_catalog.json` も正規生成物へ更新した。ゲーム内の描画・操作・通常版/VR/Studio動作は未確認。

## 最終確認

- A21＋B16＋C17＝54ルートとgateの55成果物を集合照合。全件が各ルート自身の `bin/Release/net472/<root>.dll`。全サイズ・SHA256・記録日時を照合した（Python記録の日時はµs丸めのため100ns単位の差のみ許容）。実コピー後の日時照合は元ファイルと完全一致。
- CameraControl/SubCameraの参照ビルド由来の差分は、各担当がownproject Releaseと既存試験を再実行しmanifestを再確定した。確認を省略してmanifestだけを置き換える対応は行っていない。
- UiHelpはソース更新後にUiAudit→inventory_ui→build_catalog→coverage→既存Tests→Releaseを再実行。11295 checks PASS、2989 unique entries。Releaseは警告0・エラー0。
- 実DLLメタデータでgate版2.3.0、全対象のgate依存版を確認。Activity利用14本は最小版2.3.0。gateから入力へのロード順依存はない。中央例外の既定空を維持した。
- 通常版/VR/Studioの実プロセスは配置直前0件。ゲーム起動・操作・プロセス停止は行っていない。
- gateを先頭にコピー。全52DLLとcatalogでsize・LastWriteTimeUtc・SHA256の一致を確認。
- BepInEx/plugins全体を走査し対象GUIDの重複0件。対象外の既存重複6組はパスをmanifestに記録し、削除・変更していない。
- 除外14本およびRhythmSync/BeatSyncSpeed/SpeedLimitBreakのDLLは配布対象に含めていない。

## 未導入3件

`MainGameMapOverlayProbe`、`MainGameCharacterAfterimage`、`SimpleAfterimage_VR` は既存配置DLLが存在しない。ソース改修とReleaseビルドまで完了し、今回の作業では新規有効化しないため配置を省略した。

## 配置52DLL

```text
canon_gate
AstralMode
CoordCatalog
LiquidEffectPreview
MainGameAdvIkBridge
MainGameAiShoujoMode
MainGameAlbumAspect
MainGameAllPoseMap
MainGameAutoHVoice
MainGameBlankMapAdd
MainGameCameraControl
MainGameCameraLookControl
MainGameCharacterMorphBridge
MainGameCircularDisplacement
MainGameClothColliderCards
MainGameClothesLoadProbe
MainGameClubLights
MainGameDisplacementMap
MainGameDisplayEffectViewer
MainGameDollMode
MainGameFaceSweatControl
MainGameFpsDisplay
MainGameFrameTimingProbe
MainGameFreeHMasturbationMenu
MainGameHandySync
MainGameHHandPoseProbe
MainGameKaleidoscope
MainGameLiveLookCamera
MainGameLogRelay
MainGameMapSandbox
MainGameObjectComposer
MainGamePoseCatalogCapture
MainGameStudioItemSummon
MainGameSubCameraDisplayProbe
MainGameSubtitleCore
MainGameSubtitleEventBridge
MainGameTransformGizmo
MainGameUiHelp
MainGameUiInputCapture
MainGameVoiceBreastMorph
MainGameVoiceFaceEventBridge
MainGameVoiceImpactBoost
MainGameVrAfterimageLab
MainGameVrHipIkVoiceImpactBoost
MainGameVrScreenEffectProbe
MainGameVrVoiceGate
MainGirlHipHijack
MainGirlShoulderIkStabilizer
SimpleAfterimage
StudioCharaStateLogger
StudioFacePresetTool
UnityResourceDiagnostics
```

## 証跡と復旧用保存

- バックアップ: `J:\temp\canon_gate_backup_20261028_044850`。各rootの既存DLL、UiHelp catalog、変更前hash一覧 `before.json` を含む。
- 配置結果: `J:\temp\canon_gate_survey_20261028\deployment_all_gate_result.json`。全コピー元/先、GUID、size、UTC日時、SHA256、未導入3件、既存無関係重複6組の実パスを含む。
- 配置前照合: 同フォルダ `all_gate_preflight.json`。
- 確定成果物: 同フォルダ `artifacts_a.json`、`artifacts_b.json`、`c_release_results.json`。
- A実装・試験詳細: `README_canon_gate_group_a_implementation_20261028.md`。同資料のUiHelp未完欄は本配置で解消した。

ビルド・静的照合・試験・配置の完了を記録したもので、ゲーム内の成功を証明するものではない。次回のユーザー実機確認結果・ログで動作を判断する。
