using System;

namespace MainGameAiShoujoH.Progression
{
    /// <summary>Retains native loader aliasing: six categories reference one 18-area object.</summary>
    public sealed class NativeFeelHit
    {
        private readonly Func<float, float, float> random;
        private readonly Func<int, int, int> integer;
        private static readonly int[] Width = { 15,20,25,20,20,25,25,20,25,10,10,10,10,8,10,20,20,20 };
        private static readonly int[] Minimum = { 9,8,25,12,9,25,13,8,25,5,8,25,8,6,12,9,11,25 };
        private static readonly int[] Maximum = { 13,10,30,15,11,30,17,13,30,8,10,30,12,10,15,14,15,30 };
        private readonly float[] start = new float[108], remaining = new float[18];
        public NativeFeelHit(Func<float, float, float> random, Func<int, int, int> integer)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random)); this.integer = integer ?? throw new ArgumentNullException(nameof(integer));
            // FeelHitInit iterates six alias references, appending hits without clearing.
            for (int alias = 0; alias < 6; alias++) { for (int i = 0; i < 18; i++) start[alias * 18 + i] = integer(5, 95 - Width[i]) * .01f; ResetTimers(); }
        }
        public void ResetTimers() { for (int i = 0; i < 18; i++) remaining[i] = random(Minimum[i], Maximum[i]); }
        public bool Contains(int category, int loop, float speed)
        {
            if (category < 0 || category >= 6 || loop < 0 || loop >= 18) return false;
            return speed >= start[loop] && speed <= start[loop] + Width[loop] * .01f;
        }
        public float Remaining(int loop) => loop >= 0 && loop < 18 ? remaining[loop] : float.PositiveInfinity;
        public void Range(int loop, out float low, out float high) { low = loop >= 0 && loop < 18 ? start[loop] : 1.1f; high = loop >= 0 && loop < 18 ? start[loop] + Width[loop] * .01f : 1.1f; }
        public void Advance(int loop, float delta)
        {
            if (loop < 0 || loop >= 18 || delta < 0) throw new ArgumentOutOfRangeException(nameof(loop));
            remaining[loop] -= delta;
            if (remaining[loop] > 0) return;
            remaining[loop] = random(Minimum[loop], Maximum[loop]);
            start[loop] = integer(5, 95 - Width[loop]) * .01f;
        }
    }
}
