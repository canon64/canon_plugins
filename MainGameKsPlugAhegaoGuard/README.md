# ksPlug Ahegao Null Guard

ksPlug 1.6.2 の `AI.HasReachAhegao()` に Harmony Prefix を挟み、ChaCore / HInfo / HCount が欠けたキャラだけ false を返す互換パッチ。通常のキャラは元処理を実行する。

canon_gate の許可されたH開始時に適用し、終了・無効化・破棄で自身のパッチだけを解除する。監視処理や毎フレームの探索は追加しない。ksPlug本体は変更しない。

GUID: `com.kks.maingame.ksplugahegaoguard`。通常Hでは canon_gate の EverywhereAllowedPluginGuids、AI少女モード由来のHでは AiShoujoModeAllowedPluginGuids にこのGUIDを許可する。

対象は通常版 KoikatsuSunshine + ksPlug.dll 1.6.2。ゲーム内の追加サブキャラ表示とエラー停止はユーザーによる確認が必要。
