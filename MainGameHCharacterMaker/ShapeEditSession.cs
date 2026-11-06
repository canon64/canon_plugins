using System;

namespace MainGameHCharacterMaker
{
    internal interface IShapeTarget
    {
        bool IsAlive { get; }
        float[] Read(bool body);
        bool Write(bool body, int index, float value);
        void Refresh(bool body, bool face);
    }

    /// <summary>Only fields actually edited are restored. H progression and unrelated data are never snapshotted.</summary>
    internal sealed class ShapeEditSession
    {
        private readonly IShapeTarget _target;
        private readonly Func<bool> _canEdit;
        private readonly float[][] _original, _values;
        private readonly bool[][] _dirty;
        internal bool Closed { get; private set; }
        internal bool CanCommit => !Closed && _canEdit() && _target.IsAlive;
        internal float[] Body => _values[0];
        internal float[] Face => _values[1];
        internal bool HasChanges => Array.Exists(_dirty[0], x => x) || Array.Exists(_dirty[1], x => x);

        internal ShapeEditSession(IShapeTarget target, Func<bool> canEdit)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
            if (!target.IsAlive) throw new InvalidOperationException("編集対象が存在しません。");
            _original = new[] { (float[])target.Read(true).Clone(), (float[])target.Read(false).Clone() };
            _values = new[] { (float[])_original[0].Clone(), (float[])_original[1].Clone() };
            _dirty = new[] { new bool[Body.Length], new bool[Face.Length] };
        }

        internal bool Preview(bool body, int index, float value)
        {
            if (Closed || !_canEdit() || !_target.IsAlive || !Finite(value)) return false;
            int group = body ? 0 : 1;
            if (index < 0 || index >= _values[group].Length) return false;
            if (_values[group][index] == value) return true;
            // Mark before calling game code: a native setter can mutate data and then throw.
            _dirty[group][index] = true;
            if (!_target.Write(body, index, value)) return false;
            _values[group][index] = _target.Read(body)[index];
            _target.Refresh(body, !body);
            return true;
        }

        internal bool Commit()
        {
            if (!CanCommit) return false;
            Closed = true;
            return true;
        }

        // Cleanup is intentionally independent of the now-invalid permit.
        internal void Cancel()
        {
            if (Closed) return;
            Closed = true;
            if (!_target.IsAlive) return;
            Exception failure = null;
            bool body = false, face = false;
            for (int group = 0; group < 2; group++)
                for (int index = 0; index < _dirty[group].Length; index++)
                {
                    if (!_dirty[group][index]) continue;
                    if (group == 0) body = true; else face = true;
                    try
                    {
                        if (!_target.Write(group == 0, index, _original[group][index]))
                            throw new InvalidOperationException("編集値の復元が拒否されました。");
                    }
                    catch (Exception ex) { failure = failure ?? ex; }
                }
            try { if (body || face) _target.Refresh(body, face); }
            catch (Exception ex) { failure = failure ?? ex; }
            if (failure != null) throw new InvalidOperationException("一部の編集値を復元できませんでした。", failure);
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
