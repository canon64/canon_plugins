using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
#pragma warning disable CS0649
    [DataContract] internal sealed class AiLifeAnimationClip
    {
        [DataMember] public string id, sourceBundle, sourceName, bundle, asset, status, error;
        [DataMember] public float duration, replayError;
        [DataMember] public int mappedBones, samples;
    }
    [DataContract] internal sealed class AiLifeAnimationBinding
    {
        [DataMember] public string id, phase, state, clip, status, error;
    }
    [DataContract] internal sealed class AiLifeAnimationAction
    {
        [DataMember] public string id, description, status, reason, state, outState;
        [DataMember] public int posture;
    }
    internal sealed class AiLifeAnimationStep
    {
        internal string Phase;
        internal AiLifeAnimationClip Clip;
    }
    [DataContract] internal sealed class AiLifeAnimationCatalog
    {
        [DataMember] public AiLifeAnimationClip[] clips;
        [DataMember] public AiLifeAnimationBinding[] bindings;
        internal AiLifeAnimationAction[] Actions = Array.Empty<AiLifeAnimationAction>();
        internal AiLifeAnimationStep[] Sequence(string action)
        {
            const string direct = "clip:";
            if (action.StartsWith(direct, StringComparison.Ordinal))
            {
                var clip = clips.SingleOrDefault(v => v.id == action.Substring(direct.Length));
                ValidateClip(clip);
                return new[] { new AiLifeAnimationStep { Phase = "main", Clip = clip } };
            }
            Resolve(action);
            var entries = bindings.Where(v => v.id == action).ToArray();
            if (entries.Any(v => v.status != "resolved")) throw new InvalidDataException("開始・終了を含む動作が未接続です：" + action);
            return entries.OrderBy(v => Array.IndexOf(new[] { "main:enter", "main", "out:enter", "out" }, v.phase.Contains(":enter:") ? v.phase.Substring(default(int), v.phase.LastIndexOf(':')) : v.phase))
                .ThenBy(v => v.phase.Contains(":enter:") ? int.Parse(v.phase.Split(':').Last(), System.Globalization.CultureInfo.InvariantCulture) : default(int))
                .Select(v => { var clip = clips.Single(c => c.id == v.clip); ValidateClip(clip); return new AiLifeAnimationStep { Phase = v.phase, Clip = clip }; }).ToArray();
        }
        private static void ValidateClip(AiLifeAnimationClip clip)
        {
            if (clip?.status != "converted" || string.IsNullOrWhiteSpace(clip.bundle) || Path.GetFileName(clip.bundle) != clip.bundle || string.IsNullOrWhiteSpace(clip.asset)
                || clip.duration <= default(float) || float.IsNaN(clip.duration) || float.IsInfinity(clip.duration)) throw new InvalidDataException("生活動作の変換ファイルが未確認です。");
        }
        internal AiLifeAnimationClip Resolve(string action)
        {
            var binding = bindings.SingleOrDefault(v => v.id == action && v.phase == "main");
            if (binding?.status != "resolved") throw new InvalidDataException("この生活行動の動作は未接続です：" + action);
            var clip = clips.SingleOrDefault(v => v.id == binding.clip);
            ValidateClip(clip);
            return clip;
        }
        internal static AiLifeAnimationCatalog Load(string content)
        {
            var path = Path.Combine(content, "LifeAnimations", "catalog.json");
            if (!File.Exists(path)) return null;
            using (var stream = File.OpenRead(path))
            {
                var value = (AiLifeAnimationCatalog)new DataContractJsonSerializer(typeof(AiLifeAnimationCatalog)).ReadObject(stream);
                if (value?.clips == null || value.bindings == null || value.clips.Any(v => v == null || string.IsNullOrWhiteSpace(v.id))
                    || value.clips.Select(v => v.id).Distinct().Count() != value.clips.Length
                    || value.bindings.Any(v => v == null || string.IsNullOrWhiteSpace(v.id))) throw new InvalidDataException("生活動作一覧が不正です。");
                var actions = Path.Combine(content, "LifeAnimations", "actions.json");
                if (File.Exists(actions)) using (var definitions = File.OpenRead(actions)) value.Actions = (AiLifeAnimationAction[])new DataContractJsonSerializer(typeof(AiLifeAnimationAction[])).ReadObject(definitions);
                return value;
            }
        }
    }
#pragma warning restore CS0649
}
