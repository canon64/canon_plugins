using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelItemsProbe : IDevelopmentCommand
{
    public string Name => "hotel-items-roundtrip";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object Read(object obj, string name) => obj.GetType().GetField(name, Flags)?.GetValue(obj) ?? obj.GetType().GetProperty(name, Flags)?.GetValue(obj, null);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    [Serializable] public sealed class Pose { public Vector3 position, scale; public Quaternion rotation; public bool worldScale; }
    [Serializable] public sealed class Snapshot
    {
        public string schema, settings, runtime;
        public bool visible, collarAttached, rootAttached, tipPinned;
        public Vector3 tipPosition;
        public Pose[] poses;
        public Vector3[] points;
    }
    [Serializable] public sealed class Motion { public bool running; public float phase; public Vector3 offset; }
    [Serializable] public sealed class Composer { public string layout, preset; }
    [Serializable] public sealed class Layout { public string format = "ObjectLayoutV2"; public string[] objectsJson; }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var hotelPlugin = Chainloader.PluginInfos["com.kks.maingame.aishoujomode"].Instance;
        var hotel = Read(hotelPlugin, "_hotel");
        var h = Read(hotel, "_hSession");
        if (h == null || !Equals(Read(h, "RoomReady"), true)) throw new InvalidOperationException("Hotel H is not ready");
        var room = (Transform)Read(hotel, "_sessionRoomRoot");
        var providers = Chainloader.PluginInfos.Values.Select(p => p.Instance).Where(p => p != null && p.GetType().GetMethod("CaptureHotelGadget") != null).ToArray();
        var results = new List<object>();
        foreach (var provider in providers)
        {
            string guid = provider.Info.Metadata.GUID;
            string before = null;
            try
            {
                if (!Equals(Read(provider, "HotelGadgetAllowed"), true)) throw new InvalidOperationException("Provider not allowed");
                before = (string)Call(provider, "CaptureHotelGadget", room);
                Call(provider, "SetHotelGadgetVisible", true);
                var expected = JsonUtility.FromJson<Snapshot>((string)Call(provider, "CaptureHotelGadget", room));
                if (!expected.visible) { results.Add(new { guid, passed = true, empty = true, parts = 0 }); continue; }
                bool composer = guid == "com.kks.main.gameobjectcomposer";
                if (composer)
                {
                    expected.runtime = JsonUtility.ToJson(new Composer { layout = JsonUtility.ToJson(new Layout { objectsJson = new[] {
                        "{\"id\":\"hotel-probe-a\",\"name\":\"probe A\",\"primitive\":0,\"parentKind\":\"root\",\"localPosition\":{\"x\":0.1,\"y\":1.1,\"z\":0.2},\"localScale\":{\"x\":0.05,\"y\":0.05,\"z\":0.05},\"visible\":true}",
                        "{\"id\":\"hotel-probe-b\",\"name\":\"probe B\",\"primitive\":0,\"parentKind\":\"managed\",\"parentRefId\":\"hotel-probe-a\",\"localPosition\":{\"x\":0.2,\"y\":0.1,\"z\":0.3},\"localScale\":{\"x\":0.03,\"y\":0.03,\"z\":0.03},\"visible\":true}" } }) });
                }
                // Move every part separately; this catches first-item-only restoration.
                for (int i = 0; i < expected.poses.Length; ++i)
                    expected.poses[i].position += new Vector3(.173f, .041f, -.117f);
                bool piston = guid == "com.kks.maingame.vrdildo";
                if (piston)
                {
                    var settings = JsonUtility.FromJson(expected.settings, Read(provider, "_settings").GetType());
                    settings.GetType().GetField("PistonSpeed").SetValue(settings, 1.7f);
                    settings.GetType().GetField("PistonStrokeCm").SetValue(settings, 8.4f);
                    expected.settings = JsonUtility.ToJson(settings);
                    expected.runtime = JsonUtility.ToJson(new Motion { running = true, phase = .37f, offset = new Vector3(0, .023f, 0) });
                }
                string saved = JsonUtility.ToJson(expected);
                // Recreate rather than applying over the same live object.
                Call(provider, "SetHotelGadgetVisible", false);
                Call(provider, "RestoreHotelGadget", room, saved);
                var actual = JsonUtility.FromJson<Snapshot>((string)Call(provider, "CaptureHotelGadget", room));
                if (!actual.visible || actual.poses.Length != expected.poses.Length) throw new Exception("Visible/part count mismatch");
                if (composer && JsonUtility.FromJson<Layout>(JsonUtility.FromJson<Composer>(actual.runtime).layout).objectsJson.Length != 2)
                    throw new Exception("Composer did not restore every object");
                for (int i = 0; i < expected.poses.Length; ++i)
                {
                    if (Vector3.Distance(expected.poses[i].position, actual.poses[i].position) > .001f
                        || Quaternion.Angle(expected.poses[i].rotation, actual.poses[i].rotation) > .1f
                        || Vector3.Distance(expected.poses[i].scale, actual.poses[i].scale) > .001f)
                        throw new Exception("Part pose mismatch: " + i);
                }
                if (piston)
                {
                    var motion = JsonUtility.FromJson<Motion>(actual.runtime);
                    if (!motion.running || Mathf.Abs(motion.phase - .37f) > .001f) throw new Exception("Piston state/phase did not resume");
                    var settings = Read(provider, "_settings");
                    if (Math.Abs((float)Read(settings, "PistonSpeed") - 1.7f) > .001f || Math.Abs((float)Read(settings, "PistonStrokeCm") - 8.4f) > .001f)
                        throw new Exception("Piston speed/stroke mismatch");
                    var stopped = JsonUtility.FromJson<Motion>(actual.runtime); stopped.running = false;
                    actual.runtime = JsonUtility.ToJson(stopped);
                    Call(provider, "RestoreHotelGadget", room, JsonUtility.ToJson(actual));
                    if (JsonUtility.FromJson<Motion>(JsonUtility.FromJson<Snapshot>((string)Call(provider, "CaptureHotelGadget", room)).runtime).running)
                        throw new Exception("Stopped piston restarted");
                }
                results.Add(new { guid, passed = true, parts = actual.poses.Length, piston });
            }
            catch (Exception ex) { results.Add(new { guid, passed = false, error = ex.GetBaseException().ToString() }); }
            finally
            {
                if (before != null)
                    try { Call(provider, "RestoreHotelGadget", room, before); }
                    catch (Exception ex) { results.Add(new { guid, cleanupError = ex.GetBaseException().ToString() }); }
            }
        }
        return results.ToArray();
    }
    public void Dispose() { }
}
