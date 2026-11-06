# canon_gate 2.3.0

Hシーン開始時に一度だけ許可を決定し、終了まで結果を保持する門番。2.3.0では非H用途の明示的な開始・終了も登録できる。

## 実行の流れ

ゲーム起動時は登録と通知接続だけを行う。KKSAPI（marco.kkapi）の `GameAPI.StartH` がH読込完了を通知した時、MainGameBlankMapAddの `BlankRoomState.IsInBlankRoom` を共有で一度だけ読み、登録済みプラグインの許可を決める。通常H・フリーHとも同じ入口を使う。

AllPoseRoomでは既定で許可し、他の部屋では指定GUIDの例外だけを許可する。部屋判定元が未導入・無効・取得失敗の場合は部屋条件を不許可とし、そのH中は再試行しない。マップ番号の固定値は持たない。

H中のマップ変更・シーン通知・設定変更では再判定しない。開始時にAllPoseRoomだった場合、途中でマップを変えてもそのHが終了するまで許可は保持する。

`GameAPI.EndH` またはHSceneProcの破棄時は、許可結果を全て失効させてから開始済みの処理だけを停止する。終了では部屋を読まない。重複する開始・終了通知でも検査・開始・停止を繰り返さない。

門番にはUpdate・LateUpdate・FixedUpdate・タイマー・定期再試行・常駐コルーチン・シーン検索・部屋Changedの購読がない。H中は何も監視しない。

## 後続プラグインの接続

ユーザー指定で `com.kks.maingame.aishoujomode`（AI少女モード）は管理対象外。gate登録・許可を必要とせず、未接続警告にも含めない。入力サービスは同GUIDとその所有範囲の入力保護を独立して扱う。他GUIDの許可は変更しない。

通常版・VRともKKSAPIのStartHで1回判定し、`GateApi.Current` は固定された種別・シーン参照・識別番号を公開する。Startフックはシーン参照取得専用で、そこで部屋を検査しない。VRHSceneはHSceneProcとは別の型なので、`Scene` を使い、`HScene` は通常版だけの互換参照として扱う。

canon_gate.dllを `Private=false` で参照し、`[BepInDependency(CanonGate.GateApi.PluginGuid)]` を付ける。門番未導入ならBepInExが接続先を読み込まない。

起動時に `IDisposable registration = GateApi.Register(自分のGUID, 開始処理, 停止処理)` を一度呼び、プラグイン破棄時に `registration.Dispose()` する。同一GUIDの二重登録とnullコールバックは拒否する。シーン途中の新規登録は次の開始から対象になる。

開始処理は許可されたH／キャラメイクで各1回だけ呼ばれる。不許可ならシーン用処理を開始しない。各プラグインから毎フレーム門番を呼ぶ方式にはしない。停止処理は、その開始処理が行ったコルーチン・描画・音声・パッチ等を停止・復元する。登録解除も開始済みなら1回だけ停止する。

開始コールバックが例外を投げた場合はその登録の許可を取り消し、部分的な初期化を戻すため停止を1回呼ぶ。他の登録の開始・停止は続行する。停止コールバックの例外も他プラグインの後始末を妨げない。

`GateApi.CanRun(GUID)` は登録済みの許可結果を取得するだけで、未知GUIDはfalse。問合せによる登録・部屋検査・設定評価はない。`GateApi.IsAllowed` は個別GUID例外を含まない、そのH開始時のセッション全体の許可結果。AllPoseRoomまたは所有シーンが一致するホテルHでtrueになる。

ホテルHは全登録プラグインを許可し、個別許可リストへの追加は不要。旧 `HotelModeAllowedPluginGuids` は互換用で、通常HやMakerへの許可を広げない。ホテルスコープに実際に結び付けたシーンだけが対象で、退出時に全許可を失効させる。プラグイン自身の有効無効設定は変更しない。

ホテル側は `EnterHotelMode(owner, roomId)` で入室先の客室番号を渡す。201〜206号室は同じ全許可規則で、開始後の部屋番号は `GateApi.Current.HotelRoomId` で参照できる。内装や入室経路の有無はホテル側で管理し、gateの許可だけで未作成のマップを生成しない。

遅延処理には `GateApi.Capture(GUID)` の戻り値を保持し、復帰時だけ `permit.IsValid` を読む。終了・登録解除・門番停止で失効し、次のシーンで古い許可は復活しない。引数なしCaptureはIsAllowed用。APIはUnityメインスレッド専用。

門番停止時は全許可を失効させて開始済みの処理を停止する。再有効化しても現在のシーンを再検査せず次の開始を待つ。起動時に既に開かれているH／キャラメイクへの途中接続も行わない。

旧試作APIのChanged・PermissionChanged・Tickは廃止し、Registerの開始／停止コールバックに置き換える。

AllPoseRoomの登録など、入るために必要な起動準備は門番で止めない。DLLの読込・起動準備と、シーン内の実処理を区別して後続接続を行う。

## 非Hの明示的な用途（2.3.0）

`RegisterActivity(guid, ActivityKind, start, stop, includeSessions: true)` は、タイトル・Studio・プレビュー・通常画面・起動サービスの実処理を登録する。返る `ActivityRegistration` の `Begin(scene)` を対象の開始イベントで呼び、終了で `End()`、破棄で `Dispose()` する。`IsActive` と `Scene` は保持された結果・参照で、検索や例外再評価を行わない。

中央の `[Exceptions] ActivityAllowedPluginGuids` は既定で空。非Hの開始イベントでGUID完全一致を一度だけ評価し、否認もEndまで固定する。設定変更だけで停止中の実処理が始まることはない。タイトルのAI少女モード、Studioツール、一般画面のFPS等は、それぞれのGUIDを明示許可してから次の開始で有効になる。

`includeSessions: true` なら同じ一登録で従来のH/Maker許可も受け取る。別途Registerを重ねない。H/Makerの開始時には先行非H処理を終了し、そこで改めて既存H/Maker規則を適用する。進行中のH/Makerを独自Beginで迂回できない。用途がタイトルだけなら `includeSessions: false` とする。

門番停止・終了・Disposeで実処理を停止する。全H/Maker許可は停止callbackの前に失効する。起動時のAPI登録、マップ選択肢の注入、設定モデルの作成等のbootstrapは実処理とは分けて残す。

gateからUiInputCaptureへのBepInEx必須ロード順依存を取り除き、gate→入力→ログ→gateの循環を防いだ。警告の入力は全pluginの初期化後に使う。実DLLの型解決・登録試験はゲームを起動せず実施できるが、ゲーム内操作の成功を保証する試験ではない。

通常Hの個別許可や非H用途で入力保護を使う場合は、依存サービスのGUIDも該当例外へ指定する。入力サービスは `com.kks.maingame.uiinputcapture`、ギズモは `com.kks.maingame.transformgizmo`、ログサービスは `com.kks.maingame.logrelay`。APIの起動準備は維持し、実入力取得・表示・記録は許可された寿命内で動く。gate自身の警告入力と後始末は失効後も扱える。

## 例外設定とキャラメイク

設定は `F:\kks\BepInEx\config\canon.gate.cfg` の `[Exceptions]` に保存する。指定は表示名ではなくBepInEx GUIDの完全一致で、大文字小文字を区別する。カンマまたはセミコロン区切りで複数指定でき、空なら例外なし。ワイルドカードは使わない。

`MakerAllowedPluginGuids = canon.kks.clothcollidercards` はそのGUIDをキャラメイクで許可する例。既定値は空で、この例を自動設定しない。

`EverywhereAllowedPluginGuids = example.utility.guid` はそのGUIDのH／キャラメイク開始時の場所条件を免除する例。タイトル等で常時処理を開始する設定ではなく、門番停止を迂回する許可でもない。既定値は空。

キャラメイクは `MakerFinishedLoading` で一度だけ例外を評価し、許可した処理を開始する。`MakerExiting` では停止して結果を破棄する。部屋情報やキャラメイク状態を繰り返し読まない。

設定画面等からの変更は次のH／キャラメイク開始から反映する。cfgを直接編集した場合は再起動またはメインスレッドから `GateApi.ReloadExceptions()` を呼んで読み直す。読み直しても進行中の許可は変更しない。

## 未接続プラグインの警告

AllPoseRoomのH開始時に一度だけ、`BepInEx/plugins/canon_plugins/` 配下のDLLから実際に読み込まれたプラグインと、開始時点のRegister登録を照合する。門番自身を除き、未接続なら基盤プラグインや無効状態の読み込み済みプラグインも表示する。未読込DLLや配下外のプラグインは対象外。

未接続があると「canon_gate 未接続：○件」という警告窓に名前とGUIDを名前順で全件表示する。一覧はスクロールでき、「閉じる」でそのH中は非表示となる。未接続0件・通常H・キャラメイクでは表示しない。警告はプラグインを停止する機能ではない。

閉じた状態はHSceneProcの実体ごとに保持し、同じHの表示要求・開始終了通知の重複・フォーカス復帰でも開き直さない。次のHSceneProcになった時だけ解除する。Startの補助フックは実体を記録するだけで、検査は従来どおりGameAPI.StartHの1回のみ。

閉じる操作はスクロール一覧から独立した固定位置でマウス押下・解放を直接判定する。GUILayoutのボタンID・hotControl・Inputのフレーム状態に閉じる成否を依存させない。他のUIが残したGUI.enabled・行列・色は描画中だけ整えて復元する。表示・閉じる押下・解放・非表示の瞬間だけ `[WarningUI]` ログを出し、毎フレームのログは出さない。

表示中のEscキーでも警告を閉じられる。キーは警告用OnGUIの入力イベントで処理し、ホットキー監視用のUpdateは追加しない。

H中の追加ロードや登録変更では一覧を更新せず、次のH開始時に照合する。表示中は保存済みの一覧をOnGUIで描画するだけで、再検査・ファイル検索・設定読込を行わない。非表示時は警告用コンポーネントを無効にする。

警告の入力にはMainGameUiInputCaptureを直接参照し、同プラグインを必須依存とする。窓の範囲でカメラ入力を抑止し、スクロールバーのドラッグを明示的に保持する。閉じる・H終了・フォーカス喪失・無効化・破棄では入力保持を解除する。

## 確認

AI少女モードは所有者・女の子の変更・H開始終了を明示通知し、`Exceptions/AiShoujoModeAllowedPluginGuids`に指定した音声表情・字幕・HipHijack・Composer・VMDと必要なUI・ギズモだけを開始する。歩行中の女の子とH中の女の子を切り替え、古い許可は失効させる。通常Hの部屋条件やAllPoseRoomの厳密判定は変更しない。

全身VMD再生時だけ所有者から自律歩行の停止を取得し、再生終了時に返す。顔だけのVMDでは歩行を止めない。モードや女の子を毎フレーム検索する処理は追加しない。

自動テストではH開始1回の部屋読取り回数、重複通知、H中のマップ／設定変更、登録解除、例外、キャラメイク、門番停止、コールバック例外、遅延処理の失効を確認する。多数の連続フレームの問い合わせで部屋検査・シーン検索・問合せのメモリ割当が増えないことも確認する。

ゲーム内確認はユーザーが行う。起動ログ、通常H／AllPoseRoomでの開始、H終了での停止、キャラメイク例外を確認する。既存プラグイン未接続の段階では、それらの停止やFPS改善を確認済みとは扱わない。
# 専用AI Hのnative engine所有

`AcquireDedicatedNativeEngine(requesterGuid, owner, actor, activityGeneration)` は、既に中央許可されたAI Activityのexact owner/actor/世代だけが取得できる排他的leaseです。`Bind`したHSceneProcのstart/end通知だけを通常H登録から分離します。`Current`を通常Hへ変えず、他pluginのH許可を付与しません。

取消後の遅いscene完了は`BindCleanupScene`で解放対象のidentityだけを保持でき、`IsValid`は復活しません。所有moduleはnative sceneのOnDestroyまでleaseを保持し、actor/cameraを復元してから解放します。scene探索・設定再評価・常時監視は追加していません。専用module以外のFreeH/ホテルは既存のBeginSessionへ進みます。

