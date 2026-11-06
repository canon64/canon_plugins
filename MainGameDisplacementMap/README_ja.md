# MainGameDisplacementMap

Koikatsu Sunshine / KKS VR 向け **画面全体ディスプレイスメントマップ波紋** プラグイン。

## 特徴

- SimpleAfterimage / MainGameCircularDisplacement で確立された「キャプチャ → OnPostRender オーバーレイ」パターンを踏襲
- **ディスプレイスメントマップ** (ランタイム生成 Texture2D) による有機的な波紋
- 同心円波 + 減衰 + 軽いノイズ + スクロールで自然な水面・熱波のような歪み
- Config から即時 `Enabled` 切り替え可能（最優先）
- 外部API (`SetExternalWarp`) を CircularDisplacement と同シグネチャで公開 → AstralMode からの将来対応が容易
- 全画面 (UI除外) / Chara レイヤー 両対応

## 設定 (BepInEx ConfigManager)

**00.General**
- `Enabled` — プラグインのマスターオンオフ（これをオフにすると完全停止）
- `OnlyHScene` — Hシーン中のみ
- `VerboseLog` — 詳細ログ

**20.MapGeneration** が本プラグインの核心:
- `MapResolution` (推奨 128〜256)
- `WaveCount` / `BaseFrequency` / `PropagationSpeed`
- `Damping` / `NoiseAmount`
- `ScrollSpeedX/Y`

## CircularDisplacement との違い

- Circular = 純粋数式 (Radial / Ripple / Swirl / Ring)
- 本プラグイン = **マップ駆動** でより複雑で有機的な波形を低コストで実現

両方共存可能。用途に応じて使い分けてください。

## ビルド・配置

```
dotnet build -c Release
```

生成された `bin/Release/net472/MainGameDisplacementMap.dll` を

`F:\kks\BepInEx\plugins\canon_plugins\MainGameDisplacementMap\`

へ配置（テスト用）。

## 注意

- 強めの設定にすると背景が激しく歪みます。最初は Strength 0.02 前後、Radius 0.8〜1.0 で調整を。
- マップ更新は軽量ですが、グリッド密度 (GridX/GridY) を上げすぎると描画コストが増えます。

## バージョン

0.1.0 — 初版 (マップ波紋コア実装 + Enabled 制御 + 外部API)
# CPU補間の実行時照合（2026-10-27）

CPU側の補間座標を固定せず、マップ確保時に4×4のテストテクスチャを使って実行中のUnityと一度だけ照合する。座標の3方式を比較し、変位値の最大差0.00002以内で一致した方式だけを採用する。さらに実際の変位マップでも照合する。一致しなければ従来のTexture2D.GetPixelBilinearと全ピクセル生成を維持する。

採用後は既存の必要ピクセルだけ生成する経路を使う。波・ノイズ・時間進行・マップ解像度・画質・撮影回数・外部APIは変更しない。テストテクスチャは成功・失敗・例外いずれも解放する。

`scripts/Invoke-PerfBuild.ps1 -Projects MainGameDisplacementMap -PythonScript scripts/verify_map_sampling.py` でビルドと分離検証を実行する。エンジン代用品で3種類の補間座標、解像度32/192/512、波数1/3/8、ノイズ0/0.15、複数時刻・端・範囲外UVを比較した。779,665項目を通過し、最大変位差は約0.000000358。代表条件の生成ピクセル数は36,864から9,417となった。これは分離検証上の処理数であり、実機の採用結果やFPS改善量ではない。

次回のFrameTimingProbeでは、照合済みか・マップ世代・採用方式・最大差も記録する。高速経路OFFという記録だけでは、照合失敗と未初期化を区別できないため、原因を断定しない。

高速参照経路のテクスチャ寸法は生成時に保持し、頂点や周辺4ピクセルごとのwidth/height取得を行わない。解像度変更・解放・再生成で寸法も更新する。分離テストでは参照結果の一致と、頂点参照中の寸法プロパティ呼び出し0回を確認した。追加修正の実機の所要時間は未計測。

スクロールで軸全体がテクスチャ端へ固定された場合は、描画内の同じ座標の補間とベクトル長を行・列単位で共有する。両軸固定なら一度だけ計算し、次の描画では結果を捨てる。通常座標・未対応のネイティブ参照経路は維持する。scripts/verify_clamped_uv_samples.pyで変更前とUV成分が一致する2,626,996項目を確認し、代表条件の補間は2,006回から65回になった。画質低下や描画間隔の変更ではなく、実機の短縮ミリ秒は未確認。

マップ世代・中心や半径等の設定・強度・実効スクロールが同じフレームは、頂点再計算とUV転送も行わない。時間更新と変更確認は維持し、描画の見た目が変化する時は即座に再計算する。強度0からの復帰、マップ／メッシュの交換・解放、転送失敗では古い結果を再利用しない。拡張したUV比較2,963,728項目を通過し、出力不変の連続20フレームの頂点サンプルと転送は0回だった。60Hzの120フレームでもUVは全成分が一致し、転送は120回から30回となった。実機の所要時間は次の計測で確認する。
