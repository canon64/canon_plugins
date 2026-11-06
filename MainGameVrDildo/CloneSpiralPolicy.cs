using System;

namespace MainGameVrDildo
{
    internal static class CloneSpiralPolicy
    {
        internal static bool IsClone(int mode, int id, string name)
        {
            if (mode != 0) return false; // HFlag.EMode.aibu
            string expected = id == 0 ? "立ち愛撫" : id == 1 ? "仰向け愛撫" : id == 7 ? "座り愛撫" : null;
            return expected != null && string.Equals(name, expected + "【クローン】", StringComparison.Ordinal);
        }

        internal static bool IsReady(float femaleGauge, string state)
        {
            if (float.IsNaN(femaleGauge) || femaleGauge < 70f || femaleGauge >= 100f || string.IsNullOrEmpty(state))
                return false;
            return !state.StartsWith("Orgasm", StringComparison.Ordinal)
                && !state.StartsWith("After", StringComparison.Ordinal);
        }
    }
}
