# AI少女・シャンの標準衣装抽出

2026-11-01、導入済みAI少女の `ill_Default_Merchant` から衣装一式を特定し、KKS移植用の元素材を抽出した。

変換に必要な差分・衣装の土台・登録仕様・工程の調査は [CONVERSION_RESEARCH.md](CONVERSION_RESEARCH.md) を参照。

## 特定根拠

`MerchantActor.LoadCharAsync` は女性サンプル一覧から `ill_Default_Merchant` を指定して読み込み、同一覧は `custom/00/presets_f_00.unity3d` の同名データを参照する。

`metadata/MerchantActor.LoadCharAsync.cs` に既存逆コンパイル資料の該当箇所、`metadata/merchant_sample_rows.json` に実バンドルから復号した一覧、`card/ill_Default_Merchant.json` に標準キャラの全ブロックを保存した。

## 衣装と装飾品

|区分|名前|ID|モデル名|
|---|---|---|---|
|上着|なし|240:0|p_dummy|
|ボトム|レザースカート|241:16|p_cf_bot_merchant1|
|ブラ|ツイスト|242:14|p_cf_bra_twist1|
|ショーツ|Ｔバック|243:9|p_cf_shorts_03_00|
|手袋|革手袋左手|244:3|p_cf_gloves_merchant1|
|パンスト|なし|245:0|p_dummy|
|靴下|ニーソックス|246:3|p_cf_socks_knee1_00|
|靴|レザーブーツ＆チャップス|247:13|p_cf_shoes_merchant1|
|背中|商人カバン|359:4|p_acs_merchant_ruck|
|首|商人のマフラー|355:15|p_acs_merchant_scarf|
|首|商人のネックレス|355:16|p_acs_merchant_necklace|
|手首|薬瓶の腕輪|361:20|p_acs_merchant_arm|
|眼鏡|ライトゴーグル|353:21|p_asc_merchant_goggles|
|腰|商人の道具入れ|358:5|p_acs_merchant_waist|
|髪|ポニークリップ|351:27|p_acs_ponyclip|
|腕|バンダナ巻き|360:4|p_acs_arm_bandana|

スカートとブーツの実モデルは `chara/06/fo_bot_06.unity3d` と `chara/06/fo_shoes_06.unity3d` にあり、一覧上の `MainManifest` は `add06` を指定する。

## 保存内容

- `original/`：元のフォルダー構造を保持した36ファイル、498,991,969 bytes；関連バンドル・キャラプリセット・一覧・依存マニフェスト・Unity共通素材を含む。
- `card/`：標準キャラPNGと復号JSON；衣装色、柄、光沢、金属感、装飾品の親・位置・回転・倍率・表示条件を保持する。
- `assets/`：20メッシュのOBJ、138枚のPNG、素材・Transform・Renderer・衣装コンポーネントのJSON、Unityオブジェクトの生バイナリ。
- `assets/**/*.geometry_skin.json`：20メッシュの頂点・法線・UV・ボーン番号・ウェイト・バインド行列を読みやすい配列へ復号；そのうち12メッシュがボーン追従する。
- `metadata/outfit_rows.json`：衣装IDと実バンドル・モデル・テクスチャ・サムネイルの対応表。
- `metadata/asset_index.json`：元バンドル・シリアライズファイル・オブジェクトIDと抽出ファイルの対応表。
- `metadata/original_hashes.json`：元ファイルとコピーのSHA-256。
- `metadata/manifest_dependencies.json`、`resolved_external_references.json`：共通柄テクスチャ・Unity標準シェーダーを含む依存解決記録。
- `metadata/shader_names.json`：元シェーダー名の一覧。
- `metadata/summary.json`：集計と検証結果。

OBJは形状確認用で、ボーンとウェイトは含まないため、移植では `.geometry_skin.json`、Renderer/TransformのJSON、元バンドルを合わせて使う。

## 検証結果

元ファイル36件すべてのSHA-256がコピーと一致し、OBJ20件は頂点・面があり、PNG138件は画像として読み込めた。

12件の追従メッシュは全頂点のウェイトを復号でき、ウェイト合計・ボーン番号の範囲・Rendererからメッシュとボーンへの参照を検査して異常はなかった。

衣装IDの未解決、外部参照の未解決、抽出エラー、検証エラーはいずれも0件。

元バンドルには他衣装も同梱されているが、`assets/` はシャンの衣装参照から辿ったものと依存素材を抽出している。

## KKS移植の判断

モデル・テクスチャ・元のウェイトが取得できたため、素材面では移植作業に進める。

スカート、ブーツ、手袋、下着、靴下はKKSの素体とボーンへの適合が必要で、装飾品は取付位置と倍率を合わせる。

ブラ・ショーツ・スカートには `_a` と `_b` のメッシュがあるため、移植時には元の衣装状態切替との対応を調べて残す。

質感、柄と色の合成、肌隠し、揺れ物、KKS用衣装登録・zipmod化は未実施で、KKSのゲーム内表示も未確認。

## 再抽出

`J:\system_tools\venvs\py310-common\Scripts\python.exe` で `F:\kks\work\plugins\canon_plugins\tools\ai-shan-extract\extract_shan.py` を実行し、引数は `inspect` → `extract` → `finish` → `rig` の順に指定する。

導入済みUnityPyは `J:\temp\unitypy-inspect` を利用し、Blender・Unity・ゲームは起動しない。
