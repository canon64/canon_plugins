using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    #pragma warning disable CS0649 // Serialized runtime catalog fields are populated by JsonUtility.
    [DataContract] internal sealed class AudioReference { [DataMember] public string table, bundle, asset, file; [DataMember] public int id; }
    [DataContract] internal sealed class AudioTable { [DataMember] public string name, source, text; }
    [DataContract] internal sealed class AudioCatalogData { [DataMember] public int version; [DataMember] public AudioReference[] references; [DataMember] public AudioTable[] tables; }
    #pragma warning restore CS0649
    public sealed class NativeAudioCatalog
    {
        private readonly AudioCatalogData data;
        private readonly Dictionary<string, AudioReference> references;
        private readonly HashSet<string> played = new HashSet<string>(StringComparer.Ordinal);
        private readonly string directory;
        private readonly HashSet<string> statePlayed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> effectiveRows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        public NativeAudioCatalog(string path)
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(path));
            data = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<AudioCatalogData>(path);
            if (data == null || data.version != 1 || data.references == null || data.tables == null) throw new InvalidDataException("Native H audio catalog is invalid.");
            references = new Dictionary<string, AudioReference>(StringComparer.Ordinal);
            foreach (var reference in data.references) references[reference.table + ":" + reference.id] = reference;
            foreach (var table in data.tables)
            {
                bool voice = table.name.StartsWith("HVoiceFace_", StringComparison.Ordinal);
                if (!voice && !table.name.StartsWith("HBreath_", StringComparison.Ordinal) && !table.name.StartsWith("HShort_breath_", StringComparison.Ordinal)) continue;
                int previous = -1;
                foreach (var line in table.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cells = line.Split('\t'); if (cells.Length <= 4) continue;
                    if (!int.TryParse(cells[0], out int id)) { if (!voice) continue; id = previous + 1; }
                    previous = id; effectiveRows[table.name + ":" + id] = cells;
                }
            }
        }
        public void ValidateProfile(NativeVoiceContext c)
        {
            c.Validate();
            string voice = "HVoiceFace_" + c.Personality.ToString("00") + "_";
            string breath = "HBreath_" + (c.Personality == -90 ? 5 : c.Personality).ToString("00");
            string shortBreath = "HShort_breath_" + c.Personality.ToString("00");
            var selected = references.Values.Where(r => r.table.StartsWith(voice, StringComparison.Ordinal) || r.table == breath || r.table == shortBreath).ToArray();
            if (selected.Length == 0) throw new InvalidDataException("Native AI audio profile has no effective voice table.");
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var r in selected)
            {
                if (string.IsNullOrWhiteSpace(r.file)) throw new InvalidDataException("Native AI audio conversion is missing: " + r.table + ":" + r.id);
                string path = Path.GetFullPath(Path.Combine(directory, r.file));
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new FileNotFoundException("Converted native AI audio is missing: " + r.file);
            }
        }
        public string VoiceFile(string bundle, string asset)
        {
            var reference = references.Values.FirstOrDefault(r => r.bundle == bundle && r.asset == asset);
            if (reference == null || string.IsNullOrWhiteSpace(reference.file)) throw new FileNotFoundException("Native ADV voice conversion is missing: " + bundle + "/" + asset);
            return reference.file;
        }
        public string EffectFile(string name)
        {
            var reference = references.Values.FirstOrDefault(r => r.table == "SE" && r.asset == name);
            if (reference == null || string.IsNullOrWhiteSpace(reference.file)) throw new FileNotFoundException("Native H SE conversion is missing: " + name);
            string file = Path.GetFullPath(Path.Combine(directory, reference.file));
            if (!file.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) throw new FileNotFoundException("Native H SE file is missing: " + name);
            return file;
        }
        public VoiceRequest SelectStart(NativeVoiceContext c, int timing, Func<int, int> random)
        {
            c.Validate();
            if (timing == 4 && c.FemaleInitiative || c.Yobai) return null;
            string name = c.Merchant ? "HStartVoicePattern_-90" : "HStartVoicePattern";
            var rows = new List<string[]>(); var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var table in data.tables.Where(t => t.name == name))
            foreach (var line in table.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var row = line.Split('\t');
                if (row.Length < 9 || !int.TryParse(row[0], out _)) continue;
                string key = string.Join(":", row.Take(4));
                if (indices.TryGetValue(key, out var i)) rows[i] = row; else { indices.Add(key, rows.Count); rows.Add(row); }
            }
            foreach (var row in rows)
            {
                if (I(row[2]) != c.Mode || I(row[3]) != timing || !c.Merchant && !NativeVoiceConditions.VoiceCharacter(I(row[0]), c)) continue;
                int force = I(row[1]);
                if (force == 0 && (c.Force || !c.NormalStartEvent)) continue;
                if (force != -1 && force != 0 && (c.MapH || !c.Force || force == 2 && c.Place != 15 || force == 3 && c.Place != 12 || force == 4 && !c.EndTaskMasturbation || force == 5 && c.Mood > c.StartMoodLower)) continue;
                var candidates = new List<AudioReference>();
                for (int column = 4; column + 4 < row.Length && row[column].Length != 0; column += 5)
                {
                    int[] ids = Csv(row[column]);
                    if (ids.Length == 0 || !ids.Contains(-1) && !ids.Contains(c.MotionId) || !NativeVoiceConditions.StartSpecific(Csv(row[column + 1]), c)) continue;
                    string voiceTable = "HVoiceFace_" + c.Personality.ToString("00", CultureInfo.InvariantCulture) + "_" + I(row[column + 2]).ToString("00") + "_" + I(row[column + 3]).ToString("00");
                    foreach (int id in Csv(row[column + 4])) if (references.TryGetValue(voiceTable + ":" + id, out var reference)) candidates.Add(reference);
                }
                if (candidates.Count == 0) continue;
                var available = candidates.Where(r => !played.Contains(c.Slot + ":" + r.table + ":" + r.id)).ToList();
                if (available.Count == 0)
                {
                    // Native exhaustion resets only this eligible category's IDs.
                    foreach (var r in candidates) played.Remove(c.Slot + ":" + r.table + ":" + r.id);
                    available = candidates;
                }
                var selected = available[random(available.Count)];
                played.Add(c.Slot + ":" + selected.table + ":" + selected.id);
                return new VoiceRequest { Slot = c.Slot, Bundle = selected.bundle, Asset = selected.asset, LocalFile = selected.file, Kind = timing == 3 || timing == 4 || timing == 7 ? VoiceKind.Voice : VoiceKind.StartVoice, Protect = Protected(selected), NativeFace = Face(selected, VoiceKind.Voice, c, random) };
            }
            return null;
        }
        private bool Protected(AudioReference reference)
        {
            int flag = reference.table.StartsWith("HBreath_", StringComparison.Ordinal) ? 4 : 3;
            return effectiveRows.TryGetValue(reference.table + ":" + reference.id, out var cells) && cells.Length > flag && cells[flag] == "1";
        }
        public VoiceRequest SelectShort(NativeVoiceContext c, int actionMode, int actionKind, int requestKind, Func<int, int> random)
        {
            c.Validate(); if (c.Yobai) return null;
            if (actionMode < 3) actionKind = 0;
            string pattern = "HShortBreathPattern_" + c.Slot.ToString("00") + "_" + actionMode.ToString("00") + "_" + actionKind.ToString("00");
            var baseGroups = ShortGroups(pattern, c.Slot, requestKind);
            var added = new Dictionary<string, string[]>(StringComparer.Ordinal);
            for (int suffix = 0; suffix < 5; suffix++)
                foreach (var group in ShortGroups(pattern + "_" + suffix.ToString("00"), c.Slot, requestKind)) added[group[0] + "|" + group[1]] = group;
            baseGroups.AddRange(added.Values);
            var candidates = new List<AudioReference>();
            foreach (var group in baseGroups)
            {
                if (!NativeVoiceConditions.Motion(Csv(group[1]), c.MotionId, VoiceKind.ShortBreath) || !Csv(group[0]).All(n => NativeVoiceConditions.Short(n, c))) continue;
                string table = "HShort_breath_" + c.Personality.ToString("00");
                foreach (int id in Csv(group[2])) if (references.TryGetValue(table + ":" + id, out var reference)) candidates.Add(reference);
            }
            if (candidates.Count == 0) return null;
            var selected = candidates[random(candidates.Count)];
            return new VoiceRequest { Slot = c.Slot, Bundle = selected.bundle, Asset = selected.asset, LocalFile = selected.file, Kind = VoiceKind.ShortBreath, Protect = Protected(selected), NativeFace = Face(selected, VoiceKind.ShortBreath, c, random) };
        }
        public VoiceRequest SelectVoice(NativeVoiceContext c, string state, bool requested, bool dialog, bool otherSpeaking, bool reverseDialog, Func<int, int> random)
        {
            c.Validate(); if (c.Yobai) return null;
            int kind = c.Mode == 3 ? new[] { 0, 7 }.Contains(c.MotionId) ? 1 : new[] { 10, 11, 12 }.Contains(c.MotionId) ? 2 : 0 : 0;
            string name = "HVoicePattern_" + (c.Merchant ? "-90_" : "") + c.Mode.ToString("00") + "_" + kind.ToString("00");
            var rows = new List<string[]>(); var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var table in data.tables.Where(t => t.name == name))
            foreach (var line in table.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var row = line.Split('\t'); if (row.Length < 10 || !int.TryParse(row[0], out _)) continue;
                string key = string.Join(":", row.Take(5));
                if (index.TryGetValue(key, out int position)) rows[position] = row; else { index.Add(key, rows.Count); rows.Add(row); }
            }
            string stateKey = c.Mode + ":" + c.MotionId + ":" + c.Slot + ":" + state;
            foreach (var row in rows)
            {
                bool normalEvent = c.Mood >= c.StartMoodLower && c.NormalStartEvent;
                int start = I(row[1]), how = I(row[2]); bool look = row[3] == "0";
                if (row[4] != state || !c.Merchant && !NativeVoiceConditions.VoiceCharacter(I(row[0]), c) || start == 0 && !normalEvent || start == 1 && normalEvent) continue;
                if (how == 0)
                {
                    bool specialManual = c.Mode == 3 && new[] { 3, 13, 14 }.Contains(c.MotionId) && new[] { "WLoop", "MLoop", "SLoop", "OLoop" }.Contains(state);
                    if (specialManual && !look) continue;
                    if (look ? !requested : statePlayed.Contains(stateKey)) continue;
                }
                else
                {
                    if (reverseDialog ? how == 1 && (dialog || c.Slot == 1) || how == 2 && (!dialog || c.Slot == 0) : how == 1 && (dialog || c.Slot == 0) || how == 2 && (!dialog || c.Slot == 1)) continue;
                    bool leader = reverseDialog ? c.Slot == 0 : c.Slot == 1;
                    if (look && leader && (!requested || statePlayed.Contains(stateKey)) || !look && statePlayed.Contains(stateKey) || otherSpeaking) continue;
                }
                var candidates = new List<AudioReference>();
                for (int column = 5; column + 4 < row.Length && row[column].Length != 0; column += 5)
                {
                    int[] conditions = Csv(row[column + 1]);
                    if (!NativeVoiceConditions.Motion(Csv(row[column]), c.MotionId, VoiceKind.Voice) || c.Initiative != 0 && !conditions.Contains(13) || !NativeVoiceConditions.VoiceSpecific(conditions, c)) continue;
                    if (conditions.Contains(0) && c.Mode < 4) c.HouchiTime = 0;
                    string voiceTable = "HVoiceFace_" + c.Personality.ToString("00") + "_" + I(row[column + 2]).ToString("00") + "_" + I(row[column + 3]).ToString("00");
                    foreach (int id in Csv(row[column + 4])) if (references.TryGetValue(voiceTable + ":" + id, out var reference)) candidates.Add(reference);
                }
                if (candidates.Count == 0) continue;
                var available = candidates.Where(r => !played.Contains(c.Slot + ":" + r.table + ":" + r.id)).ToList();
                if (available.Count == 0 && how == 0) { foreach (var r in candidates) played.Remove(c.Slot + ":" + r.table + ":" + r.id); available = candidates; }
                if (available.Count == 0) continue;
                var chosen = available[random(available.Count)]; played.Add(c.Slot + ":" + chosen.table + ":" + chosen.id); statePlayed.Add(stateKey);
                return new VoiceRequest { Slot = c.Slot, Bundle = chosen.bundle, Asset = chosen.asset, LocalFile = chosen.file, Kind = VoiceKind.Voice, Protect = Protected(chosen), HowTalk = how, State = state, NativeFace = Face(chosen, VoiceKind.Voice, c, random) };
            }
            return null;
        }
        public VoiceRequest SelectBreath(NativeVoiceContext c, int mode, int kind, string state, Func<int, int> random)
        {
            c.Validate(); if (mode < 3) kind = 0;
            int level = c.Force && !c.Yobai ? 2 : c.FemaleInitiative && mode != 4 ? 3 : c.PersonalPhase > 1 ? 1 : 0;
            string pattern = "HBreathPattern_" + c.Slot.ToString("00") + "_" + mode.ToString("00") + "_" + kind.ToString("00");
            var groups = BreathGroups(pattern, c.Slot, level, state, out bool force);
            bool onlyOnce = false;
            foreach (var baseTable in data.tables.Where(t => t.name == pattern))
                foreach (var line in baseTable.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                { var cells = line.Split('\t'); if (cells.Length > 3 && cells[0] == c.Slot.ToString() && cells[1] == level.ToString() && cells[2] == state) onlyOnce = cells[3] == "1"; }
            var added = new Dictionary<string, string[]>(StringComparer.Ordinal);
            for (int suffix = 0; suffix < 5; suffix++)
                foreach (var group in BreathGroups(pattern + "_" + suffix.ToString("00"), c.Slot, level, state, out _)) added[group[0] + "|" + group[1]] = group;
            groups.AddRange(added.Values);
            var candidates = new List<AudioReference>();
            foreach (var group in groups)
            {
                if (!NativeVoiceConditions.Motion(Csv(group[1]), c.MotionId, VoiceKind.Breath) || !NativeVoiceConditions.Breath(Csv(group[0]), c)) continue;
                string table = "HBreath_" + (c.Personality == -90 ? 5 : c.Personality).ToString("00");
                foreach (int id in Csv(group[2])) if (references.TryGetValue(table + ":" + id, out var reference)) candidates.Add(reference);
            }
            if (candidates.Count == 0) return null;
            var selected = candidates[random(candidates.Count)];
            string groupId = selected.table + ":" + selected.id;
            foreach (var source in data.tables.Where(t => t.name == selected.table))
                foreach (var line in source.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                { var cells = line.Split('\t'); if (cells.Length > 3 && int.TryParse(cells[0], out int id) && id == selected.id) { groupId = cells[3]; break; } }
            return new VoiceRequest { Slot = c.Slot, Bundle = selected.bundle, Asset = selected.asset, LocalFile = selected.file, Kind = VoiceKind.Breath, Protect = Protected(selected), Group = groupId, Force = force, OnlyOnce = onlyOnce, State = state, NativeFace = Face(selected, VoiceKind.Breath, c, random) };
        }
        private NativeFaceRequest Face(AudioReference reference, VoiceKind kind, NativeVoiceContext context, Func<int, int> random)
        {
            if (!effectiveRows.TryGetValue(reference.table + ":" + reference.id, out var values)) return null;
            var faces = new List<NativeFaceRequest>();
            int offset = kind == VoiceKind.Breath ? context.GaugeHit ? 36 : 6 : 4;
            int stride = kind == VoiceKind.Voice || kind == VoiceKind.StartVoice ? 32 : 10;
            int limit = kind == VoiceKind.Breath ? context.GaugeHit ? 46 : 36 : values.Length;
            for (int i = offset; i + 9 < Math.Min(values.Length, limit); i += stride)
            {
                if (!float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float open) || open < 0) break;
                faces.Add(new NativeFaceRequest { EyeOpen = open, MouthMin = F(values[i + 1]), MouthMax = F(values[i + 2]), Brows = I(values[i + 3]), Eyes = I(values[i + 4]), Mouth = I(values[i + 5]), Tear = F(values[i + 6]), Cheek = F(values[i + 7]), Highlight = values[i + 8] == "1", Blink = values[i + 9] == "1" });
            }
            if (faces.Count == 0 && kind == VoiceKind.ShortBreath) throw new InvalidDataException("Native short breath has no face endpoint data: " + reference.table + ":" + reference.id);
            return faces.Count == 0 ? null : faces[random(faces.Count)];
        }
        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private List<string[]> BreathGroups(string pattern, int role, int level, string state, out bool force)
        {
            force = false; var result = new List<string[]>();
            foreach (var table in data.tables.Where(t => t.name == pattern))
            foreach (var line in table.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var row = line.Split('\t'); if (row.Length < 10 || I(row[0]) != role || I(row[1]) != level || row[2] != state) continue;
                result.Clear(); force = row[4] == "1";
                for (int column = 7; column + 2 < row.Length && row[column].Length != 0; column += 3) result.Add(new[] { row[column], row[column + 1], row[column + 2] });
            }
            return result;
        }
        private List<string[]> ShortGroups(string pattern, int role, int requestKind)
        {
            var result = new List<string[]>();
            // Later rows replace the same role/request key without discarding other keys.
            foreach (var table in data.tables.Where(t => t.name == pattern))
            foreach (var line in table.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var row = line.Split('\t'); if (row.Length < 5 || I(row[0]) != role || I(row[1]) != requestKind) continue;
                result.Clear();
                for (int column = 2; column + 2 < row.Length && row[column].Length != 0; column += 3) result.Add(new[] { row[column], row[column + 1], row[column + 2] });
            }
            return result;
        }
        private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        private static int[] Csv(string s) => string.IsNullOrEmpty(s) ? Array.Empty<int>() : s.Split(',').Select(I).ToArray();
    }
}
