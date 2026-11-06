using System;
using System.Reflection;
using MainGameUnifiedUi;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameObjectComposer
{
    public sealed partial class Plugin
    {
        private Action _releaseHipComposer;

        private void BeginGateSession()
        {
            var type = Type.GetType("MainGirlHipHijack.Plugin, MainGirlHipHijack", false);
            var method = type?.GetMethod("ReleaseComposerSessionTargets", BindingFlags.Public | BindingFlags.Static);
            _releaseHipComposer = method == null ? null : (Action)Delegate.CreateDelegate(typeof(Action), method);
            _runtime.HSceneProc = SessionGate.HScene;
            _runtime.Flags = SessionGate.ReadScene<HFlag>("flags");
            _runtime.MainFemale = ResolveMainFemale(SessionGate.HScene);
            InitMotionDiagnostics();
            InitializeBetterPenetrationFollow();
            TryResolveRuntimeRefs();
            RefreshExternalParentTargetsIfNeeded(true);
            RegisterUnifiedTabIfAvailable();
        }

        private void EndGateSession()
        {
            SessionGate.Cleanup(
                () => { _presetPendingAction = _presetLeaveAction = null; AbortPresetEditor(); _presetDropdown = _presetSplitterHeld = false; _presetGizmoBefore = null; },
                () => { StopAllCoroutines(); _motionPresetSavePending = false; },
                () => UnifiedUiApi.UnregisterTab(UnifiedTabId),
                () => { _releaseHipComposer?.Invoke(); _releaseHipComposer = null; },
                () => StopUiCapture("session-ended"), () => ReleaseExternalUiCapture("session-ended"),
                () => DetachSelectedGizmo(), () => ClearBetterPenetrationPending(),
                () => DisposeBetterPenetrationFollow(), () => OnBlankRoomLeft(),
                () => OnHSceneEnded(), () => DisposeMotionDiagnostics());
        }

        private void OnDisable() { SessionGate.Stop(); }
    }
}
