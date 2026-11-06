using System;
using HarmonyLib;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private void InstallVoiceHook()
        {
            try
            {
                var target = AccessTools.Method(typeof(HVoiceCtrl), "IsBreathPtnConditions");
                if (target == null)
                {
                    Logger.LogWarning("Strong voice unavailable: native breath condition method missing");
                    return;
                }
                _voiceHarmony = new Harmony(Guid);
                PatchOwned(_voiceHarmony, target,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(VoiceConditionPrefix)),
                    finalizer: new HarmonyMethod(typeof(Plugin), nameof(VoiceConditionFinalizer)));
                var vrType = Type.GetType("VRHVoiceCtrl, Assembly-CSharp", false);
                var vrTarget = vrType == null ? null : AccessTools.DeclaredMethod(vrType, "IsBreathPtnConditions");
                if (vrTarget != null)
                    PatchOwned(_voiceHarmony, vrTarget,
                        prefix: new HarmonyMethod(typeof(Plugin), nameof(VoiceConditionPrefix)),
                        finalizer: new HarmonyMethod(typeof(Plugin), nameof(VoiceConditionFinalizer)));
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Strong voice hook failed: " + ex);
            }
        }

        private static void VoiceConditionPrefix(HVoiceCtrl __instance, out bool __state)
        {
            __state = false;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var plugin = Instance;
            if (plugin == null || plugin.DildoVoiceSuppressed || !plugin._aibuStrongVoice || plugin._scene == null ||
                plugin._sceneVoice != __instance || plugin._sceneFlags == null ||
                plugin._sceneFlags.voice == null || plugin._sceneFlags.mode != HFlag.EMode.aibu ||
                plugin._sceneFlags.voice.speedMotion)
                return;
            plugin._sceneFlags.voice.speedMotion = true;
            __state = true;
            if (!plugin._voiceHookObserved)
            {
                plugin._voiceHookObserved = true;
                plugin.LogDiagnostic("VR Vibe native strong voice condition applied");
            }
        }

        private static Exception VoiceConditionFinalizer(Exception __exception, bool __state)
        {
            var plugin = Instance;
            if (__state && plugin != null && plugin._scene != null && plugin._sceneFlags != null &&
                plugin._sceneFlags.voice != null)
                plugin._sceneFlags.voice.speedMotion = false;
            return __exception;
        }
    }
}
