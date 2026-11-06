# 顔の実画像の確認と診断ログ

## 取得済みデータ
F:\kks\UserData\MagicPaint\Diagnostics\20261031-140910-640-0fe565\actor-1

実ファイルを画像表示ツールで開き、RGBAも読み取って照合した。
- face-cpu-ink.png: 1024x1024、非透明画素0、最大alpha 0。
- face-gpu-ink.png: 1024x1024、非透明画素0、最大alpha 0。
- face-slot0-composed.png / assigned.png: original.pngとの差分なし。
- body-cpu-ink.png / gpu-ink.png: 2048x2048、非透明画素63,282、最大alpha 255。
- body composed / assigned: 元画像との差分範囲がインクの範囲と一致。

顔の画像に線がなく、顔の描画が表示用レイヤーだけで消えたとは言えない。顔の実書き込み前／書き込み判定を調べる。
state.txtは「有効入力なし5秒経過」、rawTriggerReceived=False、acceptedTriggerReceived=False。入力操作の前にタイムアウトした可能性があるため、トリガーが故障したとは断定しない。
顔・体ともレンダラー参照・UV・三角形は存在し、元メッシュはreadable=True。readonly対応が今回の原因だったとは裏付けられなかった。

## ユーザー追加指示と実装
顔は接地しているように見えるが厳しく、ところどころ描けない。操作・状態変更時の診断を追加し、次回配置時に既存Diagnostics/Enabledを明示ONにする。製品既定値はOFFを維持する。
出力先: F:\kks\UserData\MagicPaint\Diagnostics\magicpaint.log

INPUT、接地可否／しきい値、遮蔽物、顔・体の最短距離と符号付き深さ、座標変換方式の距離比較、一筆前後の実書込回数、法線・UV・退化面での拒否件数を記録。座標比較は保持中に取得したキャッシュだけを用い、検査のために再Bakeしない。
通常描画の条件や20mm/30mmは、このログ追加では変更していない。
接地の変動ログは一保持中に各12変化まで。トリガー・一筆結果は制限で消さない。OFFでは収集・整形・ファイル・タイマーなし。
製品ビルド、コア145項目、実VRハンドラー30項目が成功。ログと顔の実機確認は次の実行待ち。

## 配置済み
2026-10-31 14:19 JST、ゲーム終了を確認して配置。
MainGameMagicPaint.dll: 112640 bytes / SHA256 FA2FBCC8630020FCDAE04065DE74605EEE187115306DDE8A818103241C3B4278。
正規成果物とのサイズ・日時・ハッシュ一致、同名DLL重複なし、入力設定の保持を確認。
既存cfgの[Diagnostics] Enabled=trueを確認。次回起動から通常のグリップ・トリガー操作でログを取得できる。画像検査ボタンを押す必要はない。顔の原因特定と改善確認は次の実ログ待ち。

## 14:28〜14:30の実ログと遮蔽修正（配置待ち）
保持中のTRIGGER_DOWNはaccepted=True。体へのスプレー／マジックは実書き込みがあるが、顔はfaceWrites=0、RASTER_FACE stamps=0。顔へのガイド成立は記録されていない。
OCCLUDERにo_shadowcaster / layer=11が繰り返し現れ、描画途中もこの判定で一筆が切れている。影専用の補助メッシュを実物の遮蔽物として扱っていた不具合を修正する。
導入済み素材の抽出データanalysis/ai-conversation-system/kks-skeleton-objects.jsonにも、o_shadowcasterとo_shadowcaster_cmがlayer=11で存在する。
両方の名前とlayer=11が一致する補助メッシュ、およびShadowsOnlyのレンダラーを接地・噴射の候補から除く。レイヤー11全体を無視せず、表示中の服・髪は遮蔽を維持する。接地20/30mm、描画条件、座標変換は今回変更しない。
遮蔽物ログへshadowCastingを追加。既存Diagnostics/Enabled設定と出力制限を維持する。
コア153項目＋実VRハンドラー30項目が成功。ゲーム起動中のためDLLは未差し替え。顔のガイド・描画と途切れの改善は、配置後の実機結果で確認する。この不具合だけで顔の全症状を説明できるとはまだ断定しない。

## 影用メッシュ除外版の配置完了
2026-10-31 14:34、KKS終了確認後に正規DLLを配置。113152 bytes / SHA256 CAD7CD8E97A356E150E6A80E63ECD3DF96FB3E1770F4E9F1033C3B25E8C0DDE9。サイズ・日時・ハッシュ一致、同名DLL重複なし。Diagnostics/Enabled=true。顔の実機確認は次回起動待ち。

## 最新結果と肌限定への変更
ユーザー実機で顔への描画成功。14:37のログで顔の実書き込みも確認。残る遮蔽物は髪と眉毛。新しい影メッシュの証拠はない。さらにATTACHED時normal=(0,0,0)、続く100スタンプの2328候補が全てfacingRejectedで書込0。小さい面にUnityのnormalizedを適用してゼロにしていたため、ラスタと同じ退化限界1e-10で単位法線を生成する方式へ変更。小面の実ラスタと裏面拒否など7試験を追加。ユーザー最新指示で遮蔽判定自体を撤去し、顔・体のネイティブ参照だけを収集。他メッシュの収集・更新・遮蔽レイ判定・Physics判定を削除。HTMLも最新仕様を追記。修正版は配置待ち。

2026-10-31 14:42、肌以外の判定を撤去した正規DLLをKKS終了中に配置。111104 bytes / SHA256 F0905009E7C6B02B7BB120A640FF7620D7DD98EB197ED4E706E5AE14693E29FB。サイズ・日時・ハッシュ一致、重複なし、Diagnostics/Enabled=true。160コア＋30実VR入力試験成功。修正版の実機確認は次回起動待ち。

## 顔の移動・回転時の座標修正
ユーザー実機で顔が動くと座標が合わなくなるとの結果。保持中のLateUpdateは実行順未指定で、既存VMDの頭向き更新30000より先になる構造だった。32000へ移し、追加のループ・購読は導入しない。BakeMeshのワールド変換はRenderer.localToWorldMatrixを廃止し、現在Transformの位置・回転（スケール再適用なし）へ変更。Unity公式SkinAttachmentTargetの変換方式を参照。範囲粗判定は現在TransformのlocalBoundsも含めフレーム内再利用。前のストローク点は三角形の重心座標で保持して現在の変形済み肌へ移動させ、古いワールド位置との補間を撤去。他メッシュの探索・遮蔽は復活させない。169コア＋30実VR入力＝199試験成功。移動・回転後の接地・レイ・スケール・表情変形追従を製品用計算で確認。実機追従は配置後の確認待ち。
資料：https://docs.unity3d.com/2019.4/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html および https://github.com/Unity-Technologies/com.unity.demoteam.digital-human/blob/master/Runtime/SkinAttachmentTarget.cs

2026-10-31 14:58、KKS終了確認後に顔追従修正版を配置。112640 bytes / SHA256 A1BD8CBC5FAF5D4D8309CF8420A9E2F960AE9E39043179315638C8D87C77CB38。正規成果物とのサイズ・日時・ハッシュ一致、同名DLL重複なし。Diagnostics/Enabled=true。顔を動かした状態の実機確認は次回起動待ち。

## ユーザーの追加症状と仕様の再確認（未解決）
髪付近でペン本体が飛ぶ・押し戻され、線が途切れる。要求は自動接地の廃止ではなく、肌への自動接地を残したまま髪を含む他パーツを無視すること。本体固定との二択は撤回。現行読込記録current-skin-poseと接地先cf_O_faceは確認済みだが、表示上の現象が存在しない根拠にはしない。ToolModelはCollider/Rigidbodyを生成していない。VR保持時にマウス描画は停止し、ギズモは無効化。共通操作権と参照したVRコントローラーソースにも髪によってペンを動かす経路は未発見。原因を確定できておらず、今回は処理変更・ビルド・配置を行っていない。
