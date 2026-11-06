using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameAiShoujoMode;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Checks.Add(name); }
    private static void Main()
    {
        NativeActionWorldScope.Valid = true;
        var action = new ActionScene();
        ActionScene result = null; Exception failure = null;
        var scope = new NativeActionSceneScope(value => result = value, error => failure = error);
        scope.Begin();
        Check(SceneManager.LoadName == "Action" && SceneManager.LoadMode == LoadSceneMode.Additive, "real built-in walking scene requested");
        SceneManager.Activate(action);
        action.Map.Reserve();
        Check(!action.Map.Reserved, "owned walking map ignores foreign active scenes before metadata exists");
        var otherMap = new ActionGame.ActionMap(); otherMap.Reserve();
        Check(otherMap.Reserved, "H and other maps retain their native reserve callback");
        Check(result == null, "scene activation alone is not native player readiness");
        var foreignCycle = new ActionGame.Cycle(); foreignCycle.Change(ActionGame.Cycle.Type.Daytime);
        Check(foreignCycle.Changed, "unowned native clock unchanged");
        var original = action.Start();
        var args = new object[] { action, original };
        HarmonyLib.Harmony.Postfix.Invoke(null, args);
        var wrapped = (IEnumerator)args.Last();
        while (wrapped.MoveNext()) { }
        Check(action.Before && action.Child && action.Player.initialized, "all native initialization and nested coroutine run");
        Check(!action.Cycle.Changed, "owned story clock does not start scheduling or saving");
        Check(ReferenceEquals(result, action) && failure == null, "readiness returns actual initialized ActionScene");
        scope.Dispose(); scope.Dispose();
        Check(!scope.Released && SceneManager.Unload != null, "world cannot release before native scene unload completes");
        SceneManager.Load.Complete();
        Check(!scope.Released, "duplicate load completion cannot skip pending unload");
        SceneManager.CompleteUnload();
        Check(scope.Released && SceneManager.Subscribers == default, "normal exit releases native scene and subscription");

        var cancelled = new NativeActionSceneScope(_ => throw new Exception("stale readiness"), error => failure = error);
        cancelled.Begin(); cancelled.Dispose();
        Check(!cancelled.Released, "cancellation waits for scene already loading");
        SceneManager.Activate(new ActionScene());
        Check(!cancelled.Released && SceneManager.Subscribers == default, "late scene activation goes directly to unload");
        SceneManager.CompleteUnload();
        Check(cancelled.Released, "late cancelled load finishes cleanup");

        var badAction = new ActionScene { Fail = true };
        failure = null;
        var bad = new NativeActionSceneScope(_ => throw new Exception("false readiness"), error => failure = error);
        bad.Begin(); SceneManager.Activate(badAction);
        args = new object[] { badAction, badAction.Start() };
        HarmonyLib.Harmony.Postfix.Invoke(null, args);
        wrapped = (IEnumerator)args.Last(); while (wrapped.MoveNext()) { }
        Check(failure != null && SceneManager.Unload != null, "native initialization failure reports and unloads");
        SceneManager.CompleteUnload();
        Check(bad.Released, "failure cleanup completes");

        using (var lost = new NativeActionSceneScope(_ => { }, _ => { }))
        {
            NativeActionWorldScope.Valid = false;
            bool rejected = false;
            try { lost.Begin(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "lost temporary world rejects delayed load");
        }
        Check(!HarmonyLib.Harmony.Patched, "temporary hooks do not survive exit");
        Console.WriteLine("PASS " + Checks.Count + " native Action scene lifecycle checks");
    }
}
namespace MainGameAiShoujoMode
{
    internal static class NativeActionWorldScope { internal static bool Valid; internal static bool Owns(object world) => Valid; }
}
namespace Manager { internal static class Game { public static object saveData = new(); } }
internal class ActionScene
{
    public readonly ActionGame.Cycle Cycle = new();
    public readonly ActionGame.ActionMap Map = new();
    public readonly Player Player = new();
    public bool Before, Child, Fail;
    public IEnumerator Start()
    {
        Before = true; yield return Nested();
        if (Fail) throw new InvalidOperationException("injected native failure");
        Player.initialized = true;
        Cycle.Change(ActionGame.Cycle.Type.Daytime);
    }
    private IEnumerator Nested() { Child = true; yield return null; }
}
internal class Player { public bool initialized; }
namespace ActionGame
{
    internal class ActionMap
    {
        public bool Reserved;
        public void Reserve() { if (HarmonyLib.Harmony.MapPrefix == null || (bool)HarmonyLib.Harmony.MapPrefix.Invoke(null, new object[] { this })) Reserved = true; }
    }
    internal class Cycle
    {
        public enum Type { Daytime }
        public bool Changed;
        public void Change(Type type) { if (HarmonyLib.Harmony.Prefix == null || (bool)HarmonyLib.Harmony.Prefix.Invoke(null, new object[] { this })) Changed = true; }
        public void CancelMapMove() { }
    }
}
namespace UnityEngine
{
    internal class AsyncOperation
    {
        public bool isDone;
        public event Action<AsyncOperation> completed;
        public void Complete() { isDone = true; completed?.Invoke(this); }
    }
    internal class GameObject
    {
        public ActionScene Action;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new[] { Action }.Cast<T>().ToArray();
    }
}
namespace UnityEngine.SceneManagement
{
    internal enum LoadSceneMode { Additive }
    internal class SceneState { public bool Loaded = true; public ActionScene Action; }
    internal struct Scene
    {
        public SceneState State;
        public string name => State == null ? null : "Action";
        public bool isLoaded => State != null && State.Loaded;
        public bool IsValid() => State != null;
        public GameObject[] GetRootGameObjects() => new[] { new GameObject { Action = State.Action } };
    }
    internal static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static int Subscribers => sceneLoaded?.GetInvocationList().Length ?? default;
        public static AsyncOperation Load, Unload;
        public static string LoadName;
        public static LoadSceneMode LoadMode;
        private static Scene _scene;
        public static Scene GetSceneByName(string name) => _scene;
        public static AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode)
        { LoadName = name; LoadMode = mode; return Load = new AsyncOperation(); }
        public static AsyncOperation UnloadSceneAsync(Scene scene) => Unload = new AsyncOperation();
        public static void Activate(ActionScene action)
        { _scene = new Scene { State = new SceneState { Action = action } }; sceneLoaded?.Invoke(_scene, LoadSceneMode.Additive); Load.Complete(); }
        public static void CompleteUnload() { Unload.Complete(); _scene = default; Unload = null; }
    }
}
namespace HarmonyLib
{
    internal class Harmony
    {
        public static MethodInfo Prefix, Postfix, MapPrefix;
        public static bool Patched => Prefix != null || Postfix != null;
        public Harmony(string id) { }
        public void Patch(MethodBase method, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        { if (prefix != null) { if (method.DeclaringType == typeof(ActionGame.ActionMap)) MapPrefix = prefix.Method; else Prefix = prefix.Method; } if (postfix != null) Postfix = postfix.Method; }
        public void UnpatchSelf() { Prefix = null; Postfix = null; MapPrefix = null; }
    }
    internal class HarmonyMethod
    {
        public readonly MethodInfo Method;
        public HarmonyMethod(System.Type type, string method) { Method = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic); }
    }
    internal static class AccessTools
    {
        public static MethodInfo Method(System.Type type, string name, System.Type[] args = null) => args == null ? type.GetMethod(name) : type.GetMethod(name, args);
    }
}
