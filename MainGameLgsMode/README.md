# MainGameLgsMode

KKSの状態変更イベントと編集可能な手動キーから、LGSのG13用Mキー切り替え通知を出します。

- フリーH開始はM1、本編・AI少女・ホテルの歩行開始／復帰はM2を要求します。
- その他の場面は現在のM設定を維持し、未読の古い通知だけを取り消します。
- 初期キーはMで、プラグインが最後に要求したM値からM1→M2→M3→M1と循環します。
- 手動変更は次の状況変更まで維持し、同じ状況の重複イベントでは上書きしません。
- IMGUI・UGUI・TMPの文字入力中、非フォーカス時、キー長押しのリピートでは手動切り替えを行いません。
- 配置先の`settings.json`の`ManualCycleKey`を`"M"`、`"F8"`、`"Ctrl+F8"`などへ変更すると自動再読込します。
- `ManualCycleEnabled`は手動機能、`Enabled`は全機能の有効無効です。
- `FreeHMode`と`WalkingMode`は0で維持、1～3で各Mキーを指定します。
- `EnableLogs`は既定falseで、trueのときだけ専用ログとBepInEx共通ログへ出力します。
- KKS側にはUpdate・定期ポーリングを追加せず、キーイベントと状態変更イベントだけを処理します。
- `Build.ps1 -Tests`でビルドと回帰検証、`Deploy.ps1`でゲーム終了確認後に配置・サイズ日時SHA256照合を行います。
- 配置先は`BepInEx/plugins/canon_plugins/MainGameLgsMode`で、既存の設定ファイルは維持します。
- LGSのKKS用プロファイルには、配置時に生成する`LgsProfileScript.lua`の受信処理が別途必要です。
- 受信側が有効なKKSプロファイルで動作している間、通知ファイルを読み、`SetMKeyState(mode, "lhc")`を呼び出します。
- 通知の有効期限は既定15秒で、期限切れと同じトークンは再適用しません。
- ビルド・模擬イベント検証と、実際のKKS入力・G13切り替え確認は別です。
