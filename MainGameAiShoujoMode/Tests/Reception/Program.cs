using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;

internal static class TestValues
{
    private static readonly System.Text.Json.JsonElement Data = System.Text.Json.JsonDocument.Parse(
        System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json"))).RootElement;
    internal static int I(string key) => Data.GetProperty(key).GetInt32();
}

namespace UnityEngine
{
    public class Transform { public void SetParent(Transform parent, bool world) { } }
    public class GameObject
    {
        public Transform transform = new();
        public GameObject(string name, params Type[] types) { }
        public T GetComponent<T>() where T : new() => new();
    }
    public class Coroutine { public IEnumerator Steps; public bool Stopped; }
    public class WaitForSecondsRealtime { public float Seconds; public WaitForSecondsRealtime(float seconds) { Seconds = seconds; } }
    public struct Vector3 { }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public static class Screen { public static int width = TestValues.I("width"), height = TestValues.I("height"); }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem { public static EventSystem current = new(); public UnityEngine.GameObject gameObject; }
    public class StandaloneInputModule { }
}
namespace MainGameAiShoujoMode.Conversation
{
    internal class KksConversationView : IDisposable
    {
        internal record Choice(string Text, Action Select);
        internal List<Choice> Choices = new();
        internal string Line;
        internal bool Visible;
        internal Action<string, string> EditRequested;
        internal KksConversationView(UnityEngine.Transform parent, string content, Action close) { }
        internal void Show(string name, string text) { Visible = true; Line = text; }
        internal void SetChoices(IEnumerable<Choice> choices) { Choices = choices.ToList(); }
        internal void PresentLine(string name, string text) { Line = text; }
        internal void Hide() { Visible = false; }
        public void Dispose() { Visible = false; }
    }
}
namespace MainGameAiShoujoMode
{
    internal static class Plugin { internal const string Guid = "test"; }
    internal static class AiModeNativeUiCapture { internal static void Apply(string guid, UnityEngine.Rect rect) { } }
    internal class HotelFloorRuntime
    {
        internal class Data { internal string id; internal UnityEngine.Vector3 arrival; internal float yaw; }
        internal Data Definition = new();
    }
    internal sealed partial class HotelModeSession
    {
        private class Parameters { internal float N(string key) => TestValues.I(key); }
        private class Context { internal void SetPanel(bool shown) { } }
        private class Walker { internal void SetCursorReleased(bool shown) { } }
        private class Receptionist { internal string Name => "受付嬢"; internal void FocusPlayer() { } internal void RestorePlacement() { } }
        private class Authoring { internal void CaptureNative(KksConversationView view, string name, string text) { } }
        private class HSession { internal bool Busy; }
        private readonly UnityEngine.Transform transform = new();
        private readonly Parameters _p = new();
        private readonly Context _context = new();
        private readonly Walker _walker = new();
        private readonly Receptionist _receptionist = new();
        private Authoring _authoring;
        private HSession _hSession;
        private bool _receptionMenu, _walkingMenu, _closing, _returnRequested, _loading, _changingFloor;
        private readonly List<HotelFloorRuntime> _floors = new()
        {
            new() { Definition = new() { id = "lobby" } },
            new() { Definition = new() { id = "reception-suite" } }
        };
        private HotelFloorRuntime _floor;
        private UnityEngine.Coroutine pending;
        private Action arrival;
        private int travels, starts;
        private string startedRoom;
        internal HotelModeSession() { _floor = _floors.First(); }
        private UnityEngine.Coroutine StartCoroutine(IEnumerator steps)
        { steps.MoveNext(); return pending = new() { Steps = steps }; }
        private void StopCoroutine(UnityEngine.Coroutine routine) { routine.Stopped = true; }
        private void Destroy(UnityEngine.GameObject obj) { }
        private void WalkingMenuChanged() { }
        private void ReportRoomFailure(Exception ex, string stage) { throw new Exception(stage, ex); }
        private void TravelToFloor(HotelFloorRuntime destination, UnityEngine.Vector3 point, float yaw, Action entered)
        { travels++; _floor = destination; arrival = entered; }
        private void StartRoomSession(string room) { starts++; startedRoom = room; }
        private void Tick() { if (!pending.Stopped) pending.Steps.MoveNext(); }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Main()
        {
            var owner = new HotelModeSession(); owner.OpenReception();
            Require(owner._receptionView.Choices.Select(c => c.Text).SequenceEqual(new[] { "受付嬢とH", "閉じる" }), "menu choices");
            var select = owner._receptionView.Choices.First().Select; select(); select();
            Require(owner._receptionView.Line == "ではこちらへどうぞ" && owner.travels == TestValues.I("math.zero") && owner.starts == TestValues.I("math.zero"), "line must precede travel");
            owner.Tick();
            Require(owner.travels == TestValues.I("math.one") && owner.starts == TestValues.I("math.zero") && !owner._receptionView.Visible, "single transition before H");
            owner.arrival(); Require(owner.starts == TestValues.I("math.one") && owner.startedRoom == "reception-suite", "H starts only on suite arrival");
            owner = new(); owner.OpenReception(); owner.InviteReceptionist(); owner.CloseReception(); owner.Tick();
            Require(owner.travels == TestValues.I("math.zero") && owner.starts == TestValues.I("math.zero"), "close cancels invitation");
            owner.OpenReception(); owner.InviteReceptionist(); owner.DisposeReception(); owner.Tick();
            Require(owner.travels == TestValues.I("math.zero") && owner.starts == TestValues.I("math.zero"), "dispose cancels invitation");
            owner = new(); owner.OpenReception(); owner.InviteReceptionist(); owner._returnRequested = true; owner.Tick();
            Require(owner.travels == TestValues.I("math.zero"), "return rejects stale completion");
            owner = new(); owner.OpenReception(); owner._hSession = new() { Busy = true }; owner.InviteReceptionist();
            Require(owner.pending == null, "busy H rejects invitation");
            Console.WriteLine("RECEPTION_PASS menu=true lineBeforeTravel=true startAfterArrival=true duplicateRejected=true closeCancellation=true disposeCancellation=true returnCancellation=true busyRejected=true");
        }
    }
}
