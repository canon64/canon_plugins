using System;
using System.Linq;
using System.Reflection;
using CanonGate;
using MainGameVoiceFaceEventBridge.Scheduling;

namespace UnityEngine
{
    public class MonoBehaviour { }
    public static class Mathf
    {
        public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max);
    }
}
namespace MainGameHandySync
{
    internal sealed class Plugin
    {
        internal static Plugin Instance => null;
        internal bool ShouldLogDetailed() => false;
        internal bool ShouldLogError() => false;
    }
    internal static class SimpleFileLogger
    {
        internal static void Info(string s) { }
        internal static void Warn(string s) { }
        internal static void Debug(string s) { }
    }
}
public sealed class HSceneProc : UnityEngine.MonoBehaviour { }
public sealed class ChaControl : UnityEngine.MonoBehaviour { public byte sex=1; }
public class HFlag { }
public class HVoiceCtrl { }
namespace HarmonyLib { public static class AccessTools { public static System.Reflection.FieldInfo Field(Type type,string name)=>type.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic); } }

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string message)
    { ++_checks; if (!value) throw new Exception(message); }
    private static object Get(Type t, string name) => t.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static void Call(Type t, string name, params object[] args) => t.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static void Main()
    {
        GateApi.SetRunning(true);
        var adapters = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name == "SessionGate").ToArray();
        Check(adapters.Length == 17, "all 17 production adapters are compiled");
        foreach (var adapter in adapters)
        {
            string guid = adapter.Namespace;
            int starts = 0, stops = 0, capturedEpoch = 0;
            bool enabled = true;
            Action start = () => { starts++; capturedEpoch = (int)Get(adapter, "Epoch"); Check((bool)Get(adapter, "Allowed"), guid + " allowed before start"); };
            Action stop = () => { stops++; Check(!(bool)Get(adapter, "Allowed") && !(bool)Get(adapter, "Accepting"), guid + " revoked before cleanup"); Check(capturedEpoch != (int)Get(adapter, "Epoch"), guid + " pending epoch revoked before cleanup"); };
            Call(adapter, "Open", guid, start, stop, (Func<bool>)(() => enabled));
            GateApi.BeginSession(GateApi.SessionKind.H, false, new ExceptionPolicy(), new HSceneProc());
            Check(starts == 0 && !(bool)Get(adapter, "Accepting"), guid + " denied H has no work");
            GateApi.EndSession(GateApi.SessionKind.H);
            GateApi.BeginSession(GateApi.SessionKind.H, true, new ExceptionPolicy(), new HSceneProc());
            var scene = GateApi.Current.HScene;
            Check(starts == 1 && ReferenceEquals(Get(adapter, "HScene"), scene), guid + " exact accepted scene");
            int epoch = capturedEpoch;
            for (int i = 0; i < 500; i++) Check((bool)Get(adapter, "Allowed") && (int)Get(adapter, "Epoch") == epoch, guid + " fixed cached permission");
            GateApi.EndSession(GateApi.SessionKind.H);
            Call(adapter, "Stop");
            Check(stops == 1 && Get(adapter, "HScene") == null, guid + " idempotent complete stop");
            GateApi.BeginSession(GateApi.SessionKind.H, true, new ExceptionPolicy(), new HSceneProc());
            Check(starts == 2 && capturedEpoch != epoch, guid + " old async result cannot enter next H");
            Call(adapter, "Stop");
            GateApi.EndSession(GateApi.SessionKind.H);
            Check(stops == 2, guid + " disable and gate end do not double-stop");
            enabled = false;
            GateApi.BeginSession(GateApi.SessionKind.H, true, new ExceptionPolicy(), new HSceneProc());
            Check(starts == 2, guid + " disabled owner cannot restart");
            GateApi.EndSession(GateApi.SessionKind.H);
            enabled = true;
            GateApi.BeginSession(GateApi.SessionKind.Maker, false, new ExceptionPolicy(), new UnityEngine.MonoBehaviour());
            Check(starts == 2, guid + " H-only adapter never creates H effects in Maker");
            GateApi.EndSession(GateApi.SessionKind.Maker);
            Call(adapter, "Close");
        }
        var scheduler = new StableActionScheduler();
        CheckAiModeAdapters(adapters);
        int ran = 0;
        scheduler.Schedule(0, () => { ran++; scheduler.Clear(); });
        scheduler.Schedule(0, () => ran += 100);
        scheduler.DrainDue(0, ex => throw ex);
        Check(ran == 1, "stop invalidates actions already copied into drain batch");
        scheduler.Schedule(0, () => ran++);
        scheduler.DrainDue(0, ex => throw ex);
        Check(ran == 2, "next session schedules new actions normally");
        CheckHandyQueueStop();
        CheckBoostOwnershipCancellation();
        GateApi.SetRunning(false);
        Console.WriteLine("GATE_ADAPTER_TESTS_OK adapters=" + adapters.Length + " checks=" + _checks);
    }

    private static void CheckAiModeAdapters(Type[] adapters)
    {
        GateApi.AllowActivity = (guid, kind) => true;
        foreach (var adapter in adapters.Where(type => type.Namespace == "MainGameVoiceFaceEventBridge" || type.Namespace == "MainGameSubtitleCore"))
        {
            string guid = adapter.Namespace;
            Call(adapter, "Open", guid, (Action)(() => { }), (Action)(() => { }), (Func<bool>)(() => true));
            using (var mode = GateApi.EnterAiShoujoMode(new UnityEngine.MonoBehaviour()))
            {
                var actor = new ChaControl(); mode.SetActor(actor);
                Check((bool)Get(adapter, "Allowed") && (bool)Get(adapter, "Accepting"), guid + " accepts walking actor/subtitles");
                int epoch = (int)Get(adapter, "Epoch");
                mode.SetActor(new ChaControl());
                Check((int)Get(adapter, "Epoch") != epoch, guid + " actor change revokes queued work");
                mode.Suspend();
                Check(!(bool)Get(adapter, "Accepting"), guid + " H handoff stops worker admission");
                var policy = new ExceptionPolicy(); policy.Load("", "", guid);
                GateApi.BeginSession(GateApi.SessionKind.H, false, policy, new HSceneProc(), true);
                Check((bool)Get(adapter, "Allowed") && Get(adapter, "HScene") != null, guid + " accepts AI H exact scene");
                GateApi.EndSession(GateApi.SessionKind.H); mode.Resume();
                Check((bool)Get(adapter, "Allowed") && Get(adapter, "HScene") == null, guid + " returns to walking context");
            }
            Check(!(bool)Get(adapter, "Accepting"), guid + " mode exit stops admission");
            Call(adapter, "Close");
        }
        GateApi.AllowActivity = null;
    }

    private static void CheckHandyQueueStop()
    {
        // No remote request is made: no keys are configured and the pump is held locally.
        using var client = new MainGameHandySync.HandyClient();
        var type = client.GetType();
        object Field(string key) => type.GetField(key, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
        void Set(string key, object value) => type.GetField(key, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(client, value);
        Set("_pumpRunning", true);
        client.HampStart();
        Check(!(bool)Field("_qStart"), "inactive device cannot queue start");
        client.BeginSession();
        int firstEpoch = (int)Field("_sessionEpoch");
        client.EnsureHampMode(); client.HampStart(); client.HampVelocity(.8f); client.SetStrokeRange(.1f, .9f);
        Set("_pendingXp", .9f);
        Check((bool)Field("_qStart") && (bool)Field("_qMode"), "active device queues work");
        client.EndSession();
        Check(!(bool)Field("_sessionActive") && (int)Field("_sessionEpoch") != firstEpoch, "device async epoch invalidated");
        Check(!(bool)Field("_qStart") && !(bool)Field("_qMode") && (bool)Field("_qStop"), "stop wins over queued mode/start");
        Check(float.IsNaN((float)Field("_qVelocity")) && float.IsNaN((float)Field("_qStrokeMin")) && (float)Field("_pendingXp") < 0, "velocity/stroke/HDSP queue discarded");
        client.HampStart(); client.HampVelocity(1); client.SetStrokeRange(0, 1);
        Check(!(bool)Field("_qStart") && float.IsNaN((float)Field("_qVelocity")), "late callers cannot revive stopped device");
    }

    private static void CheckBoostOwnershipCancellation()
    {
        var ownership = new MainGameVoiceFaceEventBridge.Audio.TransientBoostOwnership();
        int restored = 0, resumed = 0;
        Action restore = () => restored++;
        Action resume = () => resumed++;
        ownership.Replace("hip");
        Check(!ownership.Cancel("other", true, false, restore, resume) && restored == 0, "other owner cannot touch live envelope");
        Check(ownership.Cancel("hip", true, false, restore, resume) && restored == 1 && resumed == 1, "boost pause cancellation restores and resumes");
        Check(!ownership.Cancel("hip", true, false, restore, resume) && restored == 1, "repeated owner stop is idempotent");
        ownership.Replace("hip"); ownership.Replace("belly");
        Check(!ownership.Cancel("hip", true, false, restore, resume), "older owner cannot cancel later owner");
        Check(ownership.Cancel("belly", true, true, restore, resume) && restored == 2 && resumed == 1, "cue-owned dialogue pause is never resumed by boost cancellation");
        ownership.Replace("hip"); ownership.Clear();
        Check(!ownership.Cancel("hip", false, false, restore, resume), "accepted legacy request clears previous ownership");
    }
}
