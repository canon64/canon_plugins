# MainGirlHipHijack

KoikatsuSunshine の H シーン向け BepInEx プラグインです。  
女性側 BodyIK 制御、追従編集、頭角度操作、ポーズ自動呼び出し、VR補助操作を提供します。

英語版: [README.md](README.md)

## ステータス

- ベータ版
- 女性向けワークフローを主対象に継続開発中
- 男性操作UIは現在非表示（内部機能は一部保持）
- `KoikatsuSunshine` と `KoikatsuSunshine_VR` の両方を対象にしています
- ConfigManager から `Enabled` `UI > Visible` `Logging > EnableLogs` を切り替えできます
- ホテル客室では13操作点の有効状態・重み・客室相対座標・ボーン／Composer追従先と頭・手の設定を客室ファイルへ保存し、キャラ配置とComposerの復元後に戻します。
- 再入室時は客室の姿勢を優先し、体位を変更するまで別マップの絶対座標を持つ自動プリセットで上書きしません；旧客室ファイルにHipHijack状態がない場合は本編の初期姿勢を保持します。

## 主な機能

- 女性 BodyIK 13エフェクタ制御（手/足/肩/腿/肘/膝/腰中央）
- 各エフェクタの `有効/無効` `ウェイト` `ギズモ表示` `現在アニメ姿勢へリセット`
- ボーン追従（Nearest Follow、追従ミラー、距離しきい値、VR時の頭ボーン→HMD変換）
- 女性頭角度操作
  - スライダー（X/Y/Z）
  - 回転ギズモ表示
  - `体位/モーション変更後も維持` チェック（OFF時は変更時に0へリセット）
- VR補助操作
  - 注射器を掴める範囲ではグリップを注射器に優先し、近くの頭の掴みは開始しません。刺入中・投与後も同じ優先順位です。
  - IKプロキシのVR掴みモード
  - VRでは許可されたHシーン開始時にVR掴みモードを自動でONにします。そのH中は手動でOFFにできます。通常版では自動ONにしません。
  - 左右の手首はIKがOFFでも、設定距離内でグリップを押すとIKを自動でONにして掴めます。掴み始めの位置・角度を保ち、離すとその姿勢を保持します。
  - 掴み中のトリガーで近傍ボーン追従を確定／解除できます。追従先なしでも単独のIKとして保持します。
  - VRで手首を掴んで動かしている間は、追従候補のボーンと接続線を表示します。候補ありは球が紫、候補なしは黄色になり、トリガーで表示中の候補への追従を確定します。
  - 手首を掴んでAを保持すると、手首→肘の延長上で肘から20cm先に肘IKの目標を置き、手首を支点に腕の向きを調整します。Aを離すと肘IKの位置を保持します。VR設定の「肘IK延長(cm)」で0～100cmをスライダーと数値入力で変更でき、初期値は20cmです。
  - Bで元アニメへ戻す際は、このA操作で制御した同じ腕の肘IKも解除します。反対の腕は変更しません。
  - 手錠装着中も、手錠を掴んだ側の物理A（Touch左手はX）で同じ肘IK操作ができます。通常の手首掴みへ切り替えず、手錠の拘束を維持します。物理Aの入力名は導入済みVR設定に合わせてApplicationMenuを使います。
  - 掴んでいる側のBで、その手の追従とIKを解除して元アニメへ戻します。同じ押下で腰簡易設定は発火せず、グリップを離すまで再取得しません。
  - IKで保持した手首にはグリップ解放後に手錠を装着できます。装着中は手錠側が制御し、通常の手首掴み・B解除の対象外です。手錠削除時は装着前のIK・追従へ戻ります。
  - モードOFFは通常解放として姿勢を保持します。手首IKの自動ON・B解除は設定ファイルへ自動保存しません。
  - 女性頭のグラブ回転（加算回転として保持）
  - 主要用途: VR時にトリガーを押しながら `B` で女性腰IKを有効化し、左コントローラーへ親子付けして、実コントローラーの動きをゲーム内へ反映
- ポーズプリセット（スクショ付き）
  - 保存/読込/上書き/削除
  - 体位一致時の自動呼び出し（`auto` チェック）
  - 遷移秒数・イージング（Linear/SmoothStep/EaseOut）
  - 頭角度ありポーズは頭角度も遷移ウェイトで補間
  - 頭角度なしポーズへ遷移時は頭角度を0へフェードアウト
  - `auto` 候補が0件または1件のときはループ回数による切替は行わない
- 腰系ツール
  - Enterキー（初期値）で HipQuickSetup をON/OFFトグル
  - ON時: 腰IK有効化 + SpeedHijack/女性アニメ速度切断を有効
  - OFF時: IK全OFF + 腰リンク解除 + アニメ同期リセット
  - `待機中の動きで自動挿入` をONにすると、待機中の腰動き検知から自動で挿入→ピストンへ移行します
- ログ/診断
  - `詳細ログ` で入力キャプチャや状態監視ログを出力
  - `BodyIK診断ログ` でIK適用前後の差分確認ログを出力
  - ログ出力先は `MainGameLogRelay` と専用ログファイルの両系統です
- `MainGameBlankMapAdd` 連携（VideoAllposeRoom）
  - `MainGameBlankMapAdd` が導入されている場合、VideoAllposeRoom の再生バーから HipHijack UI の表示/非表示を切替可能
  - 再生バー2段目の `説明` の右側にある `HipUI` チェックで切替

## 実行時挙動メモ

- 起動時はUI表示を強制OFFで開始します
- `auto` 候補が0件のときは BodyIK を全OFF にして、その体位では自動適用しません
- Hシーン突入時に以下をリセットします
  - SpeedHijack / 女性アニメ速度切断
  - 頭角度ギズモ表示
  - BodyIK有効状態
  - 腰リンク・HipQuickSetup状態
- `auto` 候補が1件だけのときは、その候補を使いますが、ループ回数による切替ローテーションは行いません

## 要件

- KoikatsuSunshine
- BepInEx 5.x

## 依存関係

### 必須（ハード依存）

- `MainGameTransformGizmo`
- `MainGameUiInputCapture`
- `MainGameLogRelay`

### 任意同梱（推奨）

- `MainGirlShoulderIkStabilizer`  
  必須依存ではありませんが、HipHijack配布プロファイルでは同梱対象として扱います。

## インストール

DLLを各プラグインフォルダへ配置してください。

- `BepInEx/plugins/canon_plugins/MainGirlHipHijack/MainGirlHipHijack.dll`
- `BepInEx/plugins/canon_plugins/MainGameTransformGizmo/MainGameTransformGizmo.dll`
- `BepInEx/plugins/canon_plugins/MainGameUiInputCapture/MainGameUiInputCapture.dll`
- `BepInEx/plugins/canon_plugins/MainGameLogRelay/MainGameLogRelay.dll`

任意同梱:

- `BepInEx/plugins/canon_plugins/MainGirlShoulderIkStabilizer/MainGirlShoulderIkStabilizer.dll`

## 設定と保存ファイル

- 設定: `BepInEx/plugins/canon_plugins/MainGirlHipHijack/FullIkGizmoSettings.json`
- ログ: `BepInEx/plugins/canon_plugins/MainGirlHipHijack/MainGirlHipHijack.log`
- 女性ポーズ: `BepInEx/plugins/canon_plugins/MainGirlHipHijack/pose_presets/index.json`
- 女性ポーズ画像: `BepInEx/plugins/canon_plugins/MainGirlHipHijack/pose_presets/shots/`
- 男性ポーズ内部保存: `BepInEx/plugins/canon_plugins/MainGirlHipHijack/pose_presets_male/index.json`
- 自動保存される主な項目:
  - `AutoEnableAllOnResolve`
  - `AutoPoseEnabled`
  - `AutoPoseSwitchAnimationLoops`
  - `PoseTransitionSeconds`
  - `PoseTransitionEasing`
  - `AutoInsertOnMoveEnabled`
  - `DetailLogEnabled`
  - `BodyIkDiagnosticLog`

補足:

- 初回起動時に自動生成されます
- 読み込み/保存時に値は正規化・クランプされます
- セッション限定のIK ON/OFF状態は起動時にリセットされます

## ビルド（ソース）

ターゲットフレームワーク: `net472`

ビルド:

`dotnet build MainGirlHipHijack.csproj -c Release`

出力:

`bin/Release/net472/MainGirlHipHijack.dll`

## プラグイン情報

- GUID: `com.kks.main.girlbodyikgizmo`
- Name: `MainGirlHipHijack`
- Version: `1.0.0`
- Process: `KoikatsuSunshine`, `KoikatsuSunshine_VR`
