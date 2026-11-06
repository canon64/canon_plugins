# ホテル2Fの装飾

観葉植物は生成イメージを元にした高さ約1.8m、葉14枚、78,100三角形の編集可能な実モデルで、葉・大小の葉脈・茎・陶器鉢・金属縁・受け皿・土粒を立体化している。

完成原本は `Plant/hotel-strelitzia.blend`、交換用は `Plant/hotel-strelitzia.glb`、確認画像は `Plant/plant-beauty.png`、形状検査は `Plant/topology-audit.json`。

`model_plant.py` をBlender 5.2.2の `--background --factory-startup --python-exit-code 1 --python` から実行し、引数は `-- --stage blockout` または `-- --stage detail` とする。

`export_decorations.py` を同じBlenderから実行するとGLBと専用Unityプロジェクトの `HotelSecondFloorData/decorations.json` を再生成する。

専用Unityプロジェクトの `Assets/Editor/HotelSecondFloorBuild.cs` は装飾レイヤーを読み込み、同名の天井木枠だけを差し替え、植物と額絵を追加する；元の2F Blender原本の再出力後もこのレイヤーは維持される。

植物と絵の配置を維持し、廊下を横断する梁は実際の部屋境界0.65m・4.05mの2か所へ揃え、壁沿いの木枠を幅35cmへ広げて天井内へ入れ、木と壁の隙間を塞ぐ形状とした；入口の前方突出は25mmを維持し、別の端部ブロックは追加しない。

壁全面の見切り柄・格天井・行き止まりの巨大彫刻柄は撤去し、元の無地素材へ戻す；`prepare_luxury.py`では石のアクセント壁・木枠・腰壁と、床・カーペット・ドア・ソファ・テーブルに限定してDiffuseと対応Normalを準備し、DXT5nmのXをAlpha・YをGreenから復元する；設定は共通parameters.jsonのhotelSecondFloorLuxuryに反映したluxury-settings.json、原素材と切取り範囲はluxury-provenance.jsonへ記録する。

Unity 2019.4.9f1で形状更新時は`HotelSecondFloorBuild.Build`を先に実行し、仕上げは正本AssetBuild/HotelSecondFloorFinishBuild.csを生成コピーした`HotelSecondFloorFinishBuild.Build`で10組だけ適用する；毎回元の素材割当へ戻してから設定を適用し、削除した柄が残らないようにする；ゲームやプレビューは起動しない。

`deploy_decorations.py` はBundle読戻し、植物3鉢・絵3枚・突起撤去の検査、KKS終了確認を行い、2F Bundleと検証JSONだけを配置してサイズ・日時・SHA-256を照合する。

今回の読戻し・配置はtools/hotel-door-card/verify_luxury.pyとdeploy.ps1を使い、検査証拠とバックアップを同フォルダーのevidenceへ保存する；SurfaceInspection/luxury-overlap-final.jsonは全体の重なり検査で、接触・植物内部の交差・木枠と壁の意図した埋込みを含み、欠陥0の主張には使わない；ゲーム内の見た目はユーザー確認待ち。

木目はUVのV方向を材の長手へ揃え、縦枠・横枠・直交する天井梁を面ごとに向ける；全6室の外枠は縦材を上材下面で止める直方体へ置き換え、外枠同士の同一平面・近接面の重なり0件をverify_door_frames.pyで確認した；全体検査はSurfaceInspection/door-frame-grain-overlap.json、木材31メッシュの実Bundle内の形状・UV照合とマップだけの配置はverify_luxury.py・deploy_map.ps1で行う；ゲーム内表示は未確認。

ドア外枠の正面が抜ける不具合は、Unity座標で作った枠にビルダーの三角形反転が重なり面が裏返ったことが原因で、中間ファイルの頂点順を修正した；verify_luxury.pyの--facing-onlyは旧配置版で裏向き面を検出し、修正版の完成Bundleで外枠18メッシュ・216三角形の外向きを確認する；配置と実機の表示確認は別に扱う。
