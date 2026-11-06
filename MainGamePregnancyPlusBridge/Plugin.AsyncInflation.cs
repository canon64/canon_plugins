using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CanonGate;
using CanonPlugins.Shared;
using HarmonyLib;

namespace MainGamePregnancyPlusBridge
{
    public sealed partial class Plugin
    {
        private sealed class InflationPermit { internal GateApi.Permit Permit; }
        private static readonly ConditionalWeakTable<object, InflationPermit> InflationWork = new ConditionalWeakTable<object, InflationPermit>();
        private Harmony _inflationHarmony;

        private void BindInflationWork()
        {
            if (_inflationHarmony != null) return;
            var flags = _meshInflateByDataMethod.GetParameters()[2].ParameterType;
            var begin = _controllerType.GetMethod("MeshInflate", new[] { flags, typeof(string) });
            var finish = _controllerType.GetMethod("FinalizeInflation", BindingFlags.Instance | BindingFlags.NonPublic);
            var clone = flags.GetMethod("Clone", Type.EmptyTypes);
            if (begin == null || finish == null || clone == null) throw new MissingMethodException("PregnancyPlus asynchronous inflation contract");
            _inflationHarmony = new Harmony(GUID + ".session-inflation");
            try
            {
                _inflationHarmony.Patch(begin, prefix: new HarmonyMethod(typeof(Plugin), nameof(BeginInflationWork)));
                _inflationHarmony.Patch(finish, prefix: new HarmonyMethod(typeof(Plugin), nameof(FinishInflationWork)));
                _inflationHarmony.Patch(clone, postfix: new HarmonyMethod(typeof(Plugin), nameof(CloneInflationWork)));
            }
            catch { _inflationHarmony.UnpatchSelf(); _inflationHarmony = null; throw; }
        }

        private static bool BeginInflationWork([HarmonyArgument(0)] object flags, [HarmonyArgument(1)] string callee)
        {
            if (flags == null) return true;
            if (string.Equals(callee, "MainGamePregnancyPlusBridge", StringComparison.Ordinal))
            {
                if (!PluginSessionGate.Allowed) return false;
                InflationWork.Remove(flags);
                InflationWork.Add(flags, new InflationPermit { Permit = PluginSessionGate.Capture() });
            }
            return !InflationWork.TryGetValue(flags, out var work) || work.Permit.IsValid;
        }

        private static bool FinishInflationWork([HarmonyArgument(1)] object flags) =>
            flags == null || !InflationWork.TryGetValue(flags, out var work) || work.Permit.IsValid;

        private static void CloneInflationWork(object __instance, object __result)
        {
            if (__result == null || ReferenceEquals(__instance, __result) || !InflationWork.TryGetValue(__instance, out var work)) return;
            InflationWork.Remove(__result);
            InflationWork.Add(__result, work);
        }
    }
}
