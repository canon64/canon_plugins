using System;
using BepInEx;
using CanonGate;
using HarmonyLib;
using ksPlug.Character;

namespace CanonPlugins.KsPlugAhegaoGuard
{
    [BepInPlugin(Guid, "ksPlug Ahegao Null Guard", "1.0.0")]
    [BepInDependency(GateApi.PluginGuid, "2.3.0")]
    [BepInDependency("ksPlug.Katarsys", "1.6.2")]
    [BepInProcess("KoikatsuSunshine")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.ksplugahegaoguard";
        private static Plugin _instance;
        private IDisposable _registration;
        private Harmony _harmony;
        private GateApi.Permit _permit;
        private ulong _lastSessionId;
        private bool _active;

        private void Awake()
        {
            _instance = this;
            _harmony = new Harmony(Guid);
            _registration = GateApi.Register(Guid, StartSession, StopSession);
            Logger.LogInfo("Registered ksPlug HasReachAhegao null guard (H sessions).");
        }

        private void StartSession()
        {
            var context = GateApi.Current;
            if (!isActiveAndEnabled || context == null || context.Kind != GateApi.SessionKind.H
                || context.Scene == null || context.Id == _lastSessionId) return;
            _lastSessionId = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _active = true;
            try
            {
                _harmony.Patch(AccessTools.DeclaredMethod(typeof(AI), nameof(AI.HasReachAhegao)),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeHasReachAhegao)));
                Logger.LogInfo("ksPlug HasReachAhegao null guard enabled.");
            }
            catch
            {
                StopSession();
                throw;
            }
        }

        // Called only by ksPlug's existing method; no polling or scene lookup.
        private static bool BeforeHasReachAhegao(AI __instance, ref bool __result)
        {
            var owner = _instance;
            if (owner == null || !owner._active || !owner._permit.IsValid) return true;
            var core = __instance == null ? null : __instance.ChaCore;
            if (core != null && core.HInfo != null && core.HInfo.HCount != null) return true;
            __result = false;
            return false;
        }

        private void StopSession()
        {
            _active = false;
            _permit = default;
            _harmony?.UnpatchSelf();
        }

        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            try { _registration?.Dispose(); }
            finally
            {
                _registration = null;
                StopSession();
                if (ReferenceEquals(_instance, this)) _instance = null;
            }
        }
    }
}
