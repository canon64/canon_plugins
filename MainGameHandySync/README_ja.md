# MainGameHandySync

KKS本編HシーンのアニメーションとThe Handy（Handy2）を連動させるプラグイン。

## BPM連動（0.2.0）
- 再生バーの「連動」はHandyの動作開始・停止。隣のボタンは `1 → 1/2 → 1/4 → 1` で切り替わり、倍率を保存する。
- HandySync設定の「HAMP / BPMでストローク」をONにすると、RhythmSyncの往復周期を変更通知で受け取る。既定OFF。
- 受信した往復BPMを倍率の分母で割り、1往復を `60 / 目標BPM` 秒にする。曲の1拍とキャラクターの1往復は区別し、RhythmSync側の弱・中・強の周期を使う。
- 幅は「HAMP Stroke」の下点・上点設定を使う。幅を変えた時は速度も補正し、同じ組として直列送信する。倍率はBPM連動中だけ適用する。
- BPM取得不可時・チェックOFF時は従来のBPM計算へ戻る。速度上限・下限、幅ゼロ、送信失敗はHandySync独立設定窓に表示する。独立窓は「UI / ShowUI」で表示する。
- 幅ゼロでは停止する。送信失敗時は停止を要求し、BPM連動中は次の周期・設定・動作状態の変更、または連動の入れ直しで再送する。停止要求自体が通信失敗した場合の実機停止は保証できない。
- HAMPの速度補正は機器の較正定数「VelocityConstant」（既定133）に基づく。実際の往復には折り返し・機器特性による誤差と、設定変更時の通信遅延がある。

## 使い方
1. Releaseビルドし、自身の `bin/Release/net472/MainGameHandySync.dll` を `BepInEx/plugins/canon_plugins/MainGameHandySync/` へ配置する。
2. v3の認証・ConnectionKeyを設定する。共通入力プラグインが必要。RhythmSyncは任意で、未導入なら従来動作になる。
3. canon_gateが許可するHシーンで「連動」をONにする。待機中の停止設定は継続する。
4. BPM連動を使う場合は「BPMでストローク」をONにし、再生バーで倍率を選ぶ。

自動検証は `dotnet run --project Tests/HandySync.Tests.csproj -c Release`。送信は模擬し、実機の動作確認とは分ける。

## 全通信・全通知の記録

`Logging / RecordAllHandyTraffic` でON/OFFする。既定OFF。出力先は配置DLLと同じフォルダの `MainGameHandySync.traffic.jsonl`。従来の通常・詳細・エラーログ設定とは独立する。

ONではHandy APIへのHTTP送信・応答の全文と、許可されたHセッション中にSSEで届く全種類の通知を記録する。SSEは通知フィルターを付けず、eventヘッダー・data行・未知の項目・区切りも保存する。認証情報と認証クエリは伏せる。本体がAPIへ公開しない内部センサーや音そのものは採取できない。

本体通知は動作用クライアントと別の接続・認証で購読し、速度・振幅・指令順序・キューには介入しない。OFF・H終了で購読を解除する。毎フレームの監視・APIポーリングは追加しない。

`sse-subscribed` が購読開始、`sse-ended`・`sse-unavailable` が採取中断を示す。切断中の通知は採取できない。自動再接続は行わず、記録設定を入れ直すか次のHセッションで再購読する。

検証は既存の `Tests/HandySync.Tests.csproj` と `Tests/Traffic/Traffic.Tests.csproj` で、動作指令の維持・全通知保存・認証情報の伏字・OFF時の購読解除を確認する。

詳細は `README.md` を参照。
