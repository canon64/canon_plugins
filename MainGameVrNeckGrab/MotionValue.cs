using System;

namespace MainGameVrNeckGrab
{
    internal static class MotionValue
    {
        internal static bool TryResolve(string state, bool lockW, float w, bool lockS, float s, out float value)
        {
            value = 0;
            if (string.IsNullOrEmpty(state)) return false;
            bool weak = state == "WLoop" || state.EndsWith("_WLoop", StringComparison.Ordinal);
            bool strong = state == "SLoop" || state.EndsWith("_SLoop", StringComparison.Ordinal);
            if (weak && lockW) value = w;
            else if (strong && lockS) value = s;
            else return false;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
