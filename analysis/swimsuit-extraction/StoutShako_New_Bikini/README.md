# 水着加工用の抽出素材

導入済みの StoutShako「New Bikini v1.0.4」から、三角ビキニの上下・紐・リボンと各着衣状態を抽出した。

- `editable/newbikini/meshes/`：36メッシュのOBJ、元の頂点座標・UV・ウェイト・ボーン番号・バインド行列のJSON。
- `editable/newbikini/textures/`：模様・色分けマスクなどのPNG。
- `editable/newbikini/unity_metadata/`：素材設定、ボーン階層、各部品とメッシュ・素材の対応。
- `editable/bikinithu/textures/`：元MODのサムネイル5枚。
- `source_files/`：元のAssetBundle、衣装登録CSV、manifest。
- `original/`：元zipmodのコピー。抽出元とのSHA-256一致を確認済み。
- `inventory.json`：全メッシュ・画像・描画部品の一覧とコピーのハッシュ。

上は `Micro Bikini Bra`、下は `Micro Bikini Bottom I / II / III` を収録し、CSVに元のPrefab名とテクスチャ名がある。

OBJにはボーン情報が入らないため、変形情報は同名の `_vertex_data.json` と renderer の bones 一覧を合わせて使う。JSONはUnity元座標・元頂点順であり、OBJの軸変換と頂点順を確認してから対応付ける。

元のzipmodとゲーム内ファイルは変更していない。Blenderへの組み立て、新しい衣装の制作・登録はまだ行っていない。
