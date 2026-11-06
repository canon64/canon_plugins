using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MainGameAiShoujoMode;
using MainGameUiInputCapture;
using UnityEngine;

internal static class Program
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    private static void Main(string[] args)
    {
        Console.WriteLine("AI_MODE_PLAYBACK_BAR_TEST_START");
        var settings = MapEnvironmentRuntime.Parameters.Load(args[default(int)]);
        Screen.width = settings.N("render.width"); Screen.height = settings.N("render.height");
        var player = new MapEnvironmentRuntime.SandboxFreeCamera();
        var bar = new AiModePlaybackBar();
        var errors = new List<Exception>(); bool canDraw = true;
        bar.Initialize(() => settings, player, () => canDraw, errors.Add);
        Check(!typeof(AiModePlaybackBar).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Any(m => new[] {"Update", "LateUpdate", "FixedUpdate"}.Contains(m.Name)), "no frame monitoring callbacks");
        Draw(bar, EventType.Layout); Check(!UiInputCaptureApi.Blocked && !player.Released, "hidden bar leaves walking cursor alone");
        Draw(bar, EventType.KeyDown); Draw(bar, EventType.Layout);
        Check(UiInputCaptureApi.Blocked && UiInputCaptureApi.Idle && player.Released, "opening registers host rectangle and releases cursor");
        Click(bar, "設定 ▼"); Draw(bar, EventType.Layout);
        Check(!GUILayout.Labels.Contains("部屋") && !GUILayout.Labels.Contains("マップ"), "no room or map tab");
        Check(GUILayout.Labels.Contains("Composer") && GUILayout.Labels.Contains("VMD"), "existing registered tab labels are reused");
        Click(bar, "Composer"); Draw(bar, EventType.Layout);
        Check(MainGameUnifiedUi.UnifiedUiApi.Drawn == "composer", "selected tab invokes the existing embedded drawer");
        Click(bar, "▶ 再生"); Check(MainGameVMDPlay.Plugin.Command == "play", "transport invokes VMD playback API");
        Click(bar, "ループ OFF"); Check(MainGameVMDPlay.Plugin.Command == "loop", "loop invokes existing VMD command");
        Draw(bar, EventType.MouseDown);
        Check(UiInputCaptureApi.Drag, "pointer press explicitly captures drag");
        Event.current.mousePosition = new Vector2(float.MaxValue, float.MaxValue);
        Draw(bar, EventType.MouseDrag, false);
        Check(UiInputCaptureApi.Drag, "drag remains captured outside bar");
        Draw(bar, EventType.MouseUp, false);
        Check(!UiInputCaptureApi.Drag && UiInputCaptureApi.Blocked, "mouse release ends drag and retains window block");
        Call(bar, "OnApplicationFocus", false);
        Check(!UiInputCaptureApi.Blocked && !UiInputCaptureApi.Idle && !player.Released, "focus loss restores cursor and input");
        Draw(bar, EventType.Layout);
        bar.Suspend(); Check(!UiInputCaptureApi.Blocked && !player.Released, "H handoff suspends and releases input");
        bar.Resume(); Draw(bar, EventType.Layout);
        Check(UiInputCaptureApi.Blocked && player.Released, "H ready or mode return restores visible bar");
        canDraw = false; Draw(bar, EventType.Layout);
        Check(!UiInputCaptureApi.Blocked && !player.Released, "conversation or settings suppresses bar input");
        canDraw = true; Draw(bar, EventType.Layout);
        Draw(bar, EventType.KeyDown); Draw(bar, EventType.Layout);
        Check(!UiInputCaptureApi.Blocked && !UiInputCaptureApi.Drag && !UiInputCaptureApi.Idle && !player.Released, "close restores walking input");
        Call(bar, "OnDestroy"); Call(bar, "OnDestroy");
        Check(errors.Count == default(int), "all UI operations complete without exceptions");
        AiModeNativeUiCapture.Apply("conversation", default(Rect));
        Check(UiInputCaptureApi.Blocked && UiInputCaptureApi.Idle && !UiInputCaptureApi.UguiBlocked, "native conversation keeps camera capture while removing the click-absorbing overlay");
        AiModeNativeUiCapture.Apply("conversation", default(Rect));
        Check(UiInputCaptureApi.Blocked && !UiInputCaptureApi.UguiBlocked, "native UI focus restoration does not reintroduce the click blocker");
        UiInputCaptureApi.ClearInputBlockRects("conversation"); UiInputCaptureApi.SetIdleCursorUnlock("conversation", false);
        Check(!UiInputCaptureApi.Blocked && !UiInputCaptureApi.Idle && !UiInputCaptureApi.UguiBlocked, "native conversation cleanup releases camera and cursor capture");
        UiInputCaptureApi.SetInputBlockRects("settings", new[] { default(Rect) });
        Check(UiInputCaptureApi.Blocked && UiInputCaptureApi.UguiBlocked, "IMGUI settings retain their click-absorbing overlay");
        UiInputCaptureApi.ClearInputBlockRects("settings");
        MapEnvironmentRuntime.SandboxStandClearance.Capsule(settings.N("movement.crouchHeight"), settings.N("movement.bodyHeight"),
            settings.N("movement.bodyRadius"), settings.N("movement.skin"), settings.N("render.epsilon"), out var fromHeight, out var toHeight, out var queryRadius);
        Check(fromHeight - queryRadius > settings.N("movement.bodyRadius"), "standing clearance excludes floor and lower-body geometry");
        Check(toHeight + queryRadius > settings.N("movement.crouchHeight") && toHeight + queryRadius <= settings.N("movement.bodyHeight"), "standing clearance still includes the overhead space up to standing height");
        Check(fromHeight < toHeight && queryRadius > settings.N("math.zero"), "standing clearance has a valid upward capsule");
        Console.WriteLine("AI_MODE_PLAYBACK_BAR_TEST_PASS (stubbed UI; live operation untested)");
    }
    private static void Click(AiModePlaybackBar bar, string label) { GUILayout.Click = label; Draw(bar, EventType.MouseUp); GUILayout.Click = null; }
    private static void Draw(AiModePlaybackBar bar, EventType kind, bool inside = true)
    {
        Event.current.type = Event.current.rawType = kind; Event.current.keyCode = KeyCode.Colon;
        if (inside)
        {
            var rect = (Rect)typeof(AiModePlaybackBar).GetField("_rect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bar);
            Event.current.mousePosition = new Vector2(rect.x + rect.width / Screen.width, rect.y + rect.height / Screen.height);
        }
        GUILayout.Labels.Clear(); Call(bar, "OnGUI");
    }
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
namespace MapEnvironmentRuntime
{
    public sealed class Parameters
    {
        private Dictionary<string, float> numbers;
        private Dictionary<string, string> texts;
        public float N(string key) => numbers[key]; public int I(string key) => (int)N(key); public string T(string key) => texts[key]; public bool B(string key) => bool.Parse(T(key));
        public static Parameters Load(string file)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return new Parameters {
                numbers = doc.RootElement.GetProperty("numbers").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString(), x => x.GetProperty("value").GetSingle()),
                texts = doc.RootElement.GetProperty("text").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString(), x => x.GetProperty("value").GetString())
            };
        }
    }
    public sealed class SandboxFreeCamera
    {
        public bool Released;
        public sealed class State { public bool cursorReleaseRequested; }
        public State Snapshot() => new State { cursorReleaseRequested = Released };
        public void SetCursorReleased(bool release) => Released = release;
    }
}
namespace MainGameAiShoujoMode { public static class Plugin { public const string Guid = "ai.test"; public static bool ModeAllowed = true; } }
namespace MainGameVMDPlay
{
    public sealed class PlaybackBarSnapshot
    {
        public bool Ready = true, Owns = true, Loop;
        public string Title = "Motion", Status = "Ready";
        public float Position, Duration, Volume;
        public string[] Groups = { "All" }, Entries = { "Motion" };
        public int GroupIndex, EntryIndex;
    }
    public static class Plugin
    {
        public static string Command;
        public static PlaybackBarSnapshot ReadPlaybackBarSnapshot() => new PlaybackBarSnapshot();
        public static void PlaybackBarCommand(string command, int index, float value) => Command = command;
    }
}
namespace MainGameUnifiedUi
{
    public static class UnifiedUiApi
    {
        public static string Drawn;
        public static string[] GetEmbeddedTabs() => new[] { "vmd", "VMD", "composer", "Composer" };
        public static void DrawEmbeddedTab(string tab) => Drawn = tab;
    }
}
namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static bool Blocked, Drag, Idle;
        public static bool UguiBlocked;
        public static void SetInputBlockRects(string owner, Rect[] rects) { Blocked = true; UguiBlocked = true; }
        public static void ClearBlockRects(string owner) => UguiBlocked = false;
        public static void ClearInputBlockRects(string owner) { Blocked = false; UguiBlocked = false; }
        public static void Sync(string owner, string source, bool value) => Drag = value;
        public static bool SetIdleCursorUnlock(string owner, bool value) { Idle = value; return true; }
    }
}
namespace UnityEngine
{
    public class MonoBehaviour { public bool enabled = true; }
    public enum EventType { Layout, Repaint, KeyDown, MouseDown, MouseDrag, MouseUp, Used }
    public enum KeyCode { Colon }
    public sealed class Event
    {
        public static Event current = new Event(); public EventType type, rawType; public KeyCode keyCode; public Vector2 mousePosition;
        public void Use() => type = EventType.Used;
    }
    public sealed class ExitGUIException : Exception { }
    public static class Application { public static bool isFocused = true; }
    public static class Screen { public static float width, height; }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float a, float b, float w, float h) { x = a; y = b; width = w; height = h; }
        public bool Contains(Vector2 point) => point.x >= x && point.y >= y && point.x <= x + width && point.y <= y + height;
        public static bool operator ==(Rect a, Rect b) => a.Equals(b); public static bool operator !=(Rect a, Rect b) => !a.Equals(b);
        public override bool Equals(object other) => other is Rect r && x == r.x && y == r.y && width == r.width && height == r.height;
        public override int GetHashCode() => HashCode.Combine(x, y, width, height);
    }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); public static float Min(float a, float b) => Math.Min(a, b); }
    public static class GUIUtility { public static int hotControl; }
    public sealed class GUISkin { public object box = new object(), button = new object(); }
    public static class GUI { public static bool enabled = true; public static GUISkin skin = new GUISkin(); }
    public sealed class GUILayoutOption { }
    public static class GUILayout
    {
        public static string Click; public static readonly List<string> Labels = new List<string>();
        public static void BeginArea(Rect rect, object style) { } public static void EndArea() { }
        public static void BeginHorizontal() { } public static void EndHorizontal() { }
        public static void Label(string label, params GUILayoutOption[] options) => Labels.Add(label);
        public static bool Button(string label, params GUILayoutOption[] options) { Labels.Add(label); return GUI.enabled && Click == label; }
        public static bool Toggle(bool value, string label, object style) { Labels.Add(label); return Click == label ? !value : value; }
        public static float HorizontalSlider(float value, float min, float max, params GUILayoutOption[] options) => value;
        public static Vector2 BeginScrollView(Vector2 value, params GUILayoutOption[] options) => value;
        public static void EndScrollView() { }
        public static GUILayoutOption Width(float value) => new GUILayoutOption(); public static GUILayoutOption Height(float value) => new GUILayoutOption();
    }
}
