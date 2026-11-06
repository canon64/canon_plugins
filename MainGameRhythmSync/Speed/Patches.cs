using HarmonyLib;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameSpeedLimitBreak
{
    [HarmonyPatch(typeof(HActionBase), "SetAnimatorFloat", new[] { typeof(string), typeof(float), typeof(bool), typeof(bool) })]
    internal static class Patches
    {
        private static bool Prefix(string _param, ref float _value)
        {
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return true;
            var plugin = Plugin.Instance;
            if (plugin == null)
                return true;

            plugin.TraceSetAnimatorFloat(_param, _value);
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
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return true;
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
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return;
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
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return true;
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
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return;
            var plugin = Plugin.Instance;
            if (plugin == null)
                return;

            plugin.TracePatchCall("WaitSpeedProcAibu.Postfix", __instance);
            plugin.EnforceTimelineAfterWaitSpeedProcAibu(__instance);
        }
    }

    [HarmonyPatch]
    internal static class HSceneProcUpdateTimelineForcePatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() => MainGameRhythmSync.SessionScene.Methods("Update");
        [HarmonyPriority(Priority.Normal)]
        private static void Postfix(MonoBehaviour __instance)
        {
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return;
            var plugin = Plugin.Instance;
            if (plugin == null || __instance == null)
                return;

            var scene = CanonGate.GateApi.Current;
            if (scene == null || scene.Scene != __instance) return;
            var flags = Plugin.Instance.SessionFlags;
            plugin.TracePatchCall("HSceneProc.Update.Postfix", flags);
            plugin.TraceMasturbationAnimators();
            plugin.ForceTimelineGaugeOnFlags(flags, "hscene-update");
        }
    }

    [HarmonyPatch(typeof(HSprite), "Update")]
    internal static class HSpriteUpdateTimelineForcePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(HSprite __instance)
        {
            // BlankMapAdd の追加マップ以外では発火させない。
            if (Plugin.Instance?.SessionActive != true || MainGameRhythmSync.Plugin.Instance?.IsPhaseSyncActive == true) return;
            var plugin = Plugin.Instance;
            if (plugin == null || __instance == null)
                return;

            plugin.TracePatchCall("HSprite.Update.Postfix");
            plugin.ForceTimelineGaugeOnSprite(__instance);
        }
    }
}
