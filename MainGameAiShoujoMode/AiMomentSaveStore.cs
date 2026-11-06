using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace MainGameAiShoujoMode
{
    [DataContract] internal sealed class AiMomentSave
    {
#pragma warning disable CS0649 // Save records are also filled by the serializer in linked tests.
        [DataContract] internal sealed class Pose
        {
            [DataMember] public float x, y, z, qx, qy, qz, qw;
            internal void Validate()
            {
                if (new[] { x, y, z, qx, qy, qz, qw }.Any(v => float.IsNaN(v) || float.IsInfinity(v))
                    || qx * qx + qy * qy + qz * qz + qw * qw <= default(float))
                    throw new InvalidDataException("保存位置・向きが不正です。");
            }
        }
        [DataContract] internal sealed class Actor
        {
            [DataMember] public string card, identity, source;
            [DataMember] public Pose pose;
            [DataMember] public int animation;
            [DataMember] public float animationTime, animationSpeed;
        }
        [DataContract] internal sealed class Door
        {
            [DataMember] public string path;
            [DataMember] public bool open, right;
        }
        [DataContract] internal sealed class Pod
        {
            [DataMember] public int id, animation;
            [DataMember] public float time;
        }
        [DataMember] public string format = "ai-moment-v1", savedUtc, world, parameters, life, conversation;
        [DataMember] public Pose player, view, pickup;
        [DataMember] public float fieldOfView;
        [DataMember] public bool crouching;
        [DataMember] public string playerCard;
        [DataMember(EmitDefaultValue=false)] public string dedicatedHProfiles;
        [DataMember] public Actor girl;
        [DataMember] public Door[] doors;
        [DataMember] public Pod[] pods;
        [DataMember(EmitDefaultValue=false)] public Furniture.FurnitureLayout furniture;
#pragma warning restore CS0649
        internal void Validate()
        {
            if (format != "ai-moment-v1" || world != "ai-island" || !DateTimeOffset.TryParse(savedUtc, out _)
                || string.IsNullOrWhiteSpace(parameters) || string.IsNullOrWhiteSpace(life) || string.IsNullOrWhiteSpace(conversation)
                || player == null || view == null || doors == null || pods == null || float.IsNaN(fieldOfView) || float.IsInfinity(fieldOfView)
                || fieldOfView <= default(float) || doors.Any(d => d == null || string.IsNullOrWhiteSpace(d.path))
                || doors.Select(d => d.path).Distinct(StringComparer.Ordinal).Count() != doors.Length)
                throw new InvalidDataException("AI少女モードのセーブ形式が不正です。");
            if (pods.Any(p => p == null || float.IsNaN(p.time) || float.IsInfinity(p.time)) || pods.Select(p => p.id).Distinct().Count() != pods.Length)
                throw new InvalidDataException("保存したカプセルの状態が不正です。");
            player.Validate(); view.Validate(); pickup?.Validate();furniture?.Validate();
            if (girl != null)
            {
                if (string.IsNullOrWhiteSpace(girl.identity) || string.IsNullOrWhiteSpace(girl.card) || girl.pose == null
                    || float.IsNaN(girl.animationTime) || float.IsInfinity(girl.animationTime)
                    || float.IsNaN(girl.animationSpeed) || float.IsInfinity(girl.animationSpeed))
                    throw new InvalidDataException("保存した女の子の情報が不足しています。");
                girl.pose.Validate(); Convert.FromBase64String(girl.card);
            }
            if (playerCard != null) Convert.FromBase64String(playerCard);
        }
    }

    // One atomic, checksummed checkpoint. Restore work files never replace the legacy saves.
    internal sealed class AiMomentSaveStore
    {
        [DataContract] private sealed class Envelope
        {
            [DataMember] public string payload, sha256;
        }
        private readonly string _path;
        internal AiMomentSaveStore(string path) { _path = Path.GetFullPath(path); }
        internal string WorkingDirectory => _path + ".working";
        internal string WorkingLife => Path.Combine(WorkingDirectory, "life-state.json");
        internal string WorkingConversation => Path.Combine(WorkingDirectory, "conversation-state.json");
        internal static byte[] Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = int.MaxValue }).WriteObject(stream, value); return stream.ToArray(); }
        }
        private static T Decode<T>(byte[] value)
        { using (var stream = new MemoryStream(value)) return (T)new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = int.MaxValue }).ReadObject(stream); }
        private static string Digest(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)); }
        internal AiMomentSave Read()
        {
            if (!File.Exists(_path)) return null;
            var envelope = Decode<Envelope>(File.ReadAllBytes(_path));
            if (envelope == null || string.IsNullOrEmpty(envelope.payload)) throw new InvalidDataException("セーブが空です。");
            var bytes = Convert.FromBase64String(envelope.payload);
            if (!string.Equals(envelope.sha256, Digest(bytes), StringComparison.Ordinal)) throw new InvalidDataException("セーブの破損を検出しました。以前のデータは保持しています。");
            var save = Decode<AiMomentSave>(bytes);
            if (save == null) throw new InvalidDataException("セーブを読み込めません。");
            save.Validate(); return save;
        }
        internal void Write(AiMomentSave save)
        {
            save.Validate(); var bytes = Encode(save);
            AtomicWrite(_path, Encode(new Envelope { payload = Convert.ToBase64String(bytes), sha256 = Digest(bytes) }));
        }
        internal void PrepareWorkingCopy(AiMomentSave save)
        {
            save.Validate();
            AtomicWrite(WorkingLife, Encoding.UTF8.GetBytes(save.life));
            AtomicWrite(WorkingConversation, Encoding.UTF8.GetBytes(save.conversation));
        }
        private static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, default, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(pending, path, path + ".previous"); else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
