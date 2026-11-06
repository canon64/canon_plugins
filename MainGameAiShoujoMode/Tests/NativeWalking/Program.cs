using System;
using System.Collections;
using System.Collections.Generic;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;
using UnityEngine;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Checks.Add(message); }
    private static void Drain(IEnumerator steps)
    {
        while (steps.MoveNext()) if (steps.Current is IEnumerator nested) Drain(nested);
    }
    private static void Main()
    {
        var settings = new Parameters(); var context = new Context { Settings = settings };
        var previous = new SaveData.WorldData { player = new SaveData.Player(new ChaFileControl(), false) };
        Manager.Game.saveData = previous;
        var owner = new NativeActionWalkingSession(settings);
        var temporary = Manager.Game.saveData;
        Check(!ReferenceEquals(previous, temporary) && !ReferenceEquals(previous.player.charFile, temporary.player.charFile), "temporary world and card are separate");
        Check(!owner.PlayerReady && !owner.Ready, "scene request does not expose an uninitialized player");
        var scene = NativeActionSceneScope.Last; var action = new ActionScene(); scene.Complete(action);
        Check(owner.PlayerReady && !owner.Ready && !NativeActionVrSceneScope.Last.Enabled, "native scene initialization precedes floor and VR activation");
        var walker = owner.CreateWalker(context, new Camera(), new Transform());
        Check(ReferenceEquals(walker, action.Player.gameObject.Added) && ReferenceEquals(walker.Session, context), "adapter belongs to the actual native player");
        bool rejected = false;
        try { owner.CreateWalker(context, new Camera(), new Transform()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "second walking owner rejected");
        var firstRoot = new GameObject(); Drain(owner.AttachMap(firstRoot, settings));
        var firstMap = NativeActionMapScope.Last;
        Check(owner.Ready && firstMap.Attached && ReferenceEquals(firstMap.Root, firstRoot) && NativeActionVrSceneScope.Last.Enabled, "native floor is attached before walking is exposed");
        owner.Suspend();
        Check(!action.Player.Active && !NativeActionVrSceneScope.Last.Enabled, "H entry suspends native player and VR walking");
        owner.Resume();
        Check(action.Player.Active && NativeActionVrSceneScope.Last.Enabled, "H exit restores native player and VR walking");
        var secondRoot = new GameObject(); var destination = new Vector3 { Tag = "upper floor entry" };
        var replacement = owner.ReplaceMap(secondRoot, settings, destination, Quaternion.identity);
        Check(replacement.MoveNext(), "floor replacement starts a finite load");
        Check(firstMap.Disposed && !owner.Ready && !NativeActionVrSceneScope.Last.Enabled, "old floor releases before new floor activation");
        Drain((IEnumerator)replacement.Current); Drain(replacement);
        var secondMap = NativeActionMapScope.Last;
        Check(owner.Ready && secondMap.Attached && ReferenceEquals(secondMap.Root, secondRoot) && secondMap.Feet.Tag == destination.Tag, "hotel floor replacement uses the destination floor and entry");
        var interrupted = owner.ReplaceMap(firstRoot, settings, destination, Quaternion.identity);
        interrupted.MoveNext(); var attach = (IEnumerator)interrupted.Current; attach.MoveNext(); var bake = (IEnumerator)attach.Current; bake.MoveNext();
        var pendingMap = NativeActionMapScope.Last;
        owner.Dispose(); owner.Dispose();
        Check(pendingMap.Disposed && secondMap.Disposed && !owner.Ready && !owner.Released, "cancellation disposes pending and previous floor while unload is pending");
        Check(ReferenceEquals(Manager.Game.saveData, temporary), "temporary world survives until native scene unload");
        Drain(bake); Drain(attach); Drain(interrupted); owner.EndTraversal(); owner.Resume();
        Check(!owner.Ready && !NativeActionVrSceneScope.Last.Enabled, "late load or traversal completion cannot restart a disposed session");
        scene.CompleteRelease();
        Check(owner.Released && ReferenceEquals(Manager.Game.saveData, previous), "native unload restores exact previous world");
        var failedOwner = new NativeActionWalkingSession(settings); var failedScene = NativeActionSceneScope.Last;
        NativeActionVrSceneScope.FailNext = true; failedScene.Complete(new ActionScene());
        Check(failedOwner.Error != null && !failedOwner.PlayerReady && !failedOwner.Ready, "VR initialization failure rejects readiness and initiates cleanup");
        failedScene.CompleteRelease();
        Check(failedOwner.Released && ReferenceEquals(Manager.Game.saveData, previous), "partial startup failure restores previous world after unload");
        var female = new ChaFileControl(); female.parameter.sex = settings.I("math.one");
        var femaleOwner = new NativeActionWalkingSession(settings, female); var femaleScene = NativeActionSceneScope.Last;
        Check(female.parameter.sex == settings.I("math.one") && Manager.Game.saveData.player.charFile.parameter.sex == settings.I("math.zero"), "female mode avatar card is preserved while existing native locomotion uses its hidden male backing");
        femaleOwner.Dispose(); femaleScene.CompleteRelease();
        Check(ReferenceEquals(Manager.Game.saveData, previous), "female mode locomotion returns the exact previous world");
        Console.WriteLine("PASS " + Checks.Count + " native walking session ownership and floor transition checks (Unity boundaries mocked)");
    }
    private sealed class Context : IWalkingSession { public Parameters Settings { get; set; } }
}
namespace UnityEngine
{
    internal class Object { public static void Destroy(object value) { } }
    internal class Camera { }
    internal class Transform { }
    internal struct Vector3 { public string Tag; }
    internal struct Quaternion { public static Quaternion identity => default; }
    internal class GameObject
    {
        public object Added;
        public T AddComponent<T>() where T : new() { var value = new T(); Added = value; return value; }
    }
}
namespace MapEnvironmentRuntime
{
    internal sealed class Parameters { public string T(string key) => null; public int I(string key) => key == "math.one" ? Math.Sign(int.MaxValue) : default; }
    internal interface IWalkingSession { Parameters Settings { get; } }
    internal class SandboxFreeCamera
    {
        public IWalkingSession Session;
        public void InitializeNative(IWalkingSession session, NativeActionWalkingDriver driver) { Session = session; }
    }
}
internal class ChaFileControl
{
    public readonly CardParameter parameter = new(); public bool LoadCharaFile(string path) => true;
    internal class CardParameter { public int sex { get; set; } }
}
namespace SaveData
{
    internal class Player { public ChaFileControl charFile; public Player(ChaFileControl card, bool unused) { charFile = card; } public void Randomize() { charFile = new ChaFileControl(); } }
    internal class WorldData { public Player player = new Player(new ChaFileControl(), false); public int cycle; public bool isOpening; }
}
namespace ActionGame { internal class Cycle { public enum Type { Daytime } } }
namespace Manager { internal static class Game { public static SaveData.WorldData saveData; } }
internal class ActionScene
{
    public readonly NativePlayer Player = new();
    internal class NativePlayer { public readonly GameObject gameObject = new(); public bool Active; public void SetActive(bool value) { Active = value; } }
}
namespace MainGameAiShoujoMode
{
    internal static class HotelRoomAppearanceRuntime { public static ChaFileControl CloneCard(ChaFileControl card) => new(); }
    internal sealed class NativeActionWorldScope : IDisposable
    {
        private readonly SaveData.WorldData _previous; public NativeActionWorldScope(SaveData.WorldData world) { _previous = Manager.Game.saveData; Manager.Game.saveData = world; }
        public void Dispose() { Manager.Game.saveData = _previous; }
    }
    internal sealed class NativeActionSceneScope : IDisposable
    {
        public static NativeActionSceneScope Last; private readonly Action<ActionScene> _ready;
        public bool Released { get; private set; } public event Action ReleaseCompleted;
        public NativeActionSceneScope(Action<ActionScene> ready, Action<Exception> failed) { Last = this; _ready = ready; }
        public void Begin() { } public void Complete(ActionScene scene) { _ready(scene); }
        public void Dispose() { } public void CompleteRelease() { Released = true; ReleaseCompleted?.Invoke(); }
    }
    internal sealed class NativeActionVrSceneScope : IDisposable
    {
        public static NativeActionVrSceneScope Last; public static bool FailNext; public bool Enabled;
        public NativeActionVrSceneScope(ActionScene scene) { if (FailNext) { FailNext = false; throw new InvalidOperationException("VR startup failed"); } Last = this; }
        public void Resume() { Enabled = true; } public void Suspend() { Enabled = false; } public void AlignToPlayer() { }
        public void SetWalkingMotion(bool moving) { }
        public void Dispose() { Enabled = false; }
    }
    internal sealed class NativeActionMapScope : IDisposable
    {
        public static NativeActionMapScope Last; public GameObject Root; public Vector3 Feet; public bool Attached, Disposed;
        public NativeActionMapScope(ActionScene scene) { Last = this; }
        public IEnumerator Attach(GameObject root, Parameters settings, Vector3 feet, Quaternion rotation, IEnumerable<Transform> exclusions = null)
        { Root = root; Feet = feet; yield return null; if (!Disposed) Attached = true; }
        public void Dispose() { Disposed = true; Attached = false; }
    }
    internal sealed class NativeActionWalkingDriver : IDisposable
    {
        public Vector3 Arrival => default; public Quaternion Heading => Quaternion.identity;
        public NativeActionWalkingDriver(NativeActionWalkingSession owner, Camera view, Transform origin, Parameters settings) { }
        public void MapReady() { } public void Dispose() { }
    }
    internal sealed class NativeActionUiScope : IDisposable
    {
        public NativeActionUiScope(ActionScene action) { }
        public void Dispose() { }
    }
}
