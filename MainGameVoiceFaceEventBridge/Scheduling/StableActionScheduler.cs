using System;
using System.Collections.Generic;

namespace MainGameVoiceFaceEventBridge.Scheduling
{
    internal sealed class StableActionScheduler
    {
        private sealed class Entry
        {
            internal long SequenceNo;
            internal int Epoch;
            internal float DueTime;
            internal Action Action;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _dueBuffer = new List<Entry>();
        private bool _draining;
        private long _nextSequenceNo;
        private int _epoch;

        internal int Count => _entries.Count;

        internal long Schedule(float dueTime, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            long sequenceNo = ++_nextSequenceNo;
            _entries.Add(new Entry { Epoch = _epoch, SequenceNo = sequenceNo, DueTime = dueTime, Action = action });
            return sequenceNo;
        }

        internal int DrainDue(float now, Action<Exception> onError)
        {
            if (_entries.Count <= 0) return 0;
            List<Entry> due = null;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (now < entry.DueTime) continue;
                if (due == null) due = _draining ? new List<Entry>() : _dueBuffer;
                due.Add(entry);
                _entries.RemoveAt(i);
            }

            if (due == null) return 0;
            due.Sort((a, b) =>
            {
                int byTime = a.DueTime.CompareTo(b.DueTime);
                return byTime != 0 ? byTime : a.SequenceNo.CompareTo(b.SequenceNo);
            });

            bool wasDraining = _draining;
            _draining = true;
            int count = due.Count;
            try
            {
                foreach (Entry entry in due)
                {
                    if (entry.Epoch != _epoch) continue;
                    try { entry.Action(); }
                    catch (Exception ex) { onError?.Invoke(ex); }
                }
            }
            finally
            {
                due.Clear();
                _draining = wasDraining;
            }
            return count;
        }

        internal int Clear()
        {
            unchecked { ++_epoch; }

            int count = _entries.Count;
            _entries.Clear();
            return count;
        }
    }
}
