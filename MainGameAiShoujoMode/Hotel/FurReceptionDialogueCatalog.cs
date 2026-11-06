using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode
{
    internal sealed class FurReceptionNotConnectedException : NotSupportedException
    {
        internal string ItemId { get; }
        internal FurReceptionNotConnectedException(string itemId)
            : base("フュルの台詞・音声は移植済みですが、この項目の動作は未接続です: " + itemId)
        { ItemId = itemId; }
    }

    // Material access only. Loading this catalog does not register menus, play audio,
    // change scenes or authorize a session. Unconnected actions fail explicitly.
    [DataContract]
    internal sealed class FurReceptionDialogueCatalog
    {
        [DataContract]
        internal sealed class Line
        {
            [DataMember] public string id, text, textBasis, audio, audioSha256, connectionStatus;
        }
        [DataContract]
        internal sealed class Menu
        {
            [DataMember] public string id, label, connectionStatus;
        }
        [DataMember] public Line[] lines;
        [DataMember] public Menu[] menus;

        internal static FurReceptionDialogueCatalog Load(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("台詞フォルダーが指定されていません。", nameof(directory));
            FurReceptionDialogueCatalog catalog;
            using (var stream = File.OpenRead(Path.Combine(directory, "catalog.json")))
                catalog = (FurReceptionDialogueCatalog)new DataContractJsonSerializer(typeof(FurReceptionDialogueCatalog)).ReadObject(stream);
            if (catalog?.lines == null || catalog.menus == null || catalog.lines.Length == default(int))
                throw new InvalidDataException("フュルの台詞カタログが不完全です。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in catalog.lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.id) || !ids.Add(line.id)
                    || string.IsNullOrWhiteSpace(line.text) || string.IsNullOrWhiteSpace(line.textBasis)
                    || string.IsNullOrWhiteSpace(line.audioSha256) || line.connectionStatus != "unconnected")
                    throw new InvalidDataException("フュルの台詞項目が不正です。");
                if (string.IsNullOrWhiteSpace(line.audio) || Path.GetFileName(line.audio) != line.audio
                    || line.audio.Contains(":") || !File.Exists(Path.Combine(directory, "voices", line.audio)))
                    throw new InvalidDataException("フュルの音声ファイルが不正または未配置です: " + line.id);
            }
            ids.Clear();
            foreach (var menu in catalog.menus)
                if (menu == null || string.IsNullOrWhiteSpace(menu.id) || !ids.Add(menu.id)
                    || string.IsNullOrWhiteSpace(menu.label) || menu.connectionStatus != "unconnected")
                    throw new InvalidDataException("フュルの受付項目が不正です。");
            return catalog;
        }

        internal Line GetLine(string id)
        {
            var line = lines.SingleOrDefault(x => x.id == id);
            return line ?? throw new KeyNotFoundException("フュルの台詞がありません: " + id);
        }

        internal void RequireConnectedLine(string id)
        {
            var line = GetLine(id);
            throw new FurReceptionNotConnectedException(line.id);
        }

        internal void RequireConnectedMenu(string id)
        {
            var menu = menus.SingleOrDefault(x => x.id == id);
            if (menu == null) throw new KeyNotFoundException("フュルの受付項目がありません: " + id);
            throw new FurReceptionNotConnectedException(menu.id);
        }
    }
}
