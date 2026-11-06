using System;
using HarmonyLib;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private readonly System.Collections.Generic.List<GameObject> _sessionHighlights =
            new System.Collections.Generic.List<GameObject>();

        private void BeginGateSession()
        {
            if (!IsPluginEnabled()) { SessionGate.Stop(); return; }
            _hotelRoomSession = !string.IsNullOrEmpty(CanonGate.GateApi.Current?.HotelRoomId);
            _hotelRoomRestorePending = _hotelRoomPoseOwned = _hotelRoomSession;
            _hSceneSession.Process = SessionGate.HScene;
            _femaleSession.Character = ResolveMainFemale(SessionGate.HScene);
            _maleSession.TargetMaleCha = ResolveMainMale(SessionGate.HScene);
            ResetHipQuickSetupOnHSceneEnter();
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(PluginPatches));
            _harmony.PatchAll(typeof(ShoulderSolverPatches));
            // Official VR publishes a different scene type; never search for a native HSceneProc.
            if (SessionGate.HScene == null && SessionGate.Scene != null)
            {
                var type = SessionGate.Scene.GetType();
                var late = AccessTools.Method(type, "LateUpdate");
                if (late != null) _harmony.Patch(late, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterVrSessionAnimation)));
                var change = AccessTools.Method(type, "ChangeAnimator");
                if (change != null) _harmony.Patch(change, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterVrSessionAnimatorChanged)));
            }
            TryResolveRuntimeRefs();
            PublishShoulderRuntime();
            _unifiedUiGateway.Register(DrawUnifiedContent);
            _vrGrabMode = VRGIN.Core.VR.Active;
            if (_vrGrabMode) ConnectVRWristFocus();
        }

        private void EndGateSession()
        {
            _hotelRoomSession = _hotelRoomRestorePending = _hotelRoomPoseOwned = false;
            SessionGate.Cleanup(
                () => ClearShoulderRuntime(),
                () => StopPoseTransitionIfRunning(),
                () => StopAllCoroutines(),
                () => { foreach (var go in _sessionHighlights) if (go != null) Destroy(go); _sessionHighlights.Clear(); },
                () => { _harmony?.UnpatchSelf(); _harmony = null; },
                () => _unifiedUiGateway.Unregister(),
                () => OnHSceneEnded(),
                () => DestroyFemaleHeadGizmo(),
                () => ReleaseAllInputCapture());
        }

        private void OnDisable() { SessionGate.Stop(); }

        private static void AfterVrSessionAnimation(MonoBehaviour __instance)
        {
            var plugin = Instance;
            if (!SessionGate.Allowed || plugin == null || !plugin.IsPluginEnabled()
                || !ReferenceEquals(SessionGate.Scene, __instance)) return;
            plugin.OnAfterHSceneLateUpdateBodyIK();
            plugin.UpdateBodyCtrlLink(Time.unscaledDeltaTime);
            plugin.ApplyVibeReaction();
            plugin.TickSpeedHijack();
            plugin.UpdateMaleHMD();
            plugin.ApplyFemaleHeadAdditiveRot();
        }

        private static void AfterVrSessionAnimatorChanged(MonoBehaviour __instance)
        {
            var plugin = Instance;
            if (!SessionGate.Allowed || plugin == null || !plugin.IsPluginEnabled()
                || !ReferenceEquals(SessionGate.Scene, __instance)) return;
            plugin.TryFlushPendingAbandonByPostureChange("changeAnimator", force: true);
        }
    }
}
