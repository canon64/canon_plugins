using System.Collections;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private bool _uiCaptureActive;
        private bool _hasPrevCtrlCameraCursorLock;
        private bool _prevCtrlCameraCursorLock;
        private bool _hasPrevCtrlCameraNoCtrl;
        private BaseCameraControl_Ver2.NoCtrlFunc _prevCtrlCameraNoCtrl;
        private bool _hasPrevCtrlCameraKeyCondition;
        private BaseCameraControl_Ver2.NoCtrlFunc _prevCtrlCameraKeyCondition;
        private bool _hasPrevGameCursorLock;
        private bool _prevGameCursorLock;
        private bool _hasPrevActionSceneCursorLock;
        private bool _prevActionSceneCursorLock;
        private HFlag _cachedHFlag;
        private CameraControl_Ver2 _cachedCtrlCamera;
        private bool _gizmoDragCaptureHint;
        private bool _uiCaptureForceUnlockRestore;
        private Coroutine _uiCaptureEndOfFrameRoutine;
        private float _uiCaptureHoldUntil;
        private const float UiCaptureReleaseGraceSec = 0.15f;

        private void LateUpdate()
        {
            if (_settings != null && _settings.SyncVoiceSourcesToVideoRoom)
                SyncActiveVoiceSourcesToVideoRoom(false);
            UpdateUiCaptureState();
            UpdateEditCursorUnlockLoop();
        }

        internal void NotifyGizmoDragState(bool dragging)
        {
            _gizmoDragCaptureHint = dragging;
            if (dragging)
            {
                UpdateUiCaptureState();
            }
        }

        private void UpdateUiCaptureState()
        {
            bool wantCapture = ShouldCaptureUi(out string reason);
            float now = Time.unscaledTime;
            if (wantCapture)
            {
                _uiCaptureHoldUntil = now + UiCaptureReleaseGraceSec;
                if (!_uiCaptureActive)
                {
                    StartUiCapture(reason);
                }

                TickUiCapture();
                return;
            }

            // 判定の一瞬の揺れで ON/OFF を連打しないよう、短い猶予を設ける。
            if (_uiCaptureActive && now < _uiCaptureHoldUntil)
            {
                TickUiCapture();
                return;
            }

            if (_uiCaptureActive ||
                _hasPrevCtrlCameraCursorLock ||
                _hasPrevCtrlCameraNoCtrl ||
                _hasPrevCtrlCameraKeyCondition ||
                _hasPrevGameCursorLock ||
                _hasPrevActionSceneCursorLock)
            {
                StopUiCapture("ui interaction ended");
            }
        }

        private bool ShouldCaptureUi(out string reason)
        {
            bool gizmoDragging = _gizmoDragCaptureHint || (_gizmo != null && _gizmo.IsDragging);
            bool playbackDragging = IsPlaybackBarDraggingNow();

            if (gizmoDragging && playbackDragging)
            {
                reason = "gizmo+playback-drag";
                return true;
            }

            if (gizmoDragging)
            {
                reason = "gizmo-drag";
                return true;
            }

            if (playbackDragging)
            {
                reason = "playback-drag";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private bool IsPlaybackBarReady()
        {
            if (_settings == null || !_settings.EnablePlaybackBar) return false;
            if (_mainVideoPlayer == null) return false;
            if (_videoRoomRoot == null) return false;
            if (_playbackBarHiddenByUser) return false;
            return true;
        }

        private bool IsPlaybackBarDraggingNow()
        {
            if (!IsPlaybackBarReady()) return false;
            if (_playbackSeekDragging) return true;
            if (_playbackVolumeDragging) return true;
            if (_playbackGainDragging) return true;
            if (!Input.GetMouseButton(0)) return false;

            float barHeight = Mathf.Max(GetPlaybackBarMinHeightPx(), _settings.PlaybackBarHeight);
            float marginX = Mathf.Max(0f, _settings.PlaybackBarMarginX);
            float barWidth = Mathf.Max(120f, Screen.width - marginX * 2f);
            var barRect = new Rect(marginX, Screen.height - barHeight, barWidth, barHeight);

            var mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return barRect.Contains(mouseGui);
        }

        private bool NeedsUiCaptureRecovery()
        {
            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl == null) return false;

            bool noCtrlForced = IsNoCtrlFunc(ctrl.NoCtrlCondition, AlwaysTrue);
            bool keyForced = IsNoCtrlFunc(ctrl.KeyCondition, AlwaysFalse);
            return noCtrlForced || keyForced;
        }

        private void StartUiCapture(string reason)
        {
            _uiCaptureActive = true;
            _uiCaptureForceUnlockRestore =
                reason.IndexOf("playback", System.StringComparison.Ordinal) >= 0;
            _hasPrevCtrlCameraCursorLock = false;
            _hasPrevCtrlCameraNoCtrl = false;
            _hasPrevCtrlCameraKeyCondition = false;
            _hasPrevGameCursorLock = false;
            _hasPrevActionSceneCursorLock = false;

            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl != null)
            {
                _prevCtrlCameraCursorLock = _uiCaptureForceUnlockRestore ? false : ctrl.isCursorLock;
                _hasPrevCtrlCameraCursorLock = true;
                _prevCtrlCameraNoCtrl = ctrl.NoCtrlCondition;
                _hasPrevCtrlCameraNoCtrl = true;
                _prevCtrlCameraKeyCondition = ctrl.KeyCondition;
                _hasPrevCtrlCameraKeyCondition = true;
            }

            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null)
            {
                _prevGameCursorLock = _uiCaptureForceUnlockRestore ? false : GameCursor.isLock;
                _hasPrevGameCursorLock = true;
            }

            ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
            if (actionScene != null)
            {
                _prevActionSceneCursorLock = _uiCaptureForceUnlockRestore ? false : actionScene.isCursorLock;
                _hasPrevActionSceneCursorLock = true;
            }

            EnsureUiCaptureEndOfFrameRoutine();
            LogInfo($"ui capture ON ({reason})");
        }

        private void TickUiCapture()
        {
            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl != null)
            {
                if (!_hasPrevCtrlCameraCursorLock)
                {
                    _prevCtrlCameraCursorLock = _uiCaptureForceUnlockRestore ? false : ctrl.isCursorLock;
                    _hasPrevCtrlCameraCursorLock = true;
                }
                if (!_hasPrevCtrlCameraNoCtrl)
                {
                    _prevCtrlCameraNoCtrl = ctrl.NoCtrlCondition;
                    _hasPrevCtrlCameraNoCtrl = true;
                }
                if (!_hasPrevCtrlCameraKeyCondition)
                {
                    _prevCtrlCameraKeyCondition = ctrl.KeyCondition;
                    _hasPrevCtrlCameraKeyCondition = true;
                }

                ctrl.isCursorLock = false;
                ctrl.NoCtrlCondition = AlwaysTrue;
                ctrl.KeyCondition = AlwaysFalse;
                GlobalMethod.SetCameraMoveFlag(ctrl, _bPlay: false);
            }

            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null)
            {
                if (!_hasPrevGameCursorLock)
                {
                    _prevGameCursorLock = _uiCaptureForceUnlockRestore ? false : GameCursor.isLock;
                    _hasPrevGameCursorLock = true;
                }

                gameCursor.SetCursorLock(setLockFlag: false);
                GameCursor.isDraw = true;
            }

            ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
            if (actionScene != null)
            {
                if (!_hasPrevActionSceneCursorLock)
                {
                    _prevActionSceneCursorLock = _uiCaptureForceUnlockRestore ? false : actionScene.isCursorLock;
                    _hasPrevActionSceneCursorLock = true;
                }

                actionScene.SetCursorLock(isLock: false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void StopUiCapture(string reason)
        {
            if (!_uiCaptureActive
                && !_hasPrevCtrlCameraCursorLock
                && !_hasPrevCtrlCameraNoCtrl
                && !_hasPrevCtrlCameraKeyCondition
                && !_hasPrevGameCursorLock
                && !_hasPrevActionSceneCursorLock
                && !NeedsUiCaptureRecovery())
            {
                return;
            }

            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl != null)
            {
                if (_hasPrevCtrlCameraCursorLock)
                    ctrl.isCursorLock = _prevCtrlCameraCursorLock;

                if (_hasPrevCtrlCameraNoCtrl)
                    ctrl.NoCtrlCondition = _prevCtrlCameraNoCtrl;
                else if (IsNoCtrlFunc(ctrl.NoCtrlCondition, AlwaysTrue))
                    ctrl.NoCtrlCondition = AlwaysFalse;

                if (_hasPrevCtrlCameraKeyCondition)
                    ctrl.KeyCondition = _prevCtrlCameraKeyCondition;
                else if (IsNoCtrlFunc(ctrl.KeyCondition, AlwaysFalse))
                    ctrl.KeyCondition = AlwaysTrue;

                GlobalMethod.SetCameraMoveFlag(ctrl, _bPlay: true);
            }

            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null && _hasPrevGameCursorLock)
            {
                gameCursor.SetCursorLock(_prevGameCursorLock);
                GameCursor.isDraw = !_prevGameCursorLock;
            }

            ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
            if (actionScene != null && _hasPrevActionSceneCursorLock)
            {
                actionScene.SetCursorLock(_prevActionSceneCursorLock);
            }

            bool restoreLock = (_hasPrevCtrlCameraCursorLock && _prevCtrlCameraCursorLock)
                || (_hasPrevGameCursorLock && _prevGameCursorLock)
                || (_hasPrevActionSceneCursorLock && _prevActionSceneCursorLock);
            Cursor.lockState = restoreLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !restoreLock;

            _uiCaptureActive = false;
            _uiCaptureForceUnlockRestore = false;
            _hasPrevCtrlCameraCursorLock = false;
            _hasPrevCtrlCameraNoCtrl = false;
            _hasPrevCtrlCameraKeyCondition = false;
            _hasPrevGameCursorLock = false;
            _hasPrevActionSceneCursorLock = false;
            _gizmoDragCaptureHint = false;

            StopUiCaptureEndOfFrameRoutine();
            LogInfo($"ui capture OFF ({reason})");
        }

        private void UpdateEditCursorUnlockLoop()
        {
            if (ShouldKeepEditCursorUnlocked())
            {
                EnsureUiCaptureEndOfFrameRoutine();
                return;
            }

            if (!_uiCaptureActive)
            {
                StopUiCaptureEndOfFrameRoutine();
            }
        }

        private bool ShouldKeepEditCursorUnlocked()
        {
            return _editMode
                && _gizmo != null
                && _gizmo.IsVisible
                && !_uiCaptureActive;
        }

        private void TickEditCursorUnlockOnly()
        {
            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null)
            {
                GameCursor.isDraw = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EnsureUiCaptureEndOfFrameRoutine()
        {
            if (_uiCaptureEndOfFrameRoutine != null) return;
            _uiCaptureEndOfFrameRoutine = StartCoroutine(UiCaptureEndOfFrameLoop());
        }

        private void StopUiCaptureEndOfFrameRoutine()
        {
            if (_uiCaptureEndOfFrameRoutine == null) return;
            StopCoroutine(_uiCaptureEndOfFrameRoutine);
            _uiCaptureEndOfFrameRoutine = null;
        }

        private IEnumerator UiCaptureEndOfFrameLoop()
        {
            while (_uiCaptureActive || ShouldKeepEditCursorUnlocked())
            {
                yield return new WaitForEndOfFrame();
                if (_uiCaptureActive)
                {
                    TickUiCapture();
                }
                else if (ShouldKeepEditCursorUnlocked())
                {
                    TickEditCursorUnlockOnly();
                }
            }

            _uiCaptureEndOfFrameRoutine = null;
        }

        private CameraControl_Ver2 ResolveCtrlCamera()
        {
            if (_cachedCtrlCamera != null)
            {
                return _cachedCtrlCamera;
            }

            if (_cachedHFlag == null)
            {
                _cachedHFlag = UnityEngine.Object.FindObjectOfType<HFlag>();
            }

            if (_cachedHFlag != null)
            {
                _cachedCtrlCamera = _cachedHFlag.ctrlCamera;
            }

            return _cachedCtrlCamera;
        }

        private static bool AlwaysTrue()
        {
            return true;
        }

        private static bool AlwaysFalse()
        {
            return false;
        }

        private static bool IsNoCtrlFunc(BaseCameraControl_Ver2.NoCtrlFunc current, BaseCameraControl_Ver2.NoCtrlFunc expected)
        {
            if (current == null || expected == null) return false;
            return current.Method == expected.Method && current.Target == expected.Target;
        }
    }
}
