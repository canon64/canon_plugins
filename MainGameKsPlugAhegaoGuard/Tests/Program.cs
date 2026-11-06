using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CanonGate;
using CanonPlugins.KsPlugAhegaoGuard;
using ksPlug.Character;

// Standalone Harmony integration harness. These models are not game verification.
namespace UnityEngine { public class MonoBehaviour { } }
namespace BepInEx
{
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public readonly Log Logger = new Log();
    }
    public class Log { public void LogInfo(object value) { } }
    public class BepInPlugin : Attribute { public BepInPlugin(string a, string b, string c) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute { public BepInDependency(string a, string b) { } }
    public class BepInProcess : Attribute { public BepInProcess(string a) { } }
}
namespace CanonGate
{
    public static class GateApi
    {
        public const string PluginGuid = "canon.gate";
        public enum SessionKind { None, H, Maker }
        public class SessionContext
        {
            public SessionKind Kind = SessionKind.H;
            public UnityEngine.MonoBehaviour Scene = new UnityEngine.MonoBehaviour();
            public ulong Id;
        }
        public static SessionContext Current;
        public class State { public bool Valid; }
        public struct Permit { internal State State; public bool IsValid => State != null && State.Valid; }
        private static State _state;
        private static Action _start, _stop;
        private static ulong _next;
        public static IDisposable Register(string guid, Action start, Action stop)
        { _start = start; _stop = stop; return new Registration(); }
        public static Permit Capture(string guid) => new Permit { State = _state };
        public static void Begin(bool allowed = true)
        {
            Current = new SessionContext { Id = ++_next };
            _state = new State { Valid = allowed };
            if (allowed) _start();
        }
        public static void RepeatStart() => _start();
        public static void Invalidate() { _state.Valid = false; }
        public static void End() { Invalidate(); _stop(); Current = null; }
        private class Registration : IDisposable { public void Dispose() { _stop(); } }
    }
}
namespace ksPlug.Character
{
    public class Info { public object HCount; }
    public class ChaCore { public Info HInfo; }
    public class AI
    {
        public ChaCore ChaCore { get; set; }
        public int Calls;
        public bool Result = true, Throw;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HasReachAhegao()
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("unrelated original error");
            if (ChaCore.HInfo.HCount == null) throw new NullReferenceException();
            return Result;
        }
    }
}
static class Program
{
    private static int _checks;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); _checks++; Console.WriteLine("PASS " + name); }
    static void Invoke(Plugin plugin, string method) => typeof(Plugin).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plugin, null);
    static void ExpectOriginalNull(AI ai, string name)
    {
        int before = ai.Calls;
        try { ai.HasReachAhegao(); throw new Exception("Expected null exception"); }
        catch (NullReferenceException) { Check(ai.Calls == before + 1, name); }
    }
    static void Main()
    {
        var plugin = new Plugin();
        Invoke(plugin, "Awake");
        var missing = new AI();
        GateApi.Begin(false);
        ExpectOriginalNull(missing, "denied session leaves original untouched");
        GateApi.End();
        GateApi.Begin();
        Check(!missing.HasReachAhegao() && missing.Calls == 1, "null core skips original");
        missing.ChaCore = new ChaCore();
        Check(!missing.HasReachAhegao() && missing.Calls == 1, "null HInfo skips original");
        missing.ChaCore.HInfo = new Info();
        Check(!missing.HasReachAhegao() && missing.Calls == 1, "null HCount skips original");
        var valid = new AI { ChaCore = new ChaCore { HInfo = new Info { HCount = new object() } } };
        Check(valid.HasReachAhegao() && valid.Calls == 1, "valid original true preserved");
        valid.Result = false;
        Check(!valid.HasReachAhegao() && valid.Calls == 2, "valid original false preserved");
        GateApi.RepeatStart();
        Check(!valid.HasReachAhegao() && valid.Calls == 3, "duplicate start does not duplicate original");
        valid.Throw = true;
        try { valid.HasReachAhegao(); throw new Exception("Expected original error"); }
        catch (InvalidOperationException) { Check(true, "unrelated original exceptions are not swallowed"); }
        GateApi.Invalidate();
        missing.ChaCore = null;
        ExpectOriginalNull(missing, "invalid permit cannot run guard");
        GateApi.End();
        ExpectOriginalNull(missing, "session end removes owned patch");
        GateApi.End();
        GateApi.Begin();
        Check(!missing.HasReachAhegao(), "next session reapplies guard");
        Invoke(plugin, "OnDisable");
        ExpectOriginalNull(missing, "disable removes owned patch");
        GateApi.End();
        GateApi.Begin();
        Invoke(plugin, "OnDestroy");
        ExpectOriginalNull(missing, "destroy removes owned patch");
        Console.WriteLine("Passed " + _checks + " integration checks.");
    }
}
