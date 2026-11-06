using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameAiShoujoMode.Authoring;
using UnityEngine;

namespace CanonGate
{
    public static class GateApi
    {
        private static readonly List<Tuple<Action, Action>> clients = new List<Tuple<Action, Action>>();
        private static bool active;
        internal static bool Denied;
        private sealed class Lease : IDisposable { internal Action Release; public void Dispose() { Release?.Invoke(); Release = null; } }
        public struct Permit { public bool IsValid => active; }
        public static Permit Capture(string guid) => new Permit();
        public static IDisposable Register(string guid, Action start, Action stop) { var item = Tuple.Create(start, stop); clients.Add(item); return new Lease { Release = () => clients.Remove(item) }; }
        public sealed class HotelModeScope : IDisposable { public void Dispose() { End(); } }
        public static HotelModeScope EnterHotelMode(MonoBehaviour owner, string room) => new HotelModeScope();
        internal static void Begin() { if (Denied) return; active = true; foreach (var client in clients.ToArray()) client.Item1(); }
        internal static void End() { if (!active) return; active = false; foreach (var client in clients.ToArray()) client.Item2(); }
    }
}
namespace MapEnvironmentRuntime
{
    public class Parameters { public int I(string name) => name == "math.one" ? Numbers.One : default(int); }
}
namespace HarmonyLib { public static class AccessTools { public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); } }
namespace SaveData
{
    public class Heroine { public ChaFileControl charFile; public GameObject root; public Heroine(ChaFileControl file, bool _) { charFile = file; } public void SetRoot(GameObject root) { this.root = root; } }
    public class Player { public GameObject root; public Player(ChaFileControl file, bool _) { } public void SetRoot(GameObject root) { this.root = root; } }
}
public class HSceneProc
{
    public class AnimationListInfo { }
    public List<AnimationListInfo>[] lstUseAnimInfo = Array.Empty<List<AnimationListInfo>>();
}
namespace MainGameAiShoujoMode
{
    public static class Plugin { public const string Guid = "authoring-test"; }
    public static class HotelRoomAppearanceRuntime { public static ChaFileControl CloneCard(ChaFileControl file) => file; }
    public sealed class HotelRoomSelection { public string mode, name, bundle, asset; public int id; }
    public sealed partial class AiModeHSession : IDisposable
    {
        public sealed class HLocation { public string SourceId; public Vector3 Position; public Quaternion Rotation; public int[] Categories; public bool VerifiedGround; }
        internal sealed class CameraControl
        {
            public Transform transform = new GameObject("Native camera controller").transform;
            public Vector3 CameraDir; public float CameraFov;
            public void SetCamera(Vector3 p, Vector3 a, Quaternion r, Vector3 d) { }
        }
        internal sealed class Flags { public CameraControl ctrlCamera = new CameraControl(); }
        private readonly HSceneProc _proc = new HSceneProc();
        private Action ready, ended;
        internal static AiModeHSession Last;
        internal HLocation Location;
        internal bool AllRoomSelections, RoomReady;
        public bool Busy;
        public Camera ActiveCamera = new GameObject("Native H camera").AddComponent<Camera>();
        internal Flags RoomFlags = new Flags();
        private ChaControl actor;
        internal event Action RoomSelectionChanged;
        public void Initialize(MonoBehaviour owner, MapEnvironmentRuntime.Parameters p, Action started, Action ended, Action<string> log, Action<Exception> error, Action ready) { this.ready = ready; this.ended = ended; Last = this; }
        public void Begin(SaveData.Heroine female, SaveData.Player male, GameObject map, HLocation location)
        { actor = female.root.GetComponentsInChildren<ChaControl>(true).First(); Location = location; Busy = true; CanonGate.GateApi.Begin(); RoomReady = true; ready(); }
        internal ChaControl[] RoomActors() => new[] { actor };
        internal bool RestoreRoomSelection(HotelRoomSelection value) { RoomSelectionChanged?.Invoke(); return true; }
        private static HotelRoomSelection Describe(HSceneProc.AnimationListInfo info) => new HotelRoomSelection();
        public void Cancel() { if (!Busy) return; RoomReady = false; Busy = false; CanonGate.GateApi.End(); ended(); }
        public void Dispose() { Cancel(); }
    }
}
namespace MainGameAiShoujoMode.Authoring
{
    internal sealed class EventAuthoringEditor : MonoBehaviour { }
    internal sealed partial class AuthoringStage
    {
        internal void AdoptNative(Camera native, string femaleId, string maleId, ChaControl[] actors) { SpecialCamera = native; }
    }
}
