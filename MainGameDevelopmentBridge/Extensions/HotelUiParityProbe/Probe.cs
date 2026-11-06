using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

static class Access
{
    internal static object Get(object value, string name)
    {
        if (value == null) return null;
        for (var t = value as Type ?? value.GetType(); t != null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var f = t.GetField(name, flags);
            if (f != null) return f.GetValue(value is Type ? null : value);
            var p = t.GetProperty(name, flags);
            if (p != null) return p.GetValue(value is Type ? null : value, null);
        }
        return null;
    }
    internal static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
}
public sealed class HotelPropState : IDevelopmentCommand
{
    public string Name => "hotel-prop-state";
    static object Hand(object value)
    {
        var hand = value as Component;
        var focus = Access.Get(value, "_Lock");
        return hand == null ? null : new { position = Access.V(hand.transform.position),
            tracking = Access.Get(value, "IsTracking"), validLock = Access.Get(focus, "IsValid"), retiring = Access.Get(focus, "IsInvalidating") };
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        var type = ReflectionCommands.ResolveType("MainGameHotelRoomProps.Plugin");
        var plugin = Resources.FindObjectsOfTypeAll(type).Cast<Component>().Single();
        var mode = Access.Get(ReflectionCommands.ResolveType("VRGIN.Core.VR"), "Mode");
        var props = ((IEnumerable)Access.Get(plugin, "_props")).Cast<object>().ToArray();
        var hand = Access.Get(mode, ReflectionCommands.Text(args, "hand", "RightHand") == "LeftHand" ? "Left" : "Right") as Component;
        var nearest = hand == null ? null : props.Where(p => (string)Access.Get(p, "Id") != "bed_furniture")
            .SelectMany(p => ((Collider[])Access.Get(p, "Colliders")).Where(c => c != null && c.enabled && !c.isTrigger)
                .Select(c => new { id = (string)Access.Get(p, "Id"), distance = Vector3.Distance(hand.transform.position, c.ClosestPoint(hand.transform.position)) }))
            .OrderBy(p => p.distance).Take(8).ToArray();
        return new { focused = Application.isFocused, ready = Access.Get(plugin, "_ready"), bound = Access.Get(plugin, "_bound"),
            canAct = Access.Get(plugin, "CanAct"), room = Access.Get(plugin, "_roomId"), error = Access.Get(plugin, "LastError"),
            radius = Access.Get(Access.Get(plugin, "_radius"), "Value"), nearest,
            holds = (Access.Get(plugin, "_holds") as IDictionary)?.Count,
            left = Hand(Access.Get(mode, "Left")), right = Hand(Access.Get(mode, "Right")), count = props.Length,
            props = props.Where(p => {
                string id = (string)Access.Get(p, "Id");
                return id == "bed_furniture" || id.Contains("pillow") || id.Contains("cushion") || id.StartsWith("bed_prop_");
            }).Select(p => {
                var root = (Transform)Access.Get(p, "Root");
                var colliders = ((Collider[])Access.Get(p, "Colliders")).Where(c => c != null).ToArray();
                return new { id = Access.Get(p, "Id"), root = root.name, active = root.gameObject.activeInHierarchy,
                    position = Access.V(root.position), colliderCount = colliders.Length,
                    colliders = colliders.Select(c => new { c.name, c.enabled, c.isTrigger, size = Access.V(c.bounds.size) }).ToArray() };
            }).ToArray() };
    }
    public void Dispose() { }
}
public sealed class HotelSubtitleOverlayFix : IDevelopmentCommand
{
    public string Name => "hotel-subtitle-overlay-fix";
    Harmony patch;
    static bool Before(object[] __args)
    {
        var session = Access.Get(ReflectionCommands.ResolveType("CanonGate.GateApi"), "Current");
        if (!(Access.Get(session, "IsHotelMode") is bool hotel) || !hotel) return true;
        var backend = __args[2];
        if (backend.ToString() != "Auto") return true;
        AccessTools.Method(ReflectionCommands.ResolveType("MainGameSubtitleCore.Plugin"), "ShowOverlay")
            .Invoke(null, new[] { __args[0], __args[1], __args[3], __args[4] });
        return false;
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        if (patch == null)
        {
            patch = new Harmony("canon.development.hotel-subtitle-overlay");
            patch.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGameSubtitleCore.Plugin"), "ShowSubtitleCore"),
                prefix: new HarmonyMethod(typeof(HotelSubtitleOverlayFix), nameof(Before)));
        }
        return new { installed = true, scope = "hotel Auto subtitles only" };
    }
    public void Dispose() { patch?.UnpatchSelf(); patch = null; }
}
public sealed class HotelGripAttemptProbe : IDevelopmentCommand
{
    public string Name => "hotel-grip-attempt";
    static HotelGripAttemptProbe active;
    Harmony patch;
    object setting, last;
    Dictionary<string, object> capture;
    string target;
    object capturedHand;
    DevelopmentContext context;
    EventHandler changed;
    bool Enabled => Access.Get(setting, "Value") is bool yes && yes;
    static object LockState(object hand)
    {
        var focus = Access.Get(hand, "_Lock");
        var touchType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("KKS_VR.Caress.CaressController", false)).FirstOrDefault(t => t != null);
        var touch = touchType == null ? null : (hand as Component)?.GetComponent(touchType);
        return new { present = focus != null, valid = Access.Get(focus, "IsValid"),
            retiring = Access.Get(focus, "IsInvalidating"), type = focus?.GetType().FullName,
            nativeCaressOwner = focus != null && ReferenceEquals(Access.Get(touch, "_lock"), focus) };
    }
    static void Before(object __instance, object[] __args, out bool __state)
    {
        __state = false;
        var owner = active;
        if (owner == null || owner.last != null || !owner.Enabled) return;
        __state = true;
        try
        {
            var source = __args[1];
            var sourceName = source.ToString();
            var mode = Access.Get(ReflectionCommands.ResolveType("VRGIN.Core.VR"), "Mode");
            var hand = Access.Get(mode, sourceName == "LeftHand" ? "Left" : "Right") as Component;
            owner.capturedHand = hand;
            var holds = (IDictionary)Access.Get(__instance, "_holds");
            var props = ((IEnumerable)Access.Get(__instance, "_props")).Cast<object>().ToArray();
            var radius = (float)Access.Get(Access.Get(__instance, "_radius"), "Value");
            var candidates = props.Select(p => {
                var root = (Transform)Access.Get(p, "Root");
                var colliders = ((Collider[])Access.Get(p, "Colliders")).Where(c => c != null).ToArray();
                bool alreadyHeld = holds.Values.Cast<object>().Any(h => ReferenceEquals(Access.Get(h, "Prop"), p));
                var distances = colliders.Where(c => c.enabled && !c.isTrigger && hand != null)
                    .Select(c => new { distance = Vector3.Distance(hand.transform.position, c.ClosestPoint(hand.transform.position)),
                        volume = c.bounds.size.sqrMagnitude }).OrderBy(c => c.distance).ThenBy(c => c.volume).ToArray();
                var nearest = distances.FirstOrDefault();
                return new { id = (string)Access.Get(p, "Id"), active = root != null && root.gameObject.activeInHierarchy,
                    alreadyHeld, nearest, root, colliders };
            }).ToArray();
            var selected = candidates.Where(p => p.id != "bed_furniture" && p.active && !p.alreadyHeld && p.nearest != null && p.nearest.distance <= radius)
                .OrderBy(p => p.nearest.distance).ThenBy(p => p.nearest.volume).FirstOrDefault();
            bool canAct = (bool)Access.Get(__instance, "CanAct");
            bool tracking = hand != null && (bool)Access.Get(hand, "IsTracking");
            var room = Access.Get(__instance, "_room") as GameObject;
            owner.capture = new Dictionary<string, object> {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["source"] = sourceName,
                ["requestedTarget"] = owner.target, ["radiusMeters"] = radius,
                ["focused"] = Application.isFocused, ["canAct"] = canAct,
                ["pluginEnabled"] = (__instance as Behaviour)?.isActiveAndEnabled,
                ["ready"] = Access.Get(__instance, "_ready"), ["bound"] = Access.Get(__instance, "_bound"),
                ["permitValid"] = Access.Get(Access.Get(__instance, "_permit"), "IsValid"),
                ["roomActive"] = room != null && room.activeInHierarchy,
                ["tracking"] = tracking, ["handPosition"] = hand == null ? null : Access.V(hand.transform.position),
                ["lockBefore"] = LockState(hand), ["selectedByDistance"] = selected?.id,
                ["earlyReturn"] = !canAct ? "CanAct=false" : holds.Contains(source) ? "hand-already-holding" : hand == null ? "hand-missing" : !tracking ? "hand-not-tracking" : selected == null ? "no-collider-in-radius" : null,
                ["props"] = candidates.Where(p => p.id == owner.target || p.id.StartsWith("bed_prop_") || p.id.StartsWith("frilled_pillow_"))
                    .Select(p => new { p.id, p.active, p.alreadyHeld,
                        rootPosition = p.root == null ? null : Access.V(p.root.position),
                        colliders = p.colliders.Select(c => new { c.enabled, c.isTrigger,
                            center = Access.V(c.bounds.center), size = Access.V(c.bounds.size),
                            distanceMeters = hand == null ? (float?)null : Vector3.Distance(hand.transform.position, c.ClosestPoint(hand.transform.position)) }).ToArray(),
                        rendererBounds = p.root == null ? null : p.root.GetComponentsInChildren<Renderer>(true)
                            .Select(r => new { r.name, r.enabled, active = r.gameObject.activeInHierarchy,
                                center = Access.V(r.bounds.center), size = Access.V(r.bounds.size),
                                boundsDistanceMeters = hand == null ? (float?)null : Vector3.Distance(hand.transform.position, r.bounds.ClosestPoint(hand.transform.position)) }).ToArray()
                    }).ToArray()
            };
            owner.last = owner.capture;
        }
        catch (Exception ex) { owner.last = new { error = ex.GetBaseException().Message }; }
    }
    static Exception After(object __instance, object[] __args, bool __state, Exception __exception)
    {
        var owner = active;
        if (!__state || owner == null || owner.last == null || !owner.Enabled) return __exception;
        try
        {
            if (owner.capture != null)
            {
                var holds = (IDictionary)Access.Get(__instance, "_holds");
                var hold = holds.Contains(__args[1]) ? holds[__args[1]] : null;
                owner.capture["heldAfter"] = Access.Get(Access.Get(hold, "Prop"), "Id");
                owner.capture["lockAfter"] = LockState(owner.capturedHand);
                owner.capture["exception"] = __exception?.ToString();
                owner.capture["lastErrorAfter"] = Access.Get(__instance, "LastError");
            }
        }
        catch (Exception ex) { if (owner.capture != null) owner.capture["diagnosticErrorAfter"] = ex.GetBaseException().Message; }
        var sync = SynchronizationContext.Current;
        if (sync != null) sync.Post(_ => owner.Stop(), null); else owner.Stop();
        return __exception;
    }
    void Stop()
    {
        patch?.UnpatchSelf(); patch = null;
        if (changed != null) setting.GetType().GetEvent("SettingChanged").RemoveEventHandler(setting, changed);
        changed = null;
        if (ReferenceEquals(active, this)) active = null;
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext current)
    {
        if (ReflectionCommands.Text(args, "action", "result") != "arm") return new { armed = patch != null, attempt = last };
        Stop();
        var type = ReflectionCommands.ResolveType("MainGameHotelRoomProps.Plugin");
        var plugin = Resources.FindObjectsOfTypeAll(type).Cast<Component>().Single();
        setting = Access.Get(plugin, "_diagnostics");
        if (!Enabled) throw new InvalidOperationException("Enable Hotel Room Props Diagnostics.Enabled to arm one grip attempt.");
        context = current; last = null; capture = null; capturedHand = null; active = this;
        target = ReflectionCommands.Text(args, "target", "");
        changed = (sender, e) => { if (!Enabled) Stop(); };
        setting.GetType().GetEvent("SettingChanged").AddEventHandler(setting, changed);
        patch = new Harmony("canon.development.hotel-one-grip");
        patch.Patch(AccessTools.Method(type, "GripDown"), prefix: new HarmonyMethod(typeof(HotelGripAttemptProbe), nameof(Before)),
            finalizer: new HarmonyMethod(typeof(HotelGripAttemptProbe), nameof(After)));
        return new { armed = true, target, boundary = "one grip edge before/after, then detach; renderer distances are bounds distances" };
    }
    public void Dispose() => Stop();
}
