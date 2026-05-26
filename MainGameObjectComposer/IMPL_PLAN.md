# IMPL_PLAN: ピストン / アングル ドライバ形状

## 概要
回転オブジェクト（トーラス）と同じ「ドライバ＋子」型で、ピストンとアングルにも
**パラメータでサイズが変わる視覚的な形状**を持たせる。

- ピストン = ストロークレール（全長 = 振幅×2）。子がレール上をsinでスライド。
- アングル = 扇形セクター（開き角 = 角度×2、半径指定）。子が扇の中をsinでswing。
- ドライバ本体は静止ガイド。実際に動くのは子オブジェクト。腰IKは子に追従する
  （回転オブジェクトと完全に同じ運用）。

3種のドライバ（回転トーラス / ピストンレール / アングル扇）が同じ作法で揃う。

---

## 確定事項（ユーザー回答）
- ピストン形状 = ストロークレール（両端に端板、振幅で全長が伸びる）
- アングル形状 = 扇形（角度で開きが広がる、半径で大きさ）
- パラメータ数値でメッシュのサイズが変わること（サイズ追従）が必須要件
- 既存の self-mover `motionMode`（オブジェクト自身が動く）とは別系統の追加機能
- 動くのは子、ドライバ本体は静止（回転オブジェクトに合わせる）

---

## 既存実装の流用状況（self-mover版の成果物）

self-mover（`motionMode` 2/3）として以下は**実装済み**。今回のドライバ版で数式・
クランプ・データ定義を流用する。

- ✅ ピストン/アングルの基礎フィールド（axis/amplitude/speedHz/phase/localSpace）
  — `Plugin.State.cs:118-130`
- ✅ sin運動の数式（`Mathf.Sin(2π(hz·t + phase)) × amplitude`）— `Plugin.Motion.cs:86-126`
- ✅ NormalizeData のクランプ（angle 0-180°/0.01-20Hz、piston 0-10m/0.01-20Hz）
  — `Plugin.Objects.cs:403-417`
- ✅ ComposerSettings の Default 値（Piston/Angle Axis/Amplitude/SpeedHz）
  — `Plugin.State.cs:47-55`
- ✅ 時間源 `GetSyncTime()` — `Plugin.Motion.cs:9`
- ✅ self-mover UI 部品（`DrawAngleEditor`/`DrawPistonEditor`）— `Plugin.Motion.UI.cs`
  ※ ただし `DrawMotionSection` はどこからも呼ばれていない（self-mover UI は未配線）

回転オブジェクトの実装が**参照テンプレート**になる（同じ作法で書く）。

---

## 必要材料（調査結果）

### M1. データフィールド（ドライバ種別 + 形状パラメータ） ❌未
- 場所: `ManagedObjectData`（`Plugin.State.cs:93-162`）
- 既存 `isRotationObject`（135）と同じ作法で**加算的にフラグ追加**（後方互換のため enum 化はしない）:
  ```csharp
  public bool isPistonObject;   // ストロークレール ドライバ
  public bool isAngleObject;    // 扇形 ドライバ
  ```
- 形状パラメータ（新規）:
  ```csharp
  public float pistonRodRadius = 0.01f;   // レール/ロッドの太さ(m)
  public float angleFanRadius  = 0.3f;     // 扇の半径(m)
  ```
- 流用: スライド/swing 範囲は既存 `pistonAmplitude` / `angleAmplitudeDeg` をそのまま使う ♻
- 流用: 子の個別位相は既存 `orbitPhaseTurns`（141）を流用 ♻
- 排他保証: `isRotationObject` / `isPistonObject` / `isAngleObject` は同時に1つだけ true
  （NormalizeData で保証 → M8）

### M2. ComposerSettings デフォルト値 ❌一部未
- 場所: `Plugin.State.cs:10-74`
- ✅ 既存: `DefaultPistonAxis/Amplitude/SpeedHz`, `DefaultAngleAxis/Amplitude/SpeedHz`
- ❌ 新規:
  ```csharp
  public float DefaultPistonRodRadius = 0.01f;
  public float DefaultAngleFanRadius  = 0.3f;
  ```

### M3. メッシュ生成（レール / 扇） ❌未
- 参照テンプレート: `BuildEllipticalTorusMesh`（`Plugin.RotationObject.cs:235-304`）
- ❌ `BuildPistonRailMesh(float amplitude, float rodRadius)`:
  - 軸方向（ローカル）に全長 `amplitude×2` のロッド（細い円柱）
  - 両端 ±amplitude に端板（薄い円盤 or 立方体）でストローク限界を表示
  - 原点中心。軸の向きは `pistonAxis`（Visual の localRotation で向ける）
- ❌ `BuildAngleFanMesh(float amplitudeDeg, float radius)`:
  - 軸に直交する平面上の扇形（中心角 = `amplitudeDeg×2`、半径 `radius`）
  - 中心角を −amplitude〜+amplitude に配置（0°が正面）
  - 三角形ファンで構成（中心1点 + 弧上 N 点）
- 流用: 縞テクスチャ/マテリアル（`BuildStripedMaterial` `Plugin.RotationObject.cs:98`）は
  そのまま流用可能 ♻（動きの向きが分かりやすい）

### M4. ドライバ Visual セットアップ + キャッシュ再生成 ❌未
- 参照: `ApplyRotationObjectVisualSetup`（`Plugin.RotationObject.cs:160`）、
  `RebuildTorusMeshIfNeeded`（188）
- ❌ `ApplyPistonObjectVisualSetup` / `ApplyAngleObjectVisualSetup`:
  - Visual の Collider 削除 → MeshFilter/MeshRenderer 確保 → マテリアル設定 → メッシュ生成
- ❌ `RebuildPistonMeshIfNeeded` / `RebuildAngleMeshIfNeeded`:
  - パラメータ変化時のみ再生成（torus の `CachedRx/Rz/Tube` キャッシュ方式）
  - キャッシュ: 既存 `RuntimeObjectRef.CachedRx/Rz/Tube`（`Plugin.State.cs:178-180`）を
    汎用パラメータスロットとして流用（piston: Rx=amplitude, Rz=rodRadius / angle: Rx=ampDeg, Rz=radius）♻
- ❌ `RebuildAllRuntimeObjects` の分岐追加（`Plugin.Objects.cs:195-202` の
  `if (isRotationObject)` に else if を追加）

### M5. 子の駆動 Tick ❌未
- 参照: `TickRotationOrbits`（`Plugin.RotationObject.cs:310`）、呼出元 `Plugin.cs:186`
- ❌ `TickLinearDrivers`（ピストン）:
  - 直下の子を探し、`localPosition` を毎フレーム上書き:
    `slide = axisN × sin(2π(speedHz·t + phase + child.orbitPhaseTurns)) × amplitude`
    `child.localPosition(perp成分) + slide`（torus と同様、子のオフセットは保持）
- ❌ `TickAngleDrivers`（アングル）:
  - 子を扇の中で swing。`deg = sin(2π(...)) × amplitudeDeg` で
    `localRotation = AngleAxis(deg, axis)`、半径方向に `angleFanRadius` 配置
- 流用: `GetSyncTime()`（✅）、sin数式（`Plugin.Motion.cs`✅）
- `Plugin.cs:186` の `TickMotions` の隣に2行追加して呼ぶ

### M6. 作成ボタン + 生成関数 ❌未
- 参照: `CreateRotationObject`（`Plugin.RotationObject.cs:114`）、
  ボタン描画 `DrawMainWindow`（`Plugin.UI.cs:27-36`）
- ❌ `CreatePistonObject` / `CreateAngleObject`:
  - `CreateRotationObject` を雛形に。`isPistonObject=true` 等 + 形状デフォルト設定
  - 名前は `BuildUniqueObjectName("Piston")` / `("Angle")` で
    一覧で種別が分かる（`Piston_001` / `Angle_001`）
- ❌ ボタン2個を `DrawMainWindow` の作成ボタン行に追加

### M7. UI パラメータ編集（ドライバ用） ❌未
- 参照: `DrawRotationObjectParams`（`Plugin.UI.cs:237-278`）、
  分岐元 `DrawSelectedSection`（`Plugin.UI.cs:200-214`）
- ❌ `DrawPistonObjectParams(selected)`:
  - 振幅(m) スライダー → レール全長 = 振幅×2（ラベルで明示）
  - ロッド太さ、速度(Hz)、子の位相
- ❌ `DrawAngleObjectParams(selected)`:
  - 角度(度) スライダー → 扇の開き = 角度×2（ラベルで明示）
  - 扇半径、速度(Hz)、子の位相
- ❌ `DrawSelectedSection`（`Plugin.UI.cs:201`）に `if (isPistonObject)` / `if (isAngleObject)` 分岐追加
- 流用: `DrawSingleSlider`（`Plugin.UI.cs:324`）でライブ更新 ✅
- self-mover の `DrawAngleEditor`/`DrawPistonEditor`（`Plugin.Motion.UI.cs`）とは別物
  （あちらは未配線のまま保留）

### M8. 永続化 / NormalizeData ❌一部未
- 場所: `NormalizeData`（`Plugin.Objects.cs:367-467`）
- ✅ 流用: angle/piston の amplitude/speed/phase クランプ（403-417）
- ❌ 新規追加:
  - ドライバ種別の排他保証（複数 true なら優先順位 rotation > piston > angle で1つに）
  - `pistonRodRadius` クランプ（0.0005〜0.1m）
  - `angleFanRadius` クランプ（0.01〜2m）
- ✅ JSON 互換: `JsonUtility` が欠損フィールドをデフォルト補完（既存セーブ読込で
  新フラグ=false, 新パラメータ=デフォルト → 既存挙動を壊さない）

### M9. self-mover motionMode との関係 ✅整理済
- self-mover `motionMode`（2/3 = 自身が動く）は現状維持。**触らない**
- ドライバ（isPistonObject/isAngleObject = 子を動かす静止ガイド）が今回の主機能
- `DrawMotionSection` 未配線問題は本タスクのスコープ外（保留）

### M10. NEW_PLUGIN.md / docs / analysis ✅対象外
- 既存プラグイン拡張のため NEW_PLUGIN.md スコープ外。CLAUDE.md 鉄則のみ適用

---

## 実装ステップ（順序）
1. **M1** `Plugin.State.cs`: フラグ + 形状パラメータ追加 / RuntimeObjectRef キャッシュ流用方針確定
2. **M2** `Plugin.State.cs`: ComposerSettings に Default 追加
3. **M8** `Plugin.Objects.cs`: NormalizeData に排他保証 + 新パラメータクランプ
4. **M3** メッシュビルダー `BuildPistonRailMesh` / `BuildAngleFanMesh`（新ファイル or RotationObject 隣）
5. **M4** Visual セットアップ + キャッシュ再生成 + `RebuildAllRuntimeObjects` 分岐
6. **M5** `TickLinearDrivers` / `TickAngleDrivers` + `Plugin.cs:186` に呼出追加
7. **M6** `CreatePistonObject` / `CreateAngleObject` + ボタン2個
8. **M7** `DrawPistonObjectParams` / `DrawAngleObjectParams` + `DrawSelectedSection` 分岐
9. ビルド: `dotnet build -c Release "/p:KKSDir=F:/kks"`
10. デプロイ確認 → HScene で動作確認（形状がサイズ追従するか、子がスライド/swingするか）
11. easy_deploy でデプロイ → git commit

## 制約・注意（CLAUDE.md 鉄則）
- 鉄則1: UTF-8 統一
- 鉄則8: `[BepInProcess]` = `"KoikatsuSunshine"` `"KoikatsuSunshine_VR"`（既存通り）
- 鉄則9: 専用ログ `MainGameObjectComposer.log`（既存通り）
- 鉄則10: 全パラメータ JSON 可変（Default* を ComposerSettings に追加）
- 鉄則15: GitHub push/release は easy_deploy 使用
- 場当たり禁止: 動作中の `isRotationObject` / self-mover には触らない（加算的に追加）

## 残オープン論点
- メッシュ生成を新ファイル（`Plugin.DriverShapes.cs`）にするか `Plugin.RotationObject.cs` に同居か
  → 行数次第。新ファイル推奨
- ピストン端板の形状（円盤 / 立方体）と扇のセグメント数 → 実装時にデフォルト決め打ち
