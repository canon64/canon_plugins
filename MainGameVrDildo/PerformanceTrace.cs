using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MainGameVrDildo
{
    internal enum PerformanceStage { Rig, Voice, Face, Effects, Scene, Colliders, EyeRender, NativeVoice, OralAudio }

    // Numeric aggregation only at existing call sites; formatting happens on the diagnostic sample.
    internal sealed class PerformanceTrace
    {
        private const int Count = (int)PerformanceStage.OralAudio + 1;
        private readonly long[] _total = new long[Count], _maximum = new long[Count];
        private readonly int[] _calls = new int[Count];
        internal Scope Measure(PerformanceStage stage) => new Scope(this, stage);
        internal void Record(PerformanceStage stage, long ticks)
        {
            int i = (int)stage;
            _calls[i]++; _total[i] += ticks;
            if (ticks > _maximum[i]) _maximum[i] = ticks;
        }
        internal string TakeSummary()
        {
            var text = new StringBuilder();
            for (int i = 0; i < _calls.Length; i++)
            {
                if (i != 0) text.Append(' ');
                double scale = 1000d / Stopwatch.Frequency;
                text.Append(((PerformanceStage)i).ToString()).Append("Calls=").Append(_calls[i])
                    .Append(" avgMs=").Append((_calls[i] == 0 ? 0d : _total[i] * scale / _calls[i]).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" maxMs=").Append((_maximum[i] * scale).ToString("F3", CultureInfo.InvariantCulture));
            }
            Reset();
            return text.ToString();
        }
        internal void Reset() { Array.Clear(_total, 0, Count); Array.Clear(_maximum, 0, Count); Array.Clear(_calls, 0, Count); }

        internal readonly struct Scope : IDisposable
        {
            private readonly PerformanceTrace _owner;
            private readonly PerformanceStage _stage;
            private readonly long _start;
            internal Scope(PerformanceTrace owner, PerformanceStage stage)
            { _owner = owner; _stage = stage; _start = Stopwatch.GetTimestamp(); }
            internal double ElapsedMilliseconds => _owner == null ? 0d : (Stopwatch.GetTimestamp() - _start) * (1000d / Stopwatch.Frequency);
            public void Dispose() { if (_owner != null) _owner.Record(_stage, Stopwatch.GetTimestamp() - _start); }
        }
    }
}
