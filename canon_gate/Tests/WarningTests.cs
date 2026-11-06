using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using CanonGate;
using KKAPI.MainGame;
using KKAPI.Maker;
using MainGameBlankMapAdd;
using MainGameUiInputCapture;
using UnityEngine;
using GatePlugin = CanonGate.Plugin;

internal static class WarningTests
{
    private static int _checks;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); ++_checks; Console.WriteLine("PASS " + name); }
    private static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Invoke(target, args);
    private static WarningWindow Window(GatePlugin gate) => (WarningWindow)typeof(GatePlugin)
        .GetField("_warning", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gate);
    private static PluginInfo Info(string guid, string name, string path) => new PluginInfo
    { Instance = new BaseUnityPlugin(), Metadata = new BepInPlugin(guid, name, "1"), Location = path };

    internal static void Run()
    {
        string root = Path.Combine(Paths.PluginPath, "canon_plugins");
        const string aiModeGuid = "com.kks.maingame.aishoujomode";
        var excluded = UnregisteredPlugins.Build(root, new[] {
            Info(aiModeGuid, "AI少女モード", root + "\\AI\\mode.dll"),
            Info(aiModeGuid + ".other", "Separate plugin", root + "\\other.dll") }, _ => false);
        Check(excluded.Count == 1 && excluded[0].Guid == aiModeGuid + ".other", "AI mode is excluded from warning by exact GUID only");
        Check(CanonPlugins.Shared.GateScopeExclusions.IsIndependentInputOwner(aiModeGuid)
            && CanonPlugins.Shared.GateScopeExclusions.IsIndependentInputOwner(aiModeGuid + ".panel")
            && !CanonPlugins.Shared.GateScopeExclusions.IsIndependentInputOwner(aiModeGuid + "Other")
            && !CanonPlugins.Shared.GateScopeExclusions.IsIndependentInputOwner(null), "independent AI input scope does not grant other plugin owners");
        var infos = new List<PluginInfo>
        {
            Info("z", "Same", root + "\\z.dll"), Info("a", "Same", root + "\\deep\\a.dll"),
            Info("case", "Alpha", root.ToUpperInvariant() + "\\case.dll"),
            Info("normalized", "Beta", root + "\\deep\\..\\b.dll"),
            Info("outside", "Outside", root + "_extra\\outside.dll"),
            Info("parent", "Parent", root + "\\..\\outside.dll"),
            Info("connected", "Connected", root + "\\connected.dll"),
            Info(GateApi.PluginGuid, "Gate", root + "\\gate.dll"),
            Info("fallback", "", root + "\\fallback.dll"),
            Info("z", "Duplicate", root + "\\another.dll"),
            new PluginInfo { Metadata = new BepInPlugin("not.loaded", "No instance", "1"), Location = root + "\\x.dll" },
            Info("relative", "Relative", "canon_plugins\\relative.dll"), null
        };
        var missing = UnregisteredPlugins.Build(root, infos, guid => guid == "connected");
        Check(missing.Count == 5, "audit includes only loaded unregistered plugins under exact normalized root");
        Check(string.Join(",", missing.Select(p => p.Guid)) == "case,normalized,fallback,a,z", "audit sorts names and distinguishes equal names by GUID");
        Check(missing.Single(p => p.Guid == "fallback").Name == "fallback", "missing display name falls back to GUID");
        Chainloader.PluginInfos.Clear();
        var provider = Info("com.kks.maingameblankmapadd", "Room provider", root + "\\room\\room.dll");
        provider.Instance = new MainGameBlankMapAdd.Plugin();
        Chainloader.PluginInfos.Add(provider.Metadata.GUID, provider);
        Chainloader.PluginInfos.Add("unconnected", Info("unconnected", "Unconnected", root + "\\unconnected.dll"));
        Chainloader.PluginInfos.Add("connected", Info("connected", "Connected", root + "\\connected.dll"));
        Chainloader.PluginInfos.Add(GateApi.PluginGuid, Info(GateApi.PluginGuid, "Gate", root + "\\gate.dll"));
        var registration = GateApi.Register("connected", () => { }, () => { });
        var gate = new GatePlugin(); Call(gate, "Awake");
        BlankRoomState.Inside = false;
        GameAPI.Begin();
        Check(Window(gate) == null, "normal H never creates a warning component");
        GameAPI.End(); MakerAPI.Enter();
        Check(Window(gate) == null, "maker never creates a warning component");
        MakerAPI.Exit(); BlankRoomState.Inside = true;
        var firstScene = new HSceneProc();
        Call(gate, "HSceneStarting", firstScene);
        GameAPI.Begin();
        var window = Window(gate);
        var closeTrace = new List<string>();
        window.Trace = closeTrace.Add;
        Check(window.Visible && window.Lines.Length == 2 && UiInputCaptureApi.Blocked && UiInputCaptureApi.CursorUnlocked,
            "AllPoseRoom shows unregistered names and immediately blocks camera input inside warning");
        Check(window.Lines.Any(x => x == "Unconnected\nunconnected"), "warning displays both name and GUID");
        int reads = BlankRoomState.Reads;
        var late = GateApi.Register("unconnected", () => { }, () => { });
        Chainloader.PluginInfos.Add("added", Info("added", "Added", root + "\\added.dll"));
        for (int i = 0; i < 1000; ++i)
        {
            Event.current = new Event { rawType = EventType.Repaint };
            GUILayout.Labels.Clear(); Call(window, "OnGUI");
        }
        GameAPI.Begin();
        Check(BlankRoomState.Reads == reads && window.Lines.Length == 2 && window.Lines.Any(x => x.Contains("Unconnected"))
            && !window.Lines.Any(x => x.Contains("Added")), "drawing, late registration, new plugins, and duplicate H start never refresh snapshot");
        Event.current = new Event { rawType = EventType.MouseDown, button = 0, mousePosition = new Vector2(30, 30) };
        Input.MousePressed = true; GUILayout.ScrollHot = 91; Call(window, "OnGUI");
        GUILayout.ScrollHot = 0;
        Check(UiInputCaptureApi.Dragging && GUIUtility.hotControl == 91, "scroll pointer press explicitly captures input");
        Event.current = new Event { rawType = EventType.MouseDrag, mousePosition = new Vector2(1200, 700) };
        Call(window, "OnGUI");
        Check(UiInputCaptureApi.Dragging && UiInputCaptureApi.Blocked, "drag remains captured outside warning");
        Event.current = new Event { rawType = EventType.MouseUp, button = 0 }; Input.MousePressed = false;
        Call(window, "OnGUI");
        Check(!UiInputCaptureApi.Dragging && GUIUtility.hotControl == 0 && UiInputCaptureApi.Blocked, "mouse release ends drag after GUI while window remains blocked");
        Call(window, "OnApplicationFocus", false);
        Check(!UiInputCaptureApi.Blocked && !UiInputCaptureApi.CursorUnlocked && !UiInputCaptureApi.Dragging, "focus loss releases all input ownership");
        Call(window, "OnApplicationFocus", true);
        Check(UiInputCaptureApi.Blocked && UiInputCaptureApi.CursorUnlocked, "focus return restores visible window input rect");
        var closeRect = window.CloseButtonRect;
        var closePoint = new Vector2(closeRect.x + 5, closeRect.y + 5);
        GUI.enabled = false; // Another OnGUI caller may leave shared GUI state disabled.
        Input.MousePressed = false; // Input's frame snapshot must not cancel an IMGUI press before MouseUp.
        Event.current = new Event { rawType = EventType.MouseDown, mousePosition = closePoint };
        Call(window, "OnGUI");
        Check(window.Visible && UiInputCaptureApi.Dragging && !GUI.enabled,
            "close press is retained until MouseUp even when Input reports false; inherited GUI.enabled is restored");
        Event.current = new Event { rawType = EventType.Repaint };
        Call(window, "OnGUI");
        Check(window.Visible && UiInputCaptureApi.Dragging, "intermediate repaint does not cancel pending close press");
        Event.current = new Event { rawType = EventType.MouseUp, mousePosition = closePoint };
        Call(window, "OnGUI");
        Check(Event.current.Used, "close release consumes the click after hiding window");
        Check(closeTrace.Contains("close mouse-down") && closeTrace.Contains("close mouse-up inside button")
            && closeTrace.Contains("dismissed; drawing and input capture disabled"), "close trace confirms down, up, and dismissal without synthetic Button results");
        GUI.enabled = true;
        GameAPI.Begin();
        Check(!window.Visible && !window.enabled && !UiInputCaptureApi.Blocked && !UiInputCaptureApi.CursorUnlocked,
            "close disables drawing and releases input without reopening in same H");
        GameAPI.End(); GameAPI.Begin();
        Check(!window.Visible, "repeated end/start notifications for the same H instance cannot reopen dismissed warning");
        window.Show(missing);
        Call(window, "OnApplicationFocus", false);
        Call(window, "OnApplicationFocus", true);
        Event.current = new Event { rawType = EventType.MouseDown, mousePosition = new Vector2(40, 40) };
        Call(window, "OnGUI");
        Check(!window.Visible && !UiInputCaptureApi.Blocked && !window.enabled,
            "same-scene show, focus changes and screen clicks keep dismissed warning closed");
        Call(gate, "HSceneDestroyed", new HSceneProc());
        Check(GateApi.IsAllowed, "unrelated H destruction cannot end current scene");
        GameAPI.End();
        Call(gate, "HSceneStarting", new HSceneProc());
        GameAPI.Begin();
        Check(window.Visible && window.Lines.Any(x => x.Contains("Added")) && !window.Lines.Any(x => x.Contains("Unconnected")),
            "next H displays a newly audited snapshot");
        GameAPI.End();
        Check(!window.Visible && !UiInputCaptureApi.Blocked, "H end hides warning and releases input");
        // A large snapshot must remain complete; the UI provides a scroll view, not a truncated list.
        var many = Enumerable.Range(0, 250).Select(i => new UnregisteredPlugin("Plugin " + i, "guid." + i)).ToList();
        window.Show(many); GUILayout.Labels.Clear(); Call(window, "OnGUI");
        Check(window.Lines.Length == 250 && GUILayout.Labels.Any(x => x == "Plugin 249\nguid.249"), "all 250 rows are retained and drawn in scroll view");
        Event.current = new Event { rawType = EventType.KeyDown, keyCode = KeyCode.Escape };
        Call(window, "OnGUI");
        Check(!window.Visible && !window.enabled && !UiInputCaptureApi.Blocked && !UiInputCaptureApi.CursorUnlocked && Event.current.Used,
            "Escape dismisses without mouse input and releases capture");
        Event.current = new Event { rawType = EventType.Repaint };
        window.BeginScene(new object()); window.Show(many);
        GUIUtility.hotControl = 999; window.Hide();
        Check(GUIUtility.hotControl == 999, "closing never clears another UI's hotControl");
        GUIUtility.hotControl = 0;
        window.Show(many); Call(window, "OnDisable");
        Check(!window.Visible && !UiInputCaptureApi.Blocked, "warning component disable releases input");
        window.Show(many); Call(window, "OnDestroy");
        Check(!window.Visible && !UiInputCaptureApi.Blocked, "warning component destruction releases input");
        using (GateApi.Register("com.kks.maingameblankmapadd", () => { }, () => { }))
        using (GateApi.Register("added", () => { }, () => { }))
        {
            GameAPI.Begin();
            Check(!window.Visible && !window.enabled, "zero unregistered plugins produces no warning");
            GameAPI.End();
        }
        GameAPI.Begin(); Call(gate, "OnDisable");
        Check(!window.Visible && !UiInputCaptureApi.Blocked, "gate disable hides warning");
        Call(gate, "OnEnable"); GameAPI.End(); GameAPI.Begin();
        Call(gate, "OnDestroy");
        Check(!window.Visible && !UiInputCaptureApi.Blocked && !UiInputCaptureApi.CursorUnlocked, "gate destruction hides active warning and releases input");
        registration.Dispose(); late.Dispose();
        Check(typeof(WarningWindow).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance) == null,
            "warning has no monitoring Update");
        Console.WriteLine($"{_checks} warning checks passed (stubbed IMGUI; live interaction untested).");
    }
}

namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static bool Blocked, CursorUnlocked, Dragging;
        public static void SetInputBlockRects(string owner, IList<Rect> rects) { Blocked = true; }
        public static void ClearInputBlockRects(string owner) { Blocked = false; }
        public static bool SetIdleCursorUnlock(string owner, bool enabled) { CursorUnlocked = enabled; return true; }
        public static void Sync(string owner, string source, bool active) { Dragging = active; }
    }
}
