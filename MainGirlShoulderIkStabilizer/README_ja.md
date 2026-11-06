# MainGirlShoulderIkStabilizer 1.1.1

HipHijackで手IKを有効にした側の肩を、上腕の挙上に合わせて持ち上げる。肘を曲げて手を頭の後ろへ置く姿勢でも、手の距離だけで補正が消えない。通常版と公式VRの共通FinalIK経路を使用する。

## 動作

- 背骨から取得した胸の上方向と「肩→肘」の角度を使い、腕を下げた状態を0度として評価する。
- FinalIKの腕マッピングに登録された鎖骨側の親ボーンを回し、二骨IKで解決済みの手位置・手首回転・肘の曲がる平面を維持する。手や肘のターゲット重みは再適用しない。
- 既存姿勢ですでに上がっている肩の量を差し引く。リーチ補助の上方向成分とは大きい方を使う。骨長を変えず、届く範囲へ追加回転を制限する。
- 肩位置操作と鎖保持は遷移ウェイトで受け渡す。解除中は参照を保持し、完了後に解放する。
- HipHijackの参照変更・腕IK切替・設定変更・canon_gateの開始終了で接続する。スタビライザーのUpdate/LateUpdate探索は使用しない。
- 自分が書いた回転と一致する場合だけ前回出力を取り除く。他のアニメーションや操作が更新した回転は戻さない。
- AdvIKの肩補正は担当する側だけ抑制する。カード設定・保存値は変更しない。

## 調整

ConfigurationManagerの既存設定を使用する。専用ウィンドウはない。

| 設定 | 初期値 | 内容 |
|---|---:|---|
| `Elevation.Weight` | 1 | 腕上げ追従の強さ |
| `Elevation.StartAngle` | 45° | 補正を始める上腕角 |
| `Elevation.FullAngle` | 150° | 持ち上げ量が最大になる上腕角 |
| `Elevation.MaxLift` | 35° | 初期姿勢からの持ち上げ目標 |
| `Elevation.MaxForward` | 30° | 挙上した上腕を前へ出したときの肩の前方追従上限 |
| `Motion.TransitionSeconds` | 0.15秒 | 自動補正・肩操作・鎖保持の遷移 |
| `Motion.MaxAngularSpeed` | 120°/秒 | 自動追加回転の速度上限 |
| `Safety.MaxShoulderDeltaAngleDeg` | 35° | 追加回転の上限 |
| `Safety.MaxSolverBlend` | 0.8 | 実効ブレンドの上限 |

`Shoulder.IndependentShoulders`をONにすると右側は`Elevation.RightWeight / RightStartAngle / RightFullAngle / RightMaxLift / RightMaxForward`と既存の右リーチ設定を使う。OFFでは左の値を両側で使う。`MaxLift`は目標値であり、実出力はブレンド・肩拘束・到達制限で小さくなる。

1.1.3では上腕の挙上角と胸に対する前方向から、肩を上方・前方へ追従させる。手までの距離が短く、肘を曲げた姿勢でも前方補正は消えない。腕下げ・腕を後ろへ出す姿勢では前方補正を追加せず、真上付近では前方向が不定になるため滑らかに弱める。`MaxForward=0`で前方補正だけを停止できる。手先・手首・骨長の保持と追加回転・到達制限は引き続き適用する。

1.1.4では腕上げ中の上下・前方向の目標を基準肩姿勢と上腕から決め、旧リーチ補助が手首位置で目標を変える経路を切り離した。腕下げ側から水平まで滑らかに切り替え、既定設定では水平以上で`upperModeBlend=1`になる。元の肩が下がっていても、その下がった角度に持ち上げ量を加算せず、基準姿勢からの持ち上げ目標を使う。手首による到達制限・手先保持は残るため、届かない姿勢で実際の適用量まで一定になるとは限らない。

1.1.5では`ShoulderDiagnosticLog`のON時だけKKS本体の`ExpressionBone.LateUpdate`前後と`ChaControl.LateUpdateForce`後を観測する。IK補正の前後と同じ`frame/nativePass`で補助骨・上腕・前腕・手の位置、回転、スケール、ネイティブ肩補正の入力と有効状態を記録する。`SKIN_BIND/SKIN_BONE`は診断接続時に一度だけ描画メッシュの肩ボーンと影響頂点数を記録する。OFFとH終了で追加パッチ・診断対象参照を解除し、状態収集も出力も停止する。これらは実際の処理境界の記録であり、描画直前を保証するものではない。肩計算は1.1.4のまま。

1.1.7は停止中の本体補助骨処理を補うため、腕IKの合成後に実際の`ExpressionBone.LateUpdate()`を呼び、操作中の腕のShoulder/Upperarmだけを再計算する。その他のカテゴリは呼出し中だけ除外し、全スイッチと全体enableを必ず元に戻す。補助骨の位置・回転は次のIK前、補正OFF、対象交換、H終了時に所有確認付きで復元する。後からアニメーション等が書き換えた値は巻き戻さない。`Shoulder.NativeAuxiliaryCorrection=false`でこの再計算だけを停止できる。`NATIVE_CORRECTION`と`native_aux_before/after`で実際の変化を確認する。テストは呼出し制御と復元の確認であり、本体の皮膚変形や見た目は実機で判断する。

旧`ArmState.RaisedArmStartY / RaisedArmFullY / RaisedArmScaleMin`は互換読み込み専用。角度設定へ変換しない。JSONに新項目がない場合は上記の初期値を使用し、起動時はBepInEx cfgを優先する。

## ビルド・検証

本体ツリーから `MainGirlShoulderIkStabilizer/tools/verify.ps1` を実行する。両プラグインのReleaseビルド、実装をリンクしたヘッドレス試験、HipHijackの既存characterizationを行う。`-TestsOnly` / `-BuildOnly`も使用可能。

テストは剛体の骨階層とエンジン境界のスタブを使う。Unityのスキニング、実FinalIKの収束、実機でのHarmony競合、通常版・公式VRの描画確認を代替しない。

## 導入と実機確認

依存は`MainGirlHipHijack >= 1.0.0`（肩通知API v1を追加した版）、`MainGameLogRelay`、`canon_gate`。今回の2つのDLLは組で更新する。HipHijackは現在のIK計算・座標・UIを維持し、肩通知だけを追加した版を使う。ゲーム終了後、それぞれ自身の`bin/Release/net472`成果物を対応する`F:/kks/BepInEx/plugins/canon_plugins/<plugin>/`へ配置し、サイズ・日時・SHA-256を照合する。他プラグインのbinにある同名DLLは使わない。

1.1.2では`Logging.ShoulderDiagnosticLog`がONならAwakeの初期化前から専用`log/ShoulderPoseDiagnostic.log`へ記録する。`BOOT`で設定読込・パッチ準備・gate登録の到達点、`BOOT_FAILED`で初期化例外、`STATE_FAILED`で依存APIの診断失敗を確認できる。`gate_registered`だけなら許可されたHの開始待ち、`STATE reason=gate_begin`は開始通知、`RUNTIME`はキャラ接続、`BIND`は骨参照、`CALC`と`POSE`は実測値を示す。Relay全体OFFでも記録し、上腕の前方角・肩の前方角・目標角・制限・後段の上書きを4段階で比較できる。補正OFF中も手IKが動いていれば観測だけ継続する。設定OFFでは診断収集とファイル出力を停止する。

ユーザーが画像と同じ腕上げ・肘曲げ姿勢で、肩から脇の輪郭、腕の上げ下げ、肩固定の開始・解除を確認する。左右、前屈・仰向け、体位変更、H終了後の次のHでも確認する。見た目はこの実機結果で判定する。診断項目は[LOG_MAP.md](LOG_MAP.md)参照。

到達限界では手先の維持を優先して肩補正を縮める。入力姿勢が突然到達範囲を外れた場合、角速度上限より到達制限が優先される。非一様スケール等で二骨計算が成立せず手先誤差が1mmを超えた場合は、その回の補正を戻す。
