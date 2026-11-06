using System;
using System.Collections;
using BepInEx.Bootstrap;
using HarmonyLib;
using KKAPI.MainGame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameLgsMode
{
    public sealed class GameEvents : IDisposable
    {
        private static GameEvents _active;
        private readonly ModeController _controller;
        private readonly Harmony _harmony = new Harmony(Plugin.Guid);
        private ActionScene _nativeAction;
        private MonoBehaviour _hScene;
        private bool _inH;
        private bool _ownedWalking;
        private const string AiGuid = "com.kks.maingame.aishoujomode";
        public GameEvents(ModeController controller) { _controller = controller; }

        public void Connect()
        {
            if (_active != null) throw new InvalidOperationException("LGS game events already connected.");
            _active = this;
            GameAPI.StartH += StartH; GameAPI.EndH += EndH;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            SceneManager.sceneUnloaded += SceneUnloaded;
            Patch(typeof(ActionScene), "Start", nameof(ActionStarted));
            Patch(typeof(HSceneProc), "Start", nameof(HStarting), true);
            Patch(AccessTools.TypeByName("VRHScene"), "Start", nameof(HStarting), true, optional: true);
            Patch(typeof(ActionGame.Chara.Base), "SetActive", nameof(PlayerActivated));
            Patch(typeof(CanonGate.GateApi.AiShoujoModeScope), "Resume", nameof(AiResumed));
            Patch(typeof(CanonGate.GateApi.AiShoujoModeScope), "Dispose", nameof(AiDisposed));
            if (Chainloader.PluginInfos.TryGetValue(AiGuid, out var ai) && ai.Instance != null)
            {
                var assembly = ai.Instance.GetType().Assembly;
                Patch(assembly.GetType("MainGameAiShoujoMode.NativeActionWalkingSession"), "set_Ready", nameof(OwnedReady));
                Patch(assembly.GetType("MainGameAiShoujoMode.NativeActionWalkingSession"), "Resume", nameof(OwnedResumed));
                Patch(assembly.GetType("MainGameAiShoujoMode.HotelWalkingContext"), "set_WalkingReady", nameof(OwnedReady));
                var sandbox = AccessTools.TypeByName("MapEnvironmentRuntime.SandboxSession");
                Patch(sandbox, "Apply", nameof(SandboxApplied));
            }
        }

        private void Patch(Type type, string name, string callback, bool prefix = false, bool optional = false)
        {
            var method = type == null ? null : AccessTools.DeclaredMethod(type, name);
            if (method == null)
            {
                if (optional) return;
                throw new MissingMethodException(type?.FullName ?? "Missing walking type", name);
            }
            var hook = new HarmonyMethod(typeof(GameEvents), callback);
            _harmony.Patch(method, prefix: prefix ? hook : null, postfix: prefix ? null : hook);
        }

        private static void ActionStarted(ActionScene __instance, ref IEnumerator __result)
        { if (_active != null && __result != null) __result = _active.AfterActionStart(__instance, __result); }
        private IEnumerator AfterActionStart(ActionScene scene, IEnumerator original)
        {
            try { while (original.MoveNext()) yield return original.Current; }
            finally { (original as IDisposable)?.Dispose(); }
            if (_active != this || scene == null) yield break;
            _nativeAction = scene;
            TryNativeWalking();
        }
        private static void HStarting(MonoBehaviour __instance)
        { if (_active != null) _active._hScene = __instance; }

        private void StartH(object sender, EventArgs args)
        {
            _inH = true;
            _ownedWalking = false;
            var scene = _hScene as HSceneProc;
            // Native flags identify Free H, including owned modes which launch Free H.
            _controller.Enter(scene != null && scene.flags != null && scene.flags.isFreeH
                ? GameSituation.FreeH : GameSituation.Other);
        }
        private void EndH(object sender, EventArgs args)
        {
            _inH = false; _hScene = null;
            // Owned walking can resume before or after KKAPI's EndH callback.
            // Preserve an already-published return-to-walking command in either order.
            if (!_ownedWalking) _controller.Enter(GameSituation.Other);
            TryNativeWalking();
        }
        private static void OwnedReady(bool __0)
        { _active?.OwnedWalkingChanged(__0); }
        private void OwnedWalkingChanged(bool walking)
        {
            _ownedWalking = walking;
            if (walking) _controller.Enter(GameSituation.Walking);
            else if (!_inH) _controller.Enter(GameSituation.Other);
        }
        private static void OwnedResumed(object __instance)
        {
            if (_active == null) return;
            var getter = AccessTools.PropertyGetter(__instance.GetType(), "Ready");
            if (getter != null && (bool)getter.Invoke(__instance, null))
                _active.OwnedWalkingChanged(true);
        }
        private static void AiResumed(CanonGate.GateApi.AiShoujoModeScope __instance)
        {
            var active = _active;
            if (active == null || (bool)AccessTools.PropertyGetter(__instance.GetType(), "Suspended").Invoke(__instance, null)) return;
            active.ReadAiWalking(__instance.Owner);
        }
        private static void AiDisposed() => _active?.OwnedWalkingChanged(false);
        private void ReadAiWalking(object owner)
        {
            if (owner == null) return;
            object host = AccessTools.Field(owner.GetType(), "_host")?.GetValue(owner);
            object session = host == null ? null : AccessTools.PropertyGetter(host.GetType(), "Session")?.Invoke(host, null);
            object settings = session == null ? null : AccessTools.PropertyGetter(session.GetType(), "Settings")?.Invoke(session, null);
            if (settings == null) return;
            var readText = AccessTools.Method(settings.GetType(), "T", new[] { typeof(string) });
            OwnedWalkingChanged((string)readText.Invoke(settings, new object[] { "movement.mode" }) == "walk");
        }
        private static void SandboxApplied(object __instance)
        {
            var active = _active;
            var owner = CanonGate.GateApi.AiShoujoMode?.Owner;
            if (active == null || active._inH || owner == null) return;
            object host = AccessTools.Field(owner.GetType(), "_host")?.GetValue(owner);
            object session = host == null ? null : AccessTools.PropertyGetter(host.GetType(), "Session")?.Invoke(host, null);
            if (ReferenceEquals(session, __instance)) active.ReadAiWalking(owner);
        }
        private static void PlayerActivated(ActionGame.Chara.Base __instance, bool __0)
        {
            var active = _active;
            if (active == null || !__0 || active._nativeAction == null
                || !ReferenceEquals(active._nativeAction.Player, __instance)) return;
            active.TryNativeWalking();
        }
        private bool OwnedModeOpen()
        {
            if (!Chainloader.PluginInfos.TryGetValue(AiGuid, out var info) || info.Instance == null) return false;
            var type = info.Instance.GetType();
            return AccessTools.Field(type, "_mode")?.GetValue(info.Instance) as UnityEngine.Object != null
                || AccessTools.Field(type, "_hotel")?.GetValue(info.Instance) as UnityEngine.Object != null;
        }
        private void TryNativeWalking()
        {
            if (_inH || _nativeAction == null || OwnedModeOpen()) return;
            var player = _nativeAction.Player;
            if (player != null && player.gameObject.activeInHierarchy)
                _controller.Enter(GameSituation.Walking);
        }
        private void ActiveSceneChanged(Scene previous, Scene next)
        {
            if (_nativeAction != null && _nativeAction.gameObject.scene == next) TryNativeWalking();
            else if (!_inH && !OwnedModeOpen()) _controller.Enter(GameSituation.Other);
        }
        private void SceneUnloaded(Scene scene)
        {
            if (_nativeAction == null || _nativeAction.gameObject.scene == scene)
            { _nativeAction = null; if (!_inH && !OwnedModeOpen()) _controller.Enter(GameSituation.Other); }
        }
        public void Dispose()
        {
            GameAPI.StartH -= StartH; GameAPI.EndH -= EndH;
            SceneManager.activeSceneChanged -= ActiveSceneChanged; SceneManager.sceneUnloaded -= SceneUnloaded;
            _harmony.UnpatchSelf();
            if (_active == this) _active = null;
            _nativeAction = null; _hScene = null;
        }
    }
}
