# KKS Development Bridge

KKS内の実APIを使って開発・確認を繰り返すための汎用BepInExプラグイン。AI少女への自動入場は最初のアダプターで、共通部分を特定モードへ依存させない。

## 起動と接続

自身のRelease DLLの配置には共通の `tools/Deploy-OwnPluginDll.ps1 -Names MainGameDevelopmentBridge` を使う。中央gateの例外を自動追加していたInstall-KksDevelopment.ps1は削除した。配置では許可設定を変更しない。

普段exeから起動した場合は通常タイトルで止まる。`scripts/Start-KksDevelopment.ps1 -Mode ai` で明示的に開発起動したプロセスだけ、`--kks-development-mode=ai` 引数を受けてタイトル準備・フェード完了イベントから一度だけAI少女へ入る。`hotel`、`none` も指定可能。既に実行中なら現在のモードのまま接続する。起動スクリプトは設定を永続変更せず、旧設定のAutoModeがaiでも自動入場には使用しない。インストールのAutoModeはnone固定で、Mode引数は設けない。自動再入場やプラグイン内の常時探索・ポーリングは行わない。

```powershell
./scripts/Invoke-KksDevelopment.ps1 -Request '{"op":"status"}'
./scripts/Invoke-KksDevelopment.ps1 -Request '{"op":"capture"}'
./scripts/Invoke-KksDevelopment.ps1 -Request '{"op":"find","type":"UnityEngine.Camera"}'
./scripts/Invoke-KksDevelopment.ps1 -Request '{"op":"assemblies"}'
```

標準通信は `UserData/DevelopmentBridge/mailbox/<PID>/in` へのJSONのatomic renameと、`out` の応答。プラグインはFileSystemWatcherの通知で起き、常時探索しない。クライアント側だけが時間制限付きで応答を待つ。名前付きパイプ `KKS.DevelopmentBridge.<PID>` も `-Transport pipe` で選べるが、Unityランタイムが非対応ならstatusのpipeErrorで理由を読める。ネットワーク待受は行わない。応答は `ok/result` または `ok/error`。複数プロセスは `-ProcessId` で選ぶ。長いJSONは `-RequestFile` を使う。タイムアウト後に処理済みか不明な変更を無条件再送しない。

## 共通API

ゲーム内通知は `scripts/Get-KksNotifications.ps1 -ProcessId <実PID>` で一括取得する。読み取り専用拡張を必要時にロードし、`{"op":"notifications"}` が `available/reason/count/notifications` を返す。各通知の発行元・ID・見出し・本文・重要度・UTC日時を同じJSONに展開し、通知を解除しない。拡張は `dotnet build Extensions/Notifications/Notifications.csproj -c Release` で作成する。ブリッジ本体にも同じopを実装している。

| op | 引数・結果 |
| --- | --- |
| status | 実行中シーン、AI/ホテルの初期化状態、自動入場状態と失敗、拡張一覧 |
| capture | 描画終了時の実フレームをPNGとしてUserData/DevelopmentBridgeへ保存。保存完了後に絶対パスを返す |
| find | type、任意のname部分一致。非アクティブを含むUnityオブジェクトを操作要求時だけ列挙。名前、階層、instanceId、handleを返す |
| assemblies | ロード済みアセンブリ一覧 |
| members | typeまたはhandleの公開・非公開メンバーとシグネチャ |
| get / set | typeまたはhandle、member、set時のvalue。静的・インスタンスのフィールドとプロパティ |
| invoke | typeまたはhandle、member、args、parameterTypes（完全一致の型名配列）。曖昧なオーバーロードは拒否 |
| new | type、args、必要ならparameterTypesで任意の管理型を生成 |
| release | handleを解放 |
| load-extension | pathのDLL内のIDevelopmentCommandを登録。同名の旧拡張をDisposeして置換 |
| extensions | 登録済みの拡張コマンド名 |

複合値の戻りはhandleとなる。後続要求のhandle、またはargs/value内の `{"handle":"..."}` で実オブジェクトを渡せる。null、基本値、文字列、列挙型、JSONの構造体変換を扱う。型名はフルネームまたはアセンブリ修飾名で指定する。handleはgate停止・セッション切替・プラグイン停止で失効する。

制限したAPIの許可一覧は設けない。KKS本体、Unity、他MODの公開・非公開の管理APIへリフレクションで到達できる。ジェネリック、ref/out、複雑な非同期処理、ネイティブAPI連携などは拡張DLLに実装する。全APIが同じJSON表現で自動呼出しできるという意味ではない。

## 拡張

このDLLを参照し `IDevelopmentCommand` を実装する。`Name` をコマンド名とし、`Execute(arguments, context)` はUnityメインスレッドで呼ばれる。戻り値はJSON化できるDTOとする。実オブジェクトは `context.Reflection.Pack` でhandleへ変換できる。イベント・生成物・変更の所有権を保持し、`Dispose` で解除する。DLLをバイト列としてロードするためファイルを置換できるが、CLRアセンブリ自体のアンロードはプロセス再起動まで行えない。

実例は `Extensions/RenderingProbe`。ビルドしたRenderingProbe.dllをload-extensionで読み、`{"op":"rendering-state"}` を送ると、実ゲームの色空間・環境光・カメラの画面効果・ライトを取得できる。調査要求時だけ採取し、設定を書き換えない。これはAI少女にもホテルにも他のシーンにも使える。

通信workerはJSONとファイルI/Oだけを扱い、Unity操作はSynchronizationContextへ転送する。開始時に保持したgate許可と世代を実行直前にも確認する。停止時はパイプと保留要求を取消し、拡張とhandleを破棄する。追加診断ログは設定のDiagnostics/Enabledが既定OFF。命令への応答と明示的captureはブリッジ本来の出力である。

## 検証

Testsは非公開フィールド取得、値変更、厳密なオーバーロード、曖昧要求の拒否、handle失効、本物の名前付きパイプの往復と停止を実行する。ゲーム内での自動入場と実PNG確認は別に行う。色味の正しさは実画像で比較し、設定値やビルド成功だけで修正済みとしない。
