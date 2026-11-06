# MainGameSpiralEyes

1.6.2は `KKUTSeye` に対応。導入済みKKSUTS v2.2から元の38個のGPUプログラム・描画2パス・全設定項目・フォールバック依存を保持して螺旋パスだけを追加し、`pupilfollow-kkutseye.bundle` として使う。元のMODは変更しない。標準目とKKUTS目で切替先を分け、解除時はそれぞれ元のシェーダーへ戻す。1.6.1の失敗診断も維持する。

KKUTS素材の再生成は `J:\system_tools\python310\python.exe -B tools\pupil_follow_assets.py merge-uts`。元シェーダーのハッシュが変わったら生成を拒否する。生成後の照合結果は `Tests/results/pupil-follow/kkutseye-asset-verification.json`。コード境界試験は `MainGameSyringe/Tests` に含む。KKUTS版のゲーム内描画・視線追従・注射適用・解除はユーザーの実機確認が必要。

1.6.0では注射器・ドール・ディルド・手動の螺旋を、瞳と同じ材質内の追加描画パスへ統一した。瞳のUV0、位置、拡縮、傾きと同じ座標をGPUで直接使うため、視線追従の監視や毎フレームのコピーはない。螺旋の中心は瞳の中心、倍率は標準素材由来の3倍で、ドールの位置補正は瞳基準になる。虹色の帯幅は変更しない。

注射器の `BeginFadingOverlay(owner, actor)` は目番号を条件にせず、現在の瞳へ螺旋を重ねる。ハートなどの元の表情テクスチャは置き換えない。`SetOverlayOpacity(owner, actor, level)` のlevelは0～100で、螺旋パスの透明度へ反映する。目番号や瞳素材の変更完了時も同じ濃さで再接続する。解除後は変更後の目を残す。他の連動元や手動螺旋目の濃さは注射器の減衰で下げない。

ディルド連動の一時適用API `PublicApi.BeginOverlay(owner, actor)` / `EndOverlay(owner, actor)` は、両プラグインがgateで許可されたH参加女性に限定する。開始の成功時だけ通常の目へ螺旋画像を重ね、キャラの保存設定や目パターンIDは変更しない。手動選択の螺旋目も維持し、終了・無効化では一時要求を破棄して `OverlayReleased` を通知する。

既定は `General / UseOriginalDrawing = true`、`AnimationEnabled = true`。最初に描いた3.5周の螺旋PNGの透明度を形としてそのまま使用し、GPUで回転・着色する。形を別の数式で描き直さない。回転は毎秒0.45回、虹色は外側から内側へ流れる。zipmodの静止画像も同じ正本PNGを使用する。

canon_gateが許可したHシーンで、元の瞳へGPU生成の虹色螺旋画像を重ねる。通常版・公式VRのH参加女性を開始時に取得する。ゲームが使っている材質のシェーダーだけを一時的に切り替え、材質・メッシュは追加しない。終了・無効化・キャラ破棄で元のシェーダーを戻し、ゲームが変更した瞳の位置や素材を巻き戻さない。対応は標準 `Koikano/main_eye` と `KKUTSeye`。それ以外の材質は警告して元の目を維持する。

F1のBepInEx設定で `MainGameSpiralEyes` を開き、`General / Enabled` で適用・解除する。元画像方式は回転、色の速度・周期・色相・彩度・明るさ・強度・発光と脈動、左右反転、虹色・指定2色・単色を調整できる。`AnimationEnabled = false` は `OriginalDrawingTint` の色の静止画へ切り替える。アニメーション素材が利用できない場合も同じ静止画へ戻し、そのHで警告を1回だけ出す。

周間隔、線幅、腕数、曲率、移動、位置、変形、ぼかしなどは `UseOriginalDrawing = false` の旧数式方式専用。元画像方式では形を維持するため適用しない。各 `変調.<項目名>` は振幅・周波数・位相・波形を持ち、振幅0で周期変調を止める。設定変更はイベントで反映し、GPU動作中の色・速度変更では画像を作り直さない。

アニメーションはCustomRenderTextureのGPU更新とシェーダー時間で動く。プラグインにUpdate、部屋の再照会、常駐コルーチン、タイマーはない。ゲームが目表情を変更した時だけHarmony通知から重ね画像を再接続する。音声解析は含まない。KKS側の連動元からUnityメインスレッドで `Plugin.SetParameter(key,value)` を呼べる。

`canon_spiral_eyes_v1.0.0.zipmod` は目表情一覧へ新しい「canon 虹色螺旋目」を追加する。プラグインは、許可されたH参加女性がこの追加項目を選択している場合だけアニメーションを適用する。通常の目や他MODの目は変更しない。他の目へ変更するとそのキャラのアニメーションだけ解除し、最後の対象がいなくなればGPU画像も破棄する。数値IDはSideloaderが変更するため、追加項目の画像パスと画像名で識別する。Maker・Studioでは追加項目の静止画像を使用し、アニメーション接続は含まない。

`Diagnostics / Enabled` は既定OFF。ONでは登録、H許可、対象女性数、適用した目素材数、停止をイベント時だけ記録する。

注射器の瞳円運動と螺旋は同じGPU計算を使用し、螺旋画像を別途動かす二重処理は行わない。追加投与や目素材の再接続では位相を維持する。手動螺旋目の選択中だけ元の静止表情を抑えて二重表示を防ぎ、解除時に戻す。元のハートとハイライトは元の描画パスで描き、その直後に螺旋を重ねる。

`BuildPupilFollow.ps1` は追加パスをコンパイルし、`tools/pupil_follow_assets.py` で検証済みのKKS実シェーダーの12個のプログラムと元の描画パスを無変更で保持して `Resources/pupilfollow.bundle` を作る。未知の元シェーダーのハッシュではビルドを拒否する。検証用の元シェーダーは別bundleとして `Tests/results/pupil-follow` にのみ出力し、製品bundleへ含めない。

追従のGPU検証は、元の目・ハート・ハイライトの透明度0での画像一致、適用後の座標変更、縦横の拡縮・反転・傾き、円運動の各位相、位置補正、透明度、HDR、解除を確認する。結果は `Tests/results/pupil-follow/GPU-PASS.txt` と `asset-verification.json` に保存する。`BuildPupilOrbit.ps1` は円運動bundleと注射器の刺入境界を別途検証する。ゲーム内のまぶた・髪との重なり、通常版・VRの実際の表示はユーザーによる確認待ち。

`BuildAssets.ps1` はUnity 2019.4.9f1を非表示のバッチコンパイラ・GPU検証器として使用する。ゲームやUnityの編集画面は起動しない。製品AssetBundle・正本PNG・実際のGPU画像生成と目素材接続コードを使用し、Direct3D 11で描画フレームを進めて検査する。結果は `Tests/results`、合格マーカーは `GPU-PASS.txt`。明示時刻での比較に加えて、Unityの実際の `_Time` による変化も検査する。

`Tests/DefinitionsTests.csproj` は設定と製品HSessionClient・実GateApiの許可・拒否・停止・失効を検査する。`MainGameSyringe/Tests` は製品の要求API・目変更フック・追従接続コードで複数所有者・濃さ・キャラ破棄・部分失敗を検査するが、Unity APIはスタブでありGPU検証と区別する。

配置指示時はゲーム終了を確認して `DeployRuntime.ps1` で本体の `bin/Release/net472/MainGameSpiralEyes.dll` と `pupilfollow.bundle`・`pupilfollow-kkutseye.bundle`・`pupilorbit.bundle` を一組で配置し、サイズ・日時・SHA-256を照合する。既存ファイルをJドライブへ退避し、コピー失敗では一組を戻す。旧 `DeployDll.ps1` も同じ配置へ転送する。初回導入用 `Deploy.ps1` は正本PNG・設定定義・既存bundle・zipmodも含める。GPU検証・配置・KKS実機確認は区別する。
