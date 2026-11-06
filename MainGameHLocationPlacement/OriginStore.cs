using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameHLocationPlacement
{
    internal sealed class OriginStore
    {
        private readonly string _directory;
        private readonly List<PresetOrigin> _records = new List<PresetOrigin>();
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.Ordinal);
        private static string Key(string owner, string document, string identity) => owner + "\n" + Normalize(document) + "\n" + identity;
        internal OriginStore(string directory)
        {
            _directory = Path.GetFullPath(directory);
            if (!Directory.Exists(_directory)) return;
            foreach (string path in Directory.GetFiles(_directory, "*.json"))
            {
                var record = Read(path);
                if (record.version != 1) throw new InvalidDataException("未対応の保存元情報: " + path);
                foreach (var part in record.parts) part.source.Validate();
                _records.Add(record);
            }
            foreach (string path in Directory.GetFiles(_directory, "*.pending-*"))
            {
                var pending = Read(path);
                _blocked.Add(Key(pending.owner, pending.document, pending.identity));
                if (pending.replacesIdentity != null) _blocked.Add(Key(pending.owner, pending.document, pending.replacesIdentity));
            }
        }
        internal static string Normalize(string document) => Path.GetFullPath(document).ToUpperInvariant();
        internal PresetOrigin Find(string document, SavedPreset preset)
        {
            string path = Normalize(document);
            if (_blocked.Contains(Key(preset.Owner, path, preset.Identity))) return null;
            var sameDocument = _records.Where(r => r.owner == preset.Owner && r.document == path && !_blocked.Contains(Key(r.owner, r.document, r.identity))).ToList();
            var exact = sameDocument.Where(r => r.identity == preset.Identity).ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1) return null;
            // Composer has no stable ID. A unique geometry match can preserve provenance through rename.
            if (preset.Owner != PresetDocuments.Composer) return null;
            var renamed = sameDocument.Where(r => r.poseKey == preset.PoseKey && preset.Parts.Count > 0 &&
                preset.Parts.All(p => r.parts.Any(q => q.part == p.Name && q.fingerprint == p.Fingerprint))).ToList();
            return renamed.Count == 1 ? renamed[0] : null;
        }
        internal PartOrigin Match(string document, SavedPreset preset, SavedPart part)
            => Find(document, preset)?.parts.SingleOrDefault(p => p.part == part.Name && p.fingerprint == part.Fingerprint);
        internal string DescribeMatch(string document, SavedPreset preset, SavedPart part)
        {
            if (_blocked.Contains(Key(preset.Owner, document, preset.Identity))) return "pending-save-blocked";
            var record = Find(document, preset);
            if (record == null) return "record-missing";
            var stored = record.parts.FirstOrDefault(p => p.part == part.Name);
            if (stored == null) return "part-missing";
            return stored.fingerprint == part.Fingerprint ? "matched" : "content-mismatch:stored-" + stored.fingerprint;
        }
        internal Pending Prepare(string document, SavedPreset preset, PlaceOrigin source, IEnumerable<string> changedParts, string previousIdentity = null,
            Func<SavedPart, PlaceOrigin> sourceForPart = null)
        {
            source.Validate();
            var previous = previousIdentity == null ? Find(document, preset) :
                _records.SingleOrDefault(r => r.owner == preset.Owner && r.document == Normalize(document) && r.identity == previousIdentity);
            if (previous == null) previous = Find(document, preset);
            var record = new PresetOrigin { owner = preset.Owner, document = Normalize(document), identity = preset.Identity, name = preset.Name, poseKey = preset.PoseKey, replacesIdentity = previous?.identity ?? previousIdentity };
            var changed = new HashSet<string>(changedParts, StringComparer.Ordinal);
            foreach (var part in preset.Parts)
            {
                if (changed.Contains(part.Name))
                {
                    var frame = sourceForPart == null ? source : sourceForPart(part); frame.Validate();
                    record.parts.Add(new PartOrigin { part = part.Name, fingerprint = part.Fingerprint, savedElements = part.Elements, source = frame });
                }
                else
                {
                    var old = previous?.parts.SingleOrDefault(p => p.part == part.Name && p.fingerprint == part.Fingerprint);
                    if (old != null) record.parts.Add(old);
                }
            }
            Directory.CreateDirectory(_directory);
            string final = Path.Combine(_directory, PresetDocuments.Hash(record.owner + "\n" + record.document + "\n" + record.identity) + ".json");
            string pending = final + ".pending-" + Guid.NewGuid().ToString("N");
            Write(pending, record);
            return new Pending(this, previous, record, pending, final);
        }
        private static void Write(string path, PresetOrigin record)
        { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { new DataContractJsonSerializer(typeof(PresetOrigin)).WriteObject(stream, record); stream.Flush(true); } }
        internal static PresetOrigin Read(string path)
        { using (var stream = File.OpenRead(path)) return (PresetOrigin)new DataContractJsonSerializer(typeof(PresetOrigin)).ReadObject(stream); }
        internal sealed class Pending : IDisposable
        {
            private readonly OriginStore _store;
            private readonly PresetOrigin _previous, _record;
            private readonly string _pending, _final;
            private bool _committed, _bodyWritten;
            internal Pending(OriginStore store, PresetOrigin previous, PresetOrigin record, string pending, string final)
            { _store = store; _previous = previous; _record = record; _pending = pending; _final = final; }
            internal void Commit()
            {
                if (_committed) return;
                _bodyWritten = true;
                _store._blocked.Add(Key(_record.owner, _record.document, _record.identity));
                if (_previous != null) _store._blocked.Add(Key(_previous.owner, _previous.document, _previous.identity));
                if (File.Exists(_final)) File.Replace(_pending, _final, null); else File.Move(_pending, _final);
                if (_previous != null) _store._records.Remove(_previous);
                _store._records.RemoveAll(r => r.owner == _record.owner && r.document == _record.document && r.identity == _record.identity);
                _store._records.Add(_record); _committed = true;
                _store._blocked.Remove(Key(_record.owner, _record.document, _record.identity));
                if (_previous != null) _store._blocked.Remove(Key(_previous.owner, _previous.document, _previous.identity));
                foreach (var stale in Directory.GetFiles(_store._directory, "*.pending-*"))
                {
                    var item = Read(stale);
                    if (item.owner == _record.owner && item.document == _record.document && item.identity == _record.identity) File.Delete(stale);
                }
                if (_previous != null && _previous.identity != _record.identity)
                {
                    string obsolete = Path.Combine(_store._directory, PresetDocuments.Hash(_previous.owner + "\n" + _previous.document + "\n" + _previous.identity) + ".json");
                    if (File.Exists(obsolete)) File.Delete(obsolete);
                }
            }
            public void Dispose() { if (!_committed && !_bodyWritten && File.Exists(_pending)) File.Delete(_pending); }
        }
    }
}
