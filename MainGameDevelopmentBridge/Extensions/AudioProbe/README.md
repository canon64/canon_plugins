# AudioProbe

`AudioProbeR2.dll` を開発ブリッジの `load-extension` で読み込み、`{"op":"audio-state"}` で同じ要求時点の全音源・全Listener・Hカメラ・口の実座標と音声設定を採取する。

`managerAudioListener` は `Manager.Sound` 本体が所有する実AudioListener、`soundListener` は本編がそのListenerの追従先として指定したTransformで、両者を区別する。

`sources` は停止中を含み、`nativeVoice`・`isPlaying`・`enabled`・`active` で選別でき、`reference` と `clip.reference` はブリッジの後続要求用handleになる。

`fromSoundListener`・`fromNativeCamera`・`fromListeners` の `right_m` は正が右・負が左で、`up_m`・`forward_m`・`distance_m` は同じ基準のメートル値を示す。

`{"op":"audio-state","mode":"camera-left","dx":<移動量>}` はHカメラの焦点を世界X方向へ指定メートルだけ一時移動し、本編CameraDataへ反映する；符号は世界Xであり画面の左右ではなく、連続実行は復元前に拒否する。

移動直後の応答だけで持続を判断せず、次の `audio-state` 要求でも座標を採取し、本編コントローラーや他MODによる再設定の有無を比較する。

`{"op":"audio-state","mode":"restore"}` または拡張のDisposeで保存したCameraData・位置制限・コントローラーと表示カメラの位置・回転を戻す；音源・Listener・音量・残響・永続設定は変更しない。

常時監視・イベント購読・ゲーム起動処理は持たず、要求時だけ採取・明示された一時カメラ操作を実行する。

`outputSamples` に正の二の冪を渡すと全体出力の左右RMS・peak・energy比を一回採取し、`clipThreshold` に正の閾値を渡せば絶対値が閾値以上のサンプル割合も返す；素材そのものの音割れとは分けて扱う。

`pcmSamples` に正のフレーム数を渡すと再生中の各元clipからPCMを一回読み、同じ指標と各チャンネルを返す；開始位置は現在再生位置で、`pcmOffset` を明示すればそのフレーム位置になる；GetData非対応の圧縮・ストリーミング素材は失敗理由を返す。

同じCLRアセンブリidentityの再ロードで旧実装が返る実環境があるため、実装更新後に実行中プロセスで再ロードする場合はAssemblyNameも変更し、古いカメラ試験を先にrestoreする。
