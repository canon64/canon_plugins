using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode
{
    [DataContract]
    public sealed class HotelRoomCard
    {
        [DataMember] public string room;
        [DataMember] public string card;
        [DataMember] public string name;
    }
    [DataContract]
    public sealed class HotelRoomCardsDocument
    {
        [DataMember] public string version = "v1";
        [DataMember] public HotelRoomCard[] rooms = Array.Empty<HotelRoomCard>();
        public void Validate()
        {
            if (version != "v1" || rooms == null || rooms.Any(r => r == null || !HotelRoomCards.Rooms.Contains(r.room)
                || string.IsNullOrWhiteSpace(r.card) || !Path.IsPathRooted(r.card))
                || rooms.Select(r => r.room).Distinct().Count() != rooms.Length)
                throw new InvalidDataException("客室のカード登録データが不正です。元のファイルは変更していません。");
        }
    }
    // Independent from furniture layouts and plugin Content; deployments never replace user assignments.
    public sealed class HotelRoomCards
    {
        public static readonly string[] Rooms = { "201", "202", "203", "204", "205", "206", "reception" };
        public string FilePath { get; }
        public HotelRoomCards(string path) { FilePath = path; }
        public HotelRoomCardsDocument Load()
        {
            if (!File.Exists(FilePath)) return new HotelRoomCardsDocument();
            using (var stream = File.OpenRead(FilePath))
            {
                var data = (HotelRoomCardsDocument)new DataContractJsonSerializer(typeof(HotelRoomCardsDocument)).ReadObject(stream);
                if (data == null) throw new InvalidDataException("客室のカード登録データが空です。");
                data.Validate(); return data;
            }
        }
        public void Set(string room, string card, string name)
        {
            if (!Rooms.Contains(room)) throw new ArgumentException("客室番号が不正です。", nameof(room));
            var data = Load(); // Refuse to clobber malformed data, and preserve assignments from other rooms.
            data.rooms = data.rooms.Where(r => r.room != room).Concat(string.IsNullOrWhiteSpace(card)
                ? Enumerable.Empty<HotelRoomCard>()
                : new[] { new HotelRoomCard { room = room, card = Path.GetFullPath(card), name = name } }).ToArray();
            data.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(HotelRoomCardsDocument)).WriteObject(stream, data); stream.Flush(true); }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
