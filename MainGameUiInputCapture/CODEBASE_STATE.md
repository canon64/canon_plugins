# MainGameUiInputCapture 状態

- 目的:
  - MainGame系プラグイン向けに、UI/Gizmoの入力奪取と復元を共通APIで提供する。
- 主要API:
  - `UiInputCaptureApi.Begin(ownerKey, sourceKey)`
  - `UiInputCaptureApi.Tick(ownerKey, sourceKey)`
  - `UiInputCaptureApi.End(ownerKey, sourceKey)`
  - `UiInputCaptureApi.EndOwner(ownerKey)`
  - `UiInputCaptureApi.SetIdleCursorUnlock(ownerKey, enabled)`
- 内部方針:
  - 最初の `Begin` 時に前状態を保存し、最後の `End` 時に復元。
  - 複数owner/sourceをトークンで管理。
  - `idle cursor unlock` でUI表示中のカーソル解放維持を可能化。
