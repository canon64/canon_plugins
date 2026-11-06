# ホテル3F ステージ素材一式

先に作成した内観イメージを基にした11素材のPBRテクスチャで、ゲームへの組み込みやモデルの変更は行っていません。

## 素材と推奨タイル幅

| フォルダー | 用途 | 1タイルの幅 |
| --- | --- | --- |
| 01_black_stage_stone | 黒い鏡面ステージ床 | 1m |
| 02_red_curtain_velvet | 奥の赤いカーテン | 0.5m |
| 03_burgundy_seat_velvet | 客席のワイン色ベルベット | 0.5m |
| 04_burgundy_carpet | 客席のカーペット | 1m |
| 05_dark_wall_stone | 暗色の石壁 | 1m |
| 06_charcoal_ceiling | 天井の暗色塗装 | 1m |
| 07_brushed_brass | 低いバリア・縁の真鍮 | 0.5m |
| 08_chrome_pole | 中央ポールのクローム | 0.5m |
| 09_black_fixture_paint | スポットライト・ノズルの黒塗装 | 0.5m |
| 10_frosted_led_diffuser | LEDラインの乳白カバー | 0.25m |
| 11_black_step_rubber | 出入口ステップの滑り止めゴム | 0.5m |

## 各素材のファイル

- BaseColor: sRGBの照明を含まない色です。
- NormalGL: 接線空間のOpenGL形式（+Y）で、BlenderやUnityの通常の取り込みに使用します。
- NormalDX: NormalGLの緑成分を反転したDirectX形式（-Y）です。
- Roughness: 白が粗く、黒が滑らかです。
- Metallic: 金属は白、非金属は黒です。
- Height16: 中間値32768を基準とした16bitの微細凹凸で、カーテンの大きなひだは含みません。
- AO: 平面素材用の白画像で、モデル固有の陰影はモデル作成後に別途ベイクします。
- Emission: LED素材のみ白、ほかは黒で、LEDの色と強さはシェーダー側で指定します。
- ORM: R=AO、G=Roughness、B=Metallicです。
- UnityMetallicSmoothness: R=Metallic、A=1-Roughnessで、GとBは未使用です。

## 取り込み

BlenderではBaseColor以外をNon-Colorとし、NormalGLをNormal Mapノードを経由して接続します。

UnityではNormalGLのTexture TypeをNormal mapに設定し、Create from Grayscaleは無効にします。

Unity StandardのMetallicワークフローではUnityMetallicSmoothnessをMetallicに指定し、Smoothnessの参照元をMetallic Alphaにします。

ラフネス・金属・AO・高さ・パック画像はsRGBを無効にし、繰り返し素材のWrapをRepeatにします。

床の鏡面像は反射プローブや対応する反射シェーダーで作成し、BaseColorに照明像を重ねないでください。

カーテンのひだ、客席の縫い目、バリア、ライト、ノズル、LED溝はメッシュで作成し、スモークはパーティクル等で表現します。

12_effectsには透過スモーク画像と円形スポットライト用の減衰マスクを収録し、スモークにはノーマルを使用しません。

スポットライト用マスクの読み込みは使用シェーダーの仕様に合わせ、カーテン裏の間接照明とLEDの色変化はライトまたは発光パラメーターで設定します。

客席テーブルの黒ガラスには床の色を流用してRoughnessを0.03前後に下げ、必要なら透過をシェーダー側で設定します。

## 作成方法と確認範囲

BaseColor原画は組み込みimagegenで素材別に生成し、実際に使用した指示をgeneration_prompts.jsonに収録します。

build_pbr.pyは原画の周期境界と均一素材の大きな明暗むらを補正し、色の細部と周期的な微細凹凸から整合する接線空間ノーマルを計算します。

石の色脈を盛り上がりと誤認しないよう、石とクロームのノーマルには微細な粒状凹凸を使用します。

全マップは2048四方で出力し、AI原画の実寸はmanifest.jsonに記録するため、出力寸法と原画の細部解像度は区別できます。

これは高精細モデルからベイクしたノーマルではなく、平面素材の微細凹凸用ノーマルです。

manifest.jsonには枚数、原画寸法、ノーマルの単位長誤差、境界差分を記録し、material_contact_sheet.jpgで色・ノーマル・反復を確認できます。

ゲーム内での質感・照明・反射は未確認で、最終的な見た目は実際のモデル、UV、シェーダーと照明に依存します。
