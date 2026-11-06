using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelPlacementOrder : IDevelopmentCommand
{
    public string Name => "hotel-placement-order";
    static HotelPlacementOrder active;
    Harmony patch;
    object setting;
    EventHandler changed;
    bool resetSetting;
    readonly List<object> events = new List<object>();
    bool Enabled => Get(setting, "Value") is bool enabled && enabled;
    static object Get(object value, string name)
    {
        if (value == null) return null;
        for (var type = value as Type ?? value.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(value is Type ? null : value);
            var property = type.GetProperty(name, flags);
            if (property != null) return property.GetValue(value is Type ? null : value, null);
        }
        return null;
    }
    static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };
    void Record(string stage)
    {
        if (!Enabled) return;
        try
        {
            var hotel = Get(Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance"), "_hotel");
            if (hotel == null) return;
            var session = Get(hotel, "_hSession");
            var proc = Get(session, "_proc");
            var room = Get(hotel, "_sessionRoomRoot") as Transform;
            var mapOwner = Get(ReflectionCommands.ResolveType("MainGameBlankMapAdd.Plugin"), "_roomFeatureOwner");
            var actors = Get(proc, "lstFemale") as IEnumerable;
            var preset = Get(mapOwner, "_hPlaceActive");
            events.Add(new { stage, frame = Time.frameCount, utc = DateTime.UtcNow.ToString("O"),
                ready = Get(session, "RoomReady"), key = Get(mapOwner, "_hPlaceKey"),
                activePlace = Get(preset, "Id"), placePosition = Get(preset, "Position"),
                actors = actors?.Cast<Component>().Where(c => c != null).Select(c => new {
                    c.name, id = c.GetInstanceID(), parent = c.transform.parent?.name,
                    world = V(c.transform.position), local = room == null ? null : V(room.InverseTransformPoint(c.transform.position)),
                    rotation = V(c.transform.eulerAngles), top = (Get(c, "objTop") as GameObject)?.transform.position is Vector3 top ? V(top) : null
                }).ToArray() });
        }
        catch (Exception ex) { events.Add(new { stage, error = ex.GetBaseException().Message }); }
    }
    static void Before(MethodBase __originalMethod) => active?.Record("before:" + __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name);
    static void After(MethodBase __originalMethod) => active?.Record("after:" + __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name);
    static void StartWrapper(ref IEnumerator __result)
    {
        var owner = active;
        var hotel = Get(Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance"), "_hotel");
        if (owner != null && owner.Enabled && hotel != null) __result = owner.StartSequence(__result);
    }
    IEnumerator StartSequence(IEnumerator original)
    {
        try { while (original.MoveNext()) yield return original.Current; }
        finally
        {
            (original as IDisposable)?.Dispose();
            Record("native-start-outer-completed");
            Dispose();
            if (resetSetting)
                try { setting.GetType().GetProperty("Value").SetValue(setting, false, null); }
                catch (Exception ex) { events.Add(new { stage = "diagnostic-reset", error = ex.GetBaseException().Message }); }
        }
    }
    void Boundaries(Type type, string name)
    {
        var method = AccessTools.Method(type, name);
        if (method == null) return;
        patch.Patch(method, prefix: new HarmonyMethod(typeof(HotelPlacementOrder), nameof(Before)), postfix: new HarmonyMethod(typeof(HotelPlacementOrder), nameof(After)));
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        var action = ReflectionCommands.Text(args, "action", "result");
        if (action == "stop") Dispose();
        if (action == "arm")
        {
            Dispose(); events.Clear();
            var mapType = ReflectionCommands.ResolveType("MainGameBlankMapAdd.Plugin");
            var plugin = Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
            setting = Get(plugin, "_hotelDiagnostics");
            if (!Enabled) throw new InvalidOperationException("HotelMode.DiagnosticsEnabled is required.");
            resetSetting = args.TryGetValue("resetDiagnostics", out var reset) && Convert.ToBoolean(reset);
            active = this;
            changed = (s,e) => { if (!Enabled) Dispose(); };
            setting.GetType().GetEvent("SettingChanged").AddEventHandler(setting, changed);
            patch = new Harmony("canon.development.hotel-placement-order");
            var hotelType = ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelModeSession");
            foreach (var name in new[] { "RoomSessionReady", "PrepareRoomMapControls", "RestoreRoomReadyState", "BindRoomMapControls", "FinishRoomRestore", "RevealDirectRoom" }) Boundaries(hotelType, name);
            foreach (var name in new[] { "PrepareHotelPoseLocations", "ApplyCurrentPoseLocation" }) Boundaries(mapType, name);
            var infos = (IDictionary)Get(ReflectionCommands.ResolveType("BepInEx.Bootstrap.Chainloader"), "PluginInfos");
            foreach (var info in infos.Values)
            {
                var instance = Get(info, "Instance");
                if (instance != null && instance.GetType().GetMethod("RestoreHotelGadget", new[] { typeof(Transform), typeof(string) }) != null)
                    Boundaries(instance.GetType(), "RestoreHotelGadget");
            }
            var procType = ReflectionCommands.ResolveType("HSceneProc");
            foreach (var name in new[] { "SetCharacterPositon", "SetLocalPosition", "ChangeAnimator" }) Boundaries(procType, name);
            var wrapper = new HarmonyMethod(typeof(HotelPlacementOrder), nameof(StartWrapper)) { priority = Priority.Last };
            patch.Patch(AccessTools.Method(procType, "Start"), postfix: wrapper);
        }
        return new { armed = patch != null, events = events.ToArray() };
    }
    public void Dispose()
    {
        patch?.UnpatchSelf(); patch = null;
        if (changed != null) setting.GetType().GetEvent("SettingChanged").RemoveEventHandler(setting, changed);
        changed = null;
        if (ReferenceEquals(active, this)) active = null;
    }
}
