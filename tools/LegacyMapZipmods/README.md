# 道路・VaMホテルの通常マップ化
`prepare.py` → `build.ps1` → `package.py` の順で、本編Free H・Studio用の2個のzipmodを生成・検査して `F:/kks/mods` へ配置する。
道路は既存の画像と `09.StreetlightRoad` の設定値から生成し、ホテルは `MainGameBlankMapAdd/Assets/thehotelroom_readable.assetbundle` の照明ありシーンを変換する。
ホテルは元の材質・シェーダー・テクスチャ336個をバイト一致で保持し、移動できる形状と元の348個の描画部品の照明割り当てを復元する。
IDは道路297602・ホテル297603、GUIDは `canon.kks.streetlight-road`・`canon.kks.vam-hotel` で、配置前に既存MapInfoとの衝突を検査する。
`package_validation.json` と `original_asset_validation.json` が読戻し・CRC・配置ハッシュの証跡で、ゲーム内の一覧・ロード・外見・H位置・Studioは未確認。
Pythonは `J:/system_tools/python310/python.exe` と既存の `F:/kks/work/clothing_setup/pydeps`、Unityは2019.4.9f1を使い、ゲームの起動・操作は行わない。
