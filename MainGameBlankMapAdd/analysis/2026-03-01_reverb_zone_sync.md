# 2026-03-01 Reverb Zone Sync 調査

## 原因
- `AudioReverbZone` を `_videoRoomRoot` に直接 `AddComponent` していた。
- `SyncReverbZoneToFemale()` が `zone.transform` を女性座標に合わせるため、実質的に「部屋ルート座標」と同一 Transform を操作していた。
- ログ上では `zonePos=(0,0,-60)` のままになり、`listener is outside reverb zone` が継続していた。

## 修正方針
- リバーブ専用子オブジェクト `__VoiceReverbZone` を部屋ルート配下に作成。
- `AudioReverbZone` はこの子オブジェクトへ付与する。
- 毎フレーム同期は `__VoiceReverbZone` だけを女性キャラ座標に移動する。
- 旧構成（ルート直付け）を検出したら、ルートの `AudioReverbZone` を破棄して子オブジェクト方式に移行する。

## 実装ポイント
- `Plugin.cs`
  - フィールド追加: `_reverbZoneObject`
  - 追加メソッド: `EnsureReverbZoneObject()`
  - `ApplyRoomReverb()`:
    - OFF時は子オブジェクトごと破棄
    - 旧ルート直付けを検出して移行ログ出力
    - 子オブジェクト上の `AudioReverbZone` に設定適用
  - `SyncReverbZoneToFemale()`:
    - `_reverbZoneObject.transform.position = _femaleChara.transform.position`
    - ログに `roomWorld` も出力
  - `DestroyVideoRoom()`:
    - `_reverbZoneObject = null`

## 確認観点
- ログに `migrated legacy root zone -> child zone object` が出るか。
- ログに `zone synced to female ... zoneWorld=... roomWorld=...` が出るか。
- `listener is outside reverb zone` が出続ける場合は `VoiceReverbMaxDistance` を拡大して再確認。
