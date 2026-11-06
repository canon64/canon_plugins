using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

public sealed partial class HotelPoseProbe
{
    private Vector3 _placeOriginalPosition, _placeOriginalRotation;
    private object _placeOriginalDocument;
    private Dictionary<string, string> _placeOriginalGadgets;
    private bool _placeStarted;
    private sealed class MeasuredTransform
    {
        internal string Kind, Name;
        internal Transform Transform;
        internal Vector3 Position, Scale;
        internal Quaternion Rotation;
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static Transform Female(object session) => ((IEnumerable)Call(session, "RoomActors")).Cast<Component>().First(a => a.name.StartsWith("chaF_")).transform;
    private static Dictionary<string, string> GadgetPayloads(object hotel, Transform room)
    {
        var result = new Dictionary<string, string>();
        foreach (var client in ((IEnumerable)Get(Get(hotel, "_gadgets"), "_clients")).Cast<object>())
        {
            var plugin = Get(client, "plugin"); var capture = Get(client, "capture") as MethodInfo;
            var allowed = Get(client, "allowed") as PropertyInfo;
            if (plugin == null || capture == null || allowed == null || !Equals(allowed.GetValue(plugin, null), true)) continue;
            result.Add((string)Get(client, "guid"), (string)capture.Invoke(plugin, new object[] { room }));
        }
        return result;
    }
    private static List<MeasuredTransform> Measure(object session)
    {
        var result = new List<MeasuredTransform>(); var seen = new HashSet<Transform>();
        void Add(string kind, Transform t)
        {
            if (t == null || !seen.Add(t)) return;
            result.Add(new MeasuredTransform { Kind = kind, Name = t.name, Transform = t, Position = t.position, Rotation = t.rotation, Scale = t.lossyScale });
        }
        foreach (var actor in ((IEnumerable)Call(session, "RoomActors")).Cast<Component>())
            foreach (var transform in actor.GetComponentsInChildren<Transform>(true)) Add("actor", transform);
        var composer = Provider("com.kks.main.gameobjectcomposer");
        foreach (DictionaryEntry entry in (IDictionary)Get(composer, "_runtimeObjects"))
            foreach (var transform in ((GameObject)Get(entry.Value, "GameObject")).GetComponentsInChildren<Transform>(true)) Add("composer", transform);
        var hip = Provider("com.kks.main.girlbodyikgizmo");
        int index = 0;
        foreach (var state in ((IEnumerable)Get(hip, "_bikEff")).Cast<object>())
        {
            if (Equals(Get(state, "Running"), true) && !Equals(Get(state, "ExternalOwner"), true) && Get(state, "FollowBone") == null)
                Add("independent-ik-" + index, Get(state, "Proxy") as Transform);
            index++;
        }
        return result;
    }
    private object LocationAction(string action, Dictionary<string, object> args, object hotel, object session)
    {
        var map = Provider("com.kks.maingameblankmapadd");
        var room = (Transform)Get(hotel, "_sessionRoomRoot");
        var female = Female(session);
        if (action == "place-begin")
        {
            if (Get(map, "_hPlaceDraft") != null) throw new InvalidOperationException("An H-place edit is already open.");
            _placeOriginalPosition = room.InverseTransformPoint(female.position);
            _placeOriginalRotation = (Quaternion.Inverse(room.rotation) * female.rotation).eulerAngles;
            _placeOriginalDocument = Call(Get(map, "_categoryLocations"), "ForSave");
            _placeOriginalGadgets = GadgetPayloads(hotel, room);
            _placeStarted = true;
            return new { providers = _placeOriginalGadgets.Keys.ToArray(), position = _placeOriginalPosition.ToString("R"), rotation = _placeOriginalRotation.ToString("R") };
        }
        if (!_placeStarted) throw new InvalidOperationException("Call place-begin before modifying the place.");
        if (action == "place-save") { Call(map, "SaveCategoryLocation"); return Get(map, "_hPlaceStatus"); }
        if (action == "place-sample")
        {
            _result = null;
            ((MonoBehaviour)hotel).StartCoroutine(LocationSample(hotel, session, room));
            return new { pending = true };
        }
        if (action != "place-move" && action != "place-restore") throw new ArgumentException("Unknown place action");
        if (Get(map, "_hPlaceDraft") == null) Call(map, "BeginCategoryLocationEdit");
        var draft = Get(map, "_hPlaceDraft") ?? throw new InvalidOperationException("H-place edit did not start.");
        var position = _placeOriginalPosition;
        var rotation = _placeOriginalRotation;
        if (action == "place-move")
        {
            float Number(string key) => args.TryGetValue(key, out var value) ? Convert.ToSingle(value) : 0f;
            position += new Vector3(Number("x"), Number("y"), Number("z")); rotation.y += Number("yaw");
        }
        Set(draft, "Position", new[] { position.x, position.y, position.z });
        Set(draft, "Center", new[] { position.x, position.y, position.z });
        Set(draft, "Rotation", new[] { rotation.x, rotation.y, rotation.z });
        Call(map, "SyncHPlaceEditNumbers");
        var measured = Measure(session);
        Vector3 before = female.position; Quaternion beforeRotation = female.rotation;
        Call(map, "PreviewHPlaceEdit");
        var delta = female.rotation * Quaternion.Inverse(beforeRotation);
        var rows = measured.Select(m => new { kind = m.Kind, name = m.Name,
            positionError = Vector3.Distance(m.Transform.position, female.position + delta * (m.Position - before)),
            rotationError = Quaternion.Angle(m.Transform.rotation, delta * m.Rotation), scaleError = Vector3.Distance(m.Transform.lossyScale, m.Scale) }).ToArray();
        if (action == "place-restore")
        {
            Call(map, "SaveCategoryLocation");
            Call(map, "SaveHLocationPoses", _placeOriginalDocument);
            Set(map, "_categoryLocations", _placeOriginalDocument);
            foreach (var pair in _placeOriginalGadgets)
            {
                var provider = Provider(pair.Key);
                Call(provider, "RestoreHotelGadget", room, pair.Value);
            }
        }
        return new { action, actorMove = Vector3.Distance(before, female.position), actorRotation = Quaternion.Angle(beforeRotation, female.rotation),
            checkedTransforms = rows.Length, failures = rows.Where(r => r.positionError > 0.001f || r.rotationError > 0.1f || r.scaleError > 0.001f).ToArray(),
            maxPositionError = rows.Length == 0 ? 0f : rows.Max(r => r.positionError) };
    }
    private IEnumerator LocationSample(object hotel, object session, Transform room)
    {
        yield return new WaitForEndOfFrame();
        _result = Safe(() => {
            var hip = Provider("com.kks.main.girlbodyikgizmo");
            var settings = Get(hip, "_settings");
            var states = ((IEnumerable)Get(hip, "_bikEff")).Cast<object>().Select((state, index) => {
                var proxy = Get(state, "Proxy") as Transform;
                var follow = Get(state, "FollowBone") as Transform;
                bool running = Equals(Get(state, "Running"), true);
                var correction = (Vector3)Call(hip, "ReadVibeAppliedOffset", index);
                var target = running && follow != null ? (Vector3)Call(hip, "GetFollowTargetPosition", index) : Vector3.zero;
                return new { index, running, external = Get(state, "ExternalOwner"),
                    weight = Call(hip, "GetBodyIKWeight", index), follow = follow?.name,
                    followError = running && proxy != null && follow != null ? (float?)Vector3.Distance(proxy.position - correction, target) : null,
                    actorRelative = proxy == null ? null : Female(session).InverseTransformPoint(proxy.position - correction).ToString("R") };
            }).ToArray();
            var placement = Provider("com.kks.maingame.hlocationplacement");
            return new { frame = Time.frameCount, placementStatus = Get(placement, "Status"), placementConnected = Get(placement, "Session") != null,
                selection = Call(session, "ReadRoomSelection"), placement = Call(session, "ReadRoomPlacement", room),
                states, providers = GadgetPayloads(hotel, room), settings = Fields(settings, "FemaleHeadAngleEnabled", "FemaleHeadAngleX", "FemaleHeadAngleY", "FemaleHeadAngleZ", "FemaleLeftHandPoseEnabled", "FemaleLeftHandPoseWeight") };
        });
    }
}
