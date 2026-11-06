using System;
using System.Reflection;
using HarmonyLib;

namespace MainGameDollMode
{
    internal static partial class DollModeGuardHooks
    {
        private static PropertyInfo _voiceBridgeInstance;
        private static PropertyInfo _voiceBridgePlayer;
        private static MethodInfo _voiceBridgeStop;

        private static void ApplyExternalVoiceHooks(Harmony harmony)
        {
            // The bridge plays its own AudioSources, bypassing Manager.Voice entirely.
            // Resolve once at session start; the bridge remains an optional plugin.
            Type player = AccessTools.TypeByName("MainGameVoiceFaceEventBridge.ExternalVoicePlayer");
            Type bridge = AccessTools.TypeByName("MainGameVoiceFaceEventBridge.Plugin");
            _voiceBridgeInstance = bridge == null ? null : AccessTools.Property(bridge, "Instance");
            _voiceBridgePlayer = bridge == null ? null : AccessTools.Property(bridge, "_externalVoicePlayer");
            _voiceBridgeStop = _voiceBridgePlayer == null ? null :
                AccessTools.Method(_voiceBridgePlayer.PropertyType, "Stop", new[] { typeof(string) });
            if (player == null) return;

            string[] entries = { "Play", "PlaySequence", "AppendSequence", "TryPlayAiConversation",
                "RequestImpactCue", "RequestPriorityDeepCue", "RequestContinuousPistonCue",
                "RequestHardPistonCue", "RequestOgazmCue" };
            foreach (MethodInfo method in player.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.ReturnType != typeof(bool) || Array.IndexOf(entries, method.Name) < 0) continue;
                if (!IsAlreadyPatchedByOwner(method, harmony))
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(DollModeGuardHooks), nameof(ExternalVoicePrefix)));
            }
        }

        private static bool ExternalVoicePrefix(MethodBase __originalMethod, object[] __args, ref bool __result)
        {
            if (!ShouldBlock()) return true;
            // Piston/ogazm calls return subtitle and duration through out parameters.
            ParameterInfo[] parameters = __originalMethod.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOut) continue;
                Type valueType = parameters[i].ParameterType.GetElementType();
                __args[i] = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
            }
            __result = false;
            return false;
        }

        internal static void StopPlayingVoices()
        {
            // Stop cancels queued/background WAV loads and aborts cues without resuming dialogue.
            // Always stop native voices even if the optional bridge fails to clean up.
            try
            {
                object bridge = _voiceBridgeInstance?.GetValue(null, null);
                object player = bridge == null ? null : _voiceBridgePlayer?.GetValue(bridge, null);
                if (player != null) _voiceBridgeStop?.Invoke(player, new object[] { "doll mode" });
            }
            finally { Manager.Voice.StopAll(false); }
        }
    }
}
