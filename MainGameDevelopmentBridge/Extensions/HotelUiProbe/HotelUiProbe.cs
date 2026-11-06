using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelUiProbe : IDevelopmentCommand
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public string Name => "hotel-ui-probe";
    private HotelUiClickTrace _clicks;
    private readonly List<object> _clickEvents = new List<object>();
    private string _evidencePath;
    internal static object Get(object obj, string name)
    {
        if (obj == null) return null;
        Type type = obj as Type ?? obj.GetType();
        for (var t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(obj is Type ? null : obj);
            var property = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
            if (property != null) return property.GetValue(obj is Type ? null : obj, null);
        }
        return null;
    }
    private static object Call(object obj, string name, params object[] args)
    {
        var type = obj as Type ?? obj.GetType();
        var method = type.GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(obj is Type ? null : obj, args);
    }
    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    private static object Describe(object obj)
    {
        var c = obj as Component;
        if (c == null) return obj == null ? null : new { type = obj.GetType().FullName };
        return new { type = c.GetType().FullName, name = c.name, active = c.gameObject.activeInHierarchy,
            position = V(c.transform.position), scale = V(c.transform.lossyScale) };
    }
    private static object Controller(object controller)
    {
        if (controller == null) return null;
        return new { type = controller.GetType().FullName, toolIndex = Get(controller, "ToolIndex"),
            toolEnabled = Get(controller, "ToolEnabled"), activeTool = Describe(Get(controller, "ActiveTool")),
            canAcquireFocus = Call(controller, "CanAcquireFocus") };
    }
    private static object RayState(object handler, object quad)
    {
        try { return new { quad = Describe(quad), range = Call(handler, "GetRange", quad),
            withinRange = Call(handler, "IsWithinRange", quad), laserable = Call(handler, "IsLaserable", quad) }; }
        catch (Exception ex) { return new { error = (ex.InnerException ?? ex).Message }; }
    }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var plugin = Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
        var hotel = Get(plugin, "_hotel");
        var vr = ReflectionCommands.ResolveType("VRGIN.Core.VR");
        var mode = Get(vr, "Mode");
        var registry = ReflectionCommands.ResolveType("VRGIN.Visuals.GUIQuadRegistry");
        var quads = (Get(registry, "Quads") as IEnumerable)?.Cast<object>().ToArray() ?? new object[0];
        var handlerType = ReflectionCommands.ResolveType("VRGIN.Controls.Handlers.MenuHandler");
        var handlers = Resources.FindObjectsOfTypeAll(handlerType).Cast<Component>().Where(c => c != null && c.gameObject.activeInHierarchy).ToArray();
        var state = Get(hotel, "_context");
        var walker = Get(hotel, "_walker");
        var floor = Get(hotel, "_floor");
        var native = Get(hotel, "_nativeWalking");
        string action = ReflectionCommands.Text(arguments, "action", "snapshot");
        if (action == "arm-clicks")
        {
            Dispose();
            if (!(hotel is Component component)) throw new InvalidOperationException("Hotel is not running");
            _clickEvents.Clear();
            System.IO.Directory.CreateDirectory(context.OutputDirectory);
            _evidencePath = System.IO.Path.Combine(context.OutputDirectory,
                "hotel-ui-clicks-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N") + ".txt");
            System.IO.File.WriteAllText(_evidencePath, "Hotel UI diagnostic: explicitly armed, next mouse down/up only\n");
            _clicks = component.gameObject.AddComponent<HotelUiClickTrace>();
            _clicks.Hotel = hotel;
            _clicks.Events = _clickEvents;
            _clicks.EvidencePath = _evidencePath;
            return new { armed = true, boundary = "next left mouse down/up, max 8 events", evidencePath = _evidencePath };
        }
        if (action == "clicks")
        {
            return new { events = _clickEvents.ToArray(), armed = _clicks != null && _clicks.enabled,
                evidencePath = _evidencePath, error = _clicks != null ? _clicks.Error : null };
        }
        if (action == "stop-clicks") { Dispose(); return true; }
        if (action != "snapshot") throw new ArgumentException("Unsupported probe action");
        return new {
            frame = Time.frameCount, focused = Application.isFocused, vrActive = Get(vr, "Active"),
            cursor = Cursor.lockState.ToString(), mouse = V(Input.mousePosition),
            hotel = hotel != null, loading = Get(hotel, "_loading"), changingFloor = Get(hotel, "_changingFloor"),
            walkingMenu = Get(hotel, "_walkingMenu"), elevatorMenu = Get(hotel, "_elevatorMenu"),
            floor = Get(Get(floor, "Definition"), "id"), failure = Get(hotel, "_failure"), roomFailure = Get(hotel, "_roomEntryFailure"),
            walkingReady = Get(state, "WalkingReady"), panel = Get(state, "Panel"), externalBlocked = Get(state, "ExternalInputBlocked"),
            nativeReady = Get(native, "Ready"), walkingView = Describe(Get(walker, "ViewCamera")),
            left = Controller(Get(mode, "Left")), right = Controller(Get(mode, "Right")),
            gui = quads.Select(Describe).ToArray(),
            menuHandlers = handlers.Select(h => new { owner = Describe(Get(h, "_Controller")),
                enabled = (h as Behaviour)?.enabled, laser = Get(h, "LaserVisible"), pressing = Get(h, "IsPressing"),
                target = Describe(Get(h, "_Target")), pressedButtons = Convert.ToString(Get(h, "_PressedButtons")),
                rays = quads.Select(q => RayState(h, q)).ToArray() }).ToArray()
        };
    }
    public void Dispose()
    {
        if (_clicks != null) { _clicks.enabled = false; UnityEngine.Object.Destroy(_clicks); _clicks = null; }
    }
}

public sealed class HotelUiClickTrace : MonoBehaviour
{
    internal object Hotel;
    internal List<object> Events;
    internal string EvidencePath, Error;
    private void OnGUI()
    {
        var input = Event.current;
        if (input == null || input.button != 0 || (input.rawType != EventType.MouseDown && input.rawType != EventType.MouseUp)) return;
        var rect = (Rect)HotelUiProbe.Get(Hotel, "WalkingMenuRect");
        Events.Add(new { frame = Time.frameCount, raw = input.rawType.ToString(), type = input.type.ToString(),
            mouse = new[] { input.mousePosition.x, input.mousePosition.y }, insideMenu = rect.Contains(input.mousePosition),
            menu = new[] { rect.x, rect.y, rect.width, rect.height }, hot = GUIUtility.hotControl,
            modifiers = input.modifiers.ToString(), focused = Application.isFocused, guiEnabled = GUI.enabled,
            elevator = HotelUiProbe.Get(Hotel, "_elevatorMenu"), changing = HotelUiProbe.Get(Hotel, "_changingFloor") });
        try
        {
            System.IO.File.AppendAllText(EvidencePath, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "frame={0} raw={1} type={2} mouse=({3},{4}) insideMenu={5} hot={6} modifiers={7} focus={8} guiEnabled={9} elevator={10} changingFloor={11}\n",
                Time.frameCount, input.rawType, input.type, input.mousePosition.x, input.mousePosition.y,
                rect.Contains(input.mousePosition), GUIUtility.hotControl, input.modifiers, Application.isFocused,
                GUI.enabled, HotelUiProbe.Get(Hotel, "_elevatorMenu"), HotelUiProbe.Get(Hotel, "_changingFloor")));
        }
        catch (Exception ex) { Error = ex.Message; enabled = false; }
        if (Events.Count >= 8 || input.rawType == EventType.MouseUp) enabled = false;
    }
}
