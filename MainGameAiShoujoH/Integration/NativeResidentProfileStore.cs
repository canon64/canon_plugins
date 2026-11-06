using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Presentation;

namespace MainGameAiShoujoH.Integration
{
    [DataContract]
    public sealed class NativeResidentProfile
    {
        [DataMember] public string ResidentId;
        [DataMember] public int Personality, PersonalPhase, Bust;
        [DataMember] public float Mood, StartMoodLower, Skill2;
        [DataMember] public float MoodUpper = 80;
        [DataMember] public bool HasAdditionalMotion0, HasAdditionalMotion1;
        [DataMember] public int[] Skills = Array.Empty<int>();
        [DataMember] public NativeHSettings Settings = new NativeHSettings();
        [DataMember] public bool Merchant;
        [DataMember] public List<string> ResultOperations = new List<string>();
        [DataMember] public string Origin, Evidence;
        [OnDeserialized] void Loaded(StreamingContext context) { if (ResultOperations == null) ResultOperations = new List<string>(); if (MoodUpper == 0) MoodUpper = 80; if (Skills == null) Skills = Array.Empty<int>(); if (Settings == null) Settings = new NativeHSettings(); }
        public NativeVoiceContext Context(int slot) => new NativeVoiceContext {
            Personality = Personality, PersonalPhase = PersonalPhase, Bust = Bust, Mood = Mood,
            StartMoodLower = StartMoodLower, Skill2 = Skill2, Slot = slot, Skills = Skills ?? Array.Empty<int>(), Merchant = Merchant,
            Confirmed = true, Evidence = "専用Hの新規設定値: " + Origin + "; " + Evidence };
    }
    // Each stable resident owns a separate extension document; existing cards/life state are never rewritten.
    public sealed class NativeResidentProfileStore
    {
        [DataContract] sealed class Snapshot { [DataMember] public NativeResidentProfile[] Residents; }
        public sealed class SnapshotReport { public string Json; public string[] Failures; }
        readonly string directory;
        readonly Func<NativeResidentProfile> defaults;
        public NativeResidentProfileStore(string directory, Func<NativeResidentProfile> defaults)
        { this.directory = Path.GetFullPath(directory); this.defaults = defaults ?? throw new ArgumentNullException(nameof(defaults)); }
        string PathFor(string identity)
        {
            if (!Guid.TryParseExact(identity, "N", out _)) throw new ArgumentException("Native H profile requires a stable resident UUID.");
            return Path.Combine(directory, identity + ".json");
        }
        public NativeResidentProfile ReadOrCreate(string identity)
        {
            string path = PathFor(identity);
            Directory.CreateDirectory(directory);
            using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(path)) return Read(path, identity);
                var value = defaults(); value.ResidentId = identity; Validate(value, identity); SaveDocument(path, value);
                return Read(path, identity);
            }
        }
        public void Save(NativeResidentProfile value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string path = PathFor(value.ResidentId); Validate(value, value.ResidentId); Directory.CreateDirectory(directory);
            using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(path)) value.ResultOperations = Read(path, value.ResidentId).ResultOperations; // Preserve receipts even when a configuration editor retained an older snapshot.
                SaveDocument(path, value); Read(path, value.ResidentId);
            }
        }
        public void ApplyVoiceResult(Results.ResultDelta delta)
        {
            if (delta == null || string.IsNullOrWhiteSpace(delta.OperationId)) throw new ArgumentException("Native H result operation is missing.");
            if (delta.Parameter != 1 && delta.Parameter != 14) return;
            string path = PathFor(delta.ParticipantId);
            using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var value = Read(path, delta.ParticipantId);
                float amount = delta.Assignment ? delta.AssignedValue ?? delta.Amount : delta.Amount;
                if (float.IsNaN(amount) || float.IsInfinity(amount)) throw new ArgumentException("Non-finite native assignment.");
                string receipt = delta.OperationId + ":" + delta.Parameter + ":" + amount.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + (delta.Assignment ? ":set" : "");
                if (value.ResultOperations.Contains(receipt)) return;
                if (value.ResultOperations.Any(r => r.StartsWith(delta.OperationId + ":", StringComparison.Ordinal))) throw new InvalidOperationException("Native H result operation was reused.");
                if (delta.Parameter == 1) value.Mood = value.PersonalPhase < 2 ? (value.StartMoodLower + value.MoodUpper) / 2 : Math.Max(0, Math.Min(100, (delta.Assignment ? 0 : value.Mood) + amount));
                else
                {
                    value.Skill2 = Math.Max(0, Math.Min(99999, (delta.Assignment ? 0 : value.Skill2) + amount));
                    if (value.Skill2 >= 100) value.HasAdditionalMotion0 = true;
                    if (value.Skill2 >= 170) value.HasAdditionalMotion1 = true;
                }
                value.ResultOperations.Add(receipt); SaveDocument(path, value); Read(path, delta.ParticipantId);
            }
        }
        public static NativeResidentProfile LoadDefaults(string path)
        {
            using (var stream = File.OpenRead(path)) return (NativeResidentProfile)new DataContractJsonSerializer(typeof(NativeResidentProfile)).ReadObject(stream);
        }
        public SnapshotReport Export(string[] identities, string previous = null)
        {
            var failures = new List<string>();
            var records = new Dictionary<string, NativeResidentProfile>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(previous)) foreach (var record in ParseSnapshot(previous).Residents) { Validate(record, record.ResidentId); records[record.ResidentId] = record; }
            foreach (string identity in identities.Distinct(StringComparer.Ordinal))
            {
                try
                {
                    string path = PathFor(identity);
                    if (!File.Exists(path)) continue;
                    using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) records[identity] = Read(path, identity);
                }
                catch (Exception ex) { failures.Add(identity + ": " + ex.Message); }
            }
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Snapshot)).WriteObject(stream, new Snapshot { Residents = records.Values.ToArray() });
                return new SnapshotReport { Json = System.Text.Encoding.UTF8.GetString(stream.ToArray()), Failures = failures.ToArray() };
            }
        }
        public string[] Import(string json)
        {
            var failures = new List<string>();
            foreach (var record in ParseSnapshot(json).Residents)
                try { Validate(record, record.ResidentId); Save(record); }
                catch (Exception ex) { failures.Add((record?.ResidentId ?? "unknown") + ": " + ex.Message); }
            return failures.ToArray();
        }
        static Snapshot ParseSnapshot(string json)
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            {
                var value = (Snapshot)new DataContractJsonSerializer(typeof(Snapshot)).ReadObject(stream);
                if (value?.Residents == null) throw new SerializationException("Native H checkpoint profiles are missing.");
                return value;
            }
        }
        static NativeResidentProfile Read(string path, string identity)
        {
            using (var stream = File.OpenRead(path))
            {
                var value = (NativeResidentProfile)new DataContractJsonSerializer(typeof(NativeResidentProfile)).ReadObject(stream);
                Validate(value, identity); return value;
            }
        }
        static void Validate(NativeResidentProfile value, string identity)
        {
            if (value == null || value.ResidentId != identity || string.IsNullOrWhiteSpace(value.Origin) || string.IsNullOrWhiteSpace(value.Evidence))
                throw new SerializationException("Native H resident profile identity or provenance is missing.");
            value.Context(0).Validate();
            (value.Settings ?? throw new ArgumentException("Native H settings are missing.")).Validate();
            foreach (var n in new[] { value.Mood, value.StartMoodLower, value.MoodUpper, value.Skill2 })
                if (float.IsNaN(n) || float.IsInfinity(n)) throw new SerializationException("Native H resident profile contains a nonfinite value.");
        }
        static void SaveDocument(string path, NativeResidentProfile value)
        {
            string temporary = path + ".pending";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { new DataContractJsonSerializer(typeof(NativeResidentProfile)).WriteObject(stream, value); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }
    }
}
