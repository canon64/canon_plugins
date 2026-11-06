using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MainGameFrameTimingProbe
{
    internal static class HookCleanup
    {
        internal static void Remove(Harmony harmony, IEnumerable<MethodBase> methods, string owner,
            Action<MethodBase, Exception> failed)
        {
            // Global UnpatchSelf walks unrelated plugins, including unresolved VR types.
            // Only visit methods that this capture successfully patched.
            foreach (MethodBase method in methods)
            {
                try { harmony.Unpatch(method, HarmonyPatchType.All, owner); }
                catch (Exception ex) { failed(method, ex); }
            }
        }
    }
}
