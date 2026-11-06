using System;
using UnityEngine;

namespace MainGameDisplacementMap
{
    // A regular UV grid has identical samples across rows/columns once scrolling
    // clamps an entire axis. Reuse only within one draw, after map generation.
    internal sealed class ClampedUvSamples
    {
        private readonly int _columns;
        private readonly Vector2[] _samples;
        private readonly float[] _magnitudes;
        private readonly bool[] _valid;
        private int _mode;

        internal ClampedUvSamples(int columns, int rows)
        {
            _columns = columns;
            int count = Math.Max(columns, rows);
            _samples = new Vector2[count];
            _magnitudes = new float[count];
            _valid = new bool[count];
        }

        internal bool Begin(bool managed, float scrollX, float scrollY)
        {
            bool x = scrollX >= 1f || scrollX <= -1f;
            bool y = scrollY >= 1f || scrollY <= -1f;
            _mode = !managed ? 0 : x && y ? 1 : y ? 2 : x ? 3 : 0;
            if (_mode == 0) return false;
            Array.Clear(_valid, 0, _valid.Length);
            return true;
        }

        private int Index(int vertex) => _mode == 1 ? 0 : _mode == 2 ? vertex % _columns : vertex / _columns;

        internal bool TryGet(int vertex, out Vector2 sample, out float magnitude)
        {
            int index = Index(vertex);
            sample = _samples[index];
            magnitude = _magnitudes[index];
            return _valid[index];
        }

        internal void Store(int vertex, Vector2 sample, float magnitude)
        {
            int index = Index(vertex);
            _samples[index] = sample;
            _magnitudes[index] = magnitude;
            _valid[index] = true;
        }
    }
}
