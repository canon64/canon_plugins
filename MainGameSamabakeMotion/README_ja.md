# サマバケ通常H・カテゴリ別zipmod

対象は元146体位と男女ソロで、立ちキスの実機合格待ちは解除した；現在の導入・未導入件数と各理由は`deployment/all/coverage.json`、実配置は`deployment/all/managed_installation.json`が正本。

一覧登録は導入済みAnimationLoaderへ統一し、専用DLLは体位ごとの人物・状態・補正レイヤーの切替と復元を担当する；手動モーション選択UIは追加しない。

zipmodは`F:\kks\mods\canon_samabake`、補助DLLと`SamabakeNativeCatalog.json`は`F:\kks\BepInEx\plugins\canon_plugins\MainGameSamabakeMotion`へ配置する；旧立ちキスzipmodは重複しないようソース側バックアップへ退避する。

KKSとStudioを終了して`Disable-Samabake.cmd`を実行すれば、今回のzipmod・補助DLL・専用定義だけを`deployment/disabled`へ退避する；`Enable-Samabake.cmd`で戻し、`Set-SamabakeEnabled.ps1 -Action Status`で配置状態を表示する。

撤去復元は記録済みのパスとハッシュを全件確認してから行う；別MOD、カード、ゲーム本体は移動せず、途中の移動失敗はロールバックし、変更されたファイルや復元先の衝突があれば書換え前に停止する。

補助0.5.0では元のIK目標用PV骨のアニメーションを追加し、元の体位別IK接触先・身体補正をKKSの既存ソルバーへ状態変更時に設定する；新しいフレーム監視は追加せず、体位切替・H終了・失敗時には元の設定を戻す。

速度は愛撫の`speedBody`と奉仕・挿入等の`speed`を区別して通常Hの更新をそのまま受け、身長はKKSの実際の`cf_n_height`スケールを元ゲームの身長キーデータへ逆対応する；元から強モーションがない体位はその体位自身の弱ループを使う。

膝・肘などKKSに直接対応する形状項目がない補正、愛撫の弱強間の補正差、元ゲーム側にも個別IKファイルがない体位は`deployment/all/contact_report.json`と対応表の`contact_issues`へ記録する；接触距離と実機再生の合格を意味せず、見た目と道具は引き続きユーザー確認が必要。

既存134Bundleはハッシュ照合して再利用する；新規分は必要骨の対応、全フレームの有限値・正の有界スケール・参照・状態を確認し、全フレーム実メッシュ検査を必須にはしないため、既存分の検査水準と区別する。

必要骨、人物数、元の進行データが不足するものは登録を止めて理由を残す；AnimationLoaderに2人目の男性を渡す欄がない構成や、通常H用の進行がないソロを別体位で代用しない。

`tools/all_motion_source.py`で未変換役割を処理し、IK修正版は`tools/inspect_contact_source.py`と`tools/inspect_rig_source.py`の元データ検査後に`tools/prepare_contact_assets.py`、`tools/build_all_zipmods.py`で生成する；`Test-All.ps1`で実AnimationLoader XML読込・配布アセット・元の身体曲線保持・復元44項目・撤去復元6件・元補正225表・Releaseビルドと実DLLの契約を検査してから`Install-All.ps1`で配置する。

旧`Test-Zipmod.ps1`と`Install-Zipmod.ps1`は全件版への互換入口で、立ちキスだけの旧配置は行わない；`Stop-Conversion.ps1`はこの全件変換のPythonプロセスだけを停止し、完了済み変換は再利用できる。

補助DLLはcanon_gateの既存許可に従う；診断はBepInEx設定`canon.maingame.samabake.motion.cfg`の`Diagnostics/Enabled`で切り替え、既定OFF、ON時の出力先はプラグイン配置先の`native-h.log`とBepInExログ。

ゲームは起動・操作せず、通常Hでの一覧表示・再生・終了・他体位切替・接触位置はユーザーの確認結果を受けて判断する；未導入分が残る間は全件完了とは報告しない。
