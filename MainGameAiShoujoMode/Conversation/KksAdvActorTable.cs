using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    // Native KKS actor-number semantics, independent of AI resident IDs and Unity object IDs.
    internal sealed class KksAdvActorTable<T> where T : class
    {
        private readonly Dictionary<int, T> _slots = new Dictionary<int, T>();
        private readonly Func<T> _player;
        private readonly Func<IReadOnlyList<T>> _heroines;
        private readonly int _zero, _playerNumber, _heroineOffset;
        internal T Current { get; private set; }
        internal KksAdvActorTable(Func<T> player, Func<IReadOnlyList<T>> heroines, int zero, int playerNumber, int heroineOffset)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player)); _heroines = heroines ?? throw new ArgumentNullException(nameof(heroines));
            if (playerNumber >= zero || heroineOffset <= zero) throw new ArgumentException("Native ADV actor-number settings are invalid.");
            _zero = zero; _playerNumber = playerNumber; _heroineOffset = heroineOffset;
        }
        internal void Register(int slot, T data) { _slots[slot] = data ?? throw new ArgumentNullException(nameof(data)); }
        internal bool Remove(int slot) => _slots.Remove(slot);
        internal IReadOnlyDictionary<int, T> SnapshotSlots() => new Dictionary<int, T>(_slots);
        internal T Resolve(int number)
        {
            if (number < _zero)
            {
                var data = number == _playerNumber ? _player() : _heroines()[Math.Abs(number + _heroineOffset)];
                if (data != null) return data;
            }
            return _slots.TryGetValue(number, out var bound) ? bound : Current;
        }
        internal bool SetCurrent(int number) { Current = Resolve(number); return Current != null; }
        internal void Clear() { _slots.Clear(); Current = null; }
    }
}
