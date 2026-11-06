using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MainGameHLocationPlacement
{
    internal static class PresetDocuments
    {
        internal const string Composer = "Composer", Hip = "HipHijack";
        internal static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        internal static XElement Parse(string json)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                return XElement.Load(reader);
        }
        internal static string Canonical(XElement node)
        {
            if (node.HasElements)
            {
                bool array = (string)node.Attribute("type") == "array";
                var children = array ? node.Elements() : node.Elements().OrderBy(x => x.Name.LocalName, StringComparer.Ordinal);
                return "{" + string.Join(",", children.Select(x => (array ? "" : x.Name.LocalName + ":") + Canonical(x))) + "}";
            }
            string type = (string)node.Attribute("type") ?? "string";
            string value = node.Value;
            if (type == "number") value = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return type + ":" + value.Length + ":" + value;
        }
        internal static string Text(XElement node, string key) => node.Element(key)?.Value ?? "";
        internal static List<SavedPreset> Collection(string owner, string json)
        {
            var root = Parse(json);
            if (owner == Composer)
                return (root.Element("presetsJson")?.Elements() ?? Enumerable.Empty<XElement>()).Select(n => ComposerPreset(n.Value)).ToList();
            if (owner != Hip) throw new ArgumentException("Unknown preset owner");
            return (root.Element("presets")?.Elements() ?? Enumerable.Empty<XElement>()).Select(HipPreset).ToList();
        }
        internal static SavedPreset ComposerPreset(string json)
        {
            var root = Parse(json);
            var preset = new SavedPreset { Owner = Composer, Name = Text(root, "displayName"), PoseKey = Text(root, "key") };
            preset.Identity = preset.PoseKey + "\n" + preset.Name;
            foreach (string slot in new[] { "loop1Json", "loop2Json" })
            {
                var items = (root.Element(slot)?.Elements() ?? Enumerable.Empty<XElement>()).Select(n => n.Value).ToArray();
                if (items.Length == 0) continue;
                preset.Parts.Add(new SavedPart { Name = slot, Elements = items,
                    Fingerprint = Hash(string.Join("\n", items.Select(Parse).OrderBy(n => Text(n, "id"), StringComparer.Ordinal).Select(Canonical))) });
            }
            return preset;
        }
        private static SavedPreset HipPreset(XElement root)
        {
            var preset = new SavedPreset { Owner = Hip, Identity = Text(root, "id"), Name = Text(root, "name"),
                PoseKey = Text(root, "postureMode") + "/" + Text(root, "postureId") };
            int i = 0;
            foreach (var entry in root.Element("entries")?.Elements() ?? Enumerable.Empty<XElement>())
                preset.Parts.Add(new SavedPart { Name = "IK/" + i++, Fingerprint = Hash(Canonical(entry)), Elements = new[] { ToJson(entry) } });
            return preset;
        }
        internal static string ToJson(XElement element)
        {
            var root = new XElement(element); root.Name = "root";
            // XElement retains empty text for JSON null; the JSON writer rejects
            // even empty text inside a null node. Normalize only the output copy.
            foreach (var node in root.DescendantsAndSelf().Where(n => (string)n.Attribute("type") == "null"))
                node.RemoveNodes();
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, false)) root.WriteTo(writer);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
