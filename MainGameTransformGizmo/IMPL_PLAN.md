# MainGameTransformGizmo オーバーレイカメラ実装計画

## 目的
ギズモを常に最前面（女キャラで隠れない）に表示する。

## 実装方針
専用レイヤー（31）にギズモGameObjectを配置し、メインカメラとは別にDepth-clearしたオーバーレイカメラで後から描画する。

## 必要材料（調査結果）

### ✅ Camera.main はHSceneで正しいカメラを返すか
- **確認済み**
- `UI_NoControlCamera.Start()` が `GameObject.FindGameObjectWithTag("MainCamera")` で `CameraControl_Ver2` を取得
- KKSのHSceneメインカメラは "MainCamera" タグ付き
- `Camera.main` は `"MainCamera"` タグの最初のカメラを返す = 同じカメラ
- 根拠: `F:/kks/KoikatsuSunshine_Data/Managed/Assembly-CSharp.dll` → `UI_NoControlCamera`

### ✅ HSceneに複数カメラが存在するか
- **確認済み（HSceneはメインカメラ1台のみ）**
- `CameraControl_Ver2`（メインカメラ、"MainCamera"タグ）のみ
- `FarCamera`：HSceneProcから参照なし → HSceneでは使用されていない
- `ScreenShotCamera`：OnRenderImageのみ、描画順に影響しない
- UIカメラ：なし（CanvasはScreen Space - Overlay、カメラ不要）
- 根拠：HSceneProc全コードにFarCamera・UICamera参照なし（ilspyで確認）

### ✅ メインカメラのcullingMask初期値
- **確認済み（全レイヤー描画）**
- `CameraControl_Ver2` / `BaseCameraControl_Ver2` のコードにcullingMask設定なし（ilspyで確認）
- UnityのCamera.cullingMaskデフォルトは `-1`（全レイヤー）
- よって `mainCam.cullingMask &= ~(1 << 31)` の除外操作は安全

### ✅ レイヤー31がKKSで既に使用されていないか
- **確認済み（未使用）**
- DBおよびデコンパイル内にレイヤー31への明示的な参照なし
- Unity標準の定義済みレイヤーは0〜7。31はユーザー定義可能な最後のレイヤー

### ✅ Camera.main ベースの既存コードの動作実績
- **確認済み**
- TransformGizmoはホバー判定・ドラッグ計算に `Camera.main` を既に使用
- IKギズモが動作している = HSceneで `Camera.main` は有効なカメラを返している

### ✅ Attach後のギズモGameObjectがレイヤー31になるか
- **確認済み（正しく設定される）**
- `TransformGizmoApi.Attach(proxyGo)` → `proxyGo.AddComponent<TransformGizmo>()`
- TransformGizmo.Awakeで `_moveRoot`/`_rotateRoot`/`_scaleRoot`/`_centerSphere` をproxyGoの子として生成
- その後 `SetLayerRecursive` でレイヤー31を全子GameObjectに適用
- proxyGo自体（MeshRendererなし）のレイヤーは問わない
- 根拠: `TransformGizmoApi.cs` + `TransformGizmo.cs` Awake

## リスク
- なし（全材料確認済み）

## 現在の実装状態
- オーバーレイカメラ実装済み（depth = mainCam.depth + 100）
- レイヤー31割り当て済み
- `UpdateOverlayCam()` でメインカメラ変更時も追従
- **材料確認完了 → 実装妥当と判断**
