using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Conversation
{
    [DataContract] internal sealed class KksCharacterVoiceCatalog
    {
#pragma warning disable CS0649 // Generated JSON catalog.
        [DataContract] internal sealed class Source
        {
            [DataMember] public string group, role, text, scenarioBundle, scenario;
            [DataMember] public int stage, row;
        }
        [DataContract] internal sealed class Entry
        {
            [DataMember] public string id, bundle, asset, text;
            [DataMember] public Source[] sources;
        }
        [DataContract] internal sealed class Binding
        {
            [DataMember] public string key, entryId, text, use, note;
            [DataMember] public int stage, sourceRow;
        }
        [DataMember] public int personality, pageSize;
        [DataMember] public Entry[] entries;
        [DataMember] public Binding[] bindings;
        [DataMember] public string[] unassignedSlots;
#pragma warning restore CS0649
        private Dictionary<string, Entry> _byId;
        internal static KksCharacterVoiceCatalog Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var result = (KksCharacterVoiceCatalog)new DataContractJsonSerializer(typeof(KksCharacterVoiceCatalog)).ReadObject(stream);
                if (result?.entries == null || result.bindings == null || result.unassignedSlots == null || result.pageSize <= default(int)
                    || result.entries.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.asset) || string.IsNullOrWhiteSpace(entry.bundle)
                        || !entry.bundle.StartsWith("sound/data/pcm/c" + result.personality.ToString("00") + "/", StringComparison.Ordinal)
                        || entry.sources == null)
                    || result.entries.Select(entry => entry.id).Distinct(StringComparer.Ordinal).Count() != result.entries.Length)
                    throw new InvalidDataException("個体音声カタログが不正です。");
                result._byId = result.entries.ToDictionary(entry => entry.id, StringComparer.Ordinal);
                if (result.bindings.Any(binding => binding == null || string.IsNullOrWhiteSpace(binding.key) || !result._byId.ContainsKey(binding.entryId))
                    || result.bindings.Select(binding => new { binding.key, binding.stage }).Distinct().Count() != result.bindings.Length)
                    throw new InvalidDataException("場面と音声の接続が不正です。");
                return result;
            }
        }
        internal Entry Find(string id) => _byId.TryGetValue(id, out var value) ? value : null;
        internal Binding Slot(int voice, int stage, string key) => voice == personality
            ? bindings.SingleOrDefault(binding => binding.key == key && binding.stage == stage) : null;
        internal string Resolve(int voice, string specified, string asset)
        {
            if (!string.IsNullOrEmpty(specified) || voice != personality) return specified;
            var matches = entries.Where(entry => entry.asset == asset).ToArray();
            return matches.Length == Math.Sign(pageSize) ? matches.Single().bundle : specified;
        }
        internal IEnumerable<string> Groups() => entries.SelectMany(entry => entry.sources.Select(source => source.group)).Distinct().OrderBy(value => value, StringComparer.Ordinal);
        internal IEnumerable<Entry> InGroup(string group) => group == null ? entries : group == "用途未確認"
            ? entries.Where(entry => !entry.sources.Any()) : entries.Where(entry => entry.sources.Any(source => source.group == group));
    }
}
