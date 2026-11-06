using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MainGameSamabakeMotion
{
    internal static class NativeHooks
    {
        internal static void Install(Harmony harmony) { harmony.PatchAll(typeof(NativeHooks)); }

        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void BeforeChange() { Plugin.Runtime?.BeforeChange(); }

        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void CaptureBase(HSceneProc __instance, [HarmonyArgument(0)] HSceneProc.AnimationListInfo pose)
        { Plugin.Runtime?.CaptureBase(__instance, pose); }

        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void AfterChange(HSceneProc __instance, [HarmonyArgument(0)] HSceneProc.AnimationListInfo pose)
        { Plugin.Runtime?.AfterChange(__instance, pose); }

        [HarmonyPatch(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) })]
        [HarmonyPostfix]
        private static void AfterPlay([HarmonyArgument(0)] string state) { Plugin.Runtime?.AfterPlay(state); }

        [HarmonyPatch(typeof(HandCtrl), "SetAnimation")]
        [HarmonyPostfix]
        private static void AfterHand(HandCtrl __instance, [HarmonyArgument(0)] string state)
        { Plugin.Runtime?.RestrictTouch(__instance, state); }

        // Only the imported actors bypass the original pose's IK. H action progression remains native.
        [HarmonyPatch(typeof(MotionIK), "Calc")]
        [HarmonyPrefix]
        private static bool OriginalPoseIk(MotionIK __instance)
        {
            if (NativeContactRig.IsImported(__instance) || Plugin.Runtime == null || !Plugin.Runtime.Owns(__instance.info)) return true;
            Plugin.Runtime.ShapeChanged(__instance.info);
            return false;
        }

        [HarmonyPatch(typeof(ChaControl), "UpdateShapeBodyCalcForce")]
        [HarmonyPostfix]
        private static void ShapeChanged(ChaControl __instance) { Plugin.Runtime?.ShapeChanged(__instance); }

        [HarmonyPatch(typeof(AnimatorLayerCtrl), "Proc")]
        [HarmonyPrefix]
        private static bool OriginalPoseLayer(Animator ___anim) => Plugin.Runtime == null || !Plugin.Runtime.Owns(___anim);
    }
}
