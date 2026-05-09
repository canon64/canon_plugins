using HarmonyLib;

namespace MainGirlHipHijack
{
    internal static class PluginPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "LateUpdate")]
        private static void Postfix_HSceneProcLateUpdate(HSceneProc __instance)
        {
            Plugin.Instance?.OnAfterHSceneLateUpdate(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void Postfix_HSceneProcChangeAnimator(HSceneProc __instance)
        {
            Plugin.Instance?.OnAfterHSceneChangeAnimator(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HActionBase), "SetAnimatorFloat",
            new[] { typeof(string), typeof(float), typeof(bool), typeof(bool) })]
        private static bool Prefix_HActionBaseSetAnimatorFloat(
            HActionBase __instance, string _param, float _value, bool _isMale, bool _isFemale1,
            ref bool __result)
        {
            var plugin = Plugin.Instance;
            if (plugin != null && plugin.TryApplyFemaleAnimSpeedCut(__instance, _param, _value, _isMale, _isFemale1))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
