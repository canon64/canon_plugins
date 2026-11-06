using System;
using CanonGate;
using CanonPlugins.Shared;

namespace UnityEngine
{
    public class MonoBehaviour { public bool enabled = true; public bool isActiveAndEnabled => enabled; public int GetInstanceID() => GetHashCode(); }
}
namespace BepInEx { public class BaseUnityPlugin : UnityEngine.MonoBehaviour { } }
public class HSceneProc : UnityEngine.MonoBehaviour { }
public class HFlag { }
public class HVoiceCtrl { }
public class HandCtrl { }
public class VrFlag : HFlag { }
public class VrScene : UnityEngine.MonoBehaviour
{
    public VrFlag flags = new VrFlag();
    public System.Collections.Generic.List<ChaControl> lstFemale = new System.Collections.Generic.List<ChaControl>();
}

internal static class Program
{
    private static int count;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        count++; Console.WriteLine("PASS " + message);
    }
    private static void Main()
    {
        Console.WriteLine("Testing real gate registration and session callbacks (no Time or room-reader stubs)");
        var owner = new BepInEx.BaseUnityPlugin();
        int starts = 0, stops = 0;
        PluginSessionGate.Bind(owner, "test.plugin", () => starts++, () => { Check(!PluginSessionGate.Allowed, "permission revoked before cleanup"); stops++; });
        Check(!owner.enabled && !PluginSessionGate.Allowed, "plugin frame callbacks disabled before H entry");
        GateApi.SetRunning(true);
        var policy = new ExceptionPolicy(); policy.Load("", "");
        GateApi.BeginSession(GateApi.SessionKind.H, false, policy, new HSceneProc());
        Check(starts == 0 && !owner.enabled, "ordinary room never starts plugin");
        GateApi.EndSession(GateApi.SessionKind.H);
        var scene = new HSceneProc();
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, scene);
        Check(starts == 1 && owner.enabled && PluginSessionGate.Allowed && ReferenceEquals(scene, PluginSessionGate.HScene), "AllPoseRoom starts plugin once with cached H scene");
        for (int i = 0; i < 10000; i++) { bool cached = PluginSessionGate.Allowed; }
        Check(starts == 1 && stops == 0, "reading fixed permission does not reevaluate or call handlers");
        GateApi.EndSession(GateApi.SessionKind.H);
        Check(stops == 1 && !owner.enabled && PluginSessionGate.HScene == null, "H exit cleans once and disables frame callbacks");
        GateApi.EndSession(GateApi.SessionKind.H);
        Check(stops == 1, "duplicate H exit does not repeat cleanup");
        policy.Load("", "test.plugin");
        GateApi.BeginSession(GateApi.SessionKind.H, false, policy, scene);
        Check(starts == 2 && owner.enabled && PluginSessionGate.Allowed, "gate-specific exception can allow ordinary room");
        GateApi.SetRunning(false);
        Check(stops == 2 && !owner.enabled && !PluginSessionGate.Allowed, "gate shutdown revokes and stops plugin");
        PluginSessionGate.Dispose();
        Check(stops == 2, "destroy after stopped session does not repeat cleanup");
        var vr = new VrScene();
        var view = new GateScene(vr);
        Check(ReferenceEquals(view.Source, vr) && ReferenceEquals(view.flags, vr.flags), "VR scene view reads the supplied VR instance and derived flags");
        Check(ReferenceEquals(view.lstFemale, vr.lstFemale) && view.Read<object>("not-present") == null, "VR participants use the captured scene fields without discovery");
        PluginSessionGate.Bind(owner, "test.vr", null, null);
        GateApi.SetRunning(true);
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, vr);
        Check(PluginSessionGate.HScene == null && ReferenceEquals(PluginSessionGate.Scene, vr) && ReferenceEquals(PluginSessionGate.ReadScene<HFlag>("flags"), vr.flags), "VR registration retains scene even though it is not HSceneProc");
        var oldPermit = PluginSessionGate.Capture();
        GateApi.EndSession(GateApi.SessionKind.H);
        GateApi.BeginSession(GateApi.SessionKind.H, true, policy, new VrScene());
        Check(!oldPermit.IsValid && PluginSessionGate.Allowed, "asynchronous permit from previous H cannot become valid in next H");
        PluginSessionGate.Dispose();
        PregnancyRestoreTests.Run(Check);
        GateApi.EndSession(GateApi.SessionKind.H);
        GateApi.AllowActivity = (guid, kind) => true;
        PluginSessionGate.Bind(owner, "test.ai", null, null);
        using (var mode = GateApi.EnterAiShoujoMode(new UnityEngine.MonoBehaviour()))
        {
            var girl = new ChaControl(); mode.SetActor(girl);
            Check(PluginSessionGate.Allowed && ReferenceEquals(PluginSessionGate.AiActor, girl), "shared adapter binds published AI actor");
            var aiView = new GateScene(mode.Owner, girl);
            Check(aiView.lstFemale.Contains(girl) && aiView.flags == null, "AI scene view supplies girl without fabricating H flags");
            mode.Suspend();
            Check(!PluginSessionGate.Allowed && PluginSessionGate.AiActor == null, "AI suspend revokes and clears shared actor");
            mode.Resume();
            Check(PluginSessionGate.Allowed, "AI resume starts new shared lifetime");
        }
        Check(!PluginSessionGate.Allowed, "AI owner destruction disables shared client");
        PluginSessionGate.Dispose(); GateApi.AllowActivity = null;
        PregnancyAsyncTests.Run(Check);
        Console.WriteLine("Passed " + count + " checks");
    }
}
