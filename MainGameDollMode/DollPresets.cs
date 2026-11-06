using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameDollMode
{
    [DataContract]
    internal sealed class DollPresetValues
    {
        [DataMember] public bool ApplyFemaleCharacters = true;
        [DataMember] public bool ApplyMaleCharacters;
        [DataMember] public bool DisableAhegaoPlugins = true;
        [DataMember] public string AhegaoPluginKeywordCsv = "ahegao,maingameahegao,ksplug,kplug";
        [DataMember] public int DollModeEyePattern;
        [DataMember] public int DollModeEyebrowPattern;
        [DataMember] public int DollModeMouthPattern;
        [DataMember] public float DollModeCheekRate;
        [DataMember] public int DollModeTearsLevel;
        [DataMember] public int DollModeFaceSweatLevel;
        [DataMember] public bool DollModeModEffectsEnabled;
        [DataMember] public int DollModeModTears, DollModeModDrool, DollModeModSaliva, DollModeModSweat, DollModeModNose;
        [DataMember] public int DollModeModBukkake;
        [DataMember] public bool DollModeSpiralOverlayEnabled;
        [DataMember] public float DollModeSpiralOffsetX, DollModeSpiralOffsetY;
        [DataMember] public bool AllowFaceSiruDuringDollMode = true;
        [DataMember] public float DollModeMouthOpen = .30f;
        [DataMember] public float DollModeEyesOpen = .80f;
        [DataMember] public bool DollModeMotionLockWEnabled;
        [DataMember] public float DollModeMotionLockWValue = .30f;
        [DataMember] public bool DollModeMotionLockSEnabled;
        [DataMember] public float DollModeMotionLockSValue = .80f;
        [DataMember] public bool DollModeEyeOverlayEnabled;
        [DataMember] public string DollModeEyeOverlayPngPath = "";
        [DataMember] public float DollModeTransitionSeconds = 1f;

        private static readonly FieldInfo[] Fields = typeof(DollPresetValues).GetFields(BindingFlags.Public | BindingFlags.Instance);
        internal static DollPresetValues Capture(PluginSettings settings)
        {
            var values = new DollPresetValues();
            foreach (var field in Fields) field.SetValue(values, typeof(PluginSettings).GetField(field.Name).GetValue(settings));
            return values;
        }
        internal void ApplyTo(PluginSettings settings)
        {
            foreach (var field in Fields) typeof(PluginSettings).GetField(field.Name).SetValue(settings, field.GetValue(this));
            settings.Normalize();
        }
        internal DollPresetValues Clone()
        {
            var copy = new DollPresetValues();
            foreach (var field in Fields) field.SetValue(copy, field.GetValue(this));
            return copy;
        }
        internal DollPresetValues Normalized()
        {
            var settings = new PluginSettings(); ApplyTo(settings); return Capture(settings);
        }
        internal bool Matches(DollPresetValues other)
        {
            if (other == null) return false;
            foreach (var field in Fields) if (!Equals(field.GetValue(this), field.GetValue(other))) return false;
            return true;
        }
        [OnDeserializing] private void BeforeRead(StreamingContext context)
        {
            var defaults = new DollPresetValues();
            foreach (var field in Fields) field.SetValue(this, field.GetValue(defaults));
        }
    }

    [DataContract]
    internal sealed class DollPreset
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public DollPresetValues Values;
    }

    [DataContract]
    internal sealed class DollPresetLibrary
    {
        internal const string BasicId = "basic";
        [DataMember] public int Version = 1;
        [DataMember] public string SelectedId = BasicId;
        [DataMember] public List<DollPreset> Presets = new List<DollPreset>();
        internal static DollPresetLibrary Create(PluginSettings current)
        {
            var library = new DollPresetLibrary();
            library.Presets.Add(new DollPreset { Id = BasicId, Name = "基本", Values = DollPresetValues.Capture(current) });
            return library;
        }
        internal DollPreset Find(string id) => Presets.Find(p => p != null && p.Id == id);
        internal string Add(string name, DollPresetValues values)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 64) throw new InvalidOperationException("プリセット名は1～64文字で入力してください。");
            if (Presets.Exists(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同じ名前のプリセットがあります。");
            string id = Guid.NewGuid().ToString("N");
            Presets.Add(new DollPreset { Id = id, Name = name, Values = values.Normalized() });
            return id;
        }
        internal void Overwrite(string id, DollPresetValues values)
        {
            if (id == BasicId) throw new InvalidOperationException("基本は上書きできません。別名で保存してください。");
            var preset = Find(id) ?? throw new InvalidOperationException("プリセットが見つかりません。");
            preset.Values = values.Normalized();
        }
        internal void Delete(string id)
        {
            if (id == BasicId) throw new InvalidOperationException("基本は削除できません。");
            Presets.RemoveAll(p => p.Id == id);
            if (SelectedId == id) SelectedId = BasicId;
        }
        internal DollPresetLibrary Clone()
        {
            var copy = new DollPresetLibrary { Version = Version, SelectedId = SelectedId };
            foreach (var p in Presets) copy.Presets.Add(new DollPreset { Id = p.Id, Name = p.Name, Values = p.Values.Clone() });
            return copy;
        }
        internal static DollPresetLibrary Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var library = new DataContractJsonSerializer(typeof(DollPresetLibrary)).ReadObject(stream) as DollPresetLibrary;
                if (library == null || library.Version != 1 || library.Presets == null || library.Find(BasicId) == null) throw new InvalidDataException("プリセットファイルの形式を確認してください。");
                var ids = new HashSet<string>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var preset in library.Presets)
                {
                    if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || string.IsNullOrWhiteSpace(preset.Name) || preset.Values == null || !ids.Add(preset.Id) || !names.Add(preset.Name)) throw new InvalidDataException("プリセットに欠落・重複があります。");
                }
                if (library.Find(library.SelectedId) == null) library.SelectedId = BasicId;
                return library;
            }
        }
        internal void Save(string path)
        {
            string temp = path + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(DollPresetLibrary)).WriteObject(stream, this);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
