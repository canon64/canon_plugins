using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using FreeH;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    [HarmonyPatch]
    internal static class OwnedHReturnReadPatches
    {
        [HarmonyTargetMethod]
        private static MethodBase Target() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(FreeHScene), "Start"));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BeforeNativeRead(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(UnityEngine.GameObject)
                    && method.Name == nameof(UnityEngine.GameObject.GetComponent) && method.IsGenericMethod
                    && method.GetGenericArguments().SequenceEqual(new[] { typeof(FreeHBackData) }))
                {
                    if (found) throw new InvalidOperationException("FreeH return read boundary is ambiguous.");
                    found = true;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(OwnedHReturnData), nameof(OwnedHReturnData.GetNativeReturn));
                }
                yield return instruction;
            }
            if (!found) throw new MissingMethodException("FreeH Start no longer reads FreeHBackData at the expected boundary.");
        }
    }
}
