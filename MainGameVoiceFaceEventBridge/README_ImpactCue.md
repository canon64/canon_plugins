# ImpactBoost 喘ぎ声モード

## 操作

ゲーム再起動後、再生バーの **ImpactBoost** と、その隣の **喘ぎ声モード** をONにする。
モードは初回OFF。主スイッチとモードのチェック状態はBlankMapAddのcfgへ保存する。
その隣の **音声: SBV2 / 音声: 既存** ボタンで音源を切り替える。今回はSBV2を選択済み。変更は次の発火から反映し、再起動後も保存する。

- 通常版：既存の腹ボコピーク検出から発火。
- VR版：既存の腰IK検出から発火。頭方向の距離・速度しきい値、平滑化、BodyIK/VR条件は従来設定を使用する。
- セリフがなくても再生する。再生中の再検出は予約せず見送る。
- 通常・VRともS/Wモーション中だけ受け付ける（SLoop/WLoopとA_SLoop/A_WLoop）。絶頂・絶頂後・待機などへ移ったら再生中の挿入音をOFF時と同じ設定でフェードアウトする。チェック状態は保持する。
- セリフ中に挿入した後は、実際に鳴った時間×`DialogueDowntimeMultiplier`の間、次の挿入を禁止する。初期値2なので、2秒鳴った後は4秒間セリフのみ。待機中の検出は予約しない。
- 発火条件を満たした検出ごとに`TriggerChancePercent`の確率で抽選する。初期値50%、100%なら毎回、0%なら発火しない。外れた検出で素材選択やダウンタイムは進まず、再生予約もしない。
- セリフ中は80msフェード→Pause→挿入音→UnPause→120msフェード。セリフの再生位置を書き換えない。
- 主スイッチまたはモードOFFは挿入音を60msでフェードアウトしてセリフを再開する。
- 明示的なセリフ停止、Hシーン破棄、アクティブシーン切替、追加マップ退出は挿入音とセリフを停止し、再開状態を解除する。

## 音声

`ImpactCueAudio/selection.json` に記録された音声を音源別のシャッフル候補群として使う。
既存音声は22本（Moanの2本＋Kissの20本）、SBV2音声は試聴一覧でチェックされた21本（`SBV2`フォルダー、`sbv2`配列）。
音源ごとに独立したシャッフル状態を保持する。切替中も鳴っている音声は最後まで再生し、次の発火から新しい音源を使う。
1巡で全素材を使用し、巡回の境界でも直前の素材を連続選択しない。
全素材を起動時にロードし、繰り返し再生可能な非ストリーミングAudioClipへ保持する。
挿入音の開始時にファイルを読み込まない。選択音源とJSONは既存の非公開ローカル素材として保持する。

挿入音の冒頭・末尾フェードはPCMサンプルへ適用する。設定変更時も保存済みPCMから再生成する。
セリフとOFF時のフェードはOnAudioFilterReadで音声フレームごとに補間する。

## 設定

`BepInEx/config/com.kks.maingame.voicefaceeventbridge.cfg` の `[ImpactCue]`。

| キー | 初期値 | 内容 |
|---|---:|---|
| CueSource | SBV2 | SBV2 / Original。再生バーの音源切替ボタンと共通の保存設定 |
| DialogueFadeOutMs | 80 | セリフ一時停止前のフェード |
| DialogueFadeInMs | 120 | セリフ再開時のフェード |
| CueFadeInMs | 80 | 挿入音の冒頭フェード |
| CueFadeOutMs | 80 | 挿入音の末尾フェード |
| StopFadeOutMs | 60 | モード・主スイッチOFF時のフェード |
| Volume | 1 | 挿入音量（0〜1） |
| CooldownMs | 150 | 挿入音終了後の追加間隔 |
| DialogueDowntimeMultiplier | 2 | セリフ中のダウンタイム倍率（0〜10）。鳴った秒数×倍率の間、次の挿入を禁止 |
| TriggerChancePercent | 50 | 発火率（0〜100%）。発火条件を満たした検出1回に対する確率 |

検出側のCooldownMsも併用する。再生バーの状態はBlankMapAddの`[03.Video] ImpactBoostEnabled / ImpactMoanModeEnabled`へ保存する。
通常・VRプラグインの`MoanModeEnabled`は再生バーから連動する。VR側JSONにも保存する。

## ビルドと配置

VoiceFaceEventBridge、VoiceImpactBoost、VrHipIkVoiceImpactBoost、BlankMapAddをそれぞれReleaseでビルドする。
DLLは各プロジェクト自身の`bin/Release/net472`から対応する配布先へコピーする。
VoiceFaceEventBridgeの出力に含まれる`ImpactCueAudio`（選択JSON＋43 WAV）も同DLLの隣へ配置する。
配布後は各DLLのサイズ・更新日時・ハッシュを比較し、BepInEx配下の重複を調べる。

## 検証

- `RegressionHarness`：状態遷移、見送り、クールダウン、停止、シャッフル、PCMフェードの回帰テスト。
- `RuntimeSmoke`：実際のUnity AudioSourceを使用する分離検証。製品のプレイヤー・通常/VR検出サービス・腰検出コードをリンクする。
  VR座標とキャラクターは検証用アダプター。VR機器の追跡、ゲームの口パク表示、人間によるつなぎ目の試聴は別の実機確認が必要。
- ログの`[impact-cue] preloaded=43`と`Original=22 SBV2=21`が素材準備完了、`dialogue=False`が無言時の受理、`dialogue paused/resumed sample=`が一時停止・再開位置を示す。
