using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelControlledGrip : IDevelopmentCommand
{
    public string Name => "hotel-grip-controlled-test";
    static object testing;
    Harmony patch;

    // Override only the desktop-focus term, only during this synchronous command.
    static bool Permission(object __instance, ref bool __result)
    {
        if (!ReferenceEquals(testing, __instance)) return true;
        var room = Access.Get(__instance, "_room") as GameObject;
        __result = (__instance as Behaviour).isActiveAndEnabled && (bool)Access.Get(__instance, "_ready") &&
            (bool)Access.Get(Access.Get(__instance, "_permit"), "IsValid") && room != null && room.activeInHierarchy;
        return false;
    }

    static object Selected(object[] props, IDictionary holds, Vector3 point, float radius)
    {
        return props.Where(p => (string)Access.Get(p, "Id") != "bed_furniture" &&
                ((Transform)Access.Get(p, "Root")).gameObject.activeInHierarchy &&
                !holds.Values.Cast<object>().Any(h => ReferenceEquals(Access.Get(h, "Prop"), p)))
            .SelectMany(p => ((Collider[])Access.Get(p, "Colliders")).Where(c => c != null && c.enabled && !c.isTrigger)
                .Select(c => new { prop = p, distance = (c.ClosestPoint(point) - point).sqrMagnitude, volume = c.bounds.size.sqrMagnitude }))
            .Where(v => v.distance <= radius * radius).OrderBy(v => v.distance).ThenBy(v => v.volume).FirstOrDefault()?.prop;
    }

    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        var type = ReflectionCommands.ResolveType("MainGameHotelRoomProps.Plugin");
        var plugin = Resources.FindObjectsOfTypeAll(type).Cast<Component>().Single();
        var setting = Access.Get(plugin, "_diagnostics");
        if (!(bool)Access.Get(setting, "Value")) throw new InvalidOperationException("Diagnostics.Enabled is required.");
        var holds = (IDictionary)Access.Get(plugin, "_holds");
        if (holds.Count != default(int)) throw new InvalidOperationException("A prop is currently held; do not disturb it.");
        var props = ((IEnumerable)Access.Get(plugin, "_props")).Cast<object>().ToArray();
        var targets = props.Where(p => {
            var id = (string)Access.Get(p, "Id");
            return id.StartsWith("bed_prop_") || id.StartsWith("frilled_pillow_");
        }).ToArray();
        var radius = (float)Access.Get(Access.Get(plugin, "_radius"), "Value");
        var mode = Access.Get(ReflectionCommands.ResolveType("VRGIN.Core.VR"), "Mode");
        var down = AccessTools.Method(type, "GripDown");
        var release = AccessTools.Method(type, "Release");
        var sourceType = down.GetParameters().Last().ParameterType;
        var rows = new List<object>();
        bool originalCanAct = (bool)Access.Get(plugin, "CanAct");
        patch = new Harmony("canon.development.hotel-controlled-grip");
        patch.Patch(AccessTools.PropertyGetter(type, "CanAct"), prefix: new HarmonyMethod(typeof(HotelControlledGrip), nameof(Permission)));
        try
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var hand = Access.Get(mode, side) as Component;
                var currentLock = Access.Get(hand, "_Lock");
                if (hand == null || !(bool)Access.Get(hand, "IsTracking") ||
                    (currentLock != null && (bool)Access.Get(currentLock, "IsValid")))
                {
                    rows.Add(new { side, skipped = "hand not tracked or already owned" });
                    continue;
                }
                var source = Enum.Parse(sourceType, side + "Hand");
                var handPosition = hand.transform.localPosition;
                var handRotation = hand.transform.localRotation;
                try
                {
                    foreach (var prop in targets)
                    {
                        var root = (Transform)Access.Get(prop, "Root");
                        var parent = root.parent;
                        var position = root.localPosition;
                        var rotation = root.localRotation;
                        var scale = root.localScale;
                        var sibling = root.GetSiblingIndex();
                        var colliders = (Collider[])Access.Get(prop, "Colliders");
                        var states = colliders.Select(c => c != null && c.enabled).ToArray();
                        var bounds = colliders.First(c => c != null && c.enabled && !c.isTrigger).bounds;
                        var points = from x in new[] { bounds.center.x, bounds.min.x, bounds.max.x }
                                     from y in new[] { bounds.center.y, bounds.min.y, bounds.max.y }
                                     from z in new[] { bounds.center.z, bounds.min.z, bounds.max.z }
                                     select new Vector3(x, y, z);
                        var point = points.Where(p => ReferenceEquals(Selected(props, holds, p, radius), prop)).Select(p => (Vector3?)p).FirstOrDefault();
                        if (!point.HasValue)
                        {
                            rows.Add(new { side, id = Access.Get(prop, "Id"), skipped = "none of sampled bounds points selects this prop" });
                            continue;
                        }
                        object focus = null;
                        try
                        {
                            hand.transform.position = point.Value;
                            testing = null;
                            down.Invoke(plugin, new[] { null, source });
                            var baselineHold = holds.Contains(source) ? holds[source] : null;
                            focus = Access.Get(baselineHold, "Focus");
                            bool baseline = ReferenceEquals(Access.Get(baselineHold, "Prop"), prop);
                            if (baselineHold != null) release.Invoke(plugin, new[] { source, (object)false });
                            if (focus != null && (bool)Access.Get(focus, "IsValid")) AccessTools.Method(focus.GetType(), "Release").Invoke(focus, null);
                            focus = null;
                            testing = plugin;
                            down.Invoke(plugin, new[] { null, source });
                            var hold = holds.Contains(source) ? holds[source] : null;
                            focus = Access.Get(hold, "Focus");
                            rows.Add(new { side, id = Access.Get(prop, "Id"), point = Access.V(point.Value), baselineHeld = baseline,
                                desktopFocusBypassHeld = ReferenceEquals(Access.Get(hold, "Prop"), prop),
                                parentIsHand = root.parent == hand.transform, error = Access.Get(plugin, "LastError") });
                        }
                        finally
                        {
                            testing = null;
                            try { if (holds.Contains(source)) release.Invoke(plugin, new[] { source, (object)false }); }
                            finally
                            {
                                if (focus != null && (bool)Access.Get(focus, "IsValid")) AccessTools.Method(focus.GetType(), "Release").Invoke(focus, null);
                                root.SetParent(parent, false);
                                root.localPosition = position; root.localRotation = rotation; root.localScale = scale; root.SetSiblingIndex(sibling);
                                foreach (var c in colliders.Select((value, index) => new { value, index })) if (c.value != null) c.value.enabled = states[c.index];
                                Physics.SyncTransforms();
                            }
                        }
                    }
                }
                finally { hand.transform.localPosition = handPosition; hand.transform.localRotation = handRotation; }
            }
        }
        finally { testing = null; Dispose(); }
        return new { synthetic = true, actualFocused = Application.isFocused, originalCanAct, targets = targets.Length, holdsAfter = holds.Count, results = rows };
    }
    public void Dispose() { patch?.UnpatchSelf(); patch = null; }
}
