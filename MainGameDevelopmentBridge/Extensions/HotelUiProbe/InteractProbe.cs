using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

// Records one explicit F3 press through existing native input and GUI boundaries.
public sealed class HotelInteractProbe : IDevelopmentCommand
{
    public string Name => "hotel-interact-probe";
    static HotelInteractProbe active;
    Harmony patches;
    readonly List<object> events = new List<object>();
    SynchronizationContext main;
    object hotel, setting;
    string file, error;
    EventHandler changed;
    Func<object, string> serialize;
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    static object Get(object value, string name) => HotelUiProbe.Get(value, name);
    bool Enabled => Get(setting, "Value") is bool value && value;
    static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };
    object State(string stage)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
        var context = Get(hotel, "_context");
        var camera = Get(Get(hotel, "_walker"), "ViewCamera") as Camera;
        var floor = Get(hotel, "_floor");
        var definition = Get(floor, "Definition");
        var p = Get(hotel, "_p");
        float distance = p == null ? default(float) : (float)p.GetType().GetMethod("N").Invoke(p, new object[] { "hotel.interactDistance" });
        RaycastHit hit = default(RaycastHit);
        bool hasHit = camera != null && Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        return new { stage, frame = Time.frameCount, utc = DateTime.UtcNow.ToString("O"), foregroundPid = foreground,
            focused = Application.isFocused, cursor = Cursor.lockState.ToString(), floor = Get(definition, "id"),
            loading = Get(hotel, "_loading"), changing = Get(hotel, "_changingFloor"), returning = Get(hotel, "_returnRequested"),
            ready = Get(context, "WalkingReady"), panel = Get(context, "Panel"), blocked = Get(context, "ExternalInputBlocked"),
            authoring = Get(Get(hotel, "_authoring"), "Occupied"), help = Get(Get(hotel, "_help"), "Visible"),
            cards = Get(Get(hotel, "_roomCards"), "Visible"), elevatorMenu = Get(hotel, "_elevatorMenu"),
            eye = camera == null ? null : V(camera.transform.position), forward = camera == null ? null : V(camera.transform.forward),
            distance, hit = hasHit ? hit.collider.name : null, hitDistance = hasHit ? hit.distance : default(float),
            elevator = Get(definition, "elevator") is Vector3 point ? V(point) : null };
    }
    void Record(string stage)
    {
        if (!Enabled) { Stop(); return; }
        try
        {
            var value = State(stage);
            events.Add(value);
            File.AppendAllText(file, serialize(value) + Environment.NewLine);
        }
        catch (Exception ex) { error = ex.GetBaseException().Message; Stop(); }
    }
    static void Down(object[] __args)
    {
        if (active == null || __args.Length == default(int) || Convert.ToString(__args[default(int)]) != "F3") return;
        active.Record("native-F3-down");
    }
    static void Up(object[] __args)
    {
        var probe = active;
        if (probe == null || __args.Length == default(int) || Convert.ToString(__args[default(int)]) != "F3") return;
        probe.Record("native-F3-up");
        probe.main.Post(_ => probe.Stop(), null);
    }
    static void Gui()
    {
        if (active == null) return;
        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown || (e.keyCode != KeyCode.F3 && e.keyCode != KeyCode.F)) return;
        active.Record("hotel-OnGUI-" + e.keyCode);
    }
    static void InteractBefore() { active?.Record("Interact-enter"); }
    static void InteractAfter() { active?.Record("Interact-return"); }
    void Stop()
    {
        patches?.UnpatchSelf(); patches = null;
        if (setting != null && changed != null) setting.GetType().GetEvent("SettingChanged").RemoveEventHandler(setting, changed);
        changed = null;
        if (ReferenceEquals(active, this)) active = null;
    }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var action = ReflectionCommands.Text(arguments, "action", "result");
        if (action == "stop") { Stop(); return true; }
        if (action == "result") return new { armed = patches != null, file, error, events = events.ToArray() };
        if (action != "arm") throw new ArgumentException("action must be arm, result or stop");
        Stop();
        var plugin = Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
        hotel = Get(plugin, "_hotel"); setting = Get(plugin, "_hotelDiagnostics");
        if (hotel == null || !Enabled) throw new InvalidOperationException("A hotel session and HotelMode.DiagnosticsEnabled are required.");
        events.Clear(); error = null; main = SynchronizationContext.Current;
        var json = Get(context.Reflection, "_json");
        var write = json.GetType().GetMethod("Serialize");
        serialize = value => (string)write.Invoke(json, new[] { value });
        Directory.CreateDirectory(context.OutputDirectory);
        file = Path.Combine(context.OutputDirectory, "hotel-interact-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
        active = this;
        changed = (sender, args) => { if (!Enabled) Stop(); };
        setting.GetType().GetEvent("SettingChanged").AddEventHandler(setting, changed);
        patches = new Harmony("canon.development.hotel-interact-one-press");
        var buttons = ReflectionCommands.ResolveType("KKS_VR.Controls.ButtonsSubtool");
        patches.Patch(AccessTools.Method(buttons, "ButtonDown"), prefix: new HarmonyMethod(typeof(HotelInteractProbe), nameof(Down)));
        patches.Patch(AccessTools.Method(buttons, "ButtonUp"), postfix: new HarmonyMethod(typeof(HotelInteractProbe), nameof(Up)));
        patches.Patch(AccessTools.Method(hotel.GetType(), "OnGUI"), prefix: new HarmonyMethod(typeof(HotelInteractProbe), nameof(Gui)));
        patches.Patch(AccessTools.Method(hotel.GetType(), "Interact"), prefix: new HarmonyMethod(typeof(HotelInteractProbe), nameof(InteractBefore)), postfix: new HarmonyMethod(typeof(HotelInteractProbe), nameof(InteractAfter)));
        Record("armed");
        return new { armed = true, file, boundary = "next F3 down/up; detach on release or diagnostics OFF" };
    }
    public void Dispose() => Stop();
}
