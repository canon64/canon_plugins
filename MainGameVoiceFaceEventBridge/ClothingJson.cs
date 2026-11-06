using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameVoiceFaceEventBridge
{
    [DataContract]
    internal sealed class ClothingPiece
    {
        [DataMember(Name = "衣装")] public string Name;
        [DataMember(Name = "状態")] public string State;
    }

    [DataContract]
    internal sealed class AccessoryPiece
    {
        [DataMember(Name = "名前")] public string Name;
        [DataMember(Name = "状態")] public string State;
    }

    internal static class ClothingJson
    {
        [DataContract]
        private sealed class ClothingView
        {
            [DataMember(Name = "衣装", Order = 0)] public string CoordinateName;
            [DataMember(Name = "トップス", Order = 1)] public ClothingPiece Top;
            [DataMember(Name = "ボトムス", Order = 2)] public ClothingPiece Bottom;
            [DataMember(Name = "ブラ", Order = 3)] public ClothingPiece Bra;
            [DataMember(Name = "ショーツ", Order = 4)] public ClothingPiece Shorts;
            [DataMember(Name = "手袋", Order = 5)] public ClothingPiece Gloves;
            [DataMember(Name = "パンスト", Order = 6)] public ClothingPiece Pantyhose;
            [DataMember(Name = "靴下", Order = 7)] public ClothingPiece Socks;
            [DataMember(Name = "上履き", Order = 8)] public ClothingPiece IndoorShoes;
            [DataMember(Name = "外履き", Order = 9)] public ClothingPiece OutdoorShoes;
            [DataMember(Name = "眼鏡・目隠し", Order = 10)] public AccessoryPiece Glasses;

            internal ClothingView(ClothingSnapshot snapshot)
            {
                CoordinateName = snapshot.CoordinateName;
                Top = snapshot.Clothing[ClothingSnapshot.Parts[0]];
                Bottom = snapshot.Clothing[ClothingSnapshot.Parts[1]];
                Bra = snapshot.Clothing[ClothingSnapshot.Parts[2]];
                Shorts = snapshot.Clothing[ClothingSnapshot.Parts[3]];
                Gloves = snapshot.Clothing[ClothingSnapshot.Parts[4]];
                Pantyhose = snapshot.Clothing[ClothingSnapshot.Parts[5]];
                Socks = snapshot.Clothing[ClothingSnapshot.Parts[6]];
                IndoorShoes = snapshot.Clothing[ClothingSnapshot.Parts[7]];
                OutdoorShoes = snapshot.Clothing[ClothingSnapshot.Parts[8]];
                Glasses = snapshot.Glasses;
            }
        }

        [DataContract]
        private sealed class Request
        {
            [DataMember(Name = "main", IsRequired = true)] public int Main;
        }

        [DataContract]
        private sealed class Reply
        {
            [DataMember(Name = "ok")] public bool Ok;
            [DataMember(Name = "snapshotId", EmitDefaultValue = false)] public string SnapshotId;
            [DataMember(Name = "clothing", EmitDefaultValue = false)] public ClothingView Clothing;
            [DataMember(Name = "error", EmitDefaultValue = false)] public string Error;
        }

        internal static int ReadMain(string line)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(line)))
                return ((Request)new DataContractJsonSerializer(typeof(Request)).ReadObject(stream)).Main;
        }

        internal static string Serialize(ClothingSnapshot snapshot, string error = null)
        {
            var reply = new Reply { Ok = snapshot != null, SnapshotId = snapshot?.Token, Clothing = snapshot == null ? null : new ClothingView(snapshot),
                Error = snapshot == null ? (error ?? "衣装情報を取得できません。") : null };
            var serializer = new DataContractJsonSerializer(typeof(Reply), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, reply);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
