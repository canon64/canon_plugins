using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameBlankMapAdd
{
    [DataContract]
    internal sealed class PoseLocation
    {
        [DataMember] public string Pose;
        [DataMember] public HLocationPreset Placement;
        internal PoseLocation Clone() => new PoseLocation { Pose = Pose, Placement = Placement.Clone() };
    }

    [DataContract]
    internal sealed class PoseLocationDocument
    {
        [DataMember] public int Version = 2;
        [DataMember] public bool Automatic = true;
        [DataMember] public List<HLocationPreset> Initial = new List<HLocationPreset>();
        [DataMember] public List<PoseLocation> Entries = new List<PoseLocation>();
        internal PoseLocationDocument Clone() => new PoseLocationDocument { Version = Version, Automatic = Automatic,
            Initial = Initial.Select(p => p.Clone()).ToList(), Entries = Entries.Select(e => e.Clone()).ToList() };
        internal HLocationPreset BaseFor(string map) => Initial.SingleOrDefault(p => HLocationMapIdentity.Same(p.MapKey, map));
        internal PoseLocation Find(string map, string pose) => Entries.SingleOrDefault(e => e.Pose == pose && HLocationMapIdentity.Same(e.Placement.MapKey, map));
        internal HLocationPreset Resolve(string map, string pose, HLocationPreset initial) => Find(map, pose)?.Placement ?? initial;
        internal PoseLocationDocument ForSave()
        {
            var next = Clone();
            next.Version = 2;
            next.Initial.Clear();
            return next;
        }
        internal void Validate()
        {
            if ((Version != 1 && Version != 2) || Entries == null || (Version == 1 && Initial == null)) throw new InvalidDataException("体位別配置ファイルの形式が不正です。");
            if (Initial == null) Initial = new List<HLocationPreset>();
            var maps = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in Initial) { p.Validate(); if (!maps.Add(HLocationMapIdentity.Canonical(p.MapKey))) throw new InvalidDataException("初期位置が重複しています。"); }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in Entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Pose) || e.Placement == null) throw new InvalidDataException("体位別配置が不正です。");
                e.Placement.Validate();
                if (!keys.Add(HLocationMapIdentity.Canonical(e.Placement.MapKey) + "\n" + e.Pose))
                    throw new InvalidDataException("体位別配置の登録が重複しています。");
            }
        }
    }

    [DataContract]
    internal sealed class CategoryLocation
    {
        [DataMember] public string MapKey;
        [DataMember] public int Category;
        [DataMember] public HLocationPreset Placement;
        internal CategoryLocation Clone() => new CategoryLocation { MapKey = MapKey, Category = Category, Placement = Placement.Clone() };
    }

    [DataContract]
    internal sealed class CategoryLocationDocument
    {
        [DataMember] public int Version = 1;
        [DataMember] public bool Automatic = true;
        [DataMember] public List<CategoryLocation> Entries = new List<CategoryLocation>();
        internal CategoryLocationDocument Clone() => new CategoryLocationDocument { Automatic = Automatic, Entries = Entries.Select(e => e.Clone()).ToList() };
        internal CategoryLocation Find(string map, int category) => Entries.SingleOrDefault(e => e.Category == category && HLocationMapIdentity.Same(e.MapKey, map));
        internal void Validate()
        {
            if (Version != 1 || Entries == null) throw new InvalidDataException("体位の場所ファイルの形式が不正です。");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in Entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.MapKey) || e.Category < 0 || e.Placement == null ||
                    !HLocationMapIdentity.Same(e.MapKey, e.Placement.MapKey) || !keys.Add(HLocationMapIdentity.Canonical(e.MapKey) + "\ncategory:" + e.Category))
                    throw new InvalidDataException("体位の場所データが重複または不正です。");
                e.Placement.Validate();
            }
        }
    }

    internal static class CategoryLocationSelection
    {
        // Match SetLocalPosition's category order. Hotel exposes poses beyond the native point's categories.
        internal static int? Resolve(IEnumerable<int> declared, IEnumerable<int> active, IEnumerable<int> withLocalMove = null)
        {
            var tags = declared?.Distinct().ToArray() ?? Array.Empty<int>();
            if (tags.Length == 0) return null;
            var current = new HashSet<int>(active ?? Array.Empty<int>());
            // Native SetLocalPosition skips matching entries without a local-move file.
            var moved = new HashSet<int>(withLocalMove ?? tags);
            foreach (int tag in tags) if (current.Contains(tag) && moved.Contains(tag)) return tag;
            foreach (int tag in tags) if (current.Contains(tag)) return tag;
            return tags[0];
        }
    }

    [DataContract]
    internal sealed class RoomReverbSetting
    {
        [DataMember] public string MapKey, Preset = "Room";
        [DataMember] public bool Enabled = true;
        [DataMember] public float Strength = 1;
        internal RoomReverbSetting Clone() => (RoomReverbSetting)MemberwiseClone();
    }
    [DataContract]
    internal sealed class RoomReverbDocument
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<RoomReverbSetting> Entries = new List<RoomReverbSetting>();
        internal RoomReverbDocument Clone() => new RoomReverbDocument { Entries = Entries.Select(e => e.Clone()).ToList() };
        internal RoomReverbSetting Find(string key) => Entries.SingleOrDefault(e => HLocationMapIdentity.Same(e.MapKey, key));
        internal void Validate()
        {
            if (Version != 1 || Entries == null) throw new InvalidDataException("部屋の残響ファイルの形式が不正です。");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in Entries)
                if (e == null || string.IsNullOrWhiteSpace(e.MapKey) || string.IsNullOrWhiteSpace(e.Preset) ||
                    float.IsNaN(e.Strength) || float.IsInfinity(e.Strength) || e.Strength < 0 || e.Strength > 1 || !keys.Add(HLocationMapIdentity.Canonical(e.MapKey)))
                    throw new InvalidDataException("部屋の残響データが重複または不正です。");
        }
    }

    internal static class RoomCustomizationStore
    {
        internal static T Load<T>(string path, Action<T> validate) where T : new()
        {
            T value;
            if (!File.Exists(path)) value = new T();
            else using (var stream = File.OpenRead(path)) value = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            if (value == null) throw new InvalidDataException("設定ファイルが空です。");
            validate(value); return value;
        }
        internal static void Save<T>(string path, T value, Action<T> validate)
        {
            validate(value);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
