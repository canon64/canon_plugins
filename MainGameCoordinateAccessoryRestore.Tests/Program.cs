using System;
using System.Reflection;
using CanonGate;
using CanonPlugins.Shared;
using P = MainGameCoordinateAccessoryRestore.Plugin;

internal static class Program
{
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static void Call(string name, params object[] args) => typeof(P).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Invoke(name == "Awake" || name == "OnDestroy" ? instance : null, args);
    static P instance;
    static void Main()
    {
        Console.WriteLine("Testing actual accessory plugin entry and rollback methods with stubbed game objects");
        var cha = new ChaControl();
        int revokedWrites = -1;
        using var first = GateApi.Register("first", () => { }, () => { int n = cha.Writes; Call("CoordinateChanged", cha, true); revokedWrites = cha.Writes - n; });
        instance = new P(); Call("Awake");
        Call("CoordinateChanged", cha, true);
        Check(cha.Writes == 0 && !instance.enabled, "Harmony entry outside gate performs no work");
        GateApi.SetRunning(true);
        var policy = new ExceptionPolicy(); policy.Load("", "");
        GateApi.BeginSession(GateApi.SessionKind.H, false, policy, new HSceneProc());
        Call("CoordinateChanged", cha, true);
        Check(cha.Writes == 0, "ordinary H is still denied");
        GateApi.EndSession(GateApi.SessionKind.H);
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, new HSceneProc());
        Call("CoordinateChanged", cha, true);
        Check(cha.fileStatus.showAccessory[0] && cha.fileStatus.showAccessory[1], "allowed coordinate change invokes real plugin mutation");
        Call("CoordinateReloaded", cha);
        Check(cha.Writes == 2, "reload event applies once without a frame loop");
        GateApi.EndSession(GateApi.SessionKind.H);
        Check(!cha.fileStatus.showAccessory[0] && cha.fileStatus.showAccessory[1], "H exit restores captured visibility");
        Check(revokedWrites == 0, "all permissions revoke before earlier plugin stop invokes this Harmony entry");
        int before = cha.Writes;
        Call("CoordinateReloaded", cha);
        Check(before == cha.Writes, "late reload after exit is ignored");
        Check(typeof(P).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance) == null, "no periodic reapply callback remains");
        policy.Load(P.Guid, "");
        GateApi.BeginSession(GateApi.SessionKind.Maker, false, policy);
        Call("CoordinateChanged", cha, true);
        Check(cha.fileStatus.showAccessory[0], "existing Maker behavior works through explicit gate exception");
        GateApi.SetRunning(false);
        Check(!cha.fileStatus.showAccessory[0], "gate shutdown restores Maker visibility too");
        Call("OnDestroy");
        Console.WriteLine("Passed " + count + " production-plugin checks");
    }
}
namespace UnityEngine { public class MonoBehaviour { public bool enabled = true; } }
public class HSceneProc : UnityEngine.MonoBehaviour { }
public class ChaFileCoordinate { }
public static class ChaFileDefine { public enum CoordinateType { Default } }
public class ChaControl
{
    public class Status { public bool[] showAccessory = { false, true }; }
    public Status fileStatus = new Status();
    public int Writes;
    public bool ChangeCoordinateType(ChaFileDefine.CoordinateType t, bool reload) => true;
    public bool SetNowCoordinate(ChaFileCoordinate c) => true;
    public void Reload() { }
    public void SetAccessoryStateAll(bool value) { Writes++; for(int i=0;i<fileStatus.showAccessory.Length;i++) fileStatus.showAccessory[i]=value; }
    public void SetAccessoryState(int i, bool value) { Writes++; fileStatus.showAccessory[i]=value; }
}
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)] public class BepInDependency : Attribute { public BepInDependency(string s) { } }
    public class BepInProcess : Attribute { public BepInProcess(string s) { } }
    public class BepInPlugin : Attribute { public BepInPlugin(string a,string b,string c) { } }
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour
    {
        public class Metadata { public string GUID = P.Guid; }
        public class PluginInfo { public Metadata Metadata = new Metadata(); }
        public class Log { public void LogInfo(object s) { } public void LogError(object s) { } }
        public PluginInfo Info = new PluginInfo(); public Log Logger = new Log(); public Configuration.ConfigFile Config = new Configuration.ConfigFile();
    }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; }
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string a,string b,T value,object desc) => new ConfigEntry<T>{Value=value}; }
    public class ConfigDescription { public ConfigDescription(string s,object range) { } }
    public class AcceptableValueRange<T> { public AcceptableValueRange(T a,T b) { } }
}
namespace HarmonyLib
{
    public static class Priority { public const int Last=0; }
    public class HarmonyMethod { public int priority; public HarmonyMethod(Type t,string n) { } }
    public class Harmony { public Harmony(string id) { } public void Patch(MethodInfo m, HarmonyMethod postfix=null) { } public void UnpatchSelf() { } }
    public static class AccessTools { public static MethodInfo Method(Type t,string n,Type[] p=null) => p==null?t.GetMethod(n):t.GetMethod(n,p); }
}
