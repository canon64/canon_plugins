using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelRoomReadResult
    {
        internal HotelRoomState State = new HotelRoomState { version = null };
        internal readonly List<KeyValuePair<string, Exception>> Errors = new List<KeyValuePair<string, Exception>>();
    }
    // Read each JSON member through its own subtree so a typed member error does not erase independent members.
    internal static class HotelRoomStateReader
    {
        internal static HotelRoomReadResult Read(string path, string room)
        {
            var result = new HotelRoomReadResult();
            using (var stream = File.OpenRead(path))
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                reader.MoveToContent();
                Members(reader, (key, member) =>
                {
                    switch (key)
                    {
                        case "version": result.State.version = Value<string>(member); break;
                        case "room": result.State.room = Value<string>(member); break;
                        case "selection": result.State.selection = Value<HotelRoomSelection>(member); break;
                        case "placement": result.State.placement = Value<HotelRoomPlacement>(member); break;
                        case "lighting": result.State.lighting = Value<HotelRoomLighting>(member); break;
                        case "appearance": result.State.appearance = Appearance(member, result); break;
                        case "gadgets":
                            var gadgets = new List<HotelRoomGadget>();
                            Members(member, (_, entry) => gadgets.Add(Value<HotelRoomGadget>(entry)), result, "gadgets");
                            result.State.gadgets = gadgets.ToArray(); break;
                    }
                }, result, "");
            }
            if (result.State.room != room || result.State.version != "v1") throw new InvalidDataException("客室番号または保存形式が一致しません。元ファイルを保持します。");
            return result;
        }
        private static HotelRoomAppearance Appearance(XmlReader reader, HotelRoomReadResult result)
        {
            var value = new HotelRoomAppearance();
            Members(reader, (key, member) =>
            {
                switch (key)
                {
                    case "source": value.source = Value<string>(member); break;
                    case "format": value.format = Value<string>(member); break;
                    case "female": value.female = Value<string>(member); break;
                    case "male": value.male = Value<string>(member); break;
                    case "effects":
                        var effects = new List<HotelRoomActorEffect>();
                        Members(member, (_, entry) => effects.Add(Value<HotelRoomActorEffect>(entry)), result, "appearance.effects");
                        value.effects = effects.ToArray(); break;
                    case "preferences":
                        var preferences = new List<HotelRoomPreference>();
                        Members(member, (_, entry) => preferences.Add(Value<HotelRoomPreference>(entry)), result, "appearance.preferences");
                        value.preferences = preferences.ToArray(); break;
                }
            }, result, "appearance");
            return value;
        }
        private static T Value<T>(XmlReader reader) => (T)new DataContractJsonSerializer(typeof(T)).ReadObject(reader, false);
        private static void Members(XmlReader reader, Action<string, XmlReader> member, HotelRoomReadResult result, string prefix)
        {
            if (reader.IsEmptyElement) return;
            reader.ReadStartElement();
            while (reader.MoveToContent() == XmlNodeType.Element)
            {
                string key = reader.LocalName;
                try
                {
                    using (var subtree = reader.ReadSubtree())
                    { subtree.MoveToContent(); member(key, subtree); }
                }
                catch (Exception ex) { result.Errors.Add(new KeyValuePair<string, Exception>(string.IsNullOrEmpty(prefix) ? key : prefix + "." + key, ex)); }
                reader.Skip();
            }
        }
    }
}
