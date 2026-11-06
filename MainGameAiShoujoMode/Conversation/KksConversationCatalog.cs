using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MapEnvironmentRuntime;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Conversation
{
    // Loads managed dialogue records only. It never enters KKS ActionScene/TalkScene.
    internal sealed class KksConversationCatalog
    {
        // Read managed fields with JsonCodec; KKS JsonUtility may discard custom record arrays.
#pragma warning disable CS0649
        [Serializable, DataContract]
        internal sealed class Reference
        {
            [DataMember] public string bundle, asset, sha256, className;
            [DataMember] public int rowCount;
        }
        [Serializable]
        internal sealed class Personality
        {
            public int personality;
            public string source;
            public Reference conversation, options, events, listen, common, rare;
        }
        [Serializable]
        internal sealed class Manifest
        {
            public string sourceAssemblySha256;
            public KksConversationLayout.LayoutNumber[] layoutNumbers;
            public KksNormalConversationRules.Settings normalRules;
            public KksConversationRuntimeSettings runtime;
            public Personality[] personalities;
        }
#pragma warning restore CS0649
        internal sealed class Profile
        {
            internal Personality Source { get; }
            internal KksConversationLayout Layout { get; }
            internal IReadOnlyDictionary<int, string[]> Options { get; }
            internal IReadOnlyDictionary<int, string[]> Events { get; }
            internal IReadOnlyList<KksConversationLayout.Row> CommonTopics { get; }
            internal IReadOnlyList<ActionGame.TopicTalkRare.Param> RareTopics { get; }
            internal IReadOnlyList<ActionGame.TopicListenData.Param> ListenTopics { get; }
            internal Profile(Personality source, KksConversationLayout layout, Dictionary<int, string[]> options,
                Dictionary<int, string[]> events, List<KksConversationLayout.Row> common, List<ActionGame.TopicTalkRare.Param> rare,
                List<ActionGame.TopicListenData.Param> listen)
            {
                Source = source; Layout = layout; Options = options; Events = events;
                CommonTopics = common.AsReadOnly(); RareTopics = rare.AsReadOnly(); ListenTopics = listen.AsReadOnly();
            }
        }

        private readonly Dictionary<int, Personality> _sources = new Dictionary<int, Personality>();
        private readonly Dictionary<int, Profile> _cache = new Dictionary<int, Profile>();
        private readonly KksConversationLayout.LayoutNumber[] _numbers;
        private readonly Dictionary<string, int> _nativeNumbers;
        internal KksNormalConversationRules NormalRules { get; }
        internal KksConversationRuntimeSettings Runtime { get; }
        internal KksAdvCatalog Scenarios { get; } = new KksAdvCatalog();

        internal KksConversationCatalog(string manifestPath)
        {
            var manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest?.personalities == null || manifest.layoutNumbers == null || string.IsNullOrEmpty(manifest.sourceAssemblySha256))
                throw new FormatException("KKS conversation source manifest is incomplete.");
            _numbers = manifest.layoutNumbers;
            NormalRules = new KksNormalConversationRules(manifest.normalRules);
            Runtime = manifest.runtime ?? throw new FormatException("KKS conversation runtime settings are missing.");
            _nativeNumbers = _numbers.ToDictionary(number => number.key, number => number.value, StringComparer.Ordinal);
            foreach (var personality in manifest.personalities)
            {
                if (personality == null || personality.conversation == null || _sources.ContainsKey(personality.personality))
                    throw new FormatException("Invalid or duplicate KKS conversation personality.");
                _sources.Add(personality.personality, personality);
            }
        }

        internal Profile Load(int personality)
        {
            if (_cache.TryGetValue(personality, out var cached)) return cached;
            if (!_sources.TryGetValue(personality, out var source)) throw new KeyNotFoundException("KKS conversation personality is not catalogued: " + personality);
            bool npc = string.Equals(source.conversation.className, nameof(CommunicationNPCData), StringComparison.Ordinal);
            if (!npc && !string.Equals(source.conversation.className, nameof(CommunicationInfo), StringComparison.Ordinal))
                throw new NotSupportedException("This KKS character uses a separate conversation format: " + source.conversation.className);
            var rows = npc
                ? LoadAsset<CommunicationNPCData, List<KksConversationLayout.Row>>(source.conversation, asset => asset.param.Select(Copy).ToList())
                : LoadAsset<CommunicationInfo, List<KksConversationLayout.Row>>(source.conversation, asset => asset.param.Select(Copy).ToList());
            if (rows.Count != source.conversation.rowCount) throw new FormatException("KKS conversation table changed; rebuild the source catalog.");
            var layout = KksConversationLayout.Read(rows, _numbers, npc);
            var options = HasAsset(source.options) ? LoadAsset<ExcelData, Dictionary<int, string[]>>(source.options, CopyExcel) : new Dictionary<int, string[]>();
            var events = HasAsset(source.events) ? LoadAsset<ExcelData, Dictionary<int, string[]>>(source.events, CopyExcel) : new Dictionary<int, string[]>();
            var common = HasAsset(source.common) ? LoadAsset<ActionGame.TopicTalkCommon, List<KksConversationLayout.Row>>(source.common,
                asset => asset.param.Select(row => Copy(row.bundle, row.file, row.text, row.pose, row.facialExpression, row.eyeLook, row.neckLook)).ToList()) : new List<KksConversationLayout.Row>();
            var rare = HasAsset(source.rare) ? LoadAsset<ActionGame.TopicTalkRare, List<ActionGame.TopicTalkRare.Param>>(source.rare,
                asset => asset.param.ToList()) : new List<ActionGame.TopicTalkRare.Param>();
            var listen = HasAsset(source.listen) ? LoadAsset<ActionGame.TopicListenData, List<ActionGame.TopicListenData.Param>>(source.listen, asset => asset.param.ToList()) : new List<ActionGame.TopicListenData.Param>();
            var profile = new Profile(source, layout, options, events, common, rare, listen);
            _cache.Add(personality, profile);
            return profile;
        }

        internal void Clear() => _cache.Clear();
        private static bool HasAsset(Reference reference)
        {
            if (reference == null) return false;
            if (string.IsNullOrEmpty(reference.bundle) != string.IsNullOrEmpty(reference.asset)) throw new FormatException("KKS conversation source reference is partial.");
            return !string.IsNullOrEmpty(reference.bundle);
        }

        private static TResult LoadAsset<TAsset, TResult>(Reference reference, Func<TAsset, TResult> copy) where TAsset : UnityEngine.Object
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.bundle) || string.IsNullOrWhiteSpace(reference.asset))
                throw new FormatException("KKS conversation source reference is empty.");
            if (Path.IsPathRooted(reference.bundle) || reference.bundle.Split('/', '\\').Any(part => part == ".."))
                throw new FormatException("KKS conversation source must be relative to abdata.");
            bool requested = false;
            try
            {
                var operation = AssetBundleManager.LoadAsset(reference.bundle, reference.asset, typeof(TAsset));
                requested = operation != null;
                if (operation == null || operation.IsEmpty()) throw new FileNotFoundException("KKS conversation asset is unavailable: " + reference.bundle + "/" + reference.asset);
                var asset = operation.GetAsset<TAsset>();
                if (asset == null) throw new FormatException("KKS conversation asset has the wrong type: " + reference.asset);
                return copy(asset);
            }
            finally { if (requested) AssetBundleManager.UnloadAssetBundle(reference.bundle, isUnloadForceRefCount: true); }
        }

        private static Dictionary<int, string[]> CopyExcel(ExcelData asset)
        {
            var result = new Dictionary<int, string[]>();
            foreach (var row in asset.list)
            {
                using (var cells = row.list.GetEnumerator())
                {
                    if (!cells.MoveNext() || !int.TryParse(cells.Current, out var key)) continue;
                    var values = new List<string>();
                    while (cells.MoveNext()) values.Add(cells.Current);
                    result[key] = values.ToArray();
                }
            }
            return result;
        }

        private static KksConversationLayout.Row Copy(CommunicationInfo.Param row) => new KksConversationLayout.Row
        {
            priority = row.priority, conditions = row.conditions, favor = row.favor, lewdness = row.lewdness, anger = row.anger,
            assetbundle = row.assetbundle, file = row.file, text = row.text, pose = row.pose,
            facialExpression = row.facialExpression, eyeLook = row.eyeLook, neckLook = row.neckLook, topic = row.topic
        };
        private int NativeNumber(string key) => _nativeNumbers.TryGetValue(key, out var value) ? value : throw new FormatException("Missing KKS format constant: " + key);
        private KksConversationLayout.Row Copy(string bundle, string file, string text, string pose, string expression, int eyes, int neck) => new KksConversationLayout.Row
        {
            assetbundle = bundle, file = file, text = text, pose = pose, facialExpression = expression, eyeLook = eyes, neckLook = neck,
            conditions = NativeNumber("native.layout.-1"), topic = NativeNumber("native.layout.-1")
        };
        private KksConversationLayout.Row Copy(CommunicationNPCData.Param row) => new KksConversationLayout.Row
        {
            priority = row.priority, conditions = row.conditions, assetbundle = row.assetbundle, file = row.file, text = row.text,
            pose = row.pose, facialExpression = row.facialExpression, eyeLook = row.eyeLook, neckLook = row.neckLook,
            topic = NativeNumber("native.layout.-1")
        };
    }
}
