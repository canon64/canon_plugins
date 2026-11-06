using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed partial class HotelPoseProbe : IDevelopmentCommand
{
    public string Name => "hotel-pose-probe";
    private object _result;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static object Get(object target, string name)
    {
        if (target == null) return null;
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(target);
            var p = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
            if (p != null) return p.GetValue(target, null);
        }
        throw new MissingMemberException(target.GetType().FullName, name);
    }
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static object Safe(Func<object> read) { try { return read(); } catch (Exception ex) { return new { error = ex.GetBaseException().Message }; } }
    private static object Scalar(object value)
    {
        if (value is Transform t) return new { name = t.name, world = t.position.ToString("R"), local = t.localPosition.ToString("R"), rotation = t.eulerAngles.ToString("R"), parent = t.parent == null ? null : t.parent.name };
        if (value is GameObject go) return Scalar(go.transform);
        if (value is Vector3 v) return v.ToString("R");
        if (value is Quaternion q) return q.ToString("R");
        return value == null || value is string || value.GetType().IsPrimitive ? value : value.ToString();
    }
    private static object Fields(object target, params string[] names) => names.ToDictionary(n => n, n => Safe(() => Scalar(Get(target, n))));
    private static object Provider(string guid) => Chainloader.PluginInfos[guid].Instance;
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        string action = ReflectionCommands.Text(args, "action", "snapshot");
        if (action == "result") return _result;
        var plugin = Provider("com.kks.maingame.aishoujomode");
        if (action == "diagnostics")
        {
            var setting = Get(plugin, "_hotelDiagnostics");
            setting.GetType().GetProperty("Value").SetValue(setting, Convert.ToBoolean(args["enabled"]), null);
            return Get(setting, "Value");
        }
        var hotel = Get(plugin, "_hotel") ?? throw new InvalidOperationException("Hotel is not active.");
        if (action == "enter") { Call(hotel, "StartRoomSession", "201"); return "Room entry requested once"; }
        var session = Get(hotel, "_hSession");
        if (action == "state") return new { hotel = Fields(hotel, "_loading", "_changingFloor", "_roomEntryFailure", "_closing"),
            ready = session != null && Equals(Get(session, "RoomReady"), true), busy = session != null && Equals(Get(session, "Busy"), true) };
        if (session == null || !Equals(Get(session, "RoomReady"), true)) throw new InvalidOperationException("Room is not ready.");
        var proc = Get(session, "_proc");
        if (action.StartsWith("place-", StringComparison.Ordinal)) return LocationAction(action, args, hotel, session);
        if (action == "activate-composer")
        {
            var composer = Provider("com.kks.main.gameobjectcomposer");
            var presetName = ReflectionCommands.Text(args, "preset");
            if (presetName != null)
            {
                var preset = ((IEnumerable)Get(composer, "_presets")).Cast<object>().Single(p => Equals(Get(p, "displayName"), presetName));
                Call(composer, "ActivatePreset", preset);
            }
            else
            {
                composer.GetType().GetField("_lastAutoSwitchKey", Flags).SetValue(composer, null);
                Call(composer, "TickAutoSwitchPreset");
            }
            return Fields(Get(composer, "_activePreset"), "displayName", "key");
        }
        if (action == "leave")
        {
            var flags = Get(proc, "flags");
            flags.GetType().GetField("numEnd", Flags).SetValue(flags, 0);
            flags.GetType().GetField("isHSceneEnd", Flags).SetValue(flags, true);
            return "Native room exit requested once";
        }
        if (action == "save") return Call(hotel, "SaveRoomSnapshot", "development-pose-test", null, null);
        if (action == "verify") return Call(Provider("com.kks.main.girlbodyikgizmo"), "VerifyHotelGadgetState", Get(hotel, "_sessionRoomRoot"), args["payload"]);
        if (action == "restore-hip") { Call(Provider("com.kks.main.girlbodyikgizmo"), "RestoreHotelGadget", Get(hotel, "_sessionRoomRoot"), args["payload"]); return true; }
        if (action == "test-free-point")
        {
            var hip = Provider("com.kks.main.girlbodyikgizmo");
            Call(hip, "SetBodyIK", 2, true, false, "development-persistence-test");
            var point = ((IEnumerable)Get(hip, "_bikEff")).Cast<object>().ElementAt(2);
            var proxy = (Transform)Get(point, "Proxy");
            proxy.position += new Vector3(0.025f, 0.015f, -0.01f);
            Call(hip, "SetBodyIKWeight", 2, 0.75f, false, true);
            return Scalar(proxy);
        }
        if (action == "test-bone-point")
        {
            var hip = Provider("com.kks.main.girlbodyikgizmo");
            var actor = ((IEnumerable)Call(session, "RoomActors")).Cast<Component>().First(a => a.name.StartsWith("chaF_"));
            var target = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "cf_j_spine03");
            Call(hip, "SetBodyIK", 3, true, false, "development-bone-persistence-test");
            var point = ((IEnumerable)Get(hip, "_bikEff")).Cast<object>().ElementAt(3);
            var offset = new Vector3(0.04f, 0.02f, 0.01f);
            point.GetType().GetField("FollowBone", Flags).SetValue(point, target);
            point.GetType().GetField("FollowBonePositionOffset", Flags).SetValue(point, offset);
            point.GetType().GetField("FollowBoneRotationOffset", Flags).SetValue(point, Quaternion.identity);
            Call(hip, "SetBodyIKWeight", 3, 0.6f, false, true);
            return Scalar(target);
        }
        if (action == "test-head")
        {
            var hip = Provider("com.kks.main.girlbodyikgizmo");
            Get(hip, "_settings").GetType().GetField("FemaleHeadAngleEnabled", Flags).SetValue(Get(hip, "_settings"), true);
            Call(hip, "SetFemaleHeadAngleValues", 5f, -7f, 3f);
            Call(hip, "SetFemaleHandPoseEnabled", 0, true, true, false);
            Call(hip, "SetFemaleHandPoseWeight", 0, 0.65f, true, false);
            return Fields(Get(hip, "_settings"), "FemaleHeadAngleEnabled", "FemaleHeadAngleX", "FemaleHeadAngleY", "FemaleHeadAngleZ", "FemaleLeftHandPoseEnabled", "FemaleLeftHandPoseWeight");
        }
        if (action != "snapshot") throw new ArgumentException("Unknown action");
        _result = null;
        ((MonoBehaviour)hotel).StartCoroutine(Sample(hotel, session, proc, ReflectionCommands.Text(args, "savedHipPayload")));
        return new { pending = true };
    }
    private IEnumerator Sample(object hotel, object session, object proc, string savedHipPayload)
    {
        yield return new WaitForEndOfFrame();
        _result = Safe(() =>
        {
            var hip = Provider("com.kks.main.girlbodyikgizmo");
            var composer = Provider("com.kks.main.gameobjectcomposer");
            var root = Get(hotel, "_sessionRoomRoot");
            var actors = ((IEnumerable)Call(session, "RoomActors")).Cast<object>().Select(a => new {
                root = Scalar(Get(a, "transform")), bones = ((Component)a).GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "cf_j_hips" || t.name == "cf_j_waist01" || t.name == "cf_j_root").Select(Scalar).ToArray()
            }).ToArray();
            var effectors = ((IEnumerable)Get(hip, "_bikEff")).Cast<object>().Select((e, i) => {
                var proxy = Get(e, "Proxy") as Transform; var target = Get(e, "FollowBone") as Transform;
                return new { i, state = Fields(e, "Running", "Proxy", "FollowBone", "SuppressConfiguredFollow", "FollowBonePositionOffset", "ExternalOwner"),
                    followDistance = proxy == null || target == null ? (float?)null : Vector3.Distance(proxy.position - (Vector3)Call(hip, "ReadVibeAppliedOffset", i), target.position) };
            }).ToArray();
            var runtime = Get(composer, "_runtime");
            return new { frame = Time.frameCount, room = Scalar(root), selection = Call(session, "ReadRoomSelection"),
                placement = Call(session, "ReadRoomPlacement", root), actors, effectors,
                hip = Fields(hip, "_abandonedByPostureChange", "_pendingAbandonByPostureChange", "_autoPosePendingApply", "_autoPoseLastAppliedPresetId", "_hotelRoomPoseOwned", "_vibeOwnsBodyIk"),
                composer = Fields(composer, "_lastAutoSwitchKey"), composerRoot = Fields(runtime, "Root"),
                composerPayload = Call(composer, "CaptureHotelGadget", root),
                hipPayload = Safe(() => Call(hip, "CaptureHotelGadget", root)),
                savedHipVerified = savedHipPayload == null ? (object)null : Safe(() => Call(hip, "VerifyHotelGadgetState", root, savedHipPayload)),
                hipSettings = Fields(Get(hip, "_settings"), "AutoPoseEnabled", "SpeedHijackEnabled"),
                layout = Call(composer, "SerializeCurrentLayoutSnapshot"), activePreset = Safe(() => Fields(Get(composer, "_activePreset"), "displayName", "key")),
                transforms = ((MonoBehaviour)composer).GetComponentsInChildren<Transform>(true).Take(80).Select(Scalar).ToArray()
            };
        });
    }
    public void Dispose() { _result = null; }
}
