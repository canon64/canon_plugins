# AI少女の生活素材DB

原本の生活行動・在庫・飲食・レシピ・小物・設備・地点・アニメーションをSQLiteと実ファイルへまとめる。抽出結果と未解決件数の正本はmanifest.json。

収録：品物265件、行動359件、結果表215件、飲食効果20件、レシピ166件、動作地点3,687件、小物定義83件、クリップ1,545件、OBJ 1,276件、PNG 2,323件。185 Bundleの保存コピーを原本とSHA-256照合済み。DB整合性と検索例は検査済み。

残り：外部参照106件と文字列参照候補19件は未照合。原本にピクセルがないフォント用テクスチャ8件はPNG化できない。外部参照一覧はRuntime/unresolved-pointers.json、文字列参照候補一覧はRuntime/remaining-references.json。未照合を資産欠落と断定しない。

- materials.sqlite：原本ID・行・資産・参照関係を検索するDB。
- native_objects：保存したBundle内の全オブジェクト索引。個別ファイルへ変換していないものも種別・識別情報を引ける。
- Runtime：実装から読みやすいJSON。数値は原本値であり、KKSの設定やセーブへ自動反映しない。
- assets/bundles：原本Bundleの保存コピー。DBにSHA-256を保持する。
- objects：オブジェクトの全フィールドをgzip圧縮したJSON。巨大なバイナリはassets/blobsへ分離する。
- assets/meshes・textures：OBJ・PNGの交換形式。材質・階層・骨の対応はDBと原本JSONを参照する。
- assets/raw：AnimationClip・Controller・Avatar等の原本シリアライズ。KKS用へ変換済みとは扱わない。
- Source：原本の読込規則を確認した管理コード。

検索例（PowerShell、共通Pythonを使用）：

```powershell
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py summary
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py search 風邪
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py search ベッド
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py action 0:0
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py item 0:0
& J:\system_tools\venvs\py310-common\Scripts\python.exe .\material_query.py issues --limit 50
```

actionの値はposture:pose、itemの値はcategory:item。objectには検索で返った完全なidを渡す。--outputで検索結果をJSON保存できる。

原本行はtable_rows.cells_json、数値化した行動結果はaction_results、飲食効果はfood_drink、行動と動作の入口はactions、設備はpointsとpoint_anchors、実資産はobjectsとcontainers、内部参照はpointers、文字列によるBundle参照候補はasset_refs。各参照には解決状況を持つ。asset_refsの隣接セルによる抽出は候補であり、原本の読込コードと照合して使う。

recipesは完成品と材料を在庫品へ結んだ表、event_itemsとequipment_itemsは表示小物と行動の対応表。風邪薬の例はRuntime/example-cold-medicine.json、休息の例はRuntime/example-rest.json。原本には風邪薬1個の材料として解熱草2個が定義されている。

resolvedは個別データまで解決済み、native_bundle_onlyは保存Bundle内で存在確認済みだが個別変換なし、unity_builtin_referenceはUnity標準資産、outside_scopeは今回の対象外、bundle_onlyはBundleだけ指定、unresolvedは参照先の照合未完了。Runtime/remaining-references.jsonには文字列参照の未照合候補を残す。

再生成はanalysis/ai-conversation-system内のmaterial_library.py→material_native_index.py→material_finalize.pyの順。検索は同梱material_query.pyだけで動く。再生成には既存のUnityPy・NumPy環境を使用する。

行動結果と飲食効果はeffective=1を使用し、旧行は削除しない。表の読込順が同名資産の順序に依存するケースは原本行と出典を再確認する。source map worldは原本マップ座標、source prefab hierarchyは配置前のプレハブ内座標であり、KKS座標へ自動変換しない。

H専用・キャラ本体・音声・イベントCGは今回の生活素材抽出の対象外。既存のセクシー音声・会話調査は元フォルダーに保持する。資産不足・参照未解決・変換失敗はissuesとv_unresolvedから個別に確認する。ゲームは起動せず、KKSへ配置もしない。
