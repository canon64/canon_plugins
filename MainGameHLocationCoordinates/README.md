# MainGameHLocationCoordinates

Unityメインスレッド専用の剛体座標変換API。canon_gate 2.4.0の許可されたHセッションで利用する。API単独ではGameObjectの生成/移動、IK、マップ、ファイル保存に介入しない。

```csharp
var space = CoordinatesApi.Register(new RigidPose(source.position, source.rotation));
space.Changed += change => { /* Destination更新完了後の通知 */ };
space.UpdateDestination(new RigidPose(destination.position, destination.rotation), mapOperation, "move", placeId);
RigidPose placedParent = space.Transform(originalParentPose);
RigidPose original = space.InverseTransform(placedParent);
space.Close();
```

`TransformPoint`、`TransformRotation`、`InverseTransformPoint`、`InverseTransformRotation` も公開。基準Aは変更しない。毎回Bから `B * inverse(A)` を計算するため、過去の補正を加算し続けない。スケールは含めない。

変更通知はAPI内の単調増加する操作番号とマップ側の操作番号を別々に持つ。停止・退出・次Hで古いハンドルは失効する。通知の購読者は自分の所有物だけを操作し、Close/終了時に購読を解除する。登録ハンドルごとに座標系を独立させる。

共通APIと検証プラグインのビルド/自動試験は `../MainGameComposerLocationTest/Build.ps1`。
