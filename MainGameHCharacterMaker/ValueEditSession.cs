using System;
using System.Collections.Generic;

namespace MainGameHCharacterMaker
{
    // Stores only fields touched by this window; never snapshots live H state.
    internal sealed class ValueEditSession
    {
        private sealed class Change
        {
            internal string Key;
            internal object Before, After;
            internal Func<object> Read;
            internal Action<object> Write;
        }
        private readonly Func<bool> _alive, _allowed;
        private readonly List<Change> _original = new List<Change>();
        private readonly HashSet<string> _touched = new HashSet<string>();
        private readonly List<Change> _undo = new List<Change>(), _redo = new List<Change>();
        private string _mergeKey;
        internal bool Closed { get; private set; }
        internal bool CanCommit => !Closed && _alive() && _allowed();
        internal bool CanUndo => CanCommit && _undo.Count > 0;
        internal bool CanRedo => CanCommit && _redo.Count > 0;
        internal ValueEditSession(Func<bool> alive, Func<bool> allowed) { _alive = alive; _allowed = allowed; }
        internal void BreakMerge() { _mergeKey = null; }

        internal bool Preview<T>(string key, Func<T> read, Action<T> write, T value, bool merge = false)
        {
            if (!CanCommit) return false;
            T before = read();
            if (EqualityComparer<T>.Default.Equals(before, value)) return true;
            var change = new Change { Key = key, Before = before, After = value,
                Read = () => read(), Write = v => write((T)v) };
            if (_touched.Add(key)) _original.Add(change);
            // Record before invoking native code so even a setter that throws after mutation can be cancelled.
            if (merge && _mergeKey == key && _undo.Count > 0)
                _undo[_undo.Count - 1].After = value;
            else _undo.Add(change);
            _mergeKey = merge ? key : null;
            _redo.Clear();
            change.Write(value);
            _undo[_undo.Count - 1].After = change.Read();
            return true;
        }
        internal bool Undo()
        {
            if (!CanUndo) return false;
            BreakMerge();
            var change = _undo[_undo.Count - 1];
            change.Write(change.Before);
            _undo.RemoveAt(_undo.Count - 1); _redo.Add(change);
            return true;
        }
        internal bool Redo()
        {
            if (!CanRedo) return false;
            BreakMerge();
            var change = _redo[_redo.Count - 1];
            change.Write(change.After);
            _redo.RemoveAt(_redo.Count - 1); _undo.Add(change);
            return true;
        }
        internal bool Commit()
        {
            if (!CanCommit) return false;
            Closed = true; Clear(); return true;
        }
        internal void Cancel()
        {
            if (Closed) return;
            if (!_alive()) { Closed = true; Clear(); return; }
            Exception error = null;
            for (int i = _original.Count - 1; i >= 0; i--)
            {
                try { _original[i].Write(_original[i].Before); }
                catch (Exception ex) { error = error ?? ex; }
            }
            if (error != null) throw new InvalidOperationException("パーツ・色の一部を復元できませんでした。取消を再実行してください。", error);
            Closed = true; Clear();
        }
        private void Clear() { _original.Clear(); _touched.Clear(); _undo.Clear(); _redo.Clear(); BreakMerge(); }
    }
}
