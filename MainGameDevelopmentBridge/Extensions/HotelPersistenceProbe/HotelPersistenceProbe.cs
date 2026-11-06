using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using CanonPlugins.Shared;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelPersistenceProbe : IDevelopmentCommand
{
    [Serializable] private sealed class Motion { public bool running; public float phase; public Vector3 offset; }
    public string Name => "hotel-persistence";
    private object _snapshot;
    private IEnumerator SampleAfterFrame(Func<object> sample)
    {
        yield return new WaitForEndOfFrame();
        try { _snapshot = sample(); }
        catch (Exception ex) { _snapshot = new { error = ex.GetBaseException().Message }; }
    }
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Flags)?.GetValue(obj) ?? obj.GetType().GetProperty(name, Flags)?.GetValue(obj, null);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(obj, args);
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        string action = ReflectionCommands.Text(args, "action", "snapshot");
        if (action == "result") return _snapshot;
        if (action == "codec")
        {
            var pose = new GadgetRoomPose { position = Vector3.one, scale = Vector3.one, rotation = Quaternion.identity, worldScale = true };
            var record = new GadgetRoomSnapshot { visible = true, poses = new[] { pose, pose } };
            string encoded = record.Encode();
            var decoded = GadgetRoomSnapshot.Decode(encoded);
            var settings = GadgetConfigSnapshot.Capture(Chainloader.PluginInfos["com.kks.maingame.iphonecamera"].Instance.Config);
            return new { encoded, count = decoded.poses.Length, position = decoded.poses.First().position.ToString("R"), settings };
        }
        var gateType = ReflectionCommands.ResolveType("CanonGate.GateApi");
        var gate = gateType.GetProperty("Current", Flags).GetValue(null, null);
        if (gate == null || Convert.ToString(Get(gate, "HotelRoomId")) != "201") throw new InvalidOperationException("Probe is restricted to hotel room 201.");
        var plugin = Chainloader.PluginInfos["com.kks.maingame.aishoujomode"].Instance;
        var hotel = Get(plugin, "_hotel");
        var session = Get(hotel, "_hSession");
        if (!Equals(Get(session, "RoomReady"), true)) throw new InvalidOperationException("Room 201 is not ready.");
        var proc = (HSceneProc)Get(session, "_proc");
        var actor = ((IEnumerable)Get(proc, "lstFemale")).Cast<ChaControl>().First(a => a != null);
        var effectsType = ReflectionCommands.ResolveType("MainGameHScenePoseInjector.Plugin");
        var skinType = ReflectionCommands.ResolveType("KK_SkinEffects.SkinEffectsController");
        var skin = actor.GetComponent(skinType);
        if (action == "set")
        {
            if (args.TryGetValue("coordinate", out var coordinate))
                if (!actor.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)Convert.ToInt32(coordinate)))
                    throw new InvalidOperationException("Test outfit could not be loaded.");
            var values = (Dictionary<string, object>)args["values"];
            foreach (var pair in values)
            {
                var target = proc.flags.GetType().GetField(pair.Key, Flags) != null ? (object)proc.flags : actor.fileStatus;
                var field = target.GetType().GetField(pair.Key, Flags);
                var property = target.GetType().GetProperty(pair.Key, Flags);
                var type = field?.FieldType ?? property?.PropertyType ?? throw new MissingMemberException(pair.Key);
                var value = Convert.ChangeType(pair.Value, type);
                if (field != null) field.SetValue(target, value); else property.SetValue(target, value, null);
            }
            if (values.TryGetValue("tearsLv", out var tears))
                effectsType.GetMethod("SetCharacterEffect", Flags).Invoke(null, new object[] { actor, Convert.ToInt32(args["tearChannel"]), Convert.ToInt32(tears) });
            if (args.TryGetValue("modValues", out var modValues))
                foreach (var pair in (Dictionary<string, object>)modValues)
                    skinType.GetProperty(pair.Key, Flags).SetValue(skin, Convert.ToInt32(pair.Value), null);
            if (args.TryGetValue("placementYaw", out var yaw))
            {
                var root = (Transform)Get(hotel, "_sessionRoomRoot");
                var placement = Call(session, "ReadRoomPlacement", root);
                placement.GetType().GetField("yaw").SetValue(placement, Convert.ToSingle(yaw));
                Call(hotel, "ApplyRoomPlacement", placement);
            }
            if (args.TryGetValue("siru", out var liquid))
                foreach (ChaFileDefine.SiruParts part in Enum.GetValues(typeof(ChaFileDefine.SiruParts))) actor.SetSiruFlags(part, Convert.ToByte(liquid));
            if (args.TryGetValue("siru", out _)) Call(actor, "UpdateSiru", true);
        }
        if (action == "save") return Call(hotel, "SaveRoomSnapshot", "development-test", null, null);
        if (action == "leave") { proc.flags.numEnd = default(int); proc.flags.isHSceneEnd = true; return "Room 201 native exit requested"; }
        object Sample()
        {
        var room = (Transform)Get(hotel, "_sessionRoomRoot");
        var gadgets = new List<object>();
        foreach (string guid in new[] { "com.kks.maingame.vrdildo", "com.kks.maingame.iphonecamera" })
        {
            try {
                var provider = Chainloader.PluginInfos[guid].Instance;
                if (action == "set" && args.TryGetValue("spawn", out var spawn) && Convert.ToBoolean(spawn))
                    Call(provider, "SetHotelGadgetVisible", true);
                string json = (string)Call(provider, "CaptureHotelGadget", room);
                var data = GadgetRoomSnapshot.Decode(json);
                if (action == "set" && args.TryGetValue("offsetX", out var offset))
                {
                    foreach (var pose in data.poses) pose.position.x += Convert.ToSingle(offset);
                    if (guid == "com.kks.maingame.vrdildo" && args.TryGetValue("pistonSpeed", out var speed))
                    {
                        var settingsType = Get(provider, "_settings").GetType();
                        var settings = JsonUtility.FromJson(data.settings, settingsType);
                        settingsType.GetField("AutoTarget", Flags).SetValue(settings, false);
                        settingsType.GetField("PistonSpeed", Flags).SetValue(settings, Convert.ToSingle(speed));
                        data.settings = JsonUtility.ToJson(settings);
                        data.runtime = JsonUtility.ToJson(new Motion { running = true });
                    }
                    Call(provider, "RestoreHotelGadget", room, data.Encode());
                    json = (string)Call(provider, "CaptureHotelGadget", room);
                    data = GadgetRoomSnapshot.Decode(json);
                }
                gadgets.Add(new { guid, data.visible, count = data.poses.Length, json });
            } catch (Exception ex) { gadgets.Add(new { guid, error = ex.GetBaseException().Message }); }
        }
        var status = actor.fileStatus.GetType().GetProperties(Flags).Where(p => p.CanRead && p.GetIndexParameters().Length == default(int))
            .ToDictionary(p => p.Name, p => Scalar(p.GetValue(actor.fileStatus, null)));
        var mod = new[] { "TearLevel", "DroolLevel", "SalivaLevel", "SweatLevel", "BlushLevel", "BukkakeLevel", "AnalBukkakeLevel", "CumInNoseLevel", "ButtLevel", "PussyJuiceLevel", "BloodLevel" }
            .ToDictionary(key => key, key => skin == null ? null : Get(skin, key));
        var hFlags = new[] { "gaugeFemale", "lockGugeFemale", "speed", "speedItem", "speedCalc", "motion", "motion1" }.ToDictionary(key => key, key => Get(proc.flags, key));
        return new { room = "201", status, mod, hFlags, gaugeFemale = proc.flags.gaugeFemale, selection = Call(session, "ReadRoomSelection"),
            placement = Call(session, "ReadRoomPlacement", room), gadgets };
        }
        if (action == "snapshot")
        {
            _snapshot = null;
            ((MonoBehaviour)hotel).StartCoroutine(SampleAfterFrame(Sample));
            return new { pending = true };
        }
        return Sample();
    }
    private static object Scalar(object value) => value is Array array ? array.Cast<object>().Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture)).ToArray()
        : value == null ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    public void Dispose() { }
}
