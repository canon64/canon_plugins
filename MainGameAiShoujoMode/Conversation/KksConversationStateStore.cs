using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Conversation
{
    // Stable resident identity and native KKS conversation state. AI growth/illness are separate records.
    internal sealed class KksConversationStateStore
    {
#pragma warning disable CS0649 // DataContract members are also populated by deserialization and callers outside linked test subsets.
        [DataContract] internal sealed class Result
        {
            [DataMember] public string id, bundle, asset;
            [DataMember] public int blockRow, nativeFavor, nativeLewdness, appliedFavor, appliedLewdness, topic;
            [DataMember] public int? selected, correct;
        }
        [DataContract] internal sealed class Resident
        {
            [DataMember] public string id, origin, revision;
            [DataMember] public int personality, favor, lewdness, callId;
            [DataMember] public bool friend, girlfriend;
            [DataMember] public byte[] temperament;
            [DataMember] public List<int> events = new List<int>(), introductions = new List<int>(), topics = new List<int>();
            [DataMember] public List<Result> results = new List<Result>();
        }
        [DataContract] internal sealed class PointChange
        {
            [DataMember] public string id, bundle, asset;
            [DataMember] public int sourceRow, delta, applied;
            [DataMember] public bool displayed;
            [DataMember] public int displayDelta, awardApplied;
        }
        [DataContract] internal sealed class PlayerState
        {
            [DataMember] public string revision;
            [DataMember] public int points, totalPoints;
            [DataMember] public List<PointChange> pointChanges = new List<PointChange>();
        }
        [DataContract] private sealed class Document
        {
            [DataMember] public int schema;
            [DataMember] public List<Resident> residents = new List<Resident>();
            internal PlayerState Player;
            internal bool HasPlayer;
            [DataMember(Name = "player")] private PlayerState SerializedPlayer
            {
                get => Player;
                set { Player = value; HasPlayer = true; }
            }
        }
#pragma warning restore CS0649
        private readonly string _path;
        private readonly int _schema;
        private Document _document;
        private static byte[] Encode<T>(T value)
        {
            using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return stream.ToArray(); }
        }
        private static T Decode<T>(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        private static T Copy<T>(T value) => Decode<T>(Encode(value));
        internal KksConversationStateStore(string path, int schema)
        {
            _path = Path.GetFullPath(path); _schema = schema;
            using (Acquire()) Refresh();
        }
        private IDisposable Acquire()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private void Refresh()
        {
            var current = File.Exists(_path) ? Decode<Document>(File.ReadAllBytes(_path)) : new Document
                { schema = _schema, Player = new PlayerState { revision = "initial-player" }, HasPlayer = true };
            // A missing legacy member is different from an explicitly null/corrupt player record.
            if (current != null && !current.HasPlayer) { current.Player = new PlayerState { revision = "legacy-player" }; current.HasPlayer = true; }
            Validate(current); _document = current;
        }
        private void Validate(Document document)
        {
            if (document?.residents == null || document.schema != _schema) throw new InvalidDataException("Unknown KKS conversation save format.");
            if (document.Player == null || string.IsNullOrWhiteSpace(document.Player.revision) || document.Player.pointChanges == null
                || document.Player.points < default(int) || document.Player.totalPoints < default(int)) throw new InvalidDataException("AI-mode player point record is invalid.");
            var pointOperations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var operation in document.Player.pointChanges)
                if (operation == null || string.IsNullOrWhiteSpace(operation.id) || string.IsNullOrWhiteSpace(operation.bundle)
                    || string.IsNullOrWhiteSpace(operation.asset) || !pointOperations.Add(operation.id)
                    || (!operation.displayed && (operation.displayDelta != default(int) || operation.awardApplied != default(int)))) throw new InvalidDataException("AI-mode point history is invalid.");
            var identities = new HashSet<string>(StringComparer.Ordinal); var origins = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resident in document.residents)
            {
                if (resident == null || string.IsNullOrWhiteSpace(resident.id) || string.IsNullOrWhiteSpace(resident.origin)
                    || string.IsNullOrWhiteSpace(resident.revision) || resident.temperament == null || resident.events == null
                    || resident.introductions == null || resident.topics == null || resident.results == null
                    || !identities.Add(resident.id) || !origins.Add(resident.origin)) throw new InvalidDataException("KKS conversation resident record is incomplete or duplicated.");
                var operations = new HashSet<string>(StringComparer.Ordinal);
                foreach (var result in resident.results)
                    if (result == null || string.IsNullOrWhiteSpace(result.id) || string.IsNullOrWhiteSpace(result.bundle)
                        || string.IsNullOrWhiteSpace(result.asset) || !operations.Add(result.id)) throw new InvalidDataException("KKS conversation result history is invalid.");
            }
        }
        internal Resident GetOrCreate(string origin, Func<Resident> create)
        {
            if (string.IsNullOrWhiteSpace(origin)) throw new ArgumentException("A resident needs a stable origin.", nameof(origin));
            using (Acquire())
            {
                Refresh();
                var existing = _document.residents.SingleOrDefault(resident => resident.origin == origin);
                if (existing != null) return Copy(existing);
                var candidate = Copy(_document); var resident = create() ?? throw new InvalidOperationException("Native resident initialization failed.");
                resident.id = Guid.NewGuid().ToString("N"); resident.origin = origin; resident.revision = Guid.NewGuid().ToString("N");
                candidate.residents.Add(Copy(resident)); Publish(candidate); return Copy(resident);
            }
        }
        internal Resident Commit(Resident expected, Result operation, Action<Resident> update)
        {
            if (expected == null || operation == null || string.IsNullOrWhiteSpace(operation.id)) throw new ArgumentNullException(nameof(operation));
            using (Acquire())
            {
                Refresh();
                var current = _document.residents.Single(resident => resident.id == expected.id);
                if (current.results.Any(result => result.id == operation.id)) return Copy(current);
                if (current.revision != expected.revision) throw new InvalidOperationException("KKS conversation state changed during this dialogue.");
                var candidate = Copy(_document); var resident = candidate.residents.Single(value => value.id == current.id);
                update(resident);
                if (resident.id != current.id || resident.origin != current.origin) throw new InvalidOperationException("A dialogue result cannot change resident identity.");
                resident.results.Add(Copy(operation)); resident.revision = Guid.NewGuid().ToString("N");
                Publish(candidate); return Copy(resident);
            }
        }
        internal PlayerState ReadPlayer()
        {
            using (Acquire()) { Refresh(); return Copy(_document.Player); }
        }
        internal string ExportMoment()
        { using (Acquire()) { Refresh(); return System.Text.Encoding.UTF8.GetString(Encode(_document)); } }
        internal PlayerState CommitPoints(PlayerState expected, PointChange operation, int minimum, int maximum, out bool applied)
        {
            if (expected == null || operation == null || string.IsNullOrWhiteSpace(operation.id)) throw new ArgumentNullException(nameof(operation));
            if (minimum > maximum) throw new ArgumentException("Invalid native point limits.");
            applied = false;
            using (Acquire())
            {
                Refresh(); var current = _document.Player;
                var previous = current.pointChanges.SingleOrDefault(change => change.id == operation.id);
                if (previous != null)
                {
                    if (previous.bundle != operation.bundle || previous.asset != operation.asset || previous.sourceRow != operation.sourceRow || previous.delta != operation.delta)
                        throw new InvalidOperationException("Point operation ID was reused for a different command.");
                    return Copy(current);
                }
                if (current.revision != expected.revision) throw new InvalidOperationException("AI-mode player points changed during this operation.");
                if (current.points < minimum || current.points > maximum) throw new InvalidDataException("Saved points exceed native limits.");
                var candidate = Copy(_document); var change = Copy(operation);
                change.displayed = false; change.displayDelta = default; change.awardApplied = default;
                // Native Player setter clamps the unchecked Int32 addition, including overflow.
                var points = Math.Max(minimum, Math.Min(maximum, unchecked(current.points + operation.delta)));
                change.applied = points - current.points; candidate.Player.points = points;
                candidate.Player.pointChanges.Add(change); candidate.Player.revision = Guid.NewGuid().ToString("N");
                Publish(candidate); applied = true; return Copy(candidate.Player);
            }
        }
        // Native AddPoint.Set awards only its positive target-minus-displayed-score, at the display stage.
        // That amount can span commands when an earlier display was cancelled before AddPoint.Set.
        internal PlayerState MarkPointDisplayed(string operationId, int displayDelta, int minimum, int maximum, out bool applied)
        {
            if (string.IsNullOrWhiteSpace(operationId)) throw new ArgumentException("A point display needs its command ID.", nameof(operationId));
            if (minimum > maximum) throw new ArgumentException("Invalid native point limits.");
            applied = false;
            using (Acquire())
            {
                Refresh(); var current = _document.Player;
                var previous = current.pointChanges.SingleOrDefault(change => change.id == operationId)
                    ?? throw new InvalidOperationException("A point display has no committed command.");
                if (previous.displayed)
                {
                    if (previous.displayDelta != displayDelta) throw new InvalidOperationException("Point display ID was reused with a different amount.");
                    return Copy(current);
                }
                if (current.totalPoints < minimum || current.totalPoints > maximum) throw new InvalidDataException("Saved total points exceed native limits.");
                var candidate = Copy(_document); var change = candidate.Player.pointChanges.Single(value => value.id == operationId);
                var total = displayDelta > default(int) ? Math.Max(minimum, Math.Min(maximum, unchecked(current.totalPoints + displayDelta))) : current.totalPoints;
                change.displayed = true; change.displayDelta = displayDelta; change.awardApplied = total - current.totalPoints;
                candidate.Player.totalPoints = total; candidate.Player.revision = Guid.NewGuid().ToString("N");
                Publish(candidate); applied = true; return Copy(candidate.Player);
            }
        }
        private void Publish(Document candidate)
        {
            Validate(candidate);
            var directory = Path.GetDirectoryName(_path); Directory.CreateDirectory(directory);
            var pending = Path.Combine(directory, Path.GetFileName(_path) + ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                var bytes = Encode(candidate);
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, default, bytes.Length); stream.Flush(true); }
                if (File.Exists(_path)) File.Replace(pending, _path, _path + ".previous"); else File.Move(pending, _path);
                _document = candidate;
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
