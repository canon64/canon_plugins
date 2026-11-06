using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameAiShoujoMode.Lighting
{
    [DataContract] internal sealed class LightingValue
    {
        [DataMember] public string name, kind, text;
        [DataMember] public float[] values;
    }
    [DataContract] internal sealed class LightingEffect
    {
        [DataMember] public string type, assembly, path;
        [DataMember] public bool enabled, imageEffect;
        [DataMember] public int order;
        [DataMember] public LightingValue[] fields = Array.Empty<LightingValue>();
        internal string Signature => LightingJson.Write(fields);
    }
    [DataContract] internal sealed class LightingLight
    {
        [DataMember] public string path, anchor, cookie;
        [DataMember] public bool enabled, active;
        [DataMember] public LightingValue[] settings;
    }
    [DataContract] internal sealed class LightingCamera
    {
        [DataMember] public string path, role;
        [DataMember] public LightingValue[] settings;
        [DataMember] public LightingEffect[] components;
    }
    [DataContract] internal sealed class LightingRenderer
    {
        [DataMember] public string path;
        [DataMember] public LightingValue[] settings;
    }
    [DataContract] internal sealed class LightingSnapshot
    {
        [DataMember] public string stage, utc, weather, viewNote;
        [DataMember] public float hour;
        [DataMember] public int year, month, day;
        [DataMember] public LightingValue[] environment;
        [DataMember] public LightingLight[] lights;
        [DataMember] public LightingCamera[] cameras;
        [DataMember] public LightingRenderer[] characters;
    }
    [DataContract] internal sealed class LightingProfile
    {
        [DataMember] public string schema = "ai-h-lighting-v1", capturedUtc, evidence;
        [DataMember] public bool confirmed;
        [DataMember] public LightingLight light;
        [DataMember] public LightingEffect[] effects = Array.Empty<LightingEffect>();
        internal void Validate()
        {
            if (schema != "ai-h-lighting-v1" || !confirmed || string.IsNullOrWhiteSpace(evidence)
                || (light == null && (effects == null || !effects.Any())))
                throw new InvalidDataException("比較で確認した照明設定だけを保存してください。");
            if (light != null && (!string.IsNullOrEmpty(light.cookie) || (light.anchor != "camera" && light.anchor != "world") || light.settings == null))
                throw new InvalidDataException("補助ライトの参照または座標基準を保存できません。");
            foreach (var effect in effects ?? Array.Empty<LightingEffect>())
                if (effect == null || !effect.imageEffect || string.IsNullOrWhiteSpace(effect.type) || effect.fields == null
                    || effect.fields.Any(value => value.kind == "unsupported"))
                    throw new InvalidDataException("再現できない画面効果は保存できません。");
            var values = (light?.settings ?? Array.Empty<LightingValue>()).Concat((effects ?? Array.Empty<LightingEffect>()).SelectMany(e => e.fields));
            if (values.Any(value => value == null || string.IsNullOrEmpty(value.name)
                || (value.values != null && value.values.Any(n => float.IsNaN(n) || float.IsInfinity(n)))))
                throw new InvalidDataException("照明設定に不正な値が含まれています。");
        }
    }
    internal static class LightingJson
    {
        internal static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        internal static T Read<T>(string text)
        { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
        internal static LightingProfile Load(string path)
        {
            if (!File.Exists(path)) return null;
            var profile = Read<LightingProfile>(File.ReadAllText(path, Encoding.UTF8));
            if (profile == null) throw new InvalidDataException("照明設定が空です。");
            profile.Validate(); return profile;
        }
        internal static void Save(string path, LightingProfile profile)
        {
            profile.Validate(); var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full));
            var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Write(profile), new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    // The UI explicitly chooses and confirms a cause. A mere recorded difference is never proof.
    internal sealed class LightingComparison
    {
        internal bool LightConfirmed { get; private set; }
        internal bool EffectsConfirmed { get; private set; }
        internal readonly HashSet<string> ConfirmedEffects = new HashSet<string>(StringComparer.Ordinal);
        internal string Mode { get; private set; } = "baseline";
        internal void Select(string mode)
        {
            if (mode != "baseline" && mode != "without-light" && mode != "without-effects" && !mode.StartsWith("without-effect:", StringComparison.Ordinal)) throw new ArgumentException(nameof(mode));
            Mode = mode;
        }
        internal void Confirm()
        {
            if (Mode == "without-light") LightConfirmed = true;
            else if (Mode == "without-effects") EffectsConfirmed = true;
            else if (Mode.StartsWith("without-effect:", StringComparison.Ordinal)) { EffectsConfirmed = true; ConfirmedEffects.Add(Mode.Substring("without-effect:".Length)); }
            else throw new InvalidOperationException("補助ライトまたは画面効果を切り替えてから結果を確認してください。");
        }
        internal void Reset() { LightConfirmed = false; EffectsConfirmed = false; ConfirmedEffects.Clear(); Mode = "baseline"; }
    }
}
