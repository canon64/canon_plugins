using System;

namespace MainGameHandySync
{
    internal readonly struct HampTarget
    {
        internal readonly float Min, Max, Velocity, Bpm;
        internal readonly bool Limited;
        internal float Width => Max - Min;
        internal double Period => Bpm > 0 ? 60d / Bpm : 0;
        private HampTarget(float min, float max, float velocity, float bpm, bool limited)
        { Min = min; Max = max; Velocity = velocity; Bpm = bpm; Limited = limited; }

        internal static HampTarget Create(float min, float max, float bpm, float constant, float velocityMin, float velocityMax)
        {
            if (!Finite(min) || !Finite(max) || !Finite(bpm) || !Finite(constant) ||
                !Finite(velocityMin) || !Finite(velocityMax) || bpm <= 0 || constant <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm));
            min = Clamp(min, 0, 1); max = Clamp(max, 0, 1);
            if (max < min) { float swap = min; min = max; max = swap; }
            float requested = bpm * (max - min) / constant;
            float lo = Clamp(velocityMin, 0, 1), hi = Clamp(velocityMax, 0, 1);
            if (hi < lo) lo = hi;
            float velocity = max == min ? 0 : Clamp(requested, lo, hi);
            return new HampTarget(min, max, velocity, bpm, Math.Abs(velocity - requested) > 0.000001f);
        }
        internal static int NormalizeDivisor(int value) => value == 2 || value == 4 ? value : 1;
        internal static int NextDivisor(int value) => value == 1 ? 2 : value == 2 ? 4 : 1;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
