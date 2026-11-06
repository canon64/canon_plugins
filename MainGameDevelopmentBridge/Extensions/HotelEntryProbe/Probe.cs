using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameDevelopmentBridge;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HotelEntryProbe : IDevelopmentCommand
{
    public string Name => "hotel-entry-state";
    static object Get(object target, string member)
    {
        if (target == null) return null;
        for (var t = target as Type ?? target.GetType(); t != null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var f = t.GetField(member, flags);
            if (f != null) return f.GetValue(target is Type ? null : target);
            var p = t.GetProperty(member, flags);
            if (p != null) return p.GetValue(target is Type ? null : target, null);
        }
        return null;
    }
    static object Operation(object value) => value is AsyncOperation op
        ? new { op.isDone, op.progress, op.allowSceneActivation } : null;
    static object SceneState(Scene s) => new { s.name, s.path, s.handle, s.isLoaded,
        roots = s.isLoaded ? s.GetRootGameObjects().Select(r => new { r.name, active = r.activeInHierarchy }).ToArray() : null };
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static object Map(object map) => map == null ? null : new { root = (Get(map, "mapRoot") as GameObject)?.name,
        loading = Get(map, "isMapLoading"), locked = Get(map, "_isLock"), number = Get(map, "no"),
        reference = map.GetHashCode() };
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var plugin = Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
        if (!(Get(Get(plugin, "_hotelDiagnostics"), "Value") is bool enabled) || !enabled)
            throw new InvalidOperationException("Enable HotelMode.DiagnosticsEnabled for this explicit diagnostic snapshot.");
        var hotel = Get(plugin, "_hotel");
        var h = Get(hotel, "_hSession");
        var fade = Get(hotel, "_fade");
        var nativeFade = Get(fade, "_fade");
        var floors = Get(hotel, "_floors") as IEnumerable;
        var vr = ReflectionCommands.ResolveType("VRGIN.Core.VR");
        var interpreter = Get(vr, "Interpreter");
        return new {
            utc = DateTime.UtcNow.ToString("O"), pid = System.Diagnostics.Process.GetCurrentProcess().Id, frame = Time.frameCount,
            hotel = hotel != null, loading = Get(hotel, "_loading"), opening = Get(hotel, "_directRoomOpening"),
            changing = Get(hotel, "_changingFloor"), stage = Get(hotel, "_roomEntryStage"), failure = Get(hotel, "_roomEntryFailure"),
            floor = Get(Get(Get(hotel, "_floor"), "Definition"), "id"), room = Get(Get(Get(hotel, "_hRoomFloor"), "Definition"), "id"),
            h = h == null ? null : new { busy = Get(h, "Busy"), ready = Get(h, "RoomReady"), proc = context.Reflection.Pack(Get(h, "_proc")),
                managedMap = Map(Get(h, "_map")), actualMap = Map(Get(Get(h, "_proc"), "map")),
                females = (Get(Get(h, "_proc"), "lstFemale") as ICollection)?.Count },
            fade = new { covered = Get(fade, "_covered"), owned = Get(fade, "_owned"), completion = Get(fade, "_completion") != null,
                nativeEnd = Get(nativeFade, "isEnd"), alpha = Get(Get(nativeFade, "canvasGroup"), "alpha") },
            pauseRendering = Get(ReflectionCommands.ResolveType("Valve.VR.SteamVR_Render"), "pauseRendering"),
            interpreter = Get(interpreter, "CurrentScene")?.ToString(),
            floors = floors?.Cast<object>().Select(f => new { id = Get(Get(f, "Definition"), "id"),
                bundlePending = Get(f, "_pendingBundle"), scenePending = Get(f, "_pendingScene"),
                bundleRequest = Operation(Get(f, "_bundleRequest")), sceneRequest = Operation(Get(f, "_sceneRequest")),
                rootsLoaded = Get(f, "Roots") != null }).ToArray(),
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneState(SceneManager.GetSceneAt(i))).ToArray(),
            cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid()).Select(c => new {
                c.name, c.enabled, active = c.gameObject.activeInHierarchy, c.cullingMask, c.stereoEnabled,
                position = V(c.transform.position), angles = V(c.transform.eulerAngles) }).ToArray()
        };
    }
    public void Dispose() { }
}
