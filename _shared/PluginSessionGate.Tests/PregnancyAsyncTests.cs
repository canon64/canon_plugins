using System;
using System.Reflection;
using CanonGate;
using CanonPlugins.Shared;

namespace HarmonyLib
{
    public class HarmonyArgument : Attribute { public HarmonyArgument(int index) { } }
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodInfo method, HarmonyMethod prefix = null, HarmonyMethod postfix = null) { }
        public void UnpatchSelf() { }
    }
}
namespace MainGamePregnancyPlusBridge
{
    public sealed partial class Plugin
    {
        public const string GUID = "test.pp";
        public static bool TestBeginWork(object flags, string callee) => BeginInflationWork(flags, callee);
        public static bool TestFinishWork(object flags) => FinishInflationWork(flags);
        public static void TestCloneWork(object source, object clone) => CloneInflationWork(source, clone);
    }
}
internal static class PregnancyAsyncTests
{
    internal static void Run(Action<bool,string> check)
    {
        GateApi.EndSession(GateApi.SessionKind.H);
        var owner = new BepInEx.BaseUnityPlugin();
        PluginSessionGate.Bind(owner, "test.pp", null, null);
        var policy = new ExceptionPolicy(); policy.Load("", "");
        var flags = new object(); var queuedClone = new object(); var secondClone = new object();
        check(!MainGamePregnancyPlusBridge.Plugin.TestBeginWork(flags, "MainGamePregnancyPlusBridge"), "vendor inflation entry rejects bridge work outside gate");
        GateApi.SetRunning(true);
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, new HSceneProc());
        check(MainGamePregnancyPlusBridge.Plugin.TestBeginWork(flags, "MainGamePregnancyPlusBridge") && MainGamePregnancyPlusBridge.Plugin.TestFinishWork(flags), "real vendor callbacks accept current bridge work");
        MainGamePregnancyPlusBridge.Plugin.TestCloneWork(flags, queuedClone);
        MainGamePregnancyPlusBridge.Plugin.TestCloneWork(queuedClone, secondClone);
        GateApi.SetRunning(false);
        check(!MainGamePregnancyPlusBridge.Plugin.TestFinishWork(flags) && !MainGamePregnancyPlusBridge.Plugin.TestFinishWork(secondClone), "gate stop discards asynchronous mesh result and queued clones");
        check(MainGamePregnancyPlusBridge.Plugin.TestFinishWork(new object()) && MainGamePregnancyPlusBridge.Plugin.TestBeginWork(new object(), "MainGamePregnancyPlusBridge-restore"), "native PregnancyPlus work and explicit rollback stay available");
        GateApi.SetRunning(true);
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, new HSceneProc());
        check(!MainGamePregnancyPlusBridge.Plugin.TestBeginWork(queuedClone, "lastMeshInflateFlags") && !MainGamePregnancyPlusBridge.Plugin.TestFinishWork(secondClone), "previous H queued work cannot apply in new H");
        PluginSessionGate.Dispose();
    }
}
