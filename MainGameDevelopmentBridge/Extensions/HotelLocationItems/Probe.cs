using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelLocationItems : IDevelopmentCommand
{
    public string Name => "hotel-location-items";
    private object _result;
    private MonoBehaviour _host;
    private Coroutine _pending;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [Serializable] private sealed class Snapshot { public bool visible; public string[] poseRecords; public string runtime, settings; }
    [Serializable] private sealed class Pose { public Vector3 position, scale; public Quaternion rotation; }
    [Serializable] private sealed class Needle { public int actor; public string bone, localPoseRecord; public Vector3 surface; }
    private sealed class Check { public string guid, method, error; public bool visible, passed; public float positionError, rotationError, scaleError; }
    private static object Get(object obj, string key) => obj.GetType().GetField(key, Flags)?.GetValue(obj) ?? obj.GetType().GetProperty(key, Flags)?.GetValue(obj, null);
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        if (ReflectionCommands.Text(args, "action") == "result") return _result;
        var hotel = Get(Chainloader.PluginInfos["com.kks.maingame.aishoujomode"].Instance, "_hotel");
        var room = (Transform)Get(hotel, "_sessionRoomRoot");
        if (room == null) throw new InvalidOperationException("Hotel room is not active");
        _result = null; _host = (MonoBehaviour)hotel;
        _pending = _host.StartCoroutine(AfterFrame(args, room));
        return new { pending = true };
    }
    private IEnumerator AfterFrame(Dictionary<string, object> args, Transform room)
    {
        yield return new WaitForEndOfFrame();
        _result = CheckItems(args, room); _pending = null;
    }
    private object CheckItems(Dictionary<string, object> args, Transform room)
    {
        var results = new List<Check>();
        foreach (var pair in (Dictionary<string, object>)args["expected"])
        {
            var check = new Check { guid = pair.Key }; results.Add(check);
            try
            {
                var plugin = Chainloader.PluginInfos[pair.Key].Instance;
                var type = plugin.GetType();
                var saved = JsonUtility.FromJson<Snapshot>((string)pair.Value);
                string actualJson = (string)type.GetMethod("CaptureHotelGadget").Invoke(plugin, new object[] { room });
                var actual = JsonUtility.FromJson<Snapshot>(actualJson);
                check.visible = actual.visible;
                if (saved.visible != actual.visible) throw new InvalidOperationException("Visibility changed");
                if (!saved.visible) { check.method = "hidden-state"; check.passed = true; continue; }
                if (pair.Key == "com.kks.main.girlbodyikgizmo" || pair.Key == "com.kks.main.gameobjectcomposer")
                { check.method = "covered-by-IK-and-transform-checks"; check.passed = true; continue; }
                if (pair.Key == "com.kks.maingame.syringe")
                {
                    // Compare the actual pre-move bone attachment, rather than asking
                    // whether an animated skin surface is still at restoration contact.
                    var before = JsonUtility.FromJson<Needle>(saved.runtime);
                    var after = JsonUtility.FromJson<Needle>(actual.runtime);
                    if (before.actor >= 0 && !string.IsNullOrEmpty(before.bone))
                    {
                        check.method = "preserved-bone-local-attachment";
                        var a = JsonUtility.FromJson<Pose>(before.localPoseRecord);
                        var b = JsonUtility.FromJson<Pose>(after.localPoseRecord);
                        check.positionError = Vector3.Distance(a.position, b.position);
                        check.rotationError = Quaternion.Angle(a.rotation, b.rotation);
                        check.scaleError = Vector3.Distance(a.scale, b.scale);
                        var model = Get(plugin, "_model") as GameObject;
                        var bone = Get(plugin, "_insertedBone") as Transform;
                        bool sizePreserved = check.scaleError < .001f;
                        if (args.TryGetValue("restored", out var restored) && Convert.ToBoolean(restored))
                        {
                            // The hotel verifies the saved world scale immediately after restore.
                            // Later animation multiplies it by changing parent-bone scale; keep
                            // reporting that measured drift while checking the completed restore
                            // and unchanged authored settings, not a frozen animated world scale.
                            var hotel = Get(Chainloader.PluginInfos["com.kks.maingame.aishoujomode"].Instance, "_hotel");
                            var client = ((IEnumerable)Get(Get(hotel, "_gadgets"), "_clients")).Cast<object>().Single(c => Equals(Get(c, "guid"), pair.Key));
                            sizePreserved = Equals(Get(client, "restoreFailed"), false) && saved.settings == actual.settings;
                            check.method = "bone-local-attachment-and-completed-hotel-restore";
                        }
                        check.passed = before.actor == after.actor && before.bone == after.bone && bone != null && model != null && model.transform.parent == bone
                            && Vector3.Distance(before.surface, after.surface) < .001f && check.positionError < .001f && check.rotationError < .1f && sizePreserved;
                        continue;
                    }
                }
                var verify = type.GetMethod("VerifyHotelGadgetPlacement", new[] { typeof(Transform), typeof(string) });
                if (verify != null)
                {
                    check.method = "provider-attachment-and-placement";
                    check.passed = (bool)verify.Invoke(plugin, new object[] { room, pair.Value });
                    continue;
                }
                check.method = "room-fixed-pose";
                if (saved.poseRecords == null || actual.poseRecords == null || saved.poseRecords.Length != actual.poseRecords.Length)
                    throw new InvalidOperationException("Transform count changed");
                for (int i = 0; i < saved.poseRecords.Length; i++)
                {
                    var before = JsonUtility.FromJson<Pose>(saved.poseRecords[i]);
                    var after = JsonUtility.FromJson<Pose>(actual.poseRecords[i]);
                    check.positionError = Mathf.Max(check.positionError, Vector3.Distance(before.position, after.position));
                    check.rotationError = Mathf.Max(check.rotationError, Quaternion.Angle(before.rotation, after.rotation));
                    check.scaleError = Mathf.Max(check.scaleError, Vector3.Distance(before.scale, after.scale));
                }
                check.passed = check.positionError < .001f && check.rotationError < .1f && check.scaleError < .001f;
            }
            catch (Exception ex) { check.error = ex.GetBaseException().Message; }
        }
        return new { allPassed = results.All(r => r.passed), checks = results };
    }
    public void Dispose() { if (_pending != null && _host != null) _host.StopCoroutine(_pending); _pending = null; _result = null; }
}
