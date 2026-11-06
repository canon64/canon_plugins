# canon_plugins 全プラグイン調査・canon_gate 接続分析

調査日: 2026-10-28（日本時間）。対象ソース: `F:\kks\work\plugins\canon_plugins`。対象配置先: `F:\kks\BepInEx\plugins\canon_plugins`。参照Git HEAD: `b45e70f60ce8ac49df587372e89567f68eccfbfb`。未コミット・未追跡の現行ソースも含む。

## 結論

多くの本編用プラグインは「起動準備→許可されたシーンの実処理開始→停止・復元」という共通形へ移せるが、全件への一律置換はできない。公開API、Harmony、子MonoBehaviour、描画通知、通信キュー、遅延処理を含めて個別の停止境界を決める必要がある。

本調査ではソース72系統を全件確認し、配置DLLのBepInPluginメタデータ69エントリとGUIDで照合した。子エージェント3つが24系統ずつ実ソースを分析し、親が一覧・配置差分・共通基盤・重要な根拠を照合してこの1本へ統合した。

これは静的調査と移行分析のレポートであり、canon_gateへの全件接続を実装したものではない。調査中にプラグインのソース変更・ビルド・DLL配置・ゲーム操作は行っていない。別作業の未コミット変更はそのまま保持した。実機挙動・FPS・全遅延経路の動的網羅は未確認。

## 1. 対象と数え方

| 項目 | 確認結果 |
|---|---|
| BepInPluginを持つソースのルート系統 | 72 |
| ソース上のBepInPlugin宣言 | 74（BeatSyncSpeedとSpeedLimitBreakの旧実装・Bridge宣言を各2として数えたもの） |
| 配置先DLLから抽出したプラグインGUID | 69、重複GUIDなし |
| 配置DLLのGUIDに対応するソース | 69件すべて存在 |
| ソースはあるが同GUIDの配置DLLがないもの | MainGameCharacterAfterimage、MainGameMapOverlayProbe、SimpleAfterimage_VR |
| 共通BlankRoomGateをCompile Includeするプラグインのcsproj | 50系統。組込は許可管理の完了を意味しない |
| 配置メタデータのプロセス指定 | 本編66、VR39、Studio6、指定なし1。複数指定があるため合計しない |
| GateApi.Registerの直接呼出しがある既存プラグイン | MainGameAiShoujoModeのみ。タイトル機能の実処理まで門番が制御しているという意味ではない |

上の69件は配置ファイルのメタデータ数であり、現在のゲームで実際にロード成功した件数ではない。ソースと配置DLLの全体ハッシュ一致も確認対象にしていない。旧調査の「共通門番49件」は通常のrgがGit非公開のMainGamePoseCatalogCaptureを除いた数で、今回は同系統も含めて50件とした。

テスト・デプロイ複製・診断用の退避ソースを通常プラグインとして二重計上しない。SmokeProjectはcsprojのCompile Removeに従って除外。MainGameStartupLogResetとMapEnvironmentRuntimeは通常のBepInPluginではないため、72系統とは別に後述する。

## 2. 共通化できる部分

- 起動時は設定・保存先・プラグイン登録・必要な型解決などの準備を行い、演出・キャラ操作・描画・送信の実処理は許可コールバックへ分ける。
- H開始時の部屋判定は門番で共有して1回だけ行い、そのHの終了まで許可結果を固定する。各プラグインの独自H存在検索・部屋監視は開始/終了通知へ移す候補とする。
- 既存のBlankRoomGate共有ソースは多数の入口に配置済みで、移行用の共通接続部品を置く足場になる。ただし共有ファイルだけを書き換えてRegister済み扱いにする方法では、Awakeのパッチや別コンポーネントを止められない。
- 実機能の毎フレーム処理は必要に応じて残す。カメラ補間、IK、粒子、スクロール・ドラッグ、描画処理と、毎フレームの許可再検査を混同しない。不許可時は該当ランタイム自体を起動しない構造を優先する。
- StopRoomGlow、LeaveAllPoseRoom、ResetScene、ReleaseGroup、CancelRoomCaptureなど既存の後始末を使い、足りない復元・通知解除・通信取消を追加する。単にenabled=falseにするだけでは完了しない。
- 公開API・反射入口・遅延復帰は確定済み許可またはシーン世代で検査し、新たな場所検索や設定再評価を行わない。終了時は許可失効後でも後始末を実行する。

## 3. 一律移行を妨げる重要点

### 3.1 依存関係と起動基盤

配置DLLでも `canon_gate → MainGameUiInputCapture → MainGameLogRelay` の必須依存を確認した。ここへ全件機械的に `→ canon_gate` を加えると循環し、ロードを壊す。警告UIと許可管理の分離、または明示的な基盤起動許可が必要。何も動かさないRegisterだけを置いて対応完了としない。

MainGameBlankMapAddはTransformGizmoとUiInputCaptureにも必須依存する。マップ一覧注入・Reserve時の部屋生成と判定元の準備をH開始後まで止めると、AllPoseRoom自体に入れなくなる。登録・準備・シーン実処理の境界を先に決める。

### 3.2 H読込完了後では遅い処理

現行canon_gateはGameAPI.StartH（H読込完了後）で許可する。一方、AllPoseMapの体位一覧生成、FreeHメニュー、座標/モデル初期化パッチなどには読込中に必要な入口がある。これらを無条件にStartH後へ遅らせると機能が失われる。初期化契約の整理、同じH進入中のマップ確定時点での1回判定、または読込中処理を準備として限定する設計の選定が必要であり、本レポートでは未実装の設計課題として残す。毎フレーム監視で補う方針にはしない。

### 3.3 Maker・Studio・タイトル・通常UI

現行gateのプロセス指定はKoikatsuSunshineのみ、セッションはH/Makerのみ。VR39件、Studio6件へそのまま必須依存を追加する前に、門番の実行対象と通知源を整える必要がある。

CoordCatalog・ClothColliderCardsはMaker、StudioFacePresetTool・StudioCharaStateLoggerはStudio、AiShoujoModeはタイトル、AlbumAspectはアルバム画面、DisplayEffectViewerはActionSceneの効果表示を持つ。これらを全てH限定にすると用途を壊す。入力・ログ・ヘルプ・FPS・診断・外部API受付も、H実処理と別の明示的許可契約を定義する。常駐だから無条件に門番対象外と決めない。

### 3.4 Bridgeと子コンポーネント

MainGameBeatSyncSpeed/MainGameSpeedLimitBreakの現csprojはEnableDefaultCompileItems=falseでBridge/Plugin.csだけをコンパイルする。旧Plugin.csの修正だけでは配置DLLへ反映されない。実ロジックはMainGameRhythmSync内のBeat/Speed子MonoBehaviourとパッチにもあり、ホスト1個の開始停止だけでは足りない。

同様にギズモ、残像、VMD管理、描画カメラ、Clothキャラコントローラなどの子コンポーネントは、親Pluginを止めても自動で止まるとは限らない。所有者ごとの生成/有効化/破棄まで契約に含める。

### 3.5 通信・遅延処理と復元

音声・字幕・カメラコマンド・意味姿勢要求・Handy送信は外部入口とキュー/非同期を持つ。受信サービスの寿命、要求を許可する時点、H終了後の応答破棄、HTTP/パイプの取消、音声と機器の停止を分ける。古いHの要求を次のHへ持ち越さない。

## 4. 全72系統の個別分析

各節の用途・現状はソースで確認した事実、接続方法・追加確認事項は移行の提案として読む。コード参照の相対パスはすべて `F:\kks\work\plugins\canon_plugins` を基準とする。各入口のリンクは個別節に付す。ファイルは調査時点のものなので、並行変更後は行番号を再取得する。

### 01. AstralMode

[ソース入口](<F:/kks/work/plugins/canon_plugins/AstralMode/Plugin.cs:9>)

- 用途・プロセス: H中のアストラル遷移、景色・キャラ・カメラ・音声・外部演出の切替。KKS/VR（`AstralMode/Plugin.cs:9-16`）。
- 起動準備: Awakeで設定、空間素材・光・音声関連初期化、Harmonyを適用（同:47-65）。H開始許可と素材/設定準備を分離できるが、初期化内にシーン変更がないか各Initの寿命も確認対象。
- 実入口: Updateのキー操作からEnter/Exit/ForceExitコルーチン（同:73-174）、HFlag同時絶頂通知から自動突入（`Plugin.HScene.cs:126-142`）。H開始Postfixは部屋条件なしで前回資源を掃除してH参照を設定（同:54-70）。Maker専用入口は確認していない。
- 停止・復元: OnBlankRoomLeftがStopAllCoroutines後にForceExit（`Plugin.cs:177-183`）。ForceExitはカメラ・背景・キャラ・衣装・Animator・BGM等を復元（同:241-276）。H終了Postfixにも広い復元（`Plugin.HScene.cs:77-105`）があるが、Plugin.OnDestroyはVMD/サブカメラ中心（`Plugin.VmdSuspension.cs:6-9`）で同等ではない。
- 迂回入口: `RequestEnterAstral` は `_inHScene` とNormalだけでコルーチンを開始し、部屋条件もEnabled条件もその入口にはない（`Plugin.ExternalTrigger.cs:12-24`）。VoiceFace等の反射入口を含め、開始済みセッションへ統一する必要がある。VoiceProc/LateUpdate/姿勢変更等のHarmonyもAwakeで常駐（`Plugin.cs:196-208`）。霧安全処理は現在ゲートより前（同:75-80）に動く。
- 共通化・リスク: H参照の確保/破棄と許可を中央開始・停止へ統一し、外部要求を含む全突入入口を閉じる。停止を単純なenabled=falseにすると空間・音声・キャラ状態が残る。復帰順、遷移途中のH終了、VMD復元、後続H、許可前のH開始cleanup、霧の原状復帰を個別確認する。

### 02. canon_gate

[ソース入口](<F:/kks/work/plugins/canon_plugins/canon_gate/Plugin.cs:13>)

- 用途・プロセス: 中央のH/Maker許可と未登録警告。KKSのみ、KKAPIとUiInputCapture必須（`canon_gate/Plugin.cs:13-16`）。自分自身を通常の演出プラグイン同様にRegisterする対象ではない。
- 起動準備: Config/例外、Harmony、GameAPI.StartH/EndH、MakerFinishedLoading/Exitingを登録（同:30-49）。OnDisable/ShutdownはSetRunning(false)で全セッション停止（同:54-69）。
- 実入口: H開始で一度だけ部屋を読む（同:80-100、124-139）、Maker読み込み完了でMaker開始（同:110-121）。HSceneProc.Start/OnDestroyのPrefixは参照識別用（同:142以降）。H中の場所再評価はこのPluginにはない。
- 停止・迂回: Registerの返り値Disposeが個別停止、gate停止・H/Maker終了は全失効後のStop（`GateApi.cs:24-37、94-113、131-137`）。後登録は次セッション待ちであり、現在H中のロードを救済しない。
- 共通化・リスク: `CanRun(guid)` は確定済み許可の照会、`Capture(guid)` はセッション識別付きPermit（同:35-52）。タイトル/通常画面というセッションは未定義。VR・Studioへの対応範囲、KKAPI通知とStart列挙子/マップReserveの順、gate停止中の復元、開始例外と再入、再有効化時に同じHを自動再許可しない挙動を確認する。UiInputCapture循環は依存グラフ設計で解消する必要がある。

### 03. CoordCatalog

[ソース入口](<F:/kks/work/plugins/canon_plugins/CoordCatalog/CoordCatalogPlugin.cs:9>)

- 用途・プロセス: Makerの全コーデを切り替えて画像カタログと名前JSONを出力。KKS（`CoordCatalog/CoordCatalogPlugin.cs:9`、`CatalogCapture.cs:77-106、173-184`）。
- 起動準備: 設定とCatalogCaptureコンポーネント生成（`CoordCatalogPlugin.cs:32-60`）。通常ログOFFではログファイルも作らない。
- 実入口: UpdateのCaptureKeyから公開StartCaptureへ進み、CustomBase/キャラ/UIの存在確認後にコルーチン（同:82-85、`CatalogCapture.cs:77-106`）。H入口はなくMaker用途なのでMaker許可例外を要する。
- 停止・迂回: 公開StartCaptureも入口。コルーチンはdropdownを変更して数フレームとEndOfFrameを待つ（`CatalogCapture.cs:137-158`）。完了でTextureを破棄しIsRunning=false（同:191-194）。現在の通常経路には元コーデへ戻す処理やMaker終了用cancel/finallyを確認できない。Plugin.OnDestroyはログDisposeのみ（`CoordCatalogPlugin.cs:75-79`）。
- 共通化・リスク: Maker開始で撮影受付を有効にし、終了時に子コンポーネントのコルーチン、撮影途中のTexture、IsRunningを片付ける。ログや設定準備は起動時に残せる。撮影中のMaker終了/無効化/例外時と元衣装の復帰方針、公開StartCapture直接呼び、連打を確認する。

### 04. LiquidEffectPreview

[ソース入口](<F:/kks/work/plugins/canon_plugins/LiquidEffectPreview/Plugin.cs:9>)

- 用途・プロセス: 主カメラ基準のノズル流体プレビュー。BepInProcess指定なし（`LiquidEffectPreview/Plugin.cs:9-11`）。H専用という根拠はない。
- 起動準備: PreviewSettings作成とactiveSceneChanged/sceneUnloaded購読（同:20-24）。RendererはLateUpdateで必要時生成（同:49）。
- 実入口: Config EmitOnceまたはFireKey、Enabled、Camera.mainによりLateUpdateからシミュレーションと描画（同:32-62）。H/Maker検知なし。Settings操作による単発発火もキーとは別入口。
- 停止・迂回: シーン変更/アンロード/OnDisableはClear（同:26-30、72）、OnDestroyは通知解除とRenderer.Dispose（同:73-77）。描画はPluginのLateUpdateが主でHarmony入口はない。
- 共通化・リスク: 設定準備は共通化できるが、許可する用途をH/Maker/通常プレビューのどれにするか要定義。Hだけに変更すると現機能の範囲が狭まる。停止ではClearに加えて必要なGPU資源解放と保留EmitOnce/キー押しっぱなし状態の扱いを決め、シーン跨ぎ再発火・一時停止・通常画面の許可を確認する。

### 05. MainGameAdvIkBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAdvIkBridge/Plugin.cs:13>)

- 用途・プロセス: ADVikへ肩/背骨等の設定と本編呼吸Configを反射適用。KKS/VR（`MainGameAdvIkBridge/Plugin.cs:13`付近、318-350）。
- 起動準備: AwakeはBindConfigとTryBindAdvIk。アセンブリ/型/プロパティの反射接続は準備として保持可能（同:82、242-289）。
- 実入口: UpdateはBlankRoomGate、Enabled、再接続、間隔、任意のHSceneOnlyを経てApplyBridgeSettings（同:218-239）。Applyは独立キャラを除くChaControlへ書込み、任意でADVikの静的呼吸Configも変更（同:318-350）。Maker専用入口はないがApplyInHSceneOnlyという設定上の範囲は残っている。
- 停止・迂回: Harmony/公開API入口は確認していない。専用復元処理は見当たらず、現在は書込みをやめるだけで変更値を元へ戻さない（同:355-422にSetValue）。
- 共通化・リスク: 独自H存在探索と場所監視を中央開始・停止へ置換できる。停止時のキャッシュ破棄に加え、他プラグインのConfig/Controllerの元値を所有・復元するかは個別設計が必要。ADVik不在/遅延ロード、H開始後のキャラ追加、外部による同じ値の変更、通常H/Makerでの例外許可を確認する。

### 06. MainGameAgentCamera

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAgentCamera/Plugin.cs:24>)

- 用途・プロセス: ファイルキュー経由の構図/撮影/カメラ固定。KKS/VR（`MainGameAgentCamera/Plugin.cs:24`付近）。
- 起動準備: AwakeでConfig、queue/shotsディレクトリとFileSystemWatcherを準備（同:79-100）。これは外部受付基盤で、ゲーム状態変更と分離できる。
- 実入口: Updateはゲート外でReleaseするが、その後もキューpoll自体は進む（同:161-193）。ExecuteはStrict条件で全コマンドを拒否（同:349-355）。LateUpdateはhold構図を毎フレーム適用（同:196-210）。
- 停止・迂回: キューwatcher→PollQueue→Executeが入口でHarmonyなし。ReleaseRoomCameraはhold停止、限界復元、同一controllerへの復帰（同:926-936）。OnDestroyはwatcher.Disposeのみ（同:103-107）でカメラ復帰を呼んでいない。
- 共通化・リスク: H開始で実行受付とhold更新を有効化し、終了でReleaseRoomCameraと参照破棄。受付を常駐させる場合は拒否応答を返す層として明示し、禁止中のキュー要求を次Hへ持ち越して実行しない。撮影時の一時UI非表示復元、終了直前のhold、無効化、同一/別カメラへの復元、外部キュー競合を確認する。

### 07. MainGameAibuPoseClones

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAibuPoseClones/Plugin.cs:12>)

- 用途・プロセス: 既存愛撫体位のクローン追加、仰向けクローンA_Idle維持、手モデル非表示。KKS/VR（`MainGameAibuPoseClones/Plugin.cs:12-14`）。
- 起動準備: Config、Harmony（同:46-52）。
- 実入口: CreateListAnimationFileName Postfixが元リストへクローンを挿入（同:326-350）。ChangeAnimator/SetPlay/HAibu.ProcとHSprite.OnChangePlaySelectでモーションと同ID選択を補う（同:62-149、360-380）。UpdateはHを再探索し、一覧生成後の部屋予約遅れを救うためAddCloneEntriesを再試行（同:166-187）。
- 停止・迂回: Updateのゲート外処理は手Renderer復元、H/一時体位/絶頂状態の破棄（同:168-175）。OnDestroyも手復元とUnpatch（同:55-59）。挿入済みクローン一覧を明示的に削除する経路は確認していない。
- 共通化・リスク: 中央開始で対象Hを受けて一覧追加し、中央停止で手と参照を戻す。Harmony全入口も許可されたランタイムだけに向ける。既存「予約が遅いのでH中に再試行」と一回判定方針は衝突するため、GameAPI.StartH時点の部屋表札/一覧準備順を確認する。選択中クローンで無効化、同ID切替、絶頂後復帰、再Hで二重追加しないことを確認する。

### 08. MainGameAiShoujoMode

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAiShoujoMode/Plugin.cs:15>)

- 用途・プロセス: タイトルのUploaderボタンをAI少女モードへ置換し、別SceneにSandboxHostを作って環境を表示。KKS/VR、gate必須（`MainGameAiShoujoMode/Plugin.cs:15-18`、`Plugin.Mode.cs:11-22`）。単なるラベル変更ではない。
- 起動準備: OnEnableでGateApi.RegisterとTitle Harmony、sceneUnloaded購読、既存タイトル探索（`Plugin.cs:30-39`）。Registerの両コールバックはRestoreTitlesであり、H/Makerの実動作を開始する登録ではない。
- 実入口: Title.Start→Queue→Prepareが文字差替、Title.OnUploader Prefix→HandleClick→BeginModeが実モード生成（同:41-79、106-110）。このタイトル経路にCanRunはなくH/Maker用途ではない。
- 停止・迂回: RestoreTitlesはPrepareコルーチンと文字所有権を解除（同:91-95）。OnDisableはCloseModeImmediatelyも呼ぶ（同:97-103）がgateのonStopはRestoreTitlesだけ。AiModeSessionは別MonoBehaviourとしてEnter/Returnコルーチン・Update・入力占有を持ち、Returnでホスト終了とタイトルRoot/カメラ/音声/カーソル復元（`AiModeSession.cs:25-32、43-96`）。
- 共通化・リスク: タイトル/環境モードはH一回許可と分離したライフサイクルが必要。既存Register済みという理由だけで移行完了と数えない。gate停止時に独立モードまで停止すべきか明確化し、その場合は文字復元だけでは足りない。ロード中無効化、失敗復帰、終了時のSceneアンロード、VRで必須gateが存在するかを確認する。

### 09. MainGameAlbumAspect

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAlbumAspect/Plugin.cs:12>)

- 用途・プロセス: アルバムのプレビュー/サムネイル/削除確認の画像比率調整。KKS/VR（`MainGameAlbumAspect/Plugin.cs:12`付近）。H限定の根拠なし。
- 起動準備: Config/ログとAlbumAspectPatches.Apply（同:26-74）。
- 実入口: CapFileViewer.Preview.capture、CapFileNode.LoadTexture、削除ダイアログ、Viewer初期化/ページ切替等のHarmonyからRawImageへAspectFitDriverをAttach（`AlbumAspectPatches.cs:32-42、80-97、134、190、271-278`）。Driverは独立LateUpdateで補正（`AspectFitDriver.cs:53-74`）。
- 停止・迂回: ConfigがOff/Texture不在ならRestoreBase（同:103-116）。Plugin.OnDestroyはUnpatchとInstance解除（`Plugin.cs:81-95`）だが、既存Driverへ一斉にRestoreする処理はここにはない。Driver.TickはPlugin.Instanceなしならreturn（`AspectFitDriver.cs:82-85`）なので、Unpatchだけでは既適用のRect復元を保証できない。
- 共通化・リスク: H開始停止ではなくアルバムUIの表示寿命と中央許可範囲の別枠を定める。Gate登録だけしてDriverが常駐補正を続ける実装は不可。既存Driverの追跡・復元・解除と再Attachを扱い、縦/横画像、ページ送り、削除ダイアログ、無効化後復元を確認する。

### 10. MainGameAllPoseMap

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAllPoseMap/Plugin.cs:16>)

- 用途・プロセス: H体位カテゴリ/一覧解放、仮想HPoint、特殊マップ位置ジャンプ制御。KKS/VR（`MainGameAllPoseMap/Plugin.cs:16`付近、145-165）。
- 起動準備: SettingsとHarmony（同:50-68）。
- 実入口: HSceneProc.GetCloseCategory/CreateListAnimationFileName/LoadSpecialMapStartPositionの各パッチがBlankRoomGate後にカテゴリ書換/一覧再構成/ジャンプ抑止（同:85-142）。Maker/Studio入口なし。
- 停止・迂回: OnDestroyはDestroyAllVirtualPointsとUnpatch（同:71-82）。カテゴリは既存ListをClear/AddRangeする（同:154-158）ので、終了時に仮想点を消すだけで既存値全てが戻るとはいえない。通常はH破棄で終了するが、途中停止用の復元責務は別途必要。
- 共通化・リスク: 設定/Harmony準備は維持し、許可開始で対象Hを確定、各パッチは同じ開始済みランタイムへ限定、停止で仮想点とH参照を解放。初期化中に既に一覧生成が終わる場合は中央開始で同じ適用を安全に再実行する設計が要る。リスト生成順、仮想点の寿命、同H途中停止と元カテゴリ、他一覧変更プラグインとの順序を確認する。

### 11. MainGameAutoHVoice

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAutoHVoice/Plugin.cs:15>)

- 用途・プロセス: HVoiceCtrlで最後に捕捉した音声要求を間隔再発火。KKSのみ（`MainGameAutoHVoice/Plugin.cs:15`付近、255-298）。
- 起動準備: Settings/Config/Harmony、入力/統合タブ準備（同:56-75、98-106）。
- 実入口: HSceneProc.Start PostfixがCurrentProc設定、VoiceProc Prefixがrawを捕捉、UpdateがTickAutoVoice（同:109-116、573-603）。OnGUI/統合タブ内の手動要求も実入口なのでホスト表示だけを止めても足りない。
- 停止・迂回: HSceneProc.OnDestroy PostfixはCurrentProcとlastCapturedRawをクリアするが、終了処理自体にもBlankRoomGate条件がある（同:583-593）。部屋表札が先に消えるとcleanupが通らない可能性がある。OnDestroyは入力解放とUnpatch（同:75以降）で、セッション停止相当の集約はない。
- 共通化・リスク: 中央開始でCurrentProcと捕捉状態初期化、中央停止で入力占有・UIドラッグ・捕捉raw/時刻/Procを無条件クリア。全自動/手動発火とVoiceProcを開始済みセッションへ制約する。音声既再生の停止要否は現仕様との調整が必要。開始通知時のflags準備、捕捉期限、通常H禁止、終了直前手動発火、次Hへのraw持越しを確認する。

### 12. MainGameBeatSyncSpeed

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameBeatSyncSpeed/Bridge/Plugin.cs:11>)

- 用途・プロセス: 現行はMainGameRhythmSync互換Bridge。KKS/VR、RhythmSync必須（`MainGameBeatSyncSpeed/Bridge/Plugin.cs:10-14`）。
- ビルド根拠: csprojはEnableDefaultCompileItems=false、CompileはBridge/Plugin.csと共有BlankRoomGateだけ（`MainGameBeatSyncSpeed/MainGameBeatSyncSpeed.csproj:8、15-19`）。旧ルートPlugin.cs/Patches.csを現行動作として数えてはいけない。
- 起動準備・実入口: AwakeはInstanceとResolveCore（`Bridge/Plugin.cs:48-51`）。Updateは反射でCoreのIntensity/zone/song状態を互換フィールドへコピー（同:54-60）。ResolveCoreはCore型/Instance/Config/辞書を束縛（同:63-87）。Bridge自身にH検知/Harmony/演出コルーチンはない。
- 停止・迂回: OnDestroyはInstance解除だけ（同:113-115）。privateでも反射呼出し用に存在するRefreshSongBpmAutoLoadはCoreへsong/次poll時刻を書戻してCoreメソッドをInvokeする（同:102-109）ため、完全な読取専用とは断言できない。
- 共通化・リスク: Coreの実処理制御を主対象とし、Bridgeは互換提供寿命/書込転送の許可を別途定義する。BridgeへRegisterだけ追加してCoreが従来動作を継続するなら偽対応。停止時は公開CurrentIntensity01等の無効値と参照持越し方針が要る。外部の反射依存（private設定フィールド含む）、Core許可とBridge読取の順序、次Hで古いIntensityやsongを出さないことを確認する。

### 13. MainGameBlankMapAdd

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameBlankMapAdd/Plugin.cs:29>)

- 用途・プロセス: 追加マップカタログ/マップ空間、動画再生、PlaybackBar、環境・外部メディア連携の基盤。KKSのみ（`MainGameBlankMapAdd/Plugin.cs:29`付近）。gateが一度だけ読むBlankRoomStateの供給元でもある。
- 起動準備: Settings、MapCatalogInjector、MapSessionController、Dispatcher、UI入力、WebCam、各環境、HTTP、HarmonyをAwakeで構築（同:131-168）。マップ選択前に必要なカタログ注入はH許可後に遅延できない。
- 実入口: BaseMap.LoadMapInfo/ThumbnailInfoは追加マップ情報を注入、ReserveはMapSession.Apply→表札更新（`Plugin.MapHooks.cs:10-46、78-97`）。Updateも毎フレーム表札を再評価し、環境・動画・HTTPのメインスレッドキュー・UI操作を進める（`Plugin.cs:213-230`）。H/Maker専用入口だけのプラグインではない。
- 停止・復元: OnDestroyはScene通知、overlay、HTTP、入力、UiResourceScope、AstralController、動画室、Harmony、Dispatcherを解放（同:177-205）。H終了で止める範囲はこのPlugin破棄用cleanupとは分離が必要。
- 迂回入口: HTTPのDispatcher.Enqueue（`Plugin.HttpServer.cs:230-245、441`）、SetExternalMediaが独立MonoBehaviourのleaseを作り即適用（`Plugin.ExternalMedia.cs:14-25`）、RegisterExternalMusicSourceも子コンポーネントを作りreverb適用（`Plugin.ExternalMusic.cs:10-19`）。後者LateUpdateは0.25秒ごとに外部AudioSourceへ再適用し、OnDestroyで元値復元（同:39-60）。公開API/動画イベント/予約マップ通知も入口として扱う。
- 共通化・リスク: 起動カタログ/部屋識別供給、H内空間ランタイム、通常の外部受付/UI基盤を分割する。gateがこのProviderを必要とするため、Provider自体をgate開始まで停止すると許可判定の材料が失われる。毎フレーム表札更新の代わりにReserve/破棄等の確定通知で必要な開始時情報を供給できるか調べ、GameAPI.StartHとの順を確認する。停止時の動画/音源/外部lease/HTTP保留要求/入力/環境復元、次Hへの要求持越しを確認する。

### 14. MainGameCameraControl

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCameraControl/Plugin.cs:21>)

- 用途・プロセス: カメラPreset、ボーン追従、体位連動、Beat/外部FOV、入力UI。KKSのみ（`MainGameCameraControl/Plugin.cs:21`付近）。
- 起動準備: Settings/Config、HeightShift Harmony、FOV基準、Camera.onPreCull/onPreRender等を設定（同:414-474）。
- 実入口: UpdateはBlankRoomGate.Tick後にPreset/入力/ドラッグ更新（同:494-512）、LateUpdateは遷移・追従・自動巡回・FOV（同:575-586）、OnGUIは独立窓（同:589-606）。Maker/Studio専用入口なし。
- 停止・復元: 現在の退室callbackはReleaseInputCaptureだけ（同:498）。OnDestroyは描画通知解除、FOV Harmony解除、入力/LookAtAnchor破棄/設定保存（同:476-489）。RestoreBeatFovBase、ClearBoneLinkActiveState、ClearVoiceFaceEventReturnState等は個別に存在（同:2342、2629、3006）。停止時はそれらを適切な順で集約する必要がある。
- 迂回入口: TryApplyExternalFov/FastはInstanceとcamera/VR条件だけで直接適用し、この入口にはBlankRoomGate条件がない（同:3103-3122）。Preset APIはVoiceFace設定判定後に戻り状態を捕捉して適用（同:3682-3705）。Camera.onPreCullは別のBlankRoomFovInputAllowed判定を通りFOVを書込む（同:3604-3612）。外部APIだけでなくConfig.SettingChangedでのFOV基準取得/復元も考慮する。
- 共通化・リスク: H開始で対象cameraと基準を確定し全実入口を同ランタイムへ限定、停止で入力・追従・遷移・自動巡回・外部FOV hold・描画hook所有状態を解除。カメラがH内で差替わる場合の再bindは実機能として残す。停止直後のpreCull、外部API呼び、FOV基準復元、戻りPreset、UI外ドラッグ終了を確認する。

### 15. MainGameCameraLookControl

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCameraLookControl/Plugin.cs:9>)

- 用途・プロセス: カメラ目線時の首/頭XYZ範囲とroll補正。KKS/VR/CharaStudio（`MainGameCameraLookControl/Plugin.cs:9`付近）。H限定根拠はない。
- 起動準備: LookSettings/Policy、SettingChanged、native Harmony（同:26-58）。UpdateはPolicy更新、遅延Config保存、runtime cleanup、統合タブ/入力更新（同:66-85）。
- 実入口: NeckLookController/Calculator/角度結果へのnative patch（`Plugin.Native.cs:89-94`）。BeginEvaluationは有効状態・標準カメラ目線・女性等の条件を確認しNativeParameterLeaseを取得して補正（同:137-175）。Maker/Studioを含み得るキャラ処理でありHの存在判定なし。
- 停止・迂回: OnDisableはClearRollLayers/入力解放/タブ解除、OnDestroyは追加でUnpatchと設定保存（`Plugin.cs:93-107`）。Unity Updateを止めるだけではnative patchが生きるため、BeginEvaluation入口も停止し、評価途中のleaseと最終rollを戻す必要がある。
- 共通化・リスク: 設定準備とnative patch準備、H/Maker/Studio各処理の許可範囲を分離する。現gateがStudioでロードされない点とセッション種別不足を解消する前に必須依存化できない。評価中disable、finalizerの例外復元、Studioカメラ切替、Makerキャラ差替え、首roll残留を確認する。

### 16. MainGameCharacterAfterimage

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCharacterAfterimage/Plugin.cs:11>)

- 用途・プロセス: キャラ残像用描画パイプライン。KKS/VR（`MainGameCharacterAfterimage/Plugin.cs:11`付近）。親の配置照合では現在未配置だが、ソース調査対象に含む。
- 起動準備: Settings/Config/ログとLayeredAfterimagePipeline生成（同:28-52）。
- 実入口: UpdateはBlankRoomGate.Tick後に設定再読込と0.5秒間隔のcamera解決/rebind（同:55-99）、LateUpdateはcameraがあればpipeline再生成/Bind/Tick（同:104-130）。Harmonyや公開変更APIは確認していない。
- 停止・復元: 退室callbackとOnDestroyはShutdownPipeline。実体はDisposeしnullにする（同:30、134-158）。Pipeline側には元camera状態復元/RT解放がある（`LayeredAfterimagePipeline.cs:255、332、477`）。
- 共通化・リスク: H開始でpipelineとcamera候補探索を開始、停止でDisposeに加えて_sourceCamera/再探索タイマー等を捨てる。独立の描画コンポーネントがDispose後に再生しないことを確認する。未配置なので配置済み動作と誤記しない。camera差替え、再H、設定OFF→ON、GPU資源と元camera状態の復元を確認する。

### 17. MainGameCharacterMorphBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCharacterMorphBridge/Plugin.cs:16>)

- 用途・プロセス: カード混合、体/顔形状・肌・overlay等の外部モーフ操作。KKS/VR（`MainGameCharacterMorphBridge/Plugin.cs:16`付近、`CharacterMorphBridgeApi.cs:15-109`）。
- 起動準備: Settings/ConfigとHarmony（`Plugin.cs:84-96`）。
- 実入口: H開始Postfix→SetCurrentHScene→snapshot/override初期化→自動初期化コルーチン（同:447-460、513-519）。衣装/Reload等の広いChaControl Postfixから再適用スケジュール（同:532-565）。公開APIはSetBlend/BlendTo、形状変更、再適用予約等があり、Facadeは概ねInstanceだけを確認して内部へ渡す（`CharacterMorphBridgeApi.cs:34-86`）。内部の全メソッドに許可があるとはここから保証できない。
- 停止・復元: ClearCurrentHSceneはH参照/snapshotを破棄し、初期化/Blend/音/モーション後再適用/外部再適用/着替え再適用を停止（`Plugin.cs:463-481`）。しかしH終了PrefixにもBlankRoomGate条件があり終了cleanupが抜け得る（同:522-528）。OnDisableは保存中心でH終了cleanupと同等ではない（同:99）。
- 共通化・リスク: 中央開始・停止にH参照寿命と全コルーチンを集約し、公開変更API/反射UI/再適用フックも開始済みセッションへ限定。カード辞書登録や設定照会は準備として独立可能。停止の「元形状へ復元」と「現在値を残し再適用停止」は別要件で、現cleanupはsnapshot破棄のため自動的に元へ戻るとはいえない。遷移中終了、衣装ロード通知、次Hへのoverride持越し、独立キャラ除外を確認する。

### 18. MainGameCircularDisplacement

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCircularDisplacement/Plugin.cs:12>)

- 用途・プロセス: カメラ画面の円形歪み/外部warp。KKS/VR（`MainGameCircularDisplacement/Plugin.cs:12`付近）。
- 起動準備: Bind/JSON/Shader準備（同:194以降）。
- 実入口: LateUpdateがBlankRoomGate.Tick→設定/外部活性/OnlyHScene→camera→EnsureRuntime（同:265-280）。OnlyHSceneは0.5秒H探索（同:290-297）。描画はcameraに付いたDrawer.OnRenderImage→Owner.OnImage（同:31-34、460）で独立に走る。
- 停止・迂回: 外部SetExternalWarpの各overloadはInstanceを確認しApplyExternalWarpへ渡す（同:100-135）、ClearExternalWarpは外部状態だけ解除（同:138-143）。StopRuntimeはDrawer/capture/RT/mesh/material/warpRig解放（同:763-775）だが外部活性フラグはここでは消していない。
- 共通化・リスク: H開始にruntimeの利用を許可し終了はStopRuntimeと外部要求/期限/参照を破棄、描画入口と外部APIも同寿命へ揃える。OnlyHScene設定の意味を一回判定方針と整合させる。H内の外部期限や歪み更新は実機能で残す。描画callback遅延、Drawer破棄フレーム、外部要求の次H持越し、通常画面許可、VRの画面処理を確認する。

### 19. MainGameClothColliderCards

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClothColliderCards/Plugin.cs:7>)

- 用途・プロセス: カード保存の布/コライダー定義、Maker編集、キャラ上のCloth生成。KKS/KKAPI必須（`MainGameClothColliderCards/Plugin.cs:7-9`）。
- 起動準備: AwakeのEnabledは再起動反映で、falseならPlugin無効化、trueならCharacterApi.RegisterExtraBehaviour（同:21-35）。カード拡張登録はH開始前のロード/保存基盤。
- 実入口: CharaCustomFunctionController.OnReloadでデータ読取→Rebuild→RefreshFlareSkirt（`ColliderCardController.cs:126-161`）、OnCardBeingSavedでCapture/Save（同:120-123）。公開Add/Replace/RemoveCollider/SetClothTuning/SetFlareSkirtAClothが直接生成・変更・保存する（同:45-70、89-116）。Maker窓はInsideAndLoadedでF6/OnGUI（`Plugin.MakerWindow.cs:21-31`）。
- 停止・迂回: Controller.OnDestroyはClearFlareSkirt/ClearObjects（`ColliderCardController.cs:164-168、209-213`）。独立Controller.LateUpdateは0.5秒ごとに消えたコライダーを再作成しClothを更新（`ColliderCardController.FlareSkirt.cs:105-117`）するので、Plugin.Updateだけ停止しても復活する。
- 共通化・リスク: 登録/カード定義の保存は基盤、Maker編集とHの生成/物理実行は別許可へ分割する。停止してもカード定義を失わず、Unity生成物/元Rendererを復元し全Controllerを停止できる必要がある。Maker例外、カードロードがセッション開始前の場合、OnReload再生成、保存中の停止、Cloth/元スカートの重複、Maker窓の入力占有を確認する。

### 20. MainGameClothesLoadProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClothesLoadProbe/Plugin.cs:15>)

- 用途・プロセス: 衣装/アクセサリ/Texture生成の時間・GC・長フレームCSV診断。KKS（`MainGameClothesLoadProbe/Plugin.cs:15`付近）。H限定ではなくChaControl全般。
- 起動準備: AwakeでConfig、Enabled=falseなら戻る、trueならCSV作成と対象全overloadへHarmony Prefix/Postfix/finalizer（同:43-82）。初期OFF後にConfigだけONへ変えた場合の初期化はこの経路にはない。
- 実入口: BeginはEnabledと対象メソッド/trace期限からSampleを生成（同:90-116）。Updateはフレーム時間と手動キー、後刻Flush（同:139-159）。FinishExceptionはSampleがなくてもinstanceがあれば例外Queueし得る（同:131-135）。
- 停止・復元: OnDestroyはFlush/Unpatch/Instance解除（同:182-186）。ゲーム値を復元する用途ではなく、計測窓・pending CSV・タイマー・フックを停止/flushする必要がある。
- 共通化・リスク: 起動診断/通常キャラロードまでH限定にするかは別枠。許可開始で計測を起動し停止でpending扱いを決める構成は可能だが、ゲーム起動/Makerロードの診断を失わない例外が要る。Prefixで開始した計測が停止後のPostfix/finalizerから記録される経路も揃える。初期OFF/ON、Maker着替え、H終了中例外、CSVの停止時flushと計測への負荷を確認する。

### 21. MainGameClubLights

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClubLights/Plugin.cs:11>)

- 用途・プロセス: H内追加照明、標準照明override、Beat/動画/体位連動、VRギズモ。KKS/VR（`MainGameClubLights/Plugin.cs:11`付近）。
- 起動準備: Profiles/Configと動画イベント購読（同:47-57）。
- 実入口: Updateはゲート/Enabled後にScanHSceneしH内の照明/体位/Beat/VR操作を更新（同:77-99）。ScanHSceneは1秒ごとH探索しOnHSceneStart/Endを発火（同:151-167）。開始で標準照明cache/追加ライト生成（同:170-176）。LateUpdate/OnGUIも別入口（同:118-132）。
- 停止・復元: OnBlankRoomLeftはOnHSceneEnd、UI非表示、入力解放（同:106-115）。OnHSceneEndはライト破棄、標準照明override解除、キャラ/H参照、Beat状態を初期化（同:220-232）。OnDestroyは動画unsubscribe、入力/照明破棄、保存（同:135-141）。
- 迂回入口: Awakeで購読したBlankMapAdd.OnVideoLoadedが別通知入口で、callbackは動画pathを記憶して全light entryへPreset適用する（`Plugin.VideoRoomBridge.cs:9-43`）。PlaybackBar公開APIもある（`Plugin.PlaybackBarApi.cs:16`）。同イベントを許可中だけ購読するか、停止済み状態に書込まないよう揃える。
- 共通化・リスク: 独自ScanHSceneとBlankRoomGate監視を中央Start/Stopへ置換し、既存OnHSceneStart/Endを主要候補として使う。停止callbackは入力解放とUI/動画状態も含める。Astral抑止、標準照明復元、動画切替通知が終了と競合、VR掴み途中の終了、次Hのライト二重生成を確認する。

### 22. MainGameCoordinateAccessoryRestore

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCoordinateAccessoryRestore/Plugin.cs:10>)

- 用途・プロセス: コーデ変更後に全アクセサリを表示し、遅延ロード分にも一定時間再適用。KKS（`MainGameCoordinateAccessoryRestore/Plugin.cs:10`付近）。
- 起動準備: Enabled/期間Config、ChaControl.ChangeCoordinateType/SetNowCoordinateへHarmony（同:29-42）。
- 実入口: CoordinateChanged PostfixはStrict部屋条件と変更成功を確認して_pendingへ追加（同:45-55）。LateUpdateはpending対象へSetAccessoryStateAll(true)、期間内再試行（同:59-89）。Maker専用UIなし。
- 停止・復元: ゲート外/設定OFFではpendingを消す（同:61-62）。OnDestroyはUnpatch/pending clear（同:95-99）。表示前のboolを保存していないため、過去に強制表示したアクセサリを元の非表示へ戻す機能はない。
- 共通化・リスク: 開始済みHの対象だけPostfixで予約し、終了はpending/snapshot/参照を捨てる。遅延ロードの再適用は場所監視ではなく必要な処理として残す。Maker許可を追加するなら対象キャラ判定も分離する。終了同フレームの着替え、追加アクセサリslot、複数キャラ、同H内の再変更、期間終了後に処理が残らないことを確認する。

### 23. MainGameCoordinateCardSwap

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCoordinateCardSwap/Plugin.cs:14>)

- 用途・プロセス: Hの男女へ指定コーデカード適用/同じボタンで元コーデ復元。KKS（`MainGameCoordinateCardSwap/Plugin.cs:14`付近）。
- 起動準備: Config/キー/設定UIボタン（同:61以降）。
- 実入口: UpdateキーとConfig画面のボタンがToggleへ進み、Toggle自身もStrict/Enabledを確認する（同:131-150、171-204）。ApplyはReload後、元bytes/versionと適用先Actor/Hを保存（同:272-289）。
- 停止・復元: Restoreは保存Actorと現在Hの同一性、独立Actorでないことを確認してbytes復元し、不一致なら古いbackupを捨てる（同:293-317）。現行UpdateにH終了時の自動Restore/破棄は見当たらず、通常の手動復元が中心。
- 迂回入口: ConfigボタンはUpdateを止めてもToggleを呼べるため中央ランタイム状態を入口で揃える。公開静的変更API/Harmonyは確認していない。
- 共通化・リスク: H開始で対象参照とbackup寿命を開始、終了は復元するか単に破棄するか明示する。既存RestoreはFindObjectOfTypeで「現在H」を再判定するため、中央終了通知がH破棄後なら復元せず捨てる可能性がある。終了前復元を求めるなら保存Hで安全性判定する設計が必要。男女別状態、カード変更中/Reload例外、次Hへ古いbackupを持越さないこと、他着替えプラグインの再適用通知を確認する。

### 24. MainGameDisplacementMap

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDisplacementMap/Plugin.cs:8>)

- 用途・プロセス: 画面/キャラをcaptureし変位Mapで歪みoverlay、外部warp。KKS/VR（`MainGameDisplacementMap/Plugin.cs:8`付近）。
- 起動準備: Config/JSON/ログ（同:42-58）。
- 実入口: LateUpdateはBlankRoomGate.Tick→Enabled→camera/overlay解決→描画資源生成→EndOfFrameコルーチン開始（同:64-96）。capture子コンポーネントのOnRenderImage（`Plugin.Capture.cs:30`）とoverlay描画も別Unity入口。CaptureEndOfFrameはwhile(true)でEnabled/資源条件だけ確認し継続（同:78-100）。
- 停止・復元: StopRuntimeはsourceFrameCaptureのOwnerをnullにして破棄、captureコルーチン停止、overlay/capture camera/RT/mesh/material/displacementMapを解放（同:378-400）。OnDestroyもこれを呼ぶ（`Plugin.cs:99-103`）。
- 迂回入口: SetExternalWarpはInstanceだけ確認しApplyExternalへ進む（同:120-152）。ClearExternalWarpは外部フラグを消す（同:155-160）。StopRuntimeは外部フラグ/期限をここでは消さないため、外部要求の寿命も中央停止へ含める必要がある。
- 共通化・リスク: H開始でcapture/overlayランタイムを許可、終了で既存StopRuntimeと外部要求破棄、全描画入口を同セッション寿命へ統一。H存在スキャンをcamera選択補助で残す場合も場所許可の再判定として使わない。EndOfFrame中の終了、Owner解除とDestroy遅延、camera差替え、外部要求の次H持越し、GPU解放/描画残留を確認する。

### 25. MainGameDisplayEffectViewer

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:12>)

- 用途/プロセス: 通常本編の `ActionScene` 内の効果表示を列挙・再生・非表示・復元する調査UI。`KoikatsuSunshine`のみ。H専用ではない。
- 準備: Awakeで設定・logger・窓設定を準備。実入口はUpdateからのActionScene取得と定期再列挙、OnGUIの手動操作。[Plugin.cs:104](/F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:104)、[Update:150](/F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:150)。
- 終了: `ClearRuntimeContext` はエントリと一括再生snapshotを消すだけ。OnDestroyはlogger破棄のみ。表示変更を生存オブジェクトへ戻す終了処理と、snapshotを捨てる順序を明確化する必要がある。[復元:576](/F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:576)、[ActionScene探索/消去:664](/F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:664)。
- 非Update入口: OnGUIからRefresh/SetTopic等を直接実行。Harmony/コルーチン/APIの起動は本体で確認されない。
- 共通化範囲/個別課題: 常駐の設定準備とActionScene開始・終了通知へ寄せられる。H一回許可の対象にそのまま入れると本来の通常本編機能を失う。ActionSceneの破棄通知を入口として扱い、途中での毎フレーム再探索を移行後の起動根拠にしない。

### 26. MainGameDollMode

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:17>)

- 用途/プロセス: 追加部屋の人形モード（表情・視線・虹彩・ハイライト・他プラグイン抑止・モーション固定）。`KoikatsuSunshine`のみ。
- 準備: AwakeでInstance、AssemblyLoad通知、設定、Harmony guardを用意するが、末尾で `ApplyDollModeState("startup")` も実行する。これは設定準備に留まらない。[Awake:287](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:287)。
- 実入口: Update/LateUpdate、Camera.onPreCull、EndOfFrameコルーチン、設定変更、公開 `SetDollModeEnabled`。`ApplyDollModeState` のshouldEnableは設定だけで、H許可を参照しないため、Awake/API/config経由も開始対象に含める必要がある。[API:448](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:448)、[状態適用:1372](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:1372)、[描画:1784](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:1784)。
- 停止/復元: OnDestroyに描画購読解除、入退場遷移取消、EOF停止、元ハイライト復元、guard解除、設定通知解除がある。通常退室はUpdate/LateUpdateのreturnで、同じ完全停止処理を呼んでいない。[破棄:410](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:410)、[FinalizeDollModeExit:1724](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:1724)。
- 共通化範囲: H開始にguard有効化・描画購読・状態適用、H停止に遷移中断と元の表情/視線/目UV/ハイライト/他Behaviour状態の復元をまとめる。設定をOFFに書き換えず、ユーザーの希望値と実行中状態を分ける。
- 個別課題: 顔のフェード退出とH終了の即時停止は別扱いが必要。AssemblyLoadで後から追加されるguard、既に走るEOF、Camera callbackが停止後に再装着/上書きしないことまで確認する。特にEOFは設定だけを見てcontinue/実行を決め、部屋条件を検査しない。[EOF:1997](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:1997)、[guard patch:240](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/DollModeGuardHooks.cs:240)、[AssemblyLoad:19](/F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.OptionalTargets.cs:19)。

### 27. MainGameFaceSweatControl

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:17>)

- 用途/プロセス: 追加部屋の顔汗・頬赤手動調整。`KoikatsuSunshine`のみ。Awakeは設定/logger準備、Updateは窓開閉・対象再探索・入力占有、OnGUIは適用UI。[Plugin.cs:64](/F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:64)。
- 実入口: 公開 `ApplyToCharacter`/`ClearCharacter` はUI経由とは別に直接 `ApplyFaceState` に入る。後者にはH許可がなく、fileStatus/Siruも変更する。[API:130](/F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:130)、[適用:326](/F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:326)。
- 停止/復元: 現行退出は窓を閉じ入力占有を解除するのみ。ソースコメントは本体の毎フレーム復帰を前提にするが、fileStatusへ保存する値まで完全に戻ることは実機未確認。[退出:240](/F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:240)。
- 共通化範囲: H開始で対象参照を取得し、停止でUI/入力/対象cacheを解放。全適用入口にセッション状態を共通利用する。パッチ・専用コルーチンは確認されない。
- 個別課題: Clear APIは「元値復元」ではなくゼロ指定であり区別が必要。共有入力APIの矩形・ドラッグ・cursor解除を停止処理から確実に呼ぶ。値の持越しを許す仕様か、元値snapshotが必要か未決定。

### 28. MainGameFpsDisplay

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:9>)

- 用途/プロセス: 右上FPSを0.5秒平均で表示する常駐補助。`KoikatsuSunshine`/`KoikatsuSunshine_VR`。H限定条件がなく、タイトル/通常本編を含む表示範囲を維持する。[Plugin.cs:9](/F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:9)。
- 準備/入口: AwakeのVisible設定と通知登録、OnEnable/Focus/Pauseでサンプル初期化、Updateで採取、OnGUIで描画。公開 `ToggleDisplay`/`IsDisplayEnabled` はPlaybackBar等から呼べる。[Plugin.cs:21](/F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:21)、[Update:51](/F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:51)。
- 終了: OnDestroyで設定通知解除。ゲーム状態を変更せず、描画中のGUI状態はfinally復元する。停止時はサンプルをリセットすればよく、パッチ/コルーチン/外部処理はない。[Plugin.cs:99](/F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:99)。
- 共通化範囲/個別課題: 常駐スコープの開始/停止として登録し、H終了に紐付けない。Visibleとホストの実行許可を分け、停止中API toggleが実処理を開始しない状態にする。

### 29. MainGameFrameTimingProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:20>)

- 用途/プロセス: 限定時間の性能診断。通常は次のHを測るが `HSceneOnly=false` で非H測定可能。`KoikatsuSunshine`/VR。AwakeはRemaining、warmup、baseline、測定時間、timeoutを設定。[Plugin.cs:51](/F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:51)。
- 実入口: Updateのphase遷移でH探索→warmup→baseline→動的Harmony設置→測定。焦点喪失pause/resumeとtimeoutもある。これは診断機能の進行でありH許可の再監視と区別する。[Update:68](/F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:68)、[Install:171](/F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:171)。
- 停止/復元: `Finish` が計測停止、native sampler Dispose、個別パッチ解除、snapshot報告をTask.Runで保存、Remaining消費。OnDisable/Destroyからも呼ばれる。[Finish:234](/F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:234)。
- 共通化範囲: H測定は開始通知からH参照を受ける。非H診断は独立スコープを維持。停止はFinishを再利用可能だがwarmup中断時はRemainingを消費しない既存契約を維持する。
- 個別課題: logger writerは停止後も完了する。Unityを参照しない確定snapshotの保存は許せるが、次セッションのwriter/phase管理とエラー報告を失わないこと。診断が対象プラグインの開始/停止メソッドもpatch対象とする可能性と、パッチ残留時のfail-closed処理を検討する。

### 30. MainGameFreeHMasturbationMenu

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:16>)

- 用途/プロセス: 追加部屋内FreeHのメニュー・ポーズ・速度/ゲージ制御。`KoikatsuSunshine`のみ、`FreeHOnly` 設定を維持。[CanRun:264](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:264)。
- 準備/実入口: Awakeで設定とassembly Harmonyを設置。Updateで0.5秒探索、ボタン生成、mode書換え、マウスホイール速度、ゲージ固定。HSprite.Update、HFlag.MaleGaugeUp、HMasturbation.Procのpatchは別入口。[Awake:92](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:92)、[Update:138](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:138)、[patch:1423](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:1423)。
- 停止/復元: CanRun失敗時のmode解除/ゲージlock解除/UI非表示は存在するが、BlankRoom外ではその前にreturn。OnDestroyはunpatchと自作UI削除のみ。速度復帰関数は別にある。[破棄:121](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:121)、[速度復帰:1051](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:1051)、[UI削除:1360](/F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:1360)。
- 共通化範囲: H開始でisFreeHと許可を確定、patch/button listenerも同じセッションへ所属。停止で速度・mode一時状態・ゲージlock・pending selectionを先に解除し、UI破棄と参照解放を行う。
- 個別課題: メニュー/ゲージを強制表示した状態の元値復帰、Animator速度1f復帰と本来の速度snapshotの違い、破棄時点でH参照がまだ利用可能か未解決。パッチが設定と部屋条件だけで処理を続けないようにする。

### 31. MainGameGlow

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:11>)

- 用途/プロセス: 追加部屋向け撮影カメラと画面glow、外部Glow API、VR左右描画。`KoikatsuSunshine`/VR。Awakeは設定読込だけでなくApplyConfigでrender資源準備にも入る。[Plugin.cs:73](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:73)。
- 実入口: Updateのcamera/drawer同期、LateUpdateからEOF capture、APIからexternal glow、カメラに足したMono/VR drawerのOnPostRender/OnRenderImage。[Plugin.cs:95](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:95)、[Plugin.Overlay.cs:14](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.Overlay.cs:14)、[API:48](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.Api.cs:48)。
- 停止/復元: `StopRoomGlow` がEOF停止、外部glow終了、除外renderer復元、drawer撤去、postprocess無効化。OnDestroyにはpipeline/camera/RT/左右RTの最終破棄もある。[停止:214](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:214)、[破棄:251](/F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:251)。
- 共通化範囲: セッション開始に資源/描画接続、終了にStopRoomGlow。再使用資源を残すか終了時解放するかを明記し、設定変更ApplyConfigで停止中にcamera/drawerを作り直さない。
- 個別課題: `Destroy(drawer)`はフレーム末まで遅延するため停止時点でenabled/Ownerも先に切る必要がある。EOFとVR callbackも保存済みセッション状態で無効化する。外部APIからの終了要求はH許可なしで受理し、開始要求だけ許可されたセッションへ制限する。

### 32. MainGameHandySync

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.cs:13>)

- 用途/プロセス: H動作を外部機器へ送るHAMP/HDSP/旧v2制御。`KoikatsuSunshine`/VR。Awakeはconfig、Harmony、HTTP client、LM analyzer/strokerと機器設定変更通知を準備する。[Plugin.cs:86](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.cs:86)。
- 実入口: HScene Start/OnDestroy postfix、UpdateからH参照fallback探索とHAMP tick/v2送信、StrokeLoopとLM解析子コルーチン、設定変更によるclient再構成、公開APIによる機器停止/再開、接続テスト。[H通知:19](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HScene.cs:19)、[Update:220](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.cs:220)、[API:51](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.Api.cs:51)。
- 現行停止の穴: `OnBlankRoomLeft` はstroke停止/analyzer消去のみでHAMP停止を送らない。H OnDestroy postfixも先頭部屋判定で参照クリアを飛ばし得る。通常 `StopHampIfRunning` はcooldownで停止を飛ばせるため退出の最終停止には不足。[退出:276](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.cs:276)、[H終了:39](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HScene.cs:39)、[停止:225](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HampBpm.cs:225)。
- 非同期残留: SendXptAsync finallyはpending位置指令を再帰送信し、HAMP Pumpはmode/start/stop/velocity/strokeのqueueを順にawait送信する。H終了によるqueue消去/世代検査/HTTP取消は見当たらず、単にUpdateを止めても送信が続く。[SendXpt後処理:181](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HandyClient.cs:181)、[Pump:224](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HandyClient.cs:224)、[Dispose:480](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.HandyClient.cs:480)。
- 共通化範囲: 開始時にH/flagsを渡しfallback探索を廃止。停止で新規送信を遮断→LM子解析を含むcoroutine停止→pending/queue破棄→cooldownを無視した最終機器停止→HTTP後処理無効化→参照解放、という専用順序が必要。
- 個別課題: `ForceHampStopInternal` は既にあるが、停止指令自身を通常指令キャンセルと一緒に消さない設計が必要。進行中HTTPが停止指令より後に機器へ届く競合、token再発行/設定再接続/外部API再開、v2 fire-and-forgetも対象。HTTP Disposeの呼出元やunpatch/設定通知解除をまとめたplugin終了処理は未整備。機器が実際に止まることは未確認。[強制停止:74](/F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.Api.cs:74)。

### 33. MainGameHHandPoseProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:15>)

- 用途/プロセス: 追加部屋内H手ポーズ検証UI。`KoikatsuSunshine`/VR。Awakeはlog/UI/config custom drawerを用意。[Plugin.cs:56](/F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:56)。
- 実入口: OnGUIで手ポーズ操作、窓描画時のH/キャラ0.25秒再取得、ConfigManagerの窓開閉。Updateは門番と入力占有。[OnGUI:83](/F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:83)、[Update:96](/F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:96)、[対象探索:128](/F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:128)。
- 停止/復元: 現行退出とOnDestroyは入力占有解放。手ポーズ適用値を元へ戻すsnapshot復元が本体終了処理にない。Harmony/専用coroutine/公開APIの起動は確認されない。[解放:449](/F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:449)。
- 共通化範囲: 開始時にH/キャラ参照を取得し、停止で窓と入力/対象cacheを解放。停止後ConfigManagerから窓を開いても描画/適用を始めないよう実行状態を利用する。
- 個別課題: ツールが意図したポーズ変更を残すか終了復元するか仕様が未決定。元値が必要なら変更前に保存し、生存キャラへ終了時復元する。

### 34. MainGameHScenePoseInjector

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.cs:15>)

- 用途/プロセス: 追加部屋のHポーズ/表情/首追従/値・効果chを適用する基幹。`KoikatsuSunshine`のみ。Awakeは設定とUnifiedUi tab登録。[Plugin.cs:37](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.cs:37)。
- 実入口: Updateはtab登録/H探索/自動cycle、LateUpdateは表情/首/pose blend/value/effectを上書き。API `SetCharacterEffect`、reflectionで呼ばれるApplyPose/RestoreAllSnapshots、tab callbackも対象。[Update/Late:64](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.cs:64)、[H探索:49](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.Apply.cs:49)、[Effect API:35](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.EffectApi.cs:35)。
- 停止/復元: `LeaveAllPoseRoom` にblend解除、Animator snapshot復帰、首/表情/値/効果元値復帰、H参照消去がまとまっている。OnDestroyでも呼ぶ。[Plugin.RoomGate.cs:24](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.RoomGate.cs:24)、[Animator復帰:234](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.Apply.cs:234)。
- 共通化範囲: 開始にH参照/セッション設定、停止にLeaveAllPoseRoomを移せる。0.5秒H探索とH消失確認delayは終了通知によるcleanupへ置換する。Animator controller変更の確認はポーズ管理用なので、許可再審査と混同しない。
- 個別課題: tabは毎Update登録、破棄時のUnregisterが本体にないためcallback寿命も整理する。撮影/semantic等の依存側が復元を呼ぶため、停止後Restoreは許容しApplyは拒否する。撮影停止→撮影固有の復元→Injector最終復元の順序が必要。[tab:16](/F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.UnifiedUiBridge.cs:16)。

### 35. MainGameKaleidoscope

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:11>)

- 用途/プロセス: 思考演出と手動万華鏡表示、残像連携、音量ducking。`KoikatsuSunshine`のみ。`ManualAcrossRooms` と `_manualActive` は部屋外でも手動表示を保てる仕様で、H限定へ一律変更できない。[Update:61](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:61)、[設定:153](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:153)。
- 準備/入口: Awakeでrenderer invalidationパッチ/scene通知、config、audio、shader bundle。Updateのtab/遅延保存/fade/残像mask/screen effect、LateUpdate ducking、公開SetThoughtActive/ToggleManualActive、ScreenDrawer.OnRenderImage、非同期renderer変更のIEnumerator wrapperが別入口。[Awake:42](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:42)、[renderer wrapper:72](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.RendererCache.cs:72)、[ScreenDrawer:18](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.Rendering.cs:18)。
- 停止/復元: OnDisableは残像mask解除・screen資源破棄・音量復元、OnDestroyはさらにpatch/scene解除/tab解除/config保存/bundle解放。[Plugin.Rendering.cs:371](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.Rendering.cs:371)、[OnDestroy:112](/F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:112)。
- 共通化範囲: Hの思考演出スコープと常駐手動表示スコープを分離。H終了でthoughtとduckingを止め、手動持越し設定を維持する。共通の全停止ではOnDisable相当を使い、手動/Hの片方の停止で共有RTを他方の描画中に破棄しない。
- 個別課題: H終了時fade-out中も描画が走る現仕様の扱い、audio元値の所有権、パッチ由来の遅延cache更新、Config.SettingChanged匿名通知の解除を整理する。

### 36. MainGameLiveLookCamera

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:17>)

- 用途/プロセス: 追加部屋のカメラ/ボーン追従・FOVプリセットUI。`KoikatsuSunshine`のみ。Awakeは設定/cacheとCamera.onPreCull購読。[Plugin.cs:96](/F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:96)。
- 実入口: Updateは対象cache/drag/入力、LateUpdateはカメラ姿勢/FOV適用、OnGUIは操作、Camera.onPreCullは別の再適用入口。後者にBlankRoomGate条件がない。[Update:135](/F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:135)、[描画callback:155](/F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:155)。
- 停止/復元: OnDestroyで描画購読解除/入力解放/保存。通常退出はUpdate/Late/GUIのreturnのみで、入力占有や `_boneLookActive` を解除しない。`ClearBoneLook` は追従状態を消すが元カメラ姿勢/FOV snapshot復帰ではない。[OnDestroy:126](/F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:126)、[ClearBoneLook:534](/F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:534)。
- 共通化範囲: H開始でcamera/actor参照と描画購読、停止でまずcallback/追従を止め、入力解放、参照破棄。H中のcamera対象更新は用途上必要でも、許可判定にしない。
- 個別課題: カメラを最後の位置に残す仕様か元へ戻す仕様か未決定。停止中preCullが書き込まないことと、ドラッグ中の退出/非表示/フォーカス喪失の入力解放を確認する必要がある。

### 37. MainGameLogRelay

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:8>)

- 用途/プロセス: ログ出力とowner別出力設定の常駐基盤。`KoikatsuSunshine`/VR/`CharaStudio`。H開始前/終了後のログも受けるためHセッション停止で落としてはいけない。[Plugin.cs:8](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:8)。
- 準備/入口: Awakeで保存先/設定/OwnerFileLogger/owner ruleを準備。毎フレーム処理はなく、公開Log/LogLazyとowner設定APIから駆動される。現在 `_runtimeLoggingEnabled` で出力抑止する。[Awake:50](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:50)、[Log/LogLazy:86](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:86)、[LogRelayApi.cs:24](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/LogRelayApi.cs:24)。
- 停止/復元: OnDestroyは設定保存/Instance消去。ownerのruntime overrideをClearするAPIがある。Hの終了ログを排出してから常駐基盤を停止する順序が必要。[OnDestroy:79](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:79)、[override解除:191](/F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:191)。
- 共通化範囲/個別課題: 起動基盤スコープとして登録可能。各H pluginが設定した一時owner overrideはそのplugin停止で解除するが、ログ基盤自体は継続する。常駐API呼出と保存の排他・停止後APIの扱いは確認が必要。

### 38. MainGameMapOverlayProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:11>)

- 用途/プロセス: 通常Hへ既存mapをadditiveロードし、元map renderer/lightを隠す検証用。`KoikatsuSunshine`のみ。追加部屋限定とは現ソースに書かれておらず、Hの範囲を勝手に狭めない。[Plugin.cs:22](/F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:22)。
- 準備/入口: AwakeでsceneLoaded購読、UpdateのF10/F11/F12、LoadTargetのH/BaseMap検査、遅延OnSceneLoadedでmap整列・元renderer無効化。[Update:33](/F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:33)、[sceneLoaded:67](/F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:67)。
- 停止/復元: `UnloadTarget` にadditive scene unload、元renderer/lightのenable復帰、bundle unloadと参照消去があるが、OnDestroyは通知解除だけでUnloadTargetを呼ばない。[OnDestroy:28](/F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:28)、[UnloadTarget:105](/F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:105)。
- 共通化範囲: H開始で操作を許可、停止でロード要求を失効させて元表示を復元し、scene/bundleを確実にunloadする。
- 個別課題: 停止後ロード完了してOnSceneLoadedが走る競合を世代で拒否し、そのsceneを片付ける必要がある。保存元値は「当初enabledだけ」のリストで、停止で一律trueにする復帰契約が正しいか未検証。

### 39. MainGameMapSandbox

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMapSandbox/Plugin.cs:8>)

- 用途/プロセス: 共有MapEnvironmentRuntimeをmapへattachする環境基盤。`KoikatsuSunshine`/VR。自身にAwake/Updateはなく、公開 `AttachMap` が開始入口。これはH開始より前の部屋生成にも使えるためH許可で遮断しない。[Plugin.cs:14](/F:/kks/work/plugins/canon_plugins/MainGameMapSandbox/Plugin.cs:14)。
- 準備/入口: getter群がContent parametersを読む。AttachMapでSandboxSessionをAddComponent/Initializeし、失敗時DestroyImmediateで巻き戻す。[AttachMap:28](/F:/kks/work/plugins/canon_plugins/MainGameMapSandbox/Plugin.cs:28)。
- 停止: OnDisableはWorldとsessionをDestroy。下位sessionにはUpdate、HTTP listener、撮影coroutine、OnDestroyが存在し、起動/停止対象はplugin本体だけではない。[Plugin.cs:22](/F:/kks/work/plugins/canon_plugins/MainGameMapSandbox/Plugin.cs:22)、[SandboxSession.cs:86](/F:/kks/work/plugins/canon_plugins/MapEnvironmentRuntime/SandboxSession.cs:86)、[終了:491](/F:/kks/work/plugins/canon_plugins/MapEnvironmentRuntime/SandboxSession.cs:491)。
- 共通化範囲: map所有者のattach/detachスコープへ寄せる。Hの拒否でも環境生成自体に必要な部品は維持し、H処理とは別の開始許可を持つ。
- 個別課題: 下位の撮影/HTTP commandが終了後にsession/worldを復活させないよう停止通知と取消を流す。島の可視性にはCamera callback前後のrestoreもあり、World/session破棄だけで安全と決めない。[SandboxIslandVisibility.cs:149](/F:/kks/work/plugins/canon_plugins/MapEnvironmentRuntime/SandboxIslandVisibility.cs:149)。

### 40. MainGameMilkSpray

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:14>)

- 用途/プロセス: 追加部屋Hの状態/衣服/アニメ位相またはボーン移動から液体を生成・描画。`KoikatsuSunshine`のみ。Awakeは設定/performance loggerとscene/config通知。[Plugin.cs:47](/F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:47)。
- 実入口: Updateは計測枠、LateUpdateCore→DetectShotでH参照探索/shot要求、液体simulation/render。Hモード・ゲージ・衣服・pauseは機能判定として維持する。[DetectShot:57](/F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:57)、[LateUpdateCore:261](/F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:261)。
- 停止/復元: ResetSceneでstroke/liquid/キャラ骨cache消去。scene change/unload、OnDisable、OnDestroyの処理は分散し、OnDisableはrendererをClearするがDisposeしない。[ResetScene:322](/F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:322)、[OnDestroy:334](/F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:334)。
- 共通化範囲: H開始時のH/キャラ参照を使い0.5秒のH再探索を置換。停止でshot要求→liquid→renderer→骨cacheの順に解除し、破棄時はlogger/通知も解放する。
- 個別課題: liquid mesh/render資源をセッション間に保持するか破棄するか明記。scene通知で別additive sceneのunloadまでResetする現挙動との違いを確認する。

### 41. MainGameObjectComposer

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:13>)

- 用途/プロセス: 追加部屋の配置物・motion・軌道・gizmo・女性透過・BetterPenetration追従。`KoikatsuSunshine`/VR。Awakeは設定/layout/preset読込とBetterPenetration patch/helper、診断Camera通知を準備する。[Plugin.cs:132](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:132)。
- 実入口: Updateはruntime解決/配置/プリセット/移動/各driver、LateUpdateはUI/diagnostics。公開object/movement/motion intensity API、Unity子のlate follow、SetDanTarget patch、Camera診断preCull/postRenderが別入口。[Update:187](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:187)、[follow patch:62](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.BetterPenetrationFollow.cs:62)、[公開API:22](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.PublicApi.cs:22)。
- 停止/復元: OnBlankRoomLeftは女性材質復元/weight消去/penetration対象復帰/配置実体破棄。OnDestroyはさらにmovement edit、follow、UI/外部入力、gizmo、保存、diagnostics解除。退出だけでは後者まで実行しない。[退出:178](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:178)、[破棄:243](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:243)。
- 共通化範囲: データ読込は起動準備、runtime build/follow/gizmo/描画購読はH開始へ。停止で入力/gizmo・遅延queue・各driverを止め、元材質/penetrationを復元して実体を破棄。layout/presetは保持する。
- 個別課題: late follow子のpending queueとCamera通知、公開APIから停止後のruntime再構築、ドラッグ中未保存preset flush、編集終了の保存/取消を扱う必要がある。診断描画はMotionTraceEnabled時のみでもセッション寿命に含める。[MotionDiagnostics.cs:34](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.MotionDiagnostics.cs:34)、[女性復元:165](/F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.FemaleGhost.cs:165)。

### 42. MainGamePortraitCapture

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.cs:15>)

- 用途/プロセス: 追加部屋でのポートレート撮影/連続撮影と一時的なカメラ・首/目・頭傾き変更。`KoikatsuSunshine`のみ。
- 準備/入口: Awakeの設定とCamera.onPreCull購読、Update hotkey、公開TryCapture/TryCaptureMany、LateUpdate/preCullでrender直前上書き。RoomBoundCoroutine+epochで撮影を包んでいる。[Plugin.cs:49](/F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.cs:49)、[API:137](/F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.cs:137)。
- 停止/復元: `CancelRoomCapture` は各wrapperを明示Dispose→StopAllCoroutines→override/busy解除。撮影finallyは首/目/カメラsnapshot復元、注視target削除。通常完了時のカメラを戻さない設定は、中断時には適用しない。[取消:59](/F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.cs:59)、[撮影finally:210](/F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.Capture.cs:210)。
- 共通化範囲: 既存epochをcanon_gateセッションに接続し、Updateの部屋再検査を停止通知へ置換。描画購読も許可セッション開始/停止へ移せる。
- 個別課題: 外部Semanticから予約済み撮影を受けないこと、撮影wrapperが追加された直後のcancel、カメラFOV保持子routineまでDispose対象となることを確認。Injectorを先に復元/破棄すると撮影finallyの復元先を失うため停止順序が必要。

### 43. MainGamePoseCatalogCapture

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:18>)

- 用途/プロセス: 追加部屋の全ポーズをPoseInjector経由で適用し、正面/側面/真上を連続撮影してmanifest保存。`KoikatsuSunshine`のみ。Awakeは設定とhotkey準備。[Plugin.cs:43](/F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:43)。
- 実入口: Updateはhotkeyで裸の `StartCoroutine(CaptureAllPoses())`、cancelはフラグ設定。撮影開始直後に一度だけBlankRoomを検査するが、長いpose loop/settle待機後に終了世代確認がない。[Update:59](/F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:59)、[撮影:75](/F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:75)。
- 停止/復元: finallyにInjector.RestoreAllSnapshotsとcaptureCamera削除、各RenderTexture/Textureにもfinally復帰がある。ただしOnDisable/OnDestroyの明示取消/列挙子Disposeはなく、Unityの暗黙停止だけでfinally実行保証としない。[batch finally:192](/F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:192)、[RT復帰:298](/F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:298)。
- 共通化範囲: セッション開始で操作可能、停止時は明示Dispose可能な撮影wrapperを用いて復元を完了させる。待機から復帰する前の処理取消はセッション生存検査であり、H許可の再監視ではない。
- 個別課題: 撮影が使うInjector元snapshotの所有範囲、通常完了/手動取消/H停止で残すmanifestの状態、最後のposeからの復帰先を整理する。停止順序はCatalog撮影→Injector。撮影が実際に元ポーズへ戻ることは未確認。

### 44. MainGamePregnancyPlusBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:13>)

- 用途/プロセス: 追加部屋でPregnancyPlusの腹形状/膨張値を設定し、H動作距離からBellyBoko強度を出すbridge。`KoikatsuSunshine`/VR。
- 準備/入口: Awakeは多数config/JSON/preset、SettingChanged、外部型の反射bind。Updateはbridge再bind、腹強度更新、一定間隔で全キャラへのMeshInflate呼出。PublicAPIの強度取得と設定editor通知も対象。[Awake:104](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:104)、[Update:236](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:236)、[適用:450](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:450)。
- 停止/復元: 部屋外Updateはruntime override・強度tracking・H/キャラcacheを消しdirtyにするが、外部PregnancyPlusへ適用済みの元mesh/dataを戻していない。OnDestroy/OnDisableの統合復元経路も見当たらない。[Plugin.cs:238](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:238)、[強度reset:438](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.BellyBoko.cs:438)。
- 共通化範囲: 設定/JSONは起動準備、H参照とキャラ対象取得/外部適用を開始通知へ、強度公開値と一時override解除を停止へまとめる。
- 個別課題: 元PregnancyPlus状態snapshot/APIが取得可能か、形状変更を永続希望とするか一時上書きとするか未決定。単に強度をゼロにしても設定形状自体の復元ではない。設定イベントの直接適用経路も停止中の実適用を防ぐ必要がある。公開距離取得APIが自前でHを探索し、staticのOnBellyBokoPeak通知もあるので、読み取りAPIによる停止後の参照再取得と通知購読寿命も対象となる。[Plugin.PublicApi.cs:18](/F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.PublicApi.cs:18)。

### 45. MainGameRhythmSync

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.cs:15>)

- 用途/プロセス: BPM/速度/男女同期を統合。`KoikatsuSunshine`/VR。BepInEx本体以外にBeat/Speedの2つのMonoBehaviourが実ロジックを持つ。
- 準備: 本体Awakeで旧ファイルmigrationとconfig/logger、BeatController/SpeedController.Hostを設定し、AddComponentして双方Awakeを即実行、assembly全体Harmony設置。[Plugin.cs:36](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.cs:36)、[AddComponent:66](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.cs:66)。
- 実入口: Beat.Updateはtap/H探索/audio解析/VideoRoom/BPM/モーション切替、Speed.UpdateはBPM測定/参照/VideoCue/ゲージ/字幕キー/UI/H探索。本体GroupSyncはHScene.Update prefix/postfix/LateUpdate patchから男女アニメ位相を書き換える。Speed/PatchesとWheelClimaxTraceも別入口。[Beat/Plugin.cs:136](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Beat/Plugin.cs:136)、[Speed/Plugin.cs:205](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Speed/Plugin.cs:205)、[group patch:328](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.GroupSync.cs:328)。
- 停止/復元: 本体OnDisable/DestroyとHScene destroy prefixはReleaseGroup（shared speed1、actor/item/clock/sceneリセット）。しかし親Behaviourを無効にしてもBeat/Speedは別componentとして稼働する。Speed.OnDestroyはInstance消去のみ。[ReleaseGroup:305](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.GroupSync.cs:305)、[Speed破棄:200](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Speed/Plugin.cs:200)。
- 共通化範囲: 本体/Beat/Speedを一つの所有セッションで開始し、同時に停止する。H参照を子へ渡してScanHScene/0.5秒再探索を置換、全patch/APIは同じ実行中状態を参照する。
- 個別課題: Beat音声解析やVideoRoom再取得の停止、SpeedのBPM測定・timeline gauge transition・UI入力解除・速度baseline復帰を個別に実施する。RestoreVanillaSpeedBaseline/StopBpmMeasure/StopTimelineGaugeTransitionは既存部品だが、終了経路へ統合されていない。[baseline:20](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Speed/Plugin.VanillaSpeed.cs:20)、[BPM停止:132](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Speed/Plugin.BpmMeasure.cs:132)、[timeline停止:247](/F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Speed/Plugin.VideoCues.cs:247)。

### 46. MainGameSemanticPoseBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:22>)

- 用途/プロセス: 追加部屋で自然文をembedding検索しPoseInjectorへ適用、任意でPortrait撮影を遅延予約。`KoikatsuSunshine`のみ。Awakeは設定/pose indexと診断準備。
- 実入口: 公開TryRequestPose/TryApplyNamedPoseByName、Task.Run→HTTP embedding→mainThreadActions→pose適用、CaptureAfterPoseDelay。Updateはqueue消費と退出時epoch++/queue破棄/StopAllCoroutines。[API:98](/F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:98)、[検索task:239](/F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:239)。
- 停止/復元: 既存epoch検査は古い検索結果/遅延撮影の適用を防ぐ。HTTP自体は止まらず、OnDestroyはInstance消去のみでepoch増加/queue消去をしない。[Update:80](/F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:80)、[遅延撮影:299](/F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:299)、[HTTP:385](/F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:385)。
- 共通化範囲: epochをセッション寿命へ統合し、停止通知で結果適用/遅延撮影を失効、queue/busyを消す。要求受付時のH許可は保存された許可を使う。
- 個別課題: taskの取消/HTTP abort、停止直後enqueueされたclosureの残留、plugin破棄後callbackを明示管理する。適用済みposeの復元はInjector、開始済み撮影の復元はPortraitが所有するので、Bridgeだけ停止して復元済み扱いにしない。

### 47. MainGameSpeedLimitBreak

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/Bridge/Plugin.cs:9>)

- 用途/プロセス: 現行は互換API bridge。`KoikatsuSunshine`/VR、RhythmSync依存。csprojがDefaultCompileをfalseにし、`Bridge/Plugin.cs`だけを明示Compileするため、直下旧Plugin/Patchesを現行動作として扱ってはいけない。[csproj:8](/F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/MainGameSpeedLimitBreak.csproj:8)、[Compile:17](/F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/MainGameSpeedLimitBreak.csproj:17)。
- 準備/入口: AwakeのInstance/反射config field取得、Settings getter、ApplyTapBpm、BpmFromGauge、ShowUiNoticeがRhythmSync内 `MainGameSpeedLimitBreak.Plugin` へ転送。Update/独自patch/coroutineはない。[Bridge/Plugin.cs:27](/F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/Bridge/Plugin.cs:27)、[API:43](/F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/Bridge/Plugin.cs:43)。
- 停止: OnDestroyはInstance消去のみ。実速度状態/測定/UIの停止復元はRhythmSync/Speed側の責任である。[Bridge/Plugin.cs:67](/F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/Bridge/Plugin.cs:67)。
- 共通化範囲/個別課題: 互換窓口は常駐で維持し、変更APIはRhythmSyncの実行中セッションへ委譲する。bridgeのRegister追加だけで速度本体の移行完了と数えない。反射取得の起動順失敗と終了後の転送拒否を検証する。

### 48. MainGameStudioItemSummon

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.cs:9>)

- 用途/プロセス: 名前はStudioでもBepInProcessは`KoikatsuSunshine`のみ。Studio item catalogを本編で読み、カメラ前へ複製する手動ツールで、CharaStudio用とは扱わない。[Plugin.cs:9](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.cs:9)。
- 準備/入口: Awakeで設定/log/ConfigManager操作を準備。Update/OnGUI自動処理は撤去済み。config custom drawer/SettingChangedからEnsureCatalog→BuildCatalogCoroutine、SummonByNameからCommonLib.LoadAssetとobject生成。[Awake:26](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.cs:26)、[config:14](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.Config.cs:14)、[catalog:29](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.Catalog.cs:29)、[召喚:13](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.Summon.cs:13)。
- 停止/復元: ClearSummonedは生成物をDestroyできるが、Plugin本体にOnDisable/OnDestroyから呼ぶ終了経路がない。catalog構築にはfinallyがあるが明示取消/世代は見当たらない。[削除:99](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.Summon.cs:99)、[catalog finally:189](/F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.Catalog.cs:189)。
- 共通化範囲: catalog準備は常駐手動操作として扱い、生成物は生成先scene/ownerの寿命へ所属させる。現行にH限定制約がないためH専用へするかは未決定であり、推測で追加部屋限定にしない。
- 個別課題: summon実入口が現在どのUI/外部から到達可能か、catalog構築中の停止取消、停止後遅延asset完了、Instance消去と生成物片付けを補う。共有asset loaderの資源所有も確認が必要。

### 49. MainGameSubCameraDisplayProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubCameraDisplayProbe/Plugin.cs:16>)

- 用途/プロセス: サブカメラ、ディスプレイ、録画、外部キャラカメラ貸出。KoikatsuSunshine/VR（`MainGameSubCameraDisplayProbe/Plugin.cs:16`）。起動時は設定読込、人物表示キャッシュ無効化パッチ、Config変更購読、出力先準備、ApplyRuntimeStateまで実行する（同`:117`, `:125`, `:136`, `:158`）。設定準備と実体生成を分ける必要がある。
- 実処理入口: UpdateでBlankRoomGate.Tick後、カメラ生成・追従・VR掴み・録画・入力捕捉（同`:175–233`）。停止はOnBlankRoomLeftの録画停止/DestroyProbe/UI非表示（同`:237`）、破棄時は入力解放、人物表示フック解除も行う（同`:161`）。canon_gateの停止で通常のDestroyProbe系と入力解放を必ず呼び、Updateが呼ばれない状況でも停止を完結させる。
- 迂回: Config.Enabled変更がApplyRuntimeStateを直接呼ぶ（同`:136–142`）、プリセット/RT取得/UI表示/一時停止API（`MainGameSubCameraDisplayProbe/Plugin.Api.cs:8`, `:36`, `:67`, `:72`）、外部Acquire/SetPose/Follow（`Plugin.ExternalActor.cs:8–11`）。ActorLeaseValidは既存の部屋判定も参照（同`:93`）。TryGetKaleidoscopeSourceはカメラ有効性・一時停止を確認するが許可状態は直接見ない（`Plugin.Api.cs:85–99`）。API/Configからの生成開始にも確定済みセッションを使い、停止で貸出トークンを失効させる。
- 個別課題/根拠: 人物限定描画のマテリアル/レンダラ差し替えと外部レンダースコープの復元を残す（`Plugin.ExternalActor.cs:95–98`, `:318`、`Plugin.CharactersOnly.cs:170`）。登録のみでは録画・貸出・描画フックは停止しない。録画終了・他プラグイン貸出中終了の実機確認が必要。

### 50. MainGameSubtitleCore

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubtitleCore/Plugin.cs:21>)

- 用途/プロセス: 字幕表示、入力パネル、外部テキスト転送、マイク送信/direct-change状態同期。本編（`MainGameSubtitleCore/Plugin.cs:21`）。Awakeは設定・入力状態読込、NotifyMicSendState/RequestDirectChangeState、SubtitleApiの字幕sink/転送sink登録（同`:182–199`）。マイクとdirect-changeのポーリング・再送は部屋判定より前で動く（同`:222–243`）。これをH限定停止へ巻き込むと外部状態同期が止まる。
- 実処理入口: 部屋判定後の入力パネル・外部字幕キュー消化・テスト字幕（同`:243–272`）とOnGUI（同`:275`）。破棄時はCanvas/所有EventSystem破棄と両sink解除（同`:202–219`）。H停止用には表示非表示、入力捕捉/テキスト入力状態解放、保留字幕破棄を独立した停止入口にまとめる必要がある。
- 迂回: SubtitleApi.TryShowはsink.EnqueueSubtitleへ直接渡す（`MainGameSubtitleCore/SubtitleApi.cs:84–104`）。EnqueueSubtitleはH判定なしに最大128件のキューへ複製保存（`Plugin.cs:3239–3270`）。TryForwardTextは転送sinkへ直接入り（`SubtitleApi.cs:112–136`）、ForwardTextはTryForwardInputTextを呼ぶ（`Plugin.cs:3273`）。字幕表示の許可と、H外でも必要な転送/マイク制御を分離する。
- 個別課題/根拠: Updateを単に止めるとAPI受付だけ残って、次の許可Hで古い字幕が再生され得る。終了時キュー破棄/世代紐付けと受付の応答仕様が必要。マイク切替はToggleMicSend→状態通知（同`:2646`, `:2675`）。H終了で勝手に送信設定を変更する設計とは分けて、入力欄が残らないことと状態同期継続を別々に確認する。

### 51. MainGameSubtitleEventBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubtitleEventBridge/Plugin.cs:21>)

- 用途/プロセス: 外部HTTP相当のTCP受付からSubtitleApiへ字幕投入しチャイムを鳴らす本編サービス（`MainGameSubtitleEventBridge/Plugin.cs:21`, `:278`, `:287`）。Awakeで設定、NotificationAudioPlayer、StartServerを起動（同`:79–98`）。StartServerはEnabledと既存listenerだけを見てスレッド開始する（同`:140–167`）。
- 実処理入口: サーバースレッドの要求処理→SubtitleApi.TryShow（同`:278`）と、Updateで部屋判定後のDrainQueuedChimes（同`:108–118`）。OnDestroyはStopServer/音声player.Dispose（同`:101–105`）。StopServerはlistener停止・join/interrupt（同`:174–199`）。H表示を止める場合もサーバーの健康確認/受付維持を常駐サービスとするか、H開始/終了で開閉するかを仕様として決める必要がある。
- 迂回/個別課題: Updateを止めてもサーバーは字幕APIへ投入し続け、チャイムもキューへ溜まる（同`:521–535`）。停止時のチャイムキュー破棄、再生停止、旧Hリクエスト排除が必要。HTTP受付成功と字幕のH許可を混同しない応答を設ける。再起動はReloadSettingsからもStartServerへ到達（同`:127–136`）。登録のみでは受信スレッドは止まらない。

### 52. MainGameTransformGizmo

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameTransformGizmo/Plugin.cs:10>)

- 用途/プロセス: 共有3D移動/回転/拡縮ギズモと入力捕捉。本編（`MainGameTransformGizmo/Plugin.cs:10–13`）。Awakeはログ設定、実処理はPlugin.UpdateのGizmoInputCaptureRuntime.Tick（同`:25–43`）。OnDestroyはShutdown（同`:46–51`）。この共有サービスの初期化と、個々の所有者が生成したギズモのH稼働を分ける。
- 迂回: 各TransformGizmo自身がMonoBehaviour.Updateを持ち、Plugin.Updateを止めても独立実行する（`MainGameTransformGizmo/TransformGizmo.cs:463–487`）。SetVisible等も公開（同`:399`）。停止は所有者の各ギズモSetVisible(false)/破棄と、共有入力占有解放の両方が必要。SetVisible(false)はドラッグ状態解除とoverlayカメラ停止を実施（同`:399–420`）。
- 個別課題/根拠: DestroyはoverlayCam、軸オブジェクト、マテリアル/テクスチャも解放（同`:1227–1276`）。単にUpdate許可を落とすと表示・ドラッグ捕捉が残り得る。canon_gateに個々のギズモを登録するのか、所有プラグイン停止で処分するのかを統一する。外部所有ギズモ全件の実行時数は静的調査だけでは未確定。

### 53. MainGameUiHelp

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUiHelp/Plugin.cs:14>)

- 用途/プロセス: canon_plugins設定画面の説明ポップアップ。本編/VR/Studio（`MainGameUiHelp/Plugin.cs:14–17`）。Awakeで説明カタログ読込とUnity GUIのLabel/Button/Toggle/TextField/Slider等へHarmony prefixを入れる（同`:72–108`）。Updateは15秒間隔でPluginInfosとConfigを集める（同`:111–139`）。
- 実処理/停止: GUI prefixから説明対象を捕捉しOnGUIで表示、OnDestroyでhost入力捕捉End、UnpatchSelf、背景Texture破棄（同`:295`, `:345`）。canon_gateのH停止へ丸ごと結ぶとStudioやH外設定に説明が出なくなる。常駐GUIサービスとして登録し、プロセス終了/プラグイン停止時だけ既存解放を呼ぶ区分が妥当。
- 迂回/個別課題: Harmony prefixはUpdate停止では外れない。設定スキャンと描画コールバックを、Hセッション許可検査へ変えない。gate依存のStudio/VRロード問題を解決する前にHardDependencyを追加しない。説明捕捉のホスト単位開始/終了とポップアップの入力占有を保持する必要がある。

### 54. MainGameUiInputCapture

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUiInputCapture/Plugin.cs:11>)

- 用途/プロセス: UI入力所有権、カメラ入力抑止、カーソル解放、キー割当。本編/VR/Studio（`MainGameUiInputCapture/Plugin.cs:11–15`）。AwakeでInputBindings初期化、設定、runtime作成、HSceneWheel/Cursorパッチを適用（同`:39–77`）。共有UIの基盤でありHセッションの稼働停止対象に一律分類できない。
- 実処理/停止: Update/LateUpdateでFrameUpdate、緊急解放キー（同`:81–97`）。フォーカス喪失/一時停止/OnDisableは記録中入力を取消しReleaseAll（同`:99–109`）、OnDestroyはUnpatchSelf/ReleaseAll/保存（同`:111–123`）。canon_gateがUIを閉じる時は各owner解放を先に呼び、基盤を先に止めて復帰操作ができなくなる順序を避ける。
- 迂回: UiInputCaptureApiのowner別矩形/カーソル/API入力は外部から直接呼ばれる（`MainGameUiInputCapture/UiInputCaptureApi.cs:35`, `:44`, `:59`, `:100`, `:114`）。HarmonyはBaseCameraControl/GameCursor入力を遮断（`UiInputCaptureCursorPatches.cs:14–44`）。稼働中ownerの有無とH許可は別概念で、H外Studioのownerも有効に保つ。
- 個別課題/根拠: LogRelay HardDependency（`Plugin.cs:15`）があるため、gate→UiInputCapture→LogRelay→gateを作らない。停止共通化はownerスコープ解放/世代クリアまでで、全サービス停止をH終端へ結ばない。実機でドラッグ中のH終了・フォーカス喪失・緊急解放を確認する必要がある。

### 55. MainGameUnifiedUi

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUnifiedUi/Plugin.cs:16>)

- 用途/プロセス: 複数プラグインの設定タブホスト。本編/VR（`MainGameUnifiedUi/Plugin.cs:16`）。AwakeはConfig/窓位置のみ（同`:113–129`）。CanShowUiの既存厳格部屋判定とUpdateでのCloseForBlankRoomExit（同`:82`, `:139–164`）を、確定したセッション開始/停止へ接続できる。
- 実処理/停止: Updateはホットキー、窓ドラッグ、入力捕捉、OnGUIはタブ描画（同`:139`, `:274`）。CloseForBlankRoomExitはSetVisible(false)+ReleaseInputCapture（同`:164–169`）、破棄も解放（同`:132`）。gate停止はこの閉じる処理を必ず呼ぶ。
- 迂回/個別課題: RegisterTabはdraw delegateを保持する（同`:174–204`）、公開RegisterTab/SetVisible（`MainGameUnifiedUi/UnifiedUiApi.cs:27`, `:56`）から起動可能。タブ登録は常駐メタデータとして残してよいが、表示/描画呼出は許可済みセッション内だけにし、ShowTab/SetVisibleで停止後に再表示できないようにする。ホスト停止だけでは各タブ所有プラグインの実処理停止にならない。

### 56. MainGameVMDPlay

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVMDPlay/Plugin.cs:12>)

- 用途/プロセス: VMD、音楽/動画/カメラ再生、本編キャラの制御移譲、独立キャラ。本編/VR（`MainGameVMDPlay/Plugin.cs:12–22`）。Awakeはファイル走査、外部VMD型解決、外部設定初期化、DontDestroyOnLoadの管理コンテナを作ってVmdBridgeInstallMgrする（同`:47–85`, `:88–118`）。単なる登録前後では外部管理MonoBehaviourが常駐する点が重要。
- 実処理入口: UpdateのRoomAllowed/ScanHScene（同`:137–163`）。現状H検出は1秒走査（同`:168–187`）、H開始はWaitForCharaReadyコルーチン（同`:189–195`）。gateからprocとセッション世代を渡せば各プラグインのH許可検査巡回を増やす必要はない。ただし許可後のキャラ準備待ちまで消さない。
- 停止/復元: LeaveAllPoseRoomはepoch更新、全guarded iterator Dispose、StopAllCoroutines、Astral/独立キャラ/再生/音声/カメラ解放、参照と状態初期化（`MainGameVMDPlay/Plugin.RoomGate.cs:30–51`）。ReleaseCharacterは外部controllerの再生/顔制御を止め、finallyで音声・ダンス相手・顔・骨補正・元ポーズ・元Animator/IK.enabledを個別復元（`Plugin.Release.cs:30–83`）。H終端と無効化はこの所有権返却を必須とし、成功/失敗/ロード中の全経路で冪等にする。
- 迂回: guardedコルーチンは既存RoomAllowedとepochを毎継続時に確認（`Plugin.RoomGate.cs:16–27`）。これを確定済みセッション生存トークンへ置換できる。外付けカメラLateUpdate driver（`Plugin.CameraMotion.cs:109–112`）、独立キャラ配置Update/LateUpdate（`IndependentActorPlacementDriver.cs:7–24`）、視線LateUpdate driver（`Plugin.ActorLook.cs:85–87`）はPlugin.Updateとは別に動く。PlaybackBar公開コマンドからload/play/stopへ到達（`Plugin.PlaybackBarApi.cs:118`）。driver停止/破棄、外部管理controller停止、API開始受付の失効まで必要。
- 個別課題/不明点: 外部KKS_VMDPlayPluginの管理オブジェクトの全寿命と未停止の外部controller全件は、このプラグインのソースだけでは保証できない。OnDestroyは専用パッチ/音声/カメラ/購読を解放（`Plugin.cs:219–238`）、OnDisableは独立キャラ破棄と所有権返却（`Plugin.IndependentActor.cs:88`）だが、外部管理コンテナを破棄する処理は示したAwake/OnDestroy本文にはない。gate対応時は外部側の実体一覧も追跡し、終了後に骨/音声/カメラが残らないことを実機確認する。

### 57. MainGameVoiceBreastMorph

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceBreastMorph/Plugin.cs:11>)

- 用途/プロセス: VoiceFaceの応答テキストから胸/肌等モーフ・タトゥーを遅延発火。本編/VR、VoiceFace/CharacterMorphBridge依存（`MainGameVoiceBreastMorph/Plugin.cs:11–16`）。Awakeは設定とResponseTextTriggerApi.Received購読（同`:28–41`）。Update入口がなく、gate登録とUpdate停止だけでは制御できない。
- 実処理/迂回: OnResponseTextはscheduleへActionを預け（同`:53–63`）、後からBeginTransition→コルーチン（同`:66–72`）。Transitionは50ms間隔でMorphBridgeへ書込（同`:75–107`）。追加ルール/タトゥーも別コルーチン（`Plugin.Rules.cs:179–195`, `Plugin.Tattoo.cs:167`）。購読停止と、既にVoiceFaceへ預けたActionのセッション失効判定を両方行う必要がある。
- 停止/個別課題: OnDestroyは購読解除、胸/肌/別胸コルーチン停止、CancelTattooFade（`Plugin.cs:44–50`）。H停止でこの稼働停止だけを抽出し、常駐設定は保持する。設定EnabledだけをAction開始時に見ても別Hへの持越しを防げない。元状態取得（`Plugin.Rules.cs:200`）は存在するが、破棄本文にはモーフ全復元はないため、H終端で既存変更を保持/復元する要件を明示してMorphBridge側所有権と整合させる。

### 58. MainGameVoiceFaceEventBridge

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceFaceEventBridge/Plugin.cs:23>)

- 用途/プロセス: NamedPipe受付、音声再生、顔/ポーズ等イベント、内蔵声抑止、外部cue API。本編/VR（`MainGameVoiceFaceEventBridge/Plugin.cs:23`）。Awakeで音声player、シーンイベント購読、Harmony/VoiceGuardHooks、パイプサーバーを起動（同`:163–207`）。受信サービスとH限定のキャラ変更・音声制御は分割が必要。
- 実処理入口: Updateは部屋外でもreceive timingを更新し、優先愛撫cue再生/復元を例外処理する（同`:260–280`）。許可中は内蔵声待機、player.Tick、guard復元、遅延Action、受信コマンド、response_text、シナリオ送信を順に動かす（同`:303–350`）。単純に冒頭returnを追加すると、既に止めた内蔵声の復元まで止まる。
- 受信迂回: StartOrRestartPipeServerはSettings Enabled/EnablePipeServerで起動し、H許可を直接見ない（`MainGameVoiceFaceEventBridge/Plugin.Pipe.cs:17–47`）。受信thread→OnPipeLineReceived→EnqueueCommand→DrainIncomingCommands（同`:67–123`）。Config変更/再読込から再起動（`Plugin.Setup.cs:796`, `:2732`）。H停止で受信自体を維持するならH操作要求を拒否/世代付けし、停止後に旧キューを再実行しない。StopPipeServerだけでは遅延Action/応答キュー消去にはならない。
- パッチ/API迂回: HScene開始相当パッチはCurrentProc取得/分類生成、OnDestroyパッチは音声停止とnative admission/guard復元（`Plugin.HScene.cs:125–154`）。VoiceGuardは再生中・ブロック時間・優先cueで内蔵声やキスを抑止（`Plugin.VoiceGuard.cs:13–48`）。PublicApiはcue/priority/continuous/hard/orgasmと音量boostを直接呼ぶ（`PublicApi.cs:12–80`）。Update停止だけではこのパッチ/APIは動くので、cached許可トークンを共有し、新規開始と停止後の抑止残留を管理する。
- 停止/復元: 既存OnDestroyにはnative admission初期化、sequence deactivate、ClearDelayedActions、pipe停止、response queue初期化、Unpatch、audio Dispose、VoiceProcStop復元、guard resetがある（`Plugin.cs:214–248`）。H停止にはDestroy不要のセッション停止版を作る。ClearDelayedActionsはsequence側のtransient stateを消す（`Plugin.Handlers.cs:6487–6511`）。現在のHSceneOnDestroyパッチ本文にはこのClearDelayedActions/response queue消去はないため、それだけを停止入口に採用しない。
- 個別課題: 優先愛撫cueは既存でBlankRoom外継続を明示的に許しており（`Plugin.cs:265–272`）、全H拒否で止めるのか別許可カテゴリかを整理する。パイプの常駐/H単位開閉、外部cue owner、内蔵声ロード取消、音声終了イベント、遅延Actionを別々に検証する必要があり、登録だけで対応完了とは判定できない。

### 59. MainGameVoiceImpactBoost

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceImpactBoost/Plugin.cs:7>)

- 用途/プロセス: PregnancyPlusの腹部ピークをcue/音量boostへ変換。本編/VR（`MainGameVoiceImpactBoost/Plugin.cs:7–11`）。AwakeでサービスとHSceneMonitor作成、Config変更購読、PregnancyPlus.OnBellyBokoPeak購読（同`:27–38`, `:78–83`）。
- 実処理/迂回: Updateはcue ownerを毎フレーム更新してから部屋判定、HSceneMonitorを呼ぶ（同`:41–52`）。外部イベントはHandleBellyBokoPeakへ直接入り、その中もEnabled/BlankRoomを確認（`MainGameVoiceImpactBoost/VoiceImpactBoostService.cs:25–33`）。gateの確定済み許可はイベント経路にも必要。Config変更からUpdateCueModeへ再入する点も対象（`Plugin.cs:63–74`）。
- 停止/個別課題: OnDestroyはcue owner解除、Config購読解除、ピークイベント解除（同`:55–60`, `:93–109`）。H停止ではowner解除と一発発火状態Resetを行い、購読は解除/再開方式かcached sessionでno-opにする。現状HSceneMonitorは1秒走査してH開始/終了を見てReset（`HSceneMonitor.cs:23–53`）するが、呼出元が部屋外returnするとその終了監視自体へ到達しない。gate開始/停止通知へ置換すれば追加巡回なしに確実なResetが可能。

### 60. MainGameVrAfterimageLab

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrAfterimageLab/Plugin.cs:15>)

- 用途/プロセス: 左右眼別残像の実験描画。本編/VR（`MainGameVrAfterimageLab/Plugin.cs:15–17`）。AwakeはConfig/ログ/ApplyConfig（同`:46–54`）。OnlyHScene設定でH限定性を選ぶ既存機能があり（同`:109–112`）、一律H限定化はその設定意味との整合が必要。
- 実処理入口: LateUpdateでBlankRoomGate.Tick後、有効/対象シーン確認、RT再構築、overlayDrawer、EndOfFrame captureを開始（同`:60–95`）。H検出は0.5秒走査（同`:115–125`）。gateのproc/許可セッション通知へ置換でき、H中に新しい許可検査巡回は不要。
- 停止/迂回: StopRuntimeはoverlayDrawer、RT配列、captureカメラ/rootを破棄、readyとframeを初期化（同`:178–187`）。OnDestroyも解放（同`:98–105`）。CaptureEndOfFrameはyield後にEnabled/対象scene/RT/camera有効性を確認する（`MainGameVrAfterimageLab/Plugin.Capture.cs:43–47`）が、直接のBlankRoom判定はない。停止時のコルーチン取消/世代失効と描画callback解除を合わせ、LateUpdateを止める前にStopRuntimeを実行する。
- 個別課題/根拠: 左右レンダーはcaptureCamera.Renderを複数回呼ぶ（同`:75–90`）。同フレーム内停止、設定変更でのRT再構築、実行途中のカメラ破棄との競合は静的確認だけでは確定できない。Config callbackのApplyConfigも許可済みsessionでだけ実体作成する必要がある（`MainGameVrAfterimageLab/Plugin.Config.cs:89`）。

### 61. MainGameVrDildo

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrDildo/Plugin.cs:16>)

- 用途/プロセス: 道具モデル、接触/挿入判定、愛撫反応、口音声、ゲージ/表情/汗。本編のみ（`MainGameVrDildo/Plugin.cs:16`）。Awakeで設定、voice/oral/motion gauge/eye系フック、VoiceFace cue owner、検証用コルーチンを準備（同`:103`）。VRと名にあってもBepInProcessはVR実行ファイルを指定していないため、gate依存で対応プロセスを推測しない。
- 実処理入口: Updateで厳格部屋判定後、入力/音声、0.5秒のscene解決、自動Spawn、掴み/ギズモ/コライダ（同`:378–424`）。LateUpdateは別入口で、骨反応後finallyに声/顔/ゲージ処理を実行（同`:427–432`）。canon_gateの開始は許可procを保存して準備し、停止はモデルと反応所有権を解放する。両フレーム入口に確定済みsessionを使う。
- 停止/復元: DeleteModelは汗/顔、口音声、口開き、ギズモ、掴み、コライダ、プレビュー、反応状態を解放（同`:313–335`）。OnDestroyは追加でゲージ、eye、cue owner、入力、全パッチを解除（同`:172–186`）。OnDisableは入力/ギズモのみ（`MainGameVrDildo/Plugin.InputCapture.cs:37–41`）なのでenabled=falseだけをH停止に流用しても全復元にはならない。
- 迂回/個別課題: HSprite.Update/HAibu.Procパッチ（`MainGameVrDildo/Plugin.MotionGauge.cs:14–16`, `:29`, `:50`）、oral native voice阻止（`Plugin.OralAudio.cs:85`）、public ToggleSpawn/設定、motion状態API（`Plugin.cs:200–209`, `Plugin.MotionReaction.cs:99`, `:115`）が別入口。ゲージは内部値/表示/効果を混同せず停止で復元し、ロード途中口音声はStopOralAudioのコルーチン取消（`Plugin.OralAudio.cs:179`）まで行う。通常動作停止とパッチ解除の常駐方針を分離する。

### 62. MainGameVrHipIkVoiceImpactBoost

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrHipIkVoiceImpactBoost/Plugin.cs:11>)

- 用途/プロセス: HipHijackのIK追跡位置の運動検出からVoiceFace cue/boost要求。本編/VR、両プラグインにHardDependency（`MainGameVrHipIkVoiceImpactBoost/Plugin.cs:11–15`）。AwakeはJSON/Config同期、設定イベント購読、検出器と追跡位置source作成（同`:28–57`）。
- 実処理入口: Updateでcue owner更新後、部屋/Enabled/VR条件を確認しdetector.Update（同`:73–93`）。停止条件ではResetするがUpdate自体が止まると呼ばれない。gate停止でdetector.Resetとcue owner=falseを明示し、開始で検出基準を初期化する。H内に許可巡回を増やす必要はない。
- 迂回/個別課題: Config ChangedはUpdateCueModeとJSON保存を直接実行（同`:96–109`）。UpdateCueModeは部屋/VRの現在値でcue ownerを設定する（同`:114–119`）ため、停止後Config変更でownerが復活しないようsession許可も必要。OnDestroyはowner解除とイベント解除（同`:64–70`）。運動量判定と音声受付の停止を別々に確認する。

### 63. MainGameVrNeckChain

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrNeckChain/Plugin.cs:15>)

- 用途/プロセス: 首輪/鎖、掴み、物理と肩pull連携。本編のみ（`MainGameVrNeckChain/Plugin.cs:15`）。Awakeは可視化driverとConfig/生成ボタンを準備（同`:94`）。Updateは部屋外でClearChain、許可後scene解決/掴み/ギズモ（同`:166–183`）、LateUpdateは位置/肩pull/物理更新（同`:186–228`）。
- 停止/復元: ClearChainは肩pull reset、3種類ギズモ・掴み解除、身体コライダ破棄、鎖/首輪破棄、骨/接続状態初期化（同`:595–621`）。OnDestroyは別driver破棄とタブ解除も行う（同`:623–628`）。gate停止はUpdate/LateUpdate前にこの共通停止を呼び、掴み中でも元のIKを消費側へ返す。
- 迂回/個別課題: 公開TryGetShoulderPullは取得だけでなくUpdateShoulderPullを実行し、pose APIとSyncChainAfterShoulderSolveも存在（`MainGameVrNeckChain/Plugin.ShoulderPull.cs:38–80`）。別描画driverも厳格部屋判定を持つ（`Plugin.ChainVisual.cs:19`）。公開API/driverはsessionを共有させ、HipHijack solver hookより先に鎖だけ破棄して不正参照にしない。終了順序は両者の停止が冪等であることが必要。

### 64. MainGameVrScreenEffectProbe

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrScreenEffectProbe/Plugin.cs:13>)

- 用途/プロセス: カメラへ左右眼画面効果layerを取り付ける。本編/VR（`MainGameVrScreenEffectProbe/Plugin.cs:13`）。AwakeはConfig/ログ/セッション初期状態（同`:70–85`）、Updateは部屋判定後に設定間隔でScanAndAttach（同`:100–109`）、OnGUIも独立入口（同`:112`）。
- 停止/復元: OnDestroyと部屋exitはDetachAllLayers（同`:72`, `:88–92`）。DetachAllLayersは全layer.OwnerをnullにしてDestroyしリストから除去（`MainGameVrScreenEffectProbe/Plugin.CameraScan.cs:263–281`）、layerのOnDestroy/ReleaseがRTを解放（`VrEyeScreenEffectLayer.cs:31`, `:88`）。gate停止でこの経路を呼び、Updateを止めた後も描画callbackが残らないようにする。
- 迂回/個別課題: public TrySetEnabled/UiVisible（`Plugin.cs:250–270`）からSetEnabledInternalへ入り、true時は即ScanAndAttach（同`:299–311`）。外付けlayerはOnRenderImageで稼働（`VrEyeScreenEffectLayer.cs:106`）。確定済み許可を再有効化/API/Config起点でも共有し、既存カメラ走査は対象カメラ更新用途として扱う。許可判定巡回を走査へ追加しない。

### 65. MainGameVrVoiceGate

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrVoiceGate/Plugin.cs:17>)

- 用途/プロセス: VRコントローラ押下から外部録音gateをUDP開始/停止し、保持中heartbeatと表示。本編/VR（`MainGameVrVoiceGate/Plugin.cs:17`）。Awakeは設定/ログ（同`:43–55`）、Updateは部屋判定後に押下変化とheartbeatを送る（同`:68–91`）。録音制御という外部状態を所有する。
- 停止/復元: OnDisable/OnDestroyはSendStopIfActive（同`:58–65`）、設定無効も同処理（同`:129–130`）。一方、部屋外に出たUpdateは先頭returnのみ（同`:71`）で停止を送らない。canon_gateのH停止にSendStopIfActiveを必ず接続し、_lastGateActiveとheartbeat状態を初期化する必要がある。
- 迂回/個別課題: SendGateCommandはUDP通信（同`:182–206`）、OnGUIは別表示入口（同`:94–113`）。H開始時点の許可に基づいて開始受付を有効化し、停止送信は許可が失効してからでも通す。外部受信側の期限切れ仕様・UDP到達保証は静的ソースだけで未確認なので、押下中H終了で外部録音が止まることを確認する。

### 66. MainGirlHipHijack

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGirlHipHijack/Plugin.cs:26>)

- 用途/プロセス: 女性/男性の身体IK、VR掴み、プリセット、速度制御、鎖肩pull。本編/VR（`MainGirlHipHijack/Plugin.cs:26`）。Awakeは設定/IK準備、Harmony.PatchAll、FrameCoordinatorと有効状態適用（同`:234`）。Updateだけで完結しない複数の骨制御所有者がある。
- 実処理入口: UpdateはMaintainChainShoulderHoldを部屋判定前に呼び、許可後FrameCoordinator.UpdateFrame（同`:448–453`）。HSceneProc.LateUpdateパッチからAfterHSceneLateUpdate、ChangeAnimatorから体勢変更処理、HActionBase.SetAnimatorFloatの2 overload prefixで速度を遮断（`MainGirlHipHijack/Plugin.Patches.cs:9–61`）。LateUpdate内のApplyChainShoulderPullはBlankRoom判定の外（同`:13–15`）、ChangeAnimatorのCancelChainShoulderPullも判定前（同`:22–24`）。この意図的復元/連携経路を許可開始と区別する必要がある。
- 停止/復元: OnDestroyは肩hold、BodyCtrl連携、VR掴み、ポーズ遷移、全IK、男性参照、IKキャッシュ、入力、パッチ、thumbを解放（`Plugin.cs:401–417`）。設定無効は肩/遷移/全IK/入力を停止するがDestroy全処理とは異なる（同`:424–445`）。DisableAllBodyIKは各effectorを解除（`Plugin.BodyIK.cs:579–585`）。gate停止は設定値を改変せずセッション停止入口として同等の所有権復元をまとめる。
- solver/API迂回: 肩holdはsolver.OnPreUpdate/OnPostUpdateを直接購読（`Plugin.ChainShoulderPull.cs:184–185`）。Releaseは購読解除、左右target/重み/IK.enabled、camera lookを復元（同`:255–283`）、OnDisableは肩hold解除だけ（同`:286–289`）。public追跡位置・follow API（`Plugin.PublicApi.cs:31`, `:72`, `:111`, `:136`）、ArmIkRunningChanged（`Plugin.cs:59`）、ポーズ遷移/スクリーンショットcoroutine（`Plugin.PosePresets.Runtime.cs:71`, `:339`）もsessionに紐付ける。OnGUIは独自の部屋判定（`Plugin.UI.cs:11–15`）。
- 個別課題: Updateのreturn/プラグインenabled=falseだけではHarmonyとsolver購読が止まらない。停止で外部follow/保存中遷移/入力と元Animator速度・IK重みまで返し、ShoulderIkStabilizerへ停止イベントを伝える順序が必要。鎖側のAPIが部屋を出るとfalseになることをもって、既存肩holdが復元済みとは判断しない。実機で体勢変更/対象女性変更/ロード中終了の復元が未確認。

### 67. MainGirlShoulderIkStabilizer

[ソース入口](<F:/kks/work/plugins/canon_plugins/MainGirlShoulderIkStabilizer/ShoulderIkStabilizerPlugin.cs:14>)

- 用途/プロセス: HipHijack連携の肩IK後段補正。本編/VR（`MainGirlShoulderIkStabilizer/ShoulderIkStabilizerPlugin.cs:14`）。Awakeは設定/Config、HipHijack armイベント購読、HSceneProc.LateUpdateパッチ（同`:133–149`）。このパッチが主入口で通常Updateはない（`ShoulderIkStabilizerPatches.cs:9–14`）。
- 実処理/迂回: OnAfterHSceneLateUpdateは設定ファイル再読込/有効判定後EnsureRotator（`ShoulderIkStabilizerPlugin.cs:181–193`）。生成したShoulderRotatorはIK solver.OnPostUpdateを購読し、RotateShouldersはsettingsを見て肩回転を適用（`ShoulderRotator.cs:53–96`）。パッチ呼出だけ止めても既存rotatorのsolver callbackは止まらない。
- 停止/復元: public SetEnabledFromHipHijack(false)はDisableCurrentRotator（`ShoulderIkStabilizerPlugin.cs:110–130`）、OnDestroyは全rotator破棄・Unpatch・イベント/Config解除（同`:161–178`）。rotatorのOnDisable/OnDestroyはUnhookSolver（`ShoulderRotator.cs:43–73`）。gate停止ではrotatorを直ちに無効化/解除し、再作成を確定済み許可で拒否する。Destroyの遅延フレームにsolverが再実行しないことも配慮する。
- 個別課題: 停止時DisableCurrentRotatorと全rotator破棄は別処理（`ShoulderIkStabilizerPlugin.cs:482–513`）。過去対象の残存rotatorとHipHijackイベント経路も整理する。既存ファイル再読込は設定更新用途でありH許可検査へ広げない。回転値の復元が本編animation再適用に依存する部分の実機確認が必要。

### 68. SimpleAfterimage

[ソース入口](<F:/kks/work/plugins/canon_plugins/SimpleAfterimage/Plugin.cs:13>)

- 用途/プロセス: 本編/VR残像、VMD/ビート同期、Kaleidoscope/外部warp効果（`SimpleAfterimage/Plugin.cs:13`）。Awakeで設定/プリセット/VMD設定/タブ/ApplyConfig（同`:102–119`）。Updateのタブ登録は部屋判定前、設定プリセット操作はその後（同`:122–152`）。
- 実処理入口: LateUpdateはVMD状態更新後、BlankRoomGate.Tick、有効/HScene判定、カメラ/RT/外部効果/撮影スケジュール（同`:157–217`）。gateの開始/停止へH参照を渡し、既存撮影スケジュールとは独立に許可を確定する。
- 停止/復元: StopAfterimageRuntimeはStopCaptureLoop、カメラ候補参照、描画数、frame/write index、VR履歴を消し、overlayを破棄、captureカメラ/RT/VR RTと配列を解放する（`SimpleAfterimage/Plugin.Capture.cs:136–183`）。OnDestroyはthought mask解除とタブ解除も行う（`Plugin.cs:221–235`）。停止用途ごとに必要な資源解放を確認し、Update停止だけにしない。
- 迂回/個別課題: 外部SetExternalAfterimage/SetWarpAfterimage（`Plugin.ExternalApi.cs:20–25`, `Plugin.WarpApi.cs:19–33`）、thought mask（`Plugin.Capture.cs:26–29`）、外付けOnRenderImage/OnPostRender（`Plugin.Overlay.cs:15–36`）、Config変更（`Plugin.Config.cs:80–82`）が別入口。外部強度/warp状態を次Hへ持越すかを明示し、停止で外部owner/タイムアウトとcapture coroutineを失効させる。RT解放と再入時再構築、VR両眼の残留描画は実機未確認。

### 69. SimpleAfterimage_VR

[ソース入口](<F:/kks/work/plugins/canon_plugins/SimpleAfterimage_VR/Plugin.cs:13>)

- 用途/プロセス: VR専用の旧残像方式。KoikatsuSunshine_VRのみ（`SimpleAfterimage_VR/Plugin.cs:13`）。Awakeは設定/プリセット（同`:90–97`）、Updateはプリセットの保存/読込（同`:100`）。VR専用なので現状本編専用canon_gateへHardDependencyを足せない。
- 実処理入口: LateUpdateはEnabled/VRカメラ/RT条件で稼働し、画面サイズ/速度同期、overlay、EndOfFrame captureを行う（同`:436–477`）。この本文にはH必須/BlankRoom条件がない。HScene参照は速度同期用（同`:374`）であり、既存の全VR画面効果をH限定に変更する場合は機能範囲の変更になる。
- 停止/迂回: OnDestroyはoverlay/cameraRoot破棄とReleaseSlots（同`:555–560`）。CaptureEndOfFrameはyield後Enabled/slots/srcだけを確認（同`:479–483`）、DrawOnPostRenderもEnabled/描画数だけ（同`:527`）。明示的なH停止関数を作り、coroutine取消/描画解除/RT解放/描画数初期化をまとめる必要がある。
- 個別課題: 開始許可なしで既存Config変更/撮影が走らないよう入口を統一し、停止で資源が残る単純Enabled return（同`:438`）を避ける。SimpleAfterimageとの同時有効化/同じカメラへの描画順序はこのソースだけで実行時状態を断定できない。

### 70. StudioCharaStateLogger

[ソース入口](<F:/kks/work/plugins/canon_plugins/StudioCharaStateLogger/Plugin.cs:14>)

- 用途/プロセス: Studioキャラの肌/頬/涙/液等の変更ログ。CharaStudioのみ（`StudioCharaStateLogger/Plugin.cs:14`）。AwakeでConfig、有効ならHarmonyを準備（同`:51–70`）。Hシーン入口はないのでH許可が得られず常時停止する設計は目的を壊す。
- 実処理/停止: UpdateはEnabled、手動dump、任意delta polling（同`:82–98`）。ChaControl状態書込とStudio UIの変更handlerにパッチ（同`:107–130`）。OnDestroyでUnpatch（同`:101–104`）。常駐Studio診断サービス区分として、起動/終了を共通化しH開始/停止には結ばない。
- 迂回/個別課題: 共通ChaControl setter postfixはUpdate外で発火する（同`:109–112`, `:142`, `:162`）。既存Enabledは起動時パッチ適用と毎フレーム出力条件で異なり、設定文言は有効化後Studio再起動を要求（同`:74`）。共通ライフサイクルへ移すなら動的enable/disableのパッチ再適用/解除方針も明示する。gateのStudioプロセス非対応のままHardDependencyを追加しない。

### 71. StudioFacePresetTool

[ソース入口](<F:/kks/work/plugins/canon_plugins/StudioFacePresetTool/Plugin.cs:12>)

- 用途/プロセス: Studioの選択キャラへ表情プリセットを適用/保存するUI。CharaStudioのみ（`StudioFacePresetTool/Plugin.cs:12–15`）。Awakeは設定、ログ、preset/settings store、StudioFaceRuntime、window controller生成とConfig購読（同`:35–59`）。H限定にすると本来の対象プロセスで一度も動作しない。
- 実処理/停止: Update→controller.Tick、OnGUI→controller.OnGui（同`:62–70`）、OnDestroyは全Config handler解除とShutdown（同`:72–88`）。ShutdownはSaveAll、ReleaseCaptureFlags、idle cursor解除、EndOwner（`StudioFacePresetTool/UI/FacePresetWindowController.cs:175–183`）。共通化はStudio常駐起動/終了の枠へ置く。
- 迂回/個別課題: ConfigのUI表示/キー/カーソル変更はcontrollerへ直接伝達（`StudioFacePresetTool/Plugin.cs:125–154`）、適用buttonはApplyToSelected（`UI/FacePresetWindowController.cs:444–456`）。controller停止と入力owner解放が必要で、Plugin.Updateだけ停止しても窓設定callbackは残る。gate依存はStudio対応後に決める。適用済み表情はユーザーの編集結果なので、H終了として一律復元する契約を作らない。

### 72. UnityResourceDiagnostics

[ソース入口](<F:/kks/work/plugins/canon_plugins/UnityResourceDiagnostics/Plugin.cs:12>)

- 用途/プロセス: Unity全般の資源スナップショットとBakeMesh追跡。本編（`UnityResourceDiagnostics/Plugin.cs:12`）。Awakeはログ/Config/trace callback準備、有効ならパッチと起動時snapshot（同`:38–84`）。H専用用途ではなく、H前後を含む資源差分を見るサービス。
- 実処理/停止: UpdateはEnabled、手動キー、任意時間間隔のsnapshot（同`:101–123`）。OnDestroyはUnpatch（同`:88–98`）。常駐診断区分を作り、H停止でsnapshot/traceまで無条件停止して終了後の漏れ観測を失わない。
- 迂回/個別課題: BakeMesh postfixはUpdate外でBakeMeshTrace.Recordへ入る（`UnityResourceDiagnostics/BakeMeshTrace.cs:20–25`）。Recordはtrace専用Enabledと件数上限を見て記録（同`:47–66`）、Pluginのsnapshot Enabledとは別。共通停止でパッチを外すか記録委譲を失効させる必要がある。起動時だけApplyPatchesする既存動作と、Config後変更時の適用方針を整理する。

## 5. 通常プラグインではない関連コード

### MainGameStartupLogReset（Preloader）

`MainGameStartupLogReset/Patcher.cs:15–19` のTargetDLLs/Patch/InitializeはBepInExのpreloader契約で、Chainloaderやcanon_gateより前に実行する。配置先も `F:\kks\BepInEx\patchers\canon_plugins\MainGameStartupLogReset\MainGameStartupLogReset.dll` であり、plugins配下の69件には含めない。起動ログ掃除をH開始へ遅らせると本来の用途を失うため、通常のGateApi.Register対象と混同しない。

### MapEnvironmentRuntime（共通ライブラリ）

BepInPlugin属性を持たず、MainGameMapSandbox等が生成するSandboxSession/EnvironmentWorld/SandboxHostを提供する。`SandboxSession.cs:86–97` はInitializeで環境とAPIを作り、同:125のUpdateおよび同:491の破棄でlistener/thread/接続ファイルを後始末する。`EnvironmentWorld.cs:64` は環境初期化、同:131は時間進行、同:243はRenderSettings・ライト・リソース復元。`SandboxHost.cs:42–57` はAdditive sceneと子コンポーネント生成、同:108–114はシーン・AssetBundle解放。

このライブラリ自体に別のH判定を増やすのでなく、BepInEx側の所有プラグインが許可されたランタイムの生成・破棄を管理する。スタンドアロン環境での利用とKKS内の利用を分け、外部API要求とシーン破棄の取消も確認する。

## 6. 移行前に確定すべき事項

| 論点 | 調査から必要になった判断 |
|---|---|
| 実行場所 | H/Makerに加え、Studio・タイトル・アルバム・通常本編UI・起動基盤をどう許可するかを明示する |
| 起動順 | gateから入力/ログへの依存と、各プラグインからgateへの依存を循環させない |
| 読込中パッチ | StartH完了前に必要な体位一覧・モデル・UI準備を、H進入時1回判定とどう両立するか決める |
| 実ランタイムの所有者 | 子MonoBehaviourやライブラリの開始停止を、どのプラグインのコールバックが所有するか決める |
| 外部通信 | 受信サービスの存続、未許可要求の応答、終了時のキュー/非同期/機器の停止契約を決める |
| キャラデータ | カード読込・保存はデータ保持と実オブジェクト生成を分け、未許可時に保存内容を消さない |
| 復元順 | カメラ/レンダラ/音声/IK/入力基盤より先に、その所有者の停止・復元を完了させる |
| 対応完了の定義 | 登録数だけでなく、実入口・公開API・パッチ・遅延復帰・停止復元の各経路を確認する |

## 7. 段階的な接続と検証の提案

1. まずプロセス・基盤・初期化の契約を決めて依存循環を解消し、現行gateの単一H/Makerモデルで扱えない用途を明文化する。
2. Glowなどの画面効果のような既存停止関数を持つ単純系から開始・停止の共通接続部品を検証する。具体候補はGlow、DisplacementMap、VrAfterimageLabで、描画子コンポーネントも停止対象にする。
3. VMD/姿勢/カメラ/IKを接続し、キャラやカメラの所有権、遷移途中の退出、次Hへの持越しを確認する。
4. VoiceFace/字幕/AgentCamera/SemanticPose/Handyの通信・遅延処理を接続し、終了後の結果適用や音声・機器継続がないことを確認する。
5. Maker/Studio/タイトル/アルバム/基盤を定義済み契約へ接続する。全GUIDの登録だけを埋めて警告を消す工程にはしない。
6. 通常H・AllPoseRoom・Maker・Studio・タイトル・VRそれぞれの許可/不許可、開始1回/停止1回、門番欠落/無効化、例外設定の次回反映、非同期取消、復元順をテストする。

H開始の部屋読取り回数、重複通知、H中の再検査がないことを自動確認し、ビルドと配布DLL照合を行う。ゲームが終了してから各プラグイン自身のbin/Release/net472成果物だけを配置し、実機の入力・映像・音声・キャラ状態・FPSはユーザーが確認する。ソースが移行済みでも旧DLLが配置されたままなら未対応として区別する。

## 8. 完了した調査と限界

ソースの全72系統の入口・代表的ランタイム・停止・迂回経路を静的確認し、各個別節へ根拠を記載した。共有部品、Bridgeのビルド対象、関連ライブラリ/Preloader、配置69GUID、プロセス指定、重要依存を照合した。

未実施なのは全プラグインの実機動作、全組合せの相互干渉、各遅延経路の実行時順序、性能測定、全体移行のビルド/配布/受入である。「全ソースを調査した」と「全動作を保証した」は区別する。具体的な停止漏れや読込順の懸念は個別節に残し、実装時の検証項目として扱う。

## 9. 全件索引と配置状況

配置ありはDLLメタデータに同GUIDが存在するという意味で、起動成功・ソースとの一致を保証しない。根拠入口は現行csprojで明示的に選ばれるBridgeを優先する。

| No. | 系統 | GUID | 配置 | 根拠 |
|---:|---|---|---|---|
| 01 | AstralMode | `com.kks.maingame.astralmode` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/AstralMode/Plugin.cs:9>) |
| 02 | canon_gate | `canon.gate` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/canon_gate/Plugin.cs:13>) |
| 03 | CoordCatalog | `koikatsu.coordcatalog` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/CoordCatalog/CoordCatalogPlugin.cs:9>) |
| 04 | LiquidEffectPreview | `com.canon.liquideffectpreview` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/LiquidEffectPreview/Plugin.cs:9>) |
| 05 | MainGameAdvIkBridge | `com.kks.main.advikbridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAdvIkBridge/Plugin.cs:13>) |
| 06 | MainGameAgentCamera | `com.kks.maingame.agentcamera` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAgentCamera/Plugin.cs:24>) |
| 07 | MainGameAibuPoseClones | `com.kks.maingame.aibuposeclones` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAibuPoseClones/Plugin.cs:12>) |
| 08 | MainGameAiShoujoMode | `com.kks.maingame.aishoujomode` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAiShoujoMode/Plugin.cs:15>) |
| 09 | MainGameAlbumAspect | `com.kks.maingame.albumaspect` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAlbumAspect/Plugin.cs:12>) |
| 10 | MainGameAllPoseMap | `com.kks.maingame.allposemap` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAllPoseMap/Plugin.cs:16>) |
| 11 | MainGameAutoHVoice | `com.kks.maingame.autohvoice` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameAutoHVoice/Plugin.cs:15>) |
| 12 | MainGameBeatSyncSpeed | `com.kks.maingame.beatsyncseed` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameBeatSyncSpeed/Bridge/Plugin.cs:11>) |
| 13 | MainGameBlankMapAdd | `com.kks.maingameblankmapadd` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameBlankMapAdd/Plugin.cs:29>) |
| 14 | MainGameCameraControl | `com.kks.maingame.cameracontrol` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCameraControl/Plugin.cs:21>) |
| 15 | MainGameCameraLookControl | `com.kks.maingame.cameralookcontrol` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCameraLookControl/Plugin.cs:9>) |
| 16 | MainGameCharacterAfterimage | `com.kks.maingame.characterafterimage` | なし | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCharacterAfterimage/Plugin.cs:11>) |
| 17 | MainGameCharacterMorphBridge | `com.kks.maingame.charactermorphbridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCharacterMorphBridge/Plugin.cs:16>) |
| 18 | MainGameCircularDisplacement | `com.kks.maingame.circulardisplacement` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCircularDisplacement/Plugin.cs:12>) |
| 19 | MainGameClothColliderCards | `canon.kks.clothcollidercards` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClothColliderCards/Plugin.cs:7>) |
| 20 | MainGameClothesLoadProbe | `canon.maingame.clothesloadprobe` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClothesLoadProbe/Plugin.cs:15>) |
| 21 | MainGameClubLights | `com.kks.maingame.clublights` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameClubLights/Plugin.cs:11>) |
| 22 | MainGameCoordinateAccessoryRestore | `canon.maingame.coordinateaccessoryrestore` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCoordinateAccessoryRestore/Plugin.cs:10>) |
| 23 | MainGameCoordinateCardSwap | `canon.maingame.coordinatecardswap` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameCoordinateCardSwap/Plugin.cs:14>) |
| 24 | MainGameDisplacementMap | `com.kks.maingame.displacementmap` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDisplacementMap/Plugin.cs:8>) |
| 25 | MainGameDisplayEffectViewer | `com.kks.main.displayeffectviewer` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDisplayEffectViewer/Plugin.cs:12>) |
| 26 | MainGameDollMode | `canon.maingame.dollmode` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameDollMode/Plugin.cs:17>) |
| 27 | MainGameFaceSweatControl | `canon.maingame.facesweatcontrol` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFaceSweatControl/Plugin.cs:17>) |
| 28 | MainGameFpsDisplay | `com.kks.maingame.fpsdisplay` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFpsDisplay/Plugin.cs:9>) |
| 29 | MainGameFrameTimingProbe | `com.kks.maingame.frametimingprobe` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFrameTimingProbe/Plugin.cs:20>) |
| 30 | MainGameFreeHMasturbationMenu | `com.kks.maingame.freehmasturbationmenu` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameFreeHMasturbationMenu/Plugin.cs:16>) |
| 31 | MainGameGlow | `com.kks.maingame.glow` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameGlow/Plugin.cs:11>) |
| 32 | MainGameHandySync | `com.kks.maingame.handysync` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHandySync/Plugin.cs:13>) |
| 33 | MainGameHHandPoseProbe | `canon.maingame.hscene.handposeprobe` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHHandPoseProbe/Plugin.cs:15>) |
| 34 | MainGameHScenePoseInjector | `canon.maingame.hscene.poseinjector` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameHScenePoseInjector/Plugin.cs:15>) |
| 35 | MainGameKaleidoscope | `com.kks.maingame.kaleidoscope` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameKaleidoscope/Plugin.cs:11>) |
| 36 | MainGameLiveLookCamera | `com.kks.maingame.livelookcamera` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameLiveLookCamera/Plugin.cs:17>) |
| 37 | MainGameLogRelay | `com.kks.maingame.logrelay` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameLogRelay/Plugin.cs:8>) |
| 38 | MainGameMapOverlayProbe | `com.kks.maingamemapoverlayprobe` | なし | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMapOverlayProbe/Plugin.cs:11>) |
| 39 | MainGameMapSandbox | `com.kks.maingame.mapsandbox` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMapSandbox/Plugin.cs:8>) |
| 40 | MainGameMilkSpray | `com.kks.maingame.milkspray` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameMilkSpray/Plugin.cs:14>) |
| 41 | MainGameObjectComposer | `com.kks.main.gameobjectcomposer` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameObjectComposer/Plugin.cs:13>) |
| 42 | MainGamePortraitCapture | `com.kks.maingame.portraitcapture` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePortraitCapture/Plugin.cs:15>) |
| 43 | MainGamePoseCatalogCapture | `canon.maingame.posecatalogcapture` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePoseCatalogCapture/Plugin.cs:18>) |
| 44 | MainGamePregnancyPlusBridge | `com.kks.main.pregnancyplusbridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGamePregnancyPlusBridge/Plugin.cs:13>) |
| 45 | MainGameRhythmSync | `com.kks.maingame.rhythmsync` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameRhythmSync/Plugin.cs:15>) |
| 46 | MainGameSemanticPoseBridge | `canon.maingame.semanticposebridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSemanticPoseBridge/Plugin.cs:22>) |
| 47 | MainGameSpeedLimitBreak | `com.kks.maingame.speedlimitbreak` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSpeedLimitBreak/Bridge/Plugin.cs:9>) |
| 48 | MainGameStudioItemSummon | `com.kks.maingamestudioitemsummon` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameStudioItemSummon/Plugin.cs:9>) |
| 49 | MainGameSubCameraDisplayProbe | `com.kks.maingame.subcameradisplayprobe` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubCameraDisplayProbe/Plugin.cs:16>) |
| 50 | MainGameSubtitleCore | `com.kks.maingame.subtitlecore` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubtitleCore/Plugin.cs:21>) |
| 51 | MainGameSubtitleEventBridge | `com.kks.maingame.subtitleeventbridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameSubtitleEventBridge/Plugin.cs:21>) |
| 52 | MainGameTransformGizmo | `com.kks.maingame.transformgizmo` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameTransformGizmo/Plugin.cs:10>) |
| 53 | MainGameUiHelp | `com.kks.maingame.uihelp` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUiHelp/Plugin.cs:14>) |
| 54 | MainGameUiInputCapture | `com.kks.maingame.uiinputcapture` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUiInputCapture/Plugin.cs:11>) |
| 55 | MainGameUnifiedUi | `com.kks.maingame.unifiedui` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameUnifiedUi/Plugin.cs:16>) |
| 56 | MainGameVMDPlay | `com.kks.maingame.vmdplay` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVMDPlay/Plugin.cs:12>) |
| 57 | MainGameVoiceBreastMorph | `com.kks.maingame.voicebreastmorph` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceBreastMorph/Plugin.cs:11>) |
| 58 | MainGameVoiceFaceEventBridge | `com.kks.maingame.voicefaceeventbridge` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceFaceEventBridge/Plugin.cs:23>) |
| 59 | MainGameVoiceImpactBoost | `com.kks.main.voiceimpactboost` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVoiceImpactBoost/Plugin.cs:7>) |
| 60 | MainGameVrAfterimageLab | `com.kks.maingame.vrafterimagelab` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrAfterimageLab/Plugin.cs:15>) |
| 61 | MainGameVrDildo | `com.kks.maingame.vrdildo` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrDildo/Plugin.cs:16>) |
| 62 | MainGameVrHipIkVoiceImpactBoost | `com.kks.main.vrhipikvoiceimpactboost` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrHipIkVoiceImpactBoost/Plugin.cs:11>) |
| 63 | MainGameVrNeckChain | `com.kks.maingame.vrneckchain` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrNeckChain/Plugin.cs:15>) |
| 64 | MainGameVrScreenEffectProbe | `com.kks.maingame.vrscreeneffectprobe` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrScreenEffectProbe/Plugin.cs:13>) |
| 65 | MainGameVrVoiceGate | `com.kks.maingame.vrvoicegate` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGameVrVoiceGate/Plugin.cs:17>) |
| 66 | MainGirlHipHijack | `com.kks.main.girlbodyikgizmo` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGirlHipHijack/Plugin.cs:26>) |
| 67 | MainGirlShoulderIkStabilizer | `com.kks.main.girlshoulderikstabilizer` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/MainGirlShoulderIkStabilizer/ShoulderIkStabilizerPlugin.cs:14>) |
| 68 | SimpleAfterimage | `com.kks.maingame.simpleafterimage` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/SimpleAfterimage/Plugin.cs:13>) |
| 69 | SimpleAfterimage_VR | `com.kks.maingame.simpleafterimage.vr` | なし | [ソース入口](<F:/kks/work/plugins/canon_plugins/SimpleAfterimage_VR/Plugin.cs:13>) |
| 70 | StudioCharaStateLogger | `canon.studio.charastatelogger` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/StudioCharaStateLogger/Plugin.cs:14>) |
| 71 | StudioFacePresetTool | `com.kks.studio.facepresettool` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/StudioFacePresetTool/Plugin.cs:12>) |
| 72 | UnityResourceDiagnostics | `com.kks.unityresourcediagnostics` | あり | [ソース入口](<F:/kks/work/plugins/canon_plugins/UnityResourceDiagnostics/Plugin.cs:12>) |
