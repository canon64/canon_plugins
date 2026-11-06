using System;
using HarmonyLib;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    // Bootstrap only: these labels also report a rejected runtime signature or malformed origin store.
    // The retained gate permit controls drawing; the native host owns input capture.
    internal sealed class StatusHooks : IDisposable
    {
        private readonly Harmony _harmony = new Harmony(Plugin.Guid + ".status");
        internal StatusHooks()
        {
            try
            {
                foreach (var item in new[] { new[] { RuntimeBridge.ComposerGuid, "DrawMainBody" }, new[] { RuntimeBridge.HipGuid, "DrawPosePresetSection" } })
                {
                    var method = R.Method(RuntimeBridge.Find(item[0]).GetType(), item[1]);
                    _harmony.Patch(method, prefix: new HarmonyMethod(typeof(StatusHooks), nameof(Draw)), postfix: null, transpiler: null, finalizer: null, ilmanipulator: null);
                }
                var hipStatus = R.Method(RuntimeBridge.Find(RuntimeBridge.HipGuid).GetType(), "DrawHipHijackBody");
                _harmony.Patch(hipStatus, prefix: null, postfix: null, transpiler: new HarmonyMethod(typeof(DiagnosticStatusUi), nameof(DiagnosticStatusUi.Insert)), finalizer: null, ilmanipulator: null);
            }
            catch { Dispose(); throw; }
        }
        private static void Draw()
        { var plugin = Plugin.Instance; if (plugin != null && plugin.CanShowStatus) GUILayout.Label(plugin.Status); }
        public void Dispose() => _harmony.UnpatchSelf();
    }
}
