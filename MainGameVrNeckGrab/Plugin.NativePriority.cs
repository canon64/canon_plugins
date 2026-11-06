using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MainGameVrNeckGrab
{
    public sealed partial class Plugin
    {
        void InstallNeckPriorityHooks()
        {
            var caress = AccessTools.TypeByName("KKS_VR.Caress.CaressController");
            var mouth = AccessTools.TypeByName("KKS_VR.Caress.VRMouth");
            if (caress == null || mouth == null) throw new TypeLoadException("VR標準の愛撫・キス処理が見つかりません。");
            Patch(AccessTools.Method(caress, "HandleTrigger"), transpiler: nameof(NeckTriggerTranspiler));
            Patch(AccessTools.Method(mouth, "StartKiss"), nameof(AutomaticKissPrefix));
        }
        static IEnumerable<CodeInstruction> NeckTriggerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodInfo method && method.Name == "GetPressDown" && method.ReturnType == typeof(bool))
                {
                    // The existing native check runs as before. Extra routing/distance work only
                    // happens if it returned true; no proximity polling is introduced.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(FilterNativeTrigger)));
                    ++replaced;
                }
            }
            if (replaced != 1) throw new InvalidOperationException("VR愛撫トリガーの入力境界が想定と異なります: " + replaced);
        }
        static bool FilterNativeTrigger(bool down, object caress)
        {
            if (!down) return false;
            var p = Instance;
            if (p == null || !p.Allowed) return true;
            return !p.RouteNativeController(ReflectionAccess.Get(caress, "_controller"));
        }
        static bool AutomaticKissPrefix(object __instance)
        {
            var p = Instance;
            if (p == null || !p.Allowed || !p.NeckBlocksAutomaticKiss) return true;
            var tracker = ReflectionAccess.Get(__instance, "_aibuTracker");
            return !ReferenceEquals(ReflectionAccess.Get(tracker, "Proc"), p._scene);
        }
    }
}
