using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Checks.Add(message); }
    private static void Main()
    {
        var owner = new NativeActionWalkingSession(); var player = owner.Action.Player;
        var visible = new Renderer { enabled = true }; var hidden = new Renderer { enabled = false };
        player.chaCtrl.Renderers = new[] { visible, hidden };
        var view = new Camera(); var driver = new NativeActionWalkingDriver(owner, view, new Transform(), new Parameters());
        Check(!player.move.enabled && player.move.isStop && !owner.Action.CursorLocked, "unready native player starts stopped");
        driver.SetInputBlocked(false); player.move.NativeTick();
        Check(player.move.isStop && player.agent.isStopped && !player.move.Moved, "input cannot activate walking before the map is ready");
        owner.Ready = true; driver.MapReady(); player.move.NativeTick();
        Check(player.move.enabled && !player.move.isStop && player.move.Moved && owner.Action.CursorLocked, "map readiness enables native movement");
        Check(owner.Moving, "native movement notification enables VR follow");
        driver.SetInputBlocked(true); player.move.NativeTick();
        Check(!player.move.enabled && player.move.isStop && player.agent.isStopped && !player.move.Moved, "menu or focus block survives native subscription execution");
        Check(!owner.Moving, "blocked input stops VR follow without another frame observer");
        Check(!visible.enabled && !hidden.enabled, "native movement body is hidden while mode avatar owns presentation");
        driver.SetInputBlocked(false); player.move.NativeTick();
        Check(player.move.Moved && !player.agent.isStopped, "closing the menu resumes native walking");
        driver.Dispose(); player.move.NativeTick(); driver.Dispose();
        Check(player.move.isStop && player.agent.isStopped && !player.move.Moved && !owner.Action.CursorLocked, "disposal stops subscriptions as well as the component");
        Check(visible.enabled && !hidden.enabled, "disposal restores exact renderer state");
        driver.SetInputBlocked(false); player.move.NativeTick();
        Check(!player.move.Moved, "disposed driver cannot resume native input");
        Console.WriteLine("PASS " + Checks.Count + " native driver input and cleanup checks (native subscription behavior modeled from installed DLL)");
    }
}
namespace MapEnvironmentRuntime
{
    internal interface INativeWalkingDriver { }
    internal class Parameters { public float N(string key) => default; }
    internal class WalkingStatus
    {
        public bool grounded, physicalGrounded, crouching, cursorReleased, cursorReleaseRequested;
        public Vector3 cameraPosition; public float height, radius; public string navigationState;
    }
}
namespace UnityEngine
{
    internal class Object
    {
        public static void DontDestroyOnLoad(GameObject target) { }
    }
    internal class GameObject : Object { public SceneManagement.Scene scene; }
    internal class Transform
    {
        public Transform parent; public readonly GameObject gameObject = new(); public Vector3 position; public Quaternion rotation;
        public void SetParent(Transform value, bool preserve) { parent = value; }
    }
    internal class Camera { public readonly Transform transform = new(); }
    internal class Renderer { public bool enabled; }
    internal struct Vector3
    {
        public static Vector3 zero => default; public static Vector3 up => default;
        public static Vector3 operator +(Vector3 left, Vector3 right) => default;
        public static Vector3 operator -(Vector3 left, Vector3 right) => default;
        public static Vector3 operator *(Vector3 left, float right) => default;
    }
    internal struct Quaternion { public static Quaternion identity => default; public static Quaternion Euler(float x, float y, float z) => default; }
}
namespace UnityEngine.SceneManagement
{
    internal struct Scene { public string name; public bool isLoaded; public bool IsValid() => false; }
    internal static class SceneManager { public static void MoveGameObjectToScene(GameObject target, Scene scene) { } }
}
namespace UnityEngine.AI
{
    internal class NavMeshAgent
    {
        public bool enabled = true, isOnNavMesh = true, isStopped;
        public float height, radius;
        public bool Warp(Vector3 destination) => true;
    }
    internal struct NavMeshHit { public Vector3 position; }
    internal static class NavMesh
    {
        public static int AllAreas => default;
        public static bool SamplePosition(Vector3 position, out NavMeshHit floor, float distance, int mask) { floor = default; return true; }
    }
}
internal class ActionScene
{
    public readonly NativePlayer Player = new(); public bool CursorLocked;
    public void SetCursorLock(bool value) { CursorLocked = value; }
}
internal class NativePlayer
{
    public readonly Transform transform = new(); public readonly NavMeshAgent agent = new();
    public readonly NativeMover move; public readonly Character chaCtrl = new();
    public NativePlayer() { move = new NativeMover(agent); }
}
internal class Character
{
    public Renderer[] Renderers = Array.Empty<Renderer>();
    public T[] GetComponentsInChildren<T>(bool inactive) => Renderers.Cast<T>().ToArray();
}
internal class NativeMover
{
    private readonly NavMeshAgent _agent;
    public bool enabled = true, isStop, Moved; public readonly Speeder agentSpeeder = new();
    public readonly MotionSignal animParamSpeedEvent = new MotionSignal();
    public NativeMover(NavMeshAgent agent) { _agent = agent; }
    // Mover.Base's UpdateAsObservable subscriptions are independent of mover.enabled.
    public void NativeTick() { _agent.isStopped = isStop; Moved = !isStop && _agent.isOnNavMesh; animParamSpeedEvent.Emit(Convert.ToSingle(Moved)); }
    internal class Speeder { public Mode mode => Mode.Walk; }
    internal enum Mode { Walk, Crouch }
}
internal sealed class MotionSignal : IObservable<float>
{
    private IObserver<float> _observer;
    public IDisposable Subscribe(IObserver<float> observer) { _observer = observer; return new Subscription(() => _observer = null); }
    public void Emit(float speed) { _observer?.OnNext(speed); }
    private sealed class Subscription : IDisposable { private readonly Action _release; public Subscription(Action release) { _release = release; } public void Dispose() { _release(); } }
}
namespace MainGameAiShoujoMode
{
    internal class NativeActionWalkingSession
    {
        public readonly ActionScene Action = new(); public bool Ready;
        public bool Moving; public void SetWalkingMotion(bool moving) { Moving = moving; }
        public void AlignView() { } public void BeginTraversal() { } public void EndTraversal() { }
    }
}
