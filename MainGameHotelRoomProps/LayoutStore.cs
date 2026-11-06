using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameHotelRoomProps
{
    [DataContract] public sealed class RoomLayout
    {
        [DataMember] public int schema = 1;
        [DataMember] public string roomId;
        [DataMember] public PropPose[] props;
        public void Validate(string room)
        {
            if (schema != 1 || roomId != room || props == null || props.Any(p => p == null)
                || props.Select(p => p.id).Distinct().Count() != props.Length) throw new InvalidDataException("客室配置データが不正です。");
            foreach (var p in props) p.Validate();
        }
    }
    [DataContract] public sealed class PropPose
    {
        [DataMember] public string id;
        [DataMember] public float[] position;
        [DataMember] public float[] rotation;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || position == null || position.Length != 3 || rotation == null || rotation.Length != 4
                || position.Concat(rotation).Any(v => float.IsNaN(v) || float.IsInfinity(v))
                || position.Any(v => Math.Abs(v) > 1000) || rotation.Sum(v => v*v) < .99f || rotation.Sum(v => v*v) > 1.01f)
                throw new InvalidDataException("家具の位置または向きが不正です。");
        }
    }
    public sealed class LayoutStore
    {
        public readonly string Path;
        private readonly string _room;
        public LayoutStore(string directory, string room)
        {
            if (string.IsNullOrWhiteSpace(room) || room.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
                throw new ArgumentException("客室IDが不正です。", nameof(room));
            _room=room;Path=System.IO.Path.Combine(directory,room+".json");
        }
        public RoomLayout Load()
        {
            if (!File.Exists(Path)) return null;
            using (var stream=File.OpenRead(Path))
            {
                var data=(RoomLayout)new DataContractJsonSerializer(typeof(RoomLayout)).ReadObject(stream);
                if (data == null) throw new InvalidDataException("客室配置データが空です。");
                data.Validate(_room);return data;
            }
        }
        public void Save(RoomLayout data)
        {
            data.Validate(_room);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary=Path+".tmp";
            try
            {
                using (var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {new DataContractJsonSerializer(typeof(RoomLayout)).WriteObject(stream,data);stream.Flush(true);}
                if (File.Exists(Path)) File.Replace(temporary,Path,Path+".bak");
                else File.Move(temporary,Path);
            }
            finally {if (File.Exists(temporary)) File.Delete(temporary);}
        }
    }
}
