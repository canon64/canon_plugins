using CanonGate;
using MainGameRhythmSync;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private GateApi.Permit _sessionPermit;
        private ulong _lastSessionId;
        private SessionScene _sessionScene;
        internal bool SessionActive => isActiveAndEnabled && Host != null && Host.isActiveAndEnabled && _sessionPermit.IsValid && _sessionScene != null;
        internal HFlag SessionFlags => _sessionScene?.flags;
        private const string InputOwner = "com.kks.maingame.speedlimitbreak.bpm";
        private readonly Rect[] _inputRects = new Rect[1];
        private bool _pointerHeld;
        private HFlag _savedFlags;
        private float _savedSpeed, _savedCalc, _savedItem, _savedNoClick;
        private Vector2 _savedUp;

        internal void BeginSession()
        {
            var context = GateApi.Current;
            if (context == null || context.Kind != GateApi.SessionKind.H || context.Scene == null) return;
            if (_lastSessionId == context.Id) return;
            _lastSessionId = context.Id;
            EndSession();
            _sessionPermit = GateApi.Capture(Guid);
            if (!_sessionPermit.IsValid) return;
            _sessionScene = new SessionScene(context.Scene);
            _hSceneProc = _sessionScene;
            _savedFlags = _sessionScene.flags;
            _savedSpeed = _savedFlags.speed; _savedCalc = _savedFlags.speedCalc;
            _savedItem = _savedFlags.speedItem; _savedUp = _savedFlags.speedUpClac; _savedNoClick = _savedFlags.timeNoClick;
            _insideHScene = true;
            _nextVideoRoomPollTime = _nextBpmReferenceModeScanTime = 0f;
            Settings = SettingsStore.LoadOrCreate(_pluginDir, LogInfo, LogWarn, LogError);
            ApplyConfigManagerOverrides(false, "session start");
            SyncUiFromSettings();
            ResolveBlankMapSnapshotMethod();
            enabled = true;
            LogInfo("[canon_gate] session started id=" + context.Id);
            RefreshInputCapture();
        }
        internal void EndSession()
        {
            bool wasActive = _sessionScene != null;
            if (!wasActive) { _sessionPermit = default; enabled = false; ReleaseInputCapture(); return; }
            _sessionPermit = default;
            _sessionScene = null;
            enabled = false;
            ReleaseInputCapture();
            StopBpmMeasure("session ended", true);
            ResetVideoCueRuntime(true);
            ResetVideoRoomPlaybackSnapshot();
            if (_savedFlags != null)
            {
                _savedFlags.speed = _savedSpeed; _savedFlags.speedCalc = _savedCalc;
                _savedFlags.speedItem = _savedItem; _savedFlags.speedUpClac = _savedUp; _savedFlags.timeNoClick = _savedNoClick;
            }
            _savedFlags = null;
            _insideHScene = false;
            _hSceneProc = null;
            _sessionScene = null;
            if (wasActive) LogInfo("[canon_gate] session stopped");
            _uiNotice = string.Empty;
        }
        private void RefreshInputCapture()
        {
            if (!SessionActive || !_showBpmUi || !Application.isFocused) { ReleaseInputCapture(); return; }
            _inputRects[0] = _windowRect;
            UiInputCaptureApi.SetInputBlockRects(InputOwner, _inputRects);
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, true);
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Input.GetMouseButtonDown(0) && _windowRect.Contains(mouse)) _pointerHeld = true;
            if (!Input.GetMouseButton(0)) _pointerHeld = false;
            UiInputCaptureApi.Sync(InputOwner, "window", _pointerHeld);
        }
        private void ReleaseInputCapture()
        {
            _pointerHeld = false;
            UiInputCaptureApi.Sync(InputOwner, "window", false);
            UiInputCaptureApi.ClearInputBlockRects(InputOwner);
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, false);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseInputCapture(); }
        private void OnDisable() { if (_sessionScene != null) EndSession(); else ReleaseInputCapture(); }
    }
}
