using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameBlankMapAdd
{
    [DataContract]
    internal sealed class MapWarpPoint
    {
        [DataMember] public string Id;
        [DataMember] public string MapKey;
        [DataMember] public string Name;
        [DataMember] public float X;
        [DataMember] public float Y;
        [DataMember] public float Z;
        [DataMember] public float RotationX;
        [DataMember] public float RotationY;
        [DataMember] public float RotationZ;
        [DataMember] public float Scale = 1f;
        [DataMember(EmitDefaultValue = false)] public bool HasRotationCenter;
        [DataMember(EmitDefaultValue = false)] public float CenterX;
        [DataMember(EmitDefaultValue = false)] public float CenterY;
        [DataMember(EmitDefaultValue = false)] public float CenterZ;
        [DataMember(EmitDefaultValue = false)] public bool IsOrigin;
        [DataMember(EmitDefaultValue = false)] public float[] Placement;
        [DataMember(EmitDefaultValue = false)] public bool PlacementActorCenter;
        internal MapWarpPoint Clone()
        {
            var copy = (MapWarpPoint)MemberwiseClone();
            copy.Placement = Placement == null ? null : (float[])Placement.Clone();
            return copy;
        }
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(MapKey) || string.IsNullOrWhiteSpace(Name) || Name.Length > 80)
                throw new InvalidDataException("地点のID・マップ・名前が不正です。");
            foreach (float value in new[] { X, Y, Z, RotationX, RotationY, RotationZ, Scale, CenterX, CenterY, CenterZ })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("地点の座標が不正です。");
            if (Scale < .05f || Scale > 20f) throw new InvalidDataException("地点の倍率が範囲外です。");
            if (IsOrigin)
            {
                if (Placement == null || (Placement.Length != 6 && Placement.Length != 7))
                    throw new InvalidDataException("元の場所の配置データが不正です。");
                foreach (float value in Placement)
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("元の場所の座標が不正です。");
                if (Placement.Length == 7 && (Placement[6] < .05f || Placement[6] > 20f))
                    throw new InvalidDataException("元の場所の倍率が範囲外です。");
            }
        }
    }

    [DataContract]
    internal sealed class MapWarpDocument
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<MapWarpPoint> Points = new List<MapWarpPoint>();
        internal MapWarpDocument Clone() => new MapWarpDocument { Version = Version, Points = Points.Select(p => p.Clone()).ToList() };
        internal void Validate()
        {
            if (Version != 1 || Points == null) throw new InvalidDataException("地点ファイルの形式またはバージョンが不正です。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var origins = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapWarpPoint point in Points)
            {
                if (point == null) throw new InvalidDataException("地点が空です。");
                point.Validate();
                if (!ids.Add(point.Id)) throw new InvalidDataException("地点IDが重複しています。");
                if (point.IsOrigin && !origins.Add(point.MapKey)) throw new InvalidDataException("元の場所が重複しています。");
            }
        }
    }

    internal static class MapWarpPointStore
    {
        internal static MapWarpDocument Load(string path)
        {
            if (!File.Exists(path)) return new MapWarpDocument();
            using (var stream = File.OpenRead(path))
            {
                var result = new DataContractJsonSerializer(typeof(MapWarpDocument)).ReadObject(stream) as MapWarpDocument;
                if (result == null) throw new InvalidDataException("地点ファイルが空です。");
                result.Validate(); return result;
            }
        }
        internal static void Save(string path, MapWarpDocument document)
        {
            document.Validate();
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(MapWarpDocument)).WriteObject(stream, document);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
