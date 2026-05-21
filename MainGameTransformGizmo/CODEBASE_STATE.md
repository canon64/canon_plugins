# CODEBASE_STATE: MainGameTransformGizmo

- Updated: 2026-03-10
- Project: `F:/kks/work/plugins/MainGameTransformGizmo/`
- Build: `dotnet build MainGameTransformGizmo.csproj -c Release "-p:KKSDir=F:/kks"`
- Deploy: `F:/kks/BepInEx/plugins/MainGameTransformGizmo/MainGameTransformGizmo.dll`

## 目的
- MainGame 用の Transform ギズモを汎用プラグインとして提供する。
- 他プラグインは本体ロジックを持たず、API 経由で付与/取得/破棄できるようにする。

## 公開API
- `MainGameTransformGizmo.TransformGizmoApi`
- `IsAvailable`: プラグイン有効状態を返す。
- `Attach(GameObject target)`: 対象に `TransformGizmo` を追加または既存を返す。
- `TryAttach(GameObject target, out TransformGizmo gizmo)`: 有効状態チェック付きで付与する。
- `Get(GameObject target)`: 既存ギズモ取得。
- `Detach(GameObject target)`: 既存ギズモ破棄。

## 実装状況 (2026-03-05)
- `TransformGizmo` は 3 モード:
  - `Move`
  - `Rotate`
  - `Scale`
- 中央球クリックでモード循環。
- 中央球右クリックで軸基準を切替:
  - `Local`
  - `World`
- イベント:
  - `DragStateChanged(bool)`
  - `ModeChanged(GizmoMode)`
  - `AxisSpaceChanged(GizmoAxisSpace)`
- Scale ギズモは 4 軸:
  - X / Y / Z
  - 4軸目: XYZ 同時拡大縮小 (uniform scale)
- 掴み判定は実メッシュ座標を使う:
  - 矢印/スケール先端は `TransformPoint` で計算
  - 回転リングは `LineRenderer` 実頂点で距離計算
- ドラッグ軸はギズモ向き追従:
  - 固定ワールド軸ではなく `transform.TransformDirection(...)` を使用

## 他プラグイン呼び出し手順
- 呼び出し側に依存属性を付ける:
  - `[BepInDependency(MainGameTransformGizmo.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]`
- 対象 `GameObject` に付与:
  - `var gizmo = TransformGizmoApi.Attach(targetGo);`
- 表示切替:
  - `gizmo.SetVisible(true/false);`
- UI入力奪取は呼び出し側で `DragStateChanged` を受けて制御する。

## API追加 (2026-03-10)
- `GetAxisSpace(TransformGizmo/GameObject)`
- `SetAxisSpace(TransformGizmo/GameObject, GizmoAxisSpace)`

## 既知事項
- `MainGameTransformGizmo` 単体ではカメラ奪取を行わない。
- カメラ/カーソル制御は呼び出し側プラグイン責務。
- ログ出力先:
  - `F:/kks/BepInEx/plugins/MainGameTransformGizmo/_logs/info.txt`
