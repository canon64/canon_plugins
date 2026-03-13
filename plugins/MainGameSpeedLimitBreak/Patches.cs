using HarmonyLib;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    [HarmonyPatch(typeof(HActionBase), "SetAnimatorFloat", new[] { typeof(string), typeof(float), typeof(bool), typeof(bool) })]
    internal static class Patches
    {
        private static bool Prefix(string _param, ref float _value)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
                return true;

            plugin.TryRemap(_param, ref _value);
            return true;
        }
    }

    [HarmonyPatch(typeof(HFlag), "WaitSpeedProc", new[] { typeof(bool), typeof(AnimationCurve) })]
    internal static class HFlagWaitSpeedProcPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(HFlag __instance, bool _isLock, AnimationCurve _curve)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
                return true;

            plugin.TracePatchCall("WaitSpeedProc.Prefix", __instance);
            bool hijacked = plugin.TryHijackWaitSpeedProc(__instance, _isLock, _curve);
            return !hijacked;
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(HFlag __instance, bool _isLock, AnimationCurve _curve)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
                return;

            plugin.TracePatchCall("WaitSpeedProc.Postfix", __instance);
            plugin.EnforceTimelineAfterWaitSpeedProc(__instance, _curve);
        }
    }

    [HarmonyPatch(typeof(HFlag), "WaitSpeedProcAibu")]
    internal static class HFlagWaitSpeedProcAibuPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(HFlag __instance)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
                return true;

            plugin.TracePatchCall("WaitSpeedProcAibu.Prefix", __instance);
            bool hijacked = plugin.TryHijackWaitSpeedProcAibu(__instance);
            return !hijacked;
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(HFlag __instance)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
                return;

            plugin.TracePatchCall("WaitSpeedProcAibu.Postfix", __instance);
            plugin.EnforceTimelineAfterWaitSpeedProcAibu(__instance);
        }
    }

    [HarmonyPatch(typeof(HSceneProc), "Update")]
    internal static class HSceneProcUpdateTimelineForcePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(HSceneProc __instance)
        {
            var plugin = Plugin.Instance;
            if (plugin == null || __instance == null)
                return;

            plugin.TracePatchCall("HSceneProc.Update.Postfix", __instance.flags);
            plugin.ForceTimelineGaugeOnFlags(__instance.flags, "hscene-update");
        }
    }

    [HarmonyPatch(typeof(HSprite), "Update")]
    internal static class HSpriteUpdateTimelineForcePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(HSprite __instance)
        {
            var plugin = Plugin.Instance;
            if (plugin == null || __instance == null)
                return;

            plugin.TracePatchCall("HSprite.Update.Postfix");
            plugin.ForceTimelineGaugeOnSprite(__instance);
        }
    }
}
