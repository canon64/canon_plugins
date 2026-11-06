# canon_gate接続 A担当の実装・検証

2026-10-28。対象は21系統とcanon_gate。ソース変更とcanonical Releaseビルドを実施し、ゲーム起動・操作・DLL配布は実施していない。既存dirtyの別作業を保持し、除外14系統・RhythmSync/BeatSyncSpeed/SpeedLimitBreak・既存 `_shared/PluginSessionGate.cs` は編集していない。

## 中央基盤

- canon_gateは2.3.0。既存Register/CanRun/Capture/Current/SessionContext/H/Maker契約は維持した。
- gate→UiInputCaptureのBepInEx必須ロード順依存を削除し、gate・入力・ログの依存循環を解消した。DLLの型参照は維持した。
- RegisterActivity/ActivityRegistrationを追加した。非Hの開始イベントで中央ActivityAllowedPluginGuidsを一度だけ評価し、拒否もEndまで保持する。既定例外は空である。
- ActivityのScene/Id/Capture/ActivityPermitは保持結果で、再検索や例外再評価をしない。H/Maker開始時には先行Activityを停止し、独自Beginで不許可Hを迂回できない。
- includeSessions=trueなら同じ登録でH/Makerも受ける。登録だけで警告を消す実装ではなく、各pluginの実入口/停止へ接続した。
- 全許可は最初の停止callbackより前に失効する。停止失敗は他pluginの停止を妨げない。
- 新しい `_shared/PluginActivityGate.cs` を各対象assemblyへリンクした。既存PluginSessionGateを変更していない。

## 21系統の接続箇所

| 系統 | 開始と実処理 | 停止 |
|---|---|---|
| CoordCatalog | H/Maker許可後にキーと公開StartCaptureを受付 | coroutine停止、作成Texture破棄、元コーデ、IsRunning復元 |
| LiquidEffectPreview | Preview開始/H許可でLateUpdateの流体実処理を受付 | シミュレーション・Renderer解除、シーン変更で終了 |
| MainGameAiShoujoMode | TitleイベントをActivity許可後に文字差替/モード生成へ接続 | タイトル復元とロード中を含むモード帰還、子処理入力停止 |
| MainGameAlbumAspect | Activity/H許可中のみDriver追加・補正 | 既存DriverのRect/親フレーム復元 |
| MainGameAllPoseMap | 中央H参照へカテゴリ/体位一覧を開始時適用 | 仮想点破棄、各patchを保持許可へ接続 |
| MainGameBlankMapAdd | Reserveは識別準備、中央開始でmap空間・動画室・入力・slideshow開始 | map元Renderer/Terrain/Light/Audio/Particle復元、overlay/media/HTTP保留/入力破棄 |
| MainGameClothesLoadProbe | Activity/H許可中のメソッド計測・手動診断 | trace窓終了、pendingを一度flush、旧hook通知を拒否 |
| MainGameDisplayEffectViewer | ActionSceneのsceneLoadedイベントからActivity開始、手動更新 | AllPlay snapshot復元、runtime context解除 |
| MainGameFpsDisplay | Activity/H許可中のみFPS計数とGUI | sample初期化と非表示 |
| MainGameFrameTimingProbe | 中央開始で一度だけ計測窓を開始 | hooks/recorders解除、停止ではRemainingを消費しない |
| MainGameFreeHMasturbationMenu | 中央Sceneのsprite/H参照からUI・実挙動を開始 | ゲージlock/再生速度・カスタムUI・参照解除 |
| MainGameLogRelay | API窓口はbootstrap、Activity/H許可中のみ実Emission | 実Emissionを停止、保存owner設定は維持 |
| MainGameMapOverlayProbe | 中央開始でmap/actorを一度捕捉 | 追加scene/bundle・元Renderer/Lightを復元 |
| MainGameMapSandbox | metadataは起動時cache、Hまたは明示Attachイベントの許可で環境生成 | World/Sessionを破棄し追加受付停止 |
| MainGamePoseCatalogCapture | 中央H参照で撮影開始、公開入口/継続を保持許可へ接続 | 所有IEnumeratorをDisposeしfinally復元、撮影camera破棄 |
| MainGameTransformGizmo | 許可中のみAttach/入力取得/子Update/描画 | 入力所有と生存gizmo componentを解除、元camera mask復元 |
| MainGameUiHelp | 許可中のみGUI観測・host capture・popup描画 | hover/row/host capture解除 |
| MainGameUiInputCapture | API初期化はbootstrap、実取得は保持許可またはgate警告owner | 全owner/cursor/ドラッグ解除、HFlag参照破棄 |
| StudioCharaStateLogger | Studio Activity中の変更イベントと手動dump | キャラ状態cache解除、静的hookは実記録を拒否 |
| StudioFacePresetTool | Studio Activity中のウィンドウ/手動操作 | 保存可視設定を変えず、入力captureだけ解除 |
| UnityResourceDiagnostics | Activity/H許可中の手動snapshotとBakeMesh trace | snapshot状態を破棄、hookは記録を拒否 |

## 常時処理の削減

- 中央gateにはUpdate/タイマー/場所再判定を追加していない。
- H対象の旧BlankRoomGateとHScene探索を保持許可・中央Scene参照へ置換した。
- FrameTimingのH探索retry、FreeHメニューのH/sprite定期探索、DisplayEffectのscene探索/定期再scan、Studio状態loggerのdelta polling、ResourceDiagnosticsの自動snapshotを撤去した。
- 入力基盤のHFlag探索、camera診断/autoUnstick、owner期限pollを撤去し、セッション/フォーカス/Disableの明示解放へ移した。
- BlankMapAddの部屋表札毎フレーム更新、Config監視、overlay/AI設定再適用、定期audio診断、slideshow directory scan、ImpactBoost設定pollを開始/操作/設定変更通知へ移した。
- 動画の準備/バー再生のretryはprepareCompletedイベントへ移し、H中のmap番号/loading状態の再検査を除去した。
- FPS計数、流体、動画texture、音源位置追従、slideshowの表示進行など実機能の描画/再生ループは残している。ゲーム内の描画・操作の成立は未確認である。

## 検証

- canonical Releaseビルド: 担当21系統とcanon_gate。全件成功。結果は `J:/temp/canon_gate_survey_20261028/build_a_results.json`、各build logは同ディレクトリ。
- 成果物のpath/size/UTC日時/SHA256: `J:/temp/canon_gate_survey_20261028/artifacts_a.json`。コピー元は各plugin自身のbin/Release/net472のみ。
- gate: 42 lifecycle、32 warning、13 Activityで87検査成功。スタブの通知/IMGUI検査でありゲーム内操作試験ではない。
- 実DLL: .NET Frameworkでcanonical gate/input/logをロードして型解決・bootstrap登録不許可・gateから入力への必須ロード順依存不在を確認した。道具は `J:/temp/canon_gate_survey_20261028/AssemblySmoke/`。
- LiquidEffectPreview: 23 offlineケース成功。
- FrameTimingProbe: 57検査成功。10万sampleでallocation 0、finalizerの例外維持も成功。
- BlankMapAdd: 新セッション契約に合わせた既存characterization 161検査とslideshow/date-folder/IO/UV検査成功。
- 数値監査スキルの正規SCAN: 86ファイル成功。verifyを迂回していない。
- UiHelp既存test: 並行編集中の他pluginに対するembedded_ui_audit.jsonのハッシュ不一致で停止。全担当の変更確定後にUiAuditの在庫再生成と既存tests再実行が必要。

## 配置前に残る確認

- 非H例外は初期空。Studio/Title/一般Previewの実処理を使う場合はそのGUIDを中央Activity例外へ明示する。入力/ギズモ/ログを使う個別例外では依存サービスGUIDも併記する。サービスbootstrapを許可回避に使わない。
- MainGameMapOverlayProbeの配布先フォルダは存在するが空でDLL未配置。配布する場合は新規配置になるため親の対象確定を待つ。
- 既存Session helperはcomponentの手動Disable後のPermit個別失効APIを持たない。所有pluginのcleanupを行い、共有helper変更は別14系統担当へ親が調整する。
- 通常版/VRの既存process指定を保持したが、GameAPI通知順、強型HSceneProc依存箇所、動画prepare、Maker撮影終了、Studio入力、タイトル復帰、元環境の見た目復元の実機受入は未確認。
- 配置は全担当成功後の親指示まで待機。プロセスが動いている場合は勝手に停止せず、コピーしない。今回の除外14系統とRhythm3系統を配置対象へ入れない。
