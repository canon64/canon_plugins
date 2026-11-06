# ksPlugの時間進行制御

`Plugin.KsPlugTime.cs` の `InitializeKsPlugTimePatch()` が唯一の初期化窓口で、`KsPlugTimePatch.cs` に通常版・VR版の時間進行条件を差し替える処理を集約しています。

AllPoseRoomでアストラル突入から帰還までの間だけ時間加算を止め、ksPlugの設定値や保存ファイルは書き換えません。

`00. General / Suppress ksPlug Time Passes In Astral` をOFFにすると即座に制御を無効化し、ksPlug本来の時間設定に従います。

コードから隔離する際は `InitializeKsPlugTimePatch()` の呼び出しを外し、実行中の専用パッチは `KsPlugTimePatch.Remove()` だけで解除できます。

専用Harmony識別子は `com.kks.maingame.astralmode.ksplugtime` で、解除時にAstralModeのほかのパッチや別プラグインのパッチを外しません。

対応する時間条件が見つからない場合は警告を出して対象パッチを適用せず、設定値の上書きへは戻しません。

自動検証は `AstralMode.TimePatch.Tests` で行い、VRの実DLL検査は `--installed-vr` を付けた別プロセスで実行します。

ゲーム内ではksPlugの時間経過をONにして、通常時は進行、アストラル中は停止、ksPlug設定メニューを開閉しても停止を維持、帰還すると再開することを確認してください。
