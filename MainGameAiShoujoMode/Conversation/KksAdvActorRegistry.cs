using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvActorRegistry : IDisposable
    {
        private sealed class ReferenceComparer : IEqualityComparer<SaveData.CharaData>
        {
            public bool Equals(SaveData.CharaData left, SaveData.CharaData right) => ReferenceEquals(left, right);
            public int GetHashCode(SaveData.CharaData value) => RuntimeHelpers.GetHashCode(value);
        }
        private readonly KksNativeConversationSession _session;
        private readonly Action<string> _log;
        private readonly KksAdvActorTable<SaveData.CharaData> _table;
        private readonly List<SaveData.CharaData> _heroines = new List<SaveData.CharaData>();
        private readonly Dictionary<SaveData.CharaData, KksAdvNativeMediaBackend.Target> _targets = new Dictionary<SaveData.CharaData, KksAdvNativeMediaBackend.Target>(new ReferenceComparer());
        private readonly List<KksActorPresentation> _owned = new List<KksActorPresentation>();
        private bool _disposed;
        internal event Action PresentationChanged;
        internal KksAdvActorRegistry(KksNativeConversationSession session, KksActorPresentation primary, Action<string> log)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session)); _log = log;
            _table = new KksAdvActorTable<SaveData.CharaData>(() => session.Player, () => _heroines,
                session.Settings.zero, session.Settings.advPlayerNumber, session.Settings.advHeroineNumberOffset);
            _heroines.Add(session.Heroine); _table.Register(session.Settings.zero, session.Heroine); _table.SetCurrent(session.Settings.zero);
            _targets.Add(session.Heroine, new KksAdvNativeMediaBackend.Target(session.Heroine, primary));
        }
        internal void Register(int slot, SaveData.CharaData data) { Check(); _table.Register(slot, data); NotifyPresentationChanged(); }
        internal bool Remove(int slot)
        { Check(); if (!_table.Remove(slot)) return false; NotifyPresentationChanged(); return true; }
        internal IReadOnlyList<ChaControl> CameraParticipants()
        {
            Check();
            var data = _table.SnapshotSlots().Values.ToArray();
            if (data.Any(value => value?.chaCtrl == null)) throw new InvalidOperationException("Registered ADV participant has no loaded actor.");
            return data.Select(value => value.chaCtrl).Distinct().ToArray();
        }
        // Called by owned mutations (registration, visibility, shape), never by a polling loop.
        internal void NotifyPresentationChanged() { Check(); PresentationChanged?.Invoke(); }
        internal void SetHeroines(IEnumerable<SaveData.Heroine> heroines)
        {
            Check(); var copy = (heroines ?? throw new ArgumentNullException(nameof(heroines))).Cast<SaveData.CharaData>().ToArray();
            _heroines.Clear(); _heroines.AddRange(copy);
            NotifyPresentationChanged();
        }
        internal bool SetCurrent(int number) { Check(); return _table.SetCurrent(number); }
        internal IReadOnlyDictionary<int, KksAdvNativeMediaBackend.Target> RegisteredTargets()
        {
            Check();
            return _table.SnapshotSlots().ToDictionary(pair => pair.Key, pair => Target(pair.Value));
        }
        internal KksAdvNativeMediaBackend.Target Resolve(int number)
        {
            Check(); return Target(_table.Resolve(number));
        }
        private KksAdvNativeMediaBackend.Target Target(SaveData.CharaData data)
        {
            if (data?.chaCtrl == null) throw new InvalidOperationException("KKS ADV actor slot has no loaded character.");
            if (_targets.TryGetValue(data, out var existing))
            {
                if (existing.Presentation.Actor != data.chaCtrl) throw new InvalidOperationException("KKS ADV actor root changed during the owned session.");
                return existing;
            }
            // Existing actors keep their current controller until a native command changes it.
            var presentation = new KksActorPresentation(_session, data, _log, initializeNativeBody: false);
            try
            {
                var target = new KksAdvNativeMediaBackend.Target(data, presentation);
                _targets.Add(data, target); _owned.Add(presentation); return target;
            }
            catch { presentation.Dispose(); throw; }
        }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(KksAdvActorRegistry)); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; PresentationChanged = null; _table.Clear(); _heroines.Clear(); _targets.Clear();
            var failures = new List<Exception>();
            foreach (var presentation in _owned.AsEnumerable().Reverse()) try { presentation.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            _owned.Clear();
            // The primary presentation belongs to the parent conversation and is restored by it.
            if (failures.Any()) throw new AggregateException("KKS ADV actor restoration failed.", failures);
        }
    }
}
