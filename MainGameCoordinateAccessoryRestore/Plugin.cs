using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MainGameCoordinateAccessoryRestore
{
    [BepInDependency("canon.gate")]
    [BepInPlugin(Guid, nameof(MainGameCoordinateAccessoryRestore), "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "canon.maingame.coordinateaccessoryrestore";
        private static Plugin _instance;
        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _restoreSeconds;
        private const float RestoreRetryIntervalSeconds = 0.1f;
        private struct PendingRestore
        {
            internal float Until;
            internal float NextApply;
        }

        private readonly Dictionary<ChaControl, PendingRestore> _pending = new Dictionary<ChaControl, PendingRestore>();
        private readonly List<ChaControl> _snapshot = new List<ChaControl>();
        private readonly Dictionary<ChaControl, bool[]> _originalVisibility = new Dictionary<ChaControl, bool[]>();

        private void Awake()
        {
            _instance = this;
            _enabled = Config.Bind("General", "Enabled", true, "衣装変更後に全アクセサリーを表示する");
            _restoreSeconds = Config.Bind("General", "RestoreSeconds", 1f,
                new ConfigDescription("衣装変更後の再適用期間（秒）。遅れて読み込まれるアクセサリーにも適用する。",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            _harmony = new Harmony(Guid);
            var postfix = new HarmonyMethod(typeof(Plugin), nameof(CoordinateChanged)) { priority = Priority.Last };
            _harmony.Patch(AccessTools.Method(typeof(ChaControl), "ChangeCoordinateType",
                new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) }), postfix: postfix);
            _harmony.Patch(AccessTools.Method(typeof(ChaControl), "SetNowCoordinate",
                new[] { typeof(ChaFileCoordinate) }), postfix: postfix);
            _harmony.Patch(AccessTools.Method(typeof(ChaControl), "Reload"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(CoordinateReloaded)));
            Logger.LogInfo("Ready: restore all accessory visibility after coordinate changes (1.0.0).");
        
            CanonPlugins.Shared.PluginSessionGate.Bind(this, Info.Metadata.GUID, () => { }, EndGateSession);
        }

        private static void CoordinateChanged(ChaControl __instance, bool __result)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var plugin = _instance;
            if (!__result || plugin == null || !plugin._enabled.Value || __instance == null) return;
            if (__instance.fileStatus?.showAccessory == null) return;
            plugin._originalVisibility[__instance] = (bool[])__instance.fileStatus.showAccessory.Clone();
            __instance.SetAccessoryStateAll(true);
        }

        private static void CoordinateReloaded(ChaControl __instance)
        {
            var plugin = _instance;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || plugin == null || !plugin._enabled.Value ||
                __instance == null || !plugin._originalVisibility.ContainsKey(__instance)) return;
            __instance.SetAccessoryStateAll(true);
        }

        private void EndGateSession()
        {
            foreach (var pair in _originalVisibility)
            {
                var cha = pair.Key;
                if (cha == null || cha.fileStatus?.showAccessory == null) continue;
                try
                {
                    for (int i = 0; i < Math.Min(pair.Value.Length, cha.fileStatus.showAccessory.Length); i++)
                        cha.SetAccessoryState(i, pair.Value[i]);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Accessory rollback failed: " + ex);
                }
            }
            _originalVisibility.Clear();
            _pending.Clear();
            _snapshot.Clear();
        }

        private void OnDestroy()
        {
            CanonPlugins.Shared.PluginSessionGate.Dispose();
            _harmony?.UnpatchSelf();
            _pending.Clear();
            if (_instance == this) _instance = null;
        }
    }
}
