# HS2マップ変換の試作

本編の女の子部屋とMODの温泉ホテルを、それぞれ元アイコン付きのKKS用zipmodへ変換する。

## 実行順

`bootstrap.ps1` → 共通Pythonで `prepare_sources.py` → `export_map.py` → `build_trials.ps1` → Unityの正常終了確認後に `package_maps.py` の順で実行する。

設定は `trials.json`、作業と完成品は `F:/kks/work/hs2_map_trials`、配置先は `F:/kks/mods/Local HS2 Conversion Trials` に置く。

## 確認範囲

本編とMODの登録からシーンとアイコンを引き、依存Bundleと共通メッシュを解決し、KKSのFree H・Studio登録と床上のH位置を作る。

完成Bundleの型・参照・Collider・SunLightInfo・Sprite・元アイコンの画素・ID衝突・ZIP・配置ハッシュを検査する。

HS2専用スクリプト、アニメーション、水面・鏡の効果、焼込み照明は再現せず、非標準素材はStandardへ置換し、スキン付き小物は静止姿勢で取り出す。

ゲームの起動や自動操作は行わず、両方の一覧表示・ロード・描画・床・H位置・Studioの実機結果を受け取ってから全件へ展開する。

## 全件変換

ユーザーの追加指示で実機結果を待たずに全件へ展開し、`inventory_maps.py` → `index_kks.py` → `start_bulk.ps1` の順に実行する。

全登録373件から同じGUIDの旧版を除いた356件を対象とし、進捗は `J:/workspaces/hs2_kks_map_conversion/progress.json`、各段階の記録は同じ場所の `reports` に残す。

作業用はJドライブ、完成品の実体は `F:/kks/mods/Local HS2 Bulk Imports` に置き、検査済みzipmodだけを一時拡張子から原子的に配置する。

`status.py` で進捗を表示し、失敗マップは一括変換を止めず個別に記録し、次の起動で再試行する。

元にアイコン登録がある場合は実画像を必ず取得・画素検査し、元に登録のないStudioマップ等だけは登録なしと書いた文字画像を使う。
