using System;

namespace MainGameHandySync
{
    internal sealed class RhythmStrokeSource
    {
        internal bool Available { get; private set; }
        internal float Bpm { get; private set; }
        internal bool Receive(bool active, double rate, float timeScale)
        {
            double bpm = rate * timeScale * 60d;
            bool available = active && !double.IsNaN(bpm) && !double.IsInfinity(bpm) && bpm > 0 && bpm <= float.MaxValue;
            bool changed = available != Available || (available && Math.Abs(Bpm - bpm) >= 0.0001d);
            Available = available; Bpm = available ? (float)bpm : 0;
            return changed;
        }
        internal bool TryGetBpm(bool enabled, int divisor, out float bpm)
        {
            bpm = enabled && Available ? Bpm / HampTarget.NormalizeDivisor(divisor) : 0;
            return bpm > 0;
        }
        internal void Reset() { Available = false; Bpm = 0; }
    }
}
