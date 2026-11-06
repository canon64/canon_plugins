using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using MainGameAiShoujoMode;
using MainGameDevelopmentBridge;
using UnityEngine;

// Live adapter for the old DLL. NativeHotelInteractionScope is linked from production.
public sealed class HotelNativeInputFix : IDevelopmentCommand
{
    public string Name => "hotel-native-input-fix";
    static HotelNativeInputFix active;
    static bool trusted;
    Harmony patches;
    NativeHotelInteractionScope input;
    object hotel;
    static object Get(object target, string name)
    {
        if (target == null) return null;
        for (var t = target as Type ?? target.GetType(); t != null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var f = t.GetField(name, flags);
            if (f != null) return f.GetValue(target is Type ? null : target);
            var p = t.GetProperty(name, flags);
            if (p != null) return p.GetValue(target is Type ? null : target, null);
        }
        return null;
    }
    static bool Yes(object target, string name) => Get(target, name) is bool value && value;
    static object CurrentHotel() => Get(Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance"), "_hotel");
    bool Owns() => hotel is Component c && c != null && ReferenceEquals(CurrentHotel(), hotel)
        && !Yes(hotel, "_closing") && !Yes(hotel, "_returnRequested") && Yes(Get(hotel, "_nativeWalking"), "Ready")
        && !Yes(Get(hotel, "_hSession"), "Busy");
    void Press()
    {
        var context = Get(hotel, "_context");
        if (Yes(hotel, "_loading") || Yes(hotel, "_changingFloor") || !Yes(context, "WalkingReady") || Yes(context, "Panel")
            || Yes(hotel, "_walkingMenu") || Yes(Get(hotel, "_roomCards"), "Visible") || Yes(Get(hotel, "_help"), "Visible")
            || Yes(Get(hotel, "_authoring"), "Occupied")) return;
        Trace("received");
        trusted = true;
        try { AccessTools.Method(hotel.GetType(), "Interact").Invoke(hotel, null); }
        finally { trusted = false; }
        Trace("complete");
    }
    void Trace(string stage)
    {
        var plugin = Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
        if (!Yes(Get(plugin, "_hotelDiagnostics"), "Value")) return;
        (Get(hotel, "_log") as Action<string>)?.Invoke("HOTEL_VR_NATIVE_INTERACT stage=" + stage
            + " focused=" + Application.isFocused + " elevatorMenu=" + Yes(hotel, "_elevatorMenu"));
        if (stage != "received") return;
        var camera = Get(Get(hotel, "_walker"), "ViewCamera") as Camera;
        var p = Get(hotel, "_p");
        var distance = (float)p.GetType().GetMethod("N").Invoke(p, new object[] { "hotel.interactDistance" });
        var floor = Get(hotel, "_floor");
        var elevator = (Vector3)Get(Get(floor, "Definition"), "elevator");
        var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        (Get(hotel, "_log") as Action<string>)?.Invoke("HOTEL_VR_INTERACT_RAY eye=" + camera.transform.position.ToString("F4")
            + " forward=" + camera.transform.forward.ToString("F4") + " maxDistance=" + distance
            + " elevator=" + elevator.ToString("F4") + " elevatorDistance=" + Vector3.Distance(camera.transform.position, elevator)
            + " targetColliders=" + string.Join(";", ((Collider[])Get(floor, "ElevatorColliders")).Select(c => c.name))
            + " hits=" + string.Join(";", hits.OrderBy(h => h.distance).Select(h => h.collider.name + ":" + h.distance)));
    }
    static bool Blocked(object context) => trusted ? !Yes(context, "WalkingReady") || Yes(context, "Panel") : Yes(context, "ExternalInputBlocked");
    static CursorLockMode CursorState() => trusted ? CursorLockMode.Locked : Cursor.lockState;
    static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
    {
        var block = AccessTools.PropertyGetter(ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelWalkingContext"), "ExternalInputBlocked");
        var cursor = AccessTools.PropertyGetter(typeof(Cursor), "lockState");
        bool foundBlock = false, foundCursor = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(block))
            { foundBlock = true; yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(HotelNativeInputFix), nameof(Blocked))) { labels = instruction.labels }; }
            else if (instruction.Calls(cursor))
            { foundCursor = true; yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(HotelNativeInputFix), nameof(CursorState))) { labels = instruction.labels }; }
            else yield return instruction;
        }
        if (!foundBlock || !foundCursor) throw new InvalidOperationException("Expected desktop interaction guards were not found.");
    }
    static void WalkingResumed() => active?.Bind();
    void Bind()
    {
        var next = CurrentHotel();
        if (!(next is Component component) || component == null || Get(next, "_nativeWalking") == null || ReferenceEquals(next, hotel)) return;
        Release(hotel);
        hotel = next;
        input = new NativeHotelInteractionScope(Owns, Press);
        var lifetime = component.gameObject.AddComponent<HotelNativeInputFixLifetime>();
        lifetime.Release = () => Release(next);
    }
    void Release(object expected)
    {
        if (!ReferenceEquals(hotel, expected)) return;
        input?.Dispose(); input = null; hotel = null;
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        if (patches == null)
        {
            active = this;
            patches = new Harmony("canon.development.hotel-native-input-adapter");
            try
            {
                patches.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelModeSession"), "Interact"),
                    transpiler: new HarmonyMethod(typeof(HotelNativeInputFix), nameof(Redirect)));
                patches.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGameAiShoujoMode.NativeActionVrSceneScope"), "Resume"),
                    postfix: new HarmonyMethod(typeof(HotelNativeInputFix), nameof(WalkingResumed)));
                Bind();
            }
            catch { Dispose(); throw; }
        }
        return new { installed = true, hotelBound = hotel != null, ownsInput = hotel != null && Owns(),
            focused = Application.isFocused, elevatorMenu = hotel == null ? (bool?)null : Yes(hotel, "_elevatorMenu") };
    }
    public void Dispose()
    {
        Release(hotel); patches?.UnpatchSelf(); patches = null;
        if (ReferenceEquals(active, this)) active = null;
    }
}
public sealed class HotelNativeInputFixLifetime : MonoBehaviour
{
    internal Action Release;
    void OnDestroy() { var release = Release; Release = null; release?.Invoke(); }
}
