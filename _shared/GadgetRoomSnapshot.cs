using System;
using System.Linq;
using UnityEngine;
using BepInEx.Configuration;

namespace CanonPlugins.Shared
{
    [Serializable] internal sealed class GadgetRoomPose
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public bool worldScale = false;
        internal static GadgetRoomPose Read(Transform room, Transform value) => new GadgetRoomPose
        { position = room.InverseTransformPoint(value.position), rotation = Quaternion.Inverse(room.rotation) * value.rotation, scale = value.lossyScale, worldScale = true };
        internal void Apply(Transform room, Transform target)
        {
            target.SetPositionAndRotation(room.TransformPoint(position), room.rotation * rotation.normalized);
            var parentScale = !worldScale || target.parent == null ? Vector3.one : target.parent.lossyScale;
            target.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }
        internal void Validate()
        {
            foreach (var v in new[] { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w, scale.x, scale.y, scale.z })
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("ガジェットの座標・向きが不正です。");
            float norm = Quaternion.Dot(rotation, rotation);
            if (norm <= float.Epsilon || float.IsInfinity(norm) || scale.x == default(float) || scale.y == default(float) || scale.z == default(float))
                throw new ArgumentException("ガジェットの向き・倍率が不正です。");
        }
    }
    [Serializable] internal sealed class GadgetRoomSnapshot
    {
        public string schema = "v1";
        public bool visible;
        public bool collarAttached = false, rootAttached = false;
        // Optional payloads keep v1 room records readable by newer providers.
        public string settings = null, runtime = null;
        public bool tipPinned = false;
        public Vector3 tipPosition = default(Vector3);
        public GadgetRoomPose[] poses;
        // Unity's runtime serializer can omit arrays of plugin-defined classes.
        // Primitive string arrays carry each pose through that serializer intact.
        public string[] poseRecords;
        public Vector3[] points = Array.Empty<Vector3>();
        internal static GadgetRoomSnapshot Capture(Transform room, params Transform[] roots)
        {
            bool visible = roots != null && roots.All(t => t != null);
            return new GadgetRoomSnapshot { visible = visible, poses = visible ? roots.Select(t => GadgetRoomPose.Read(room, t)).ToArray() : Array.Empty<GadgetRoomPose>() };
        }
        internal void Validate()
        {
            if (schema != "v1") throw new ArgumentException("ガジェットのschemaが不正です：expected=v1 actual=" + (schema ?? "null"));
            if (poses == null) throw new ArgumentException("ガジェットのposes配列が欠損しています。visible=" + visible);
            if (poses.Any(p => p == null)) throw new ArgumentException("ガジェットのposes配列にnullがあります。count=" + poses.Length);
            if (visible && !poses.Any()) throw new ArgumentException("ガジェットは表示中ですが保存座標がありません。visible=true poses=empty");
            foreach (var pose in poses) pose.Validate();
            if (tipPinned && new[] { tipPosition.x, tipPosition.y, tipPosition.z }.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new ArgumentException("鎖先端の保存位置が不正です。");
            foreach (var point in points ?? Array.Empty<Vector3>())
                if (new[] { point.x, point.y, point.z }.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new ArgumentException("鎖の保存座標が不正です。");
        }
        internal string Encode()
        {
            Validate();
            poseRecords = poses.Select(p => JsonUtility.ToJson(p)).ToArray();
            return JsonUtility.ToJson(this);
        }
        internal static GadgetRoomSnapshot Decode(string text)
        {
            var value = JsonUtility.FromJson<GadgetRoomSnapshot>(text);
            if (value == null) throw new ArgumentException("ガジェットの保存データが空です。");
            if (value.poseRecords != null) value.poses = value.poseRecords.Select(p => JsonUtility.FromJson<GadgetRoomPose>(p)).ToArray();
            // Older hidden records contain no transforms and Unity omitted the field.
            if (value.poses == null && !value.visible) value.poses = Array.Empty<GadgetRoomPose>();
            value.Validate(); return value;
        }
    }
    [Serializable] internal sealed class GadgetConfigSnapshot
    {
        [Serializable] public sealed class Entry { public string section, key, value; }
        public Entry[] entries;
        public string[] entryRecords;
        private static bool Included(ConfigDefinition key) =>
            key.Section != "Diagnostics" && key.Section != "Shortcuts" && key.Section != "Logging" &&
            !(key.Section == "General" && key.Key == "Enabled");
        internal static string Capture(ConfigFile config) => JsonUtility.ToJson(new GadgetConfigSnapshot {
            entryRecords = config.Where(pair => Included(pair.Key)).Select(pair => JsonUtility.ToJson(new Entry {
                section = pair.Key.Section, key = pair.Key.Key, value = pair.Value.GetSerializedValue() })).ToArray() });
        internal static void Apply(ConfigFile config, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var saved = JsonUtility.FromJson<GadgetConfigSnapshot>(json);
            if (saved?.entryRecords != null) saved.entries = saved.entryRecords.Select(item => JsonUtility.FromJson<Entry>(item)).ToArray();
            if (saved?.entries == null) throw new ArgumentException("アイテムの設定データが不正です。");
            bool autoSave = config.SaveOnConfigSet;
            try
            {
                config.SaveOnConfigSet = false;
                foreach (var item in saved.entries)
                {
                    if (item == null) throw new ArgumentException("アイテムの設定項目が空です。");
                    var key = new ConfigDefinition(item.section, item.key);
                    if (Included(key) && config.ContainsKey(key)) config[key].SetSerializedValue(item.value);
                }
            }
            finally { config.SaveOnConfigSet = autoSave; }
        }
    }
}
