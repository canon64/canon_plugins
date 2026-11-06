using System;

namespace CanonPlugins.ShoulderPose
{
    // Time is advanced by a real solver pass, never by a polling behaviour.
    internal sealed class WeightTransition
    {
        private float start, elapsed, activeDuration = -1f;
        internal float Value { get; private set; }
        internal float Target { get; private set; }
        internal bool Settled => Value == Target;
        internal void Reset(float value = 0f) { Value = Target = start = value; elapsed = 0f; activeDuration = -1f; }
        internal void SetTarget(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            if (value == Target) return;
            start = Value; Target = value; elapsed = 0f;
        }
        internal float Step(float dt, float duration)
        {
            if (Settled) return Value;
            if (activeDuration != duration)
            {
                start = Value; elapsed = 0f; activeDuration = duration;
            }
            elapsed += Math.Max(0f, Math.Min(0.05f, dt));
            float t = duration <= 0f ? 1f : Math.Min(1f, elapsed / duration);
            Value = t >= 1f ? Target : start + (Target - start) * t * t * (3f - 2f * t);
            return Value;
        }
    }

    internal sealed class SolverPassState
    {
        private int generation, depth;
        private bool solved, failed;
        internal bool IsOuter => depth == 0;
        internal int Enter()
        {
            if (depth++ == 0) { solved = false; failed = false; }
            return generation;
        }
        internal void MarkSolved() { if (depth > 0) solved = true; }
        internal bool Exit(int token, bool success)
        {
            if (token != generation || depth <= 0) return false;
            failed |= !success;
            return --depth == 0 && solved && !failed;
        }
        internal void Reset() { generation++; depth = 0; solved = failed = false; }
    }
}
