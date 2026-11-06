using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MainGameAiShoujoMode;

internal static class Program
{
    private static readonly List<string> Checks = new List<string>();
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Checks.Add(name); }
    private static void Main()
    {
        var action = new ActionScene { Player = new Player { initialized = true } };
        var interpreter = new FakeInterpreter(); FakeVr.Interpreter = interpreter;
        var scope = new NativeActionVrSceneScope(action, typeof(FakeVr), typeof(FakeInterpreter), typeof(FakeSceneInterpreter),
            () => new NativeActionControlScope(typeof(FakeVr)));
        interpreter.Tick();
        Check(interpreter.SceneName == "OtherScene", "constructing the binding does not prematurely enable walking");
        int reads = interpreter.Reads;
        scope.Resume();
        Check(interpreter.SceneName == "ActionScene" && interpreter.WalkingAvailable, "real Harmony patch enters native walking scene decision");
        Check(FakeVr.Mode.Right.Active is KKS_VR.Controls.GameplayTool && FakeVr.Mode.Right.ToolEnabled, "right controller uses installed walking tool");
        scope.SetWalkingMotion(true); interpreter.SceneInterpreter.Aligned = false; interpreter.SceneInterpreter.OnUpdate();
        Check(interpreter.SceneInterpreter.Aligned && interpreter.SceneInterpreter.OnlyPosition, "native player movement activates existing VR camera follow");
        scope.SetWalkingMotion(false); interpreter.SceneInterpreter.Aligned = false; interpreter.SceneInterpreter.OnUpdate();
        Check(!interpreter.SceneInterpreter.Aligned, "stationary player does not repeatedly recenter room-scale view");
        interpreter.SceneInterpreter.SetNativeWalking(true); interpreter.SceneInterpreter.OnUpdate();
        Check(interpreter.SceneInterpreter.Aligned, "original VRMOD walking input still follows normally");
        interpreter.SceneInterpreter.SetNativeWalking(false);
        scope.AlignToPlayer();
        Check(interpreter.SceneInterpreter.Aligned && !interpreter.SceneInterpreter.OnlyPosition && interpreter.SceneInterpreter.Quiet, "one-shot alignment uses the native full player pose");
        interpreter.Tick();
        Check(interpreter.Reads == reads, "owned decision bypasses original scene searches");
        int entries = interpreter.Entries;
        scope.Resume();
        Check(interpreter.Entries == entries, "duplicate resume does not restart input and camera lifetime");
        bool rejected = false;
        try { new NativeActionVrSceneScope(action, typeof(FakeVr), typeof(FakeInterpreter)); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "overlapping VR ownership rejected");
        interpreter.InH = true;
        scope.Suspend();
        Check(interpreter.SceneName == "HScene" && !interpreter.WalkingAvailable, "suspension restores native H decision and releases walking");
        Check(FakeVr.Mode.Right.Active is FakeWarpTool && FakeVr.Mode.Right.ToolEnabled, "suspension restores previous right controller tool");
        interpreter.SceneInterpreter.Aligned = false;
        scope.AlignToPlayer();
        Check(!interpreter.SceneInterpreter.Aligned, "suspended walking cannot move the H camera");
        reads = interpreter.Reads;
        scope.Suspend();
        Check(interpreter.Reads == reads, "duplicate suspension does not call native transition twice");
        interpreter.InH = false; scope.Resume();
        Check(interpreter.SceneName == "ActionScene", "return from H resumes native walking");
        scope.Dispose(); scope.Dispose();
        Check(interpreter.SceneName == "OtherScene" && !interpreter.WalkingAvailable, "exit restores native scene and releases camera/input");
        interpreter.SceneInterpreter.Aligned = false; interpreter.SceneInterpreter.OnUpdate();
        Check(!interpreter.SceneInterpreter.Aligned, "camera follow patch is removed on exit");
        using (var controls = new NativeActionControlScope(typeof(FakeVr)))
            FakeVr.Mode.Right.ToolIndex = FakeVr.Mode.Right.Tools.FindIndex(tool => tool is FakeMenuTool);
        Check(FakeVr.Mode.Right.Active is FakeMenuTool, "cleanup preserves a tool explicitly selected by the user");
        reads = interpreter.Reads; interpreter.Tick();
        Check(interpreter.Reads > reads && interpreter.SceneName == "OtherScene", "original detector runs after unpatch");
        rejected = false;
        try { scope.Resume(); } catch (ObjectDisposedException) { rejected = true; }
        Check(rejected, "disposed owner cannot reactivate walking");
        FakeVr.Active = false;
        rejected = false;
        try { new NativeActionVrSceneScope(action, typeof(FakeVr), typeof(FakeInterpreter)); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "inactive VRMOD fails before scene patch");
        FakeVr.Active = true;
        rejected = false;
        try { new NativeActionVrSceneScope(new ActionScene(), typeof(FakeVr), typeof(FakeInterpreter)); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "uninitialized native player cannot be advertised as ready");
        using (var next = new NativeActionVrSceneScope(action, typeof(FakeVr), typeof(FakeInterpreter)))
        { next.Resume(); Check(interpreter.SceneName == "ActionScene", "next session can own native VR normally"); }
        Console.WriteLine("PASS " + Checks.Count + " native Action VR checks using installed Harmony");
    }
}
internal class ActionScene { public Player Player; }
internal class Player { public bool initialized; }
internal static class FakeVr
{
    public static bool Active { get; set; } = true;
    public static object Interpreter { get; set; }
    public static FakeMode Mode { get; } = new FakeMode();
}
internal sealed class FakeInterpreter
{
    private enum SceneType { OtherScene, ActionScene, HScene }
    private SceneType _current;
    public string SceneName => _current.ToString();
    public bool WalkingAvailable, InH;
    public int Entries, Reads;
    public readonly FakeSceneInterpreter SceneInterpreter = new FakeSceneInterpreter();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private SceneType DetectScene() { Reads++; return InH ? SceneType.HScene : SceneType.OtherScene; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateScene()
    {
        var next = DetectScene();
        if (next == _current) return;
        WalkingAvailable = next == SceneType.ActionScene;
        if (WalkingAvailable) Entries++;
        _current = next;
    }
    public void Tick() => UpdateScene();
}
internal sealed class FakeSceneInterpreter
{
    private bool _Walking;
    public bool Aligned, OnlyPosition, Quiet;
    public void MoveCameraToPlayer(bool onlyPosition, bool quiet) { Aligned = true; OnlyPosition = onlyPosition; Quiet = quiet; }
    public void SetNativeWalking(bool value) { _Walking = value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OnUpdate() { if (_Walking) MoveCameraToPlayer(true, true); }
}
internal sealed class FakeMode { public FakeController Right { get; } = new FakeController(); }
internal sealed class FakeController
{
    public readonly List<FakeTool> Tools = new List<FakeTool> { new FakeWarpTool(), new KKS_VR.Controls.GameplayTool(), new FakeMenuTool() };
    public int ToolIndex { get; set; }
    public FakeTool Active => Tools[ToolIndex];
    public bool ToolEnabled { get => Active.Enabled; set => Active.Enabled = value; }
}
internal class FakeTool { public bool Enabled = true; }
internal sealed class FakeWarpTool : FakeTool { }
internal sealed class FakeMenuTool : FakeTool { }
namespace KKS_VR.Controls { internal sealed class GameplayTool : FakeTool { } }
