using System;
namespace MainGameCandle
{
    // Pure product policy, also exercised by the headless tests.
    internal sealed class CandleFlow
    {
        internal const int FlyingLimit = 128, DepositLimit = 512;
        internal const float ExtinguishAngle = 135f;
        internal static bool CanIgnite(float angle) => !float.IsNaN(angle) && angle >= 0 && angle < ExtinguishAngle;
        readonly Random _random;
        double _budget, _portion;
        double _variation=1;
        internal CandleFlow() : this(new Random()) { }
        internal CandleFlow(Random random) { _random = random; }
        internal static double Rate(double angle, double start, double multiplier)
        {
            if (double.IsNaN(angle) || double.IsNaN(start) || double.IsNaN(multiplier) || multiplier <= 0) return 0;
            start = Math.Max(0, Math.Min(85, start));
            if (angle <= start) return 0;
            if (angle < 90) return 6 * Smooth((angle - start) / (90 - start)) * multiplier;
            return (6 + 9 * Smooth((angle - 90) / 45)) * multiplier;
        }
        static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
        internal int Advance(double seconds, double rate, Action<double> emitDrop = null,double variation=1,double dropVolume=1)
        {
            variation=Math.Max(0,Math.Min(1,variation));
            if(variation!=_variation){Reset();_variation=variation;}
            if(double.IsNaN(dropVolume)||double.IsInfinity(dropVolume)||dropVolume<=0){Reset();return 0;}
            if (rate <= 0 || double.IsNaN(rate) || double.IsInfinity(rate)) { Reset(); return 0; }
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
            // Discard backlog after a stalled frame. Never burst hundreds of drops on resume.
            _budget += Math.Min(.25, seconds) * rate;
            int emit = 0;
            if (_portion == 0) _portion = NextPortion();
            while (_budget + 1e-9 >= _portion && emit < FlyingLimit)
            {
                // A longer accumulation interval carries proportionally more wax.
                // Consume the same volume used to schedule it, preserving flow per second.
                double volume = _portion;
                _budget -= volume; _portion = NextPortion(); emit++;
                emitDrop?.Invoke(volume*dropVolume);
            }
            return emit;
        }
        double NextPortion()
        {
            // Mean stays one; cap the long tail at two average intervals.
            // Small and large drops remain visibly distinct without multi-second random gaps.
            double portion=_random.NextDouble()<.5?.08+.34*_random.NextDouble():1.5+.5*_random.NextDouble();
            return 1+(portion-1)*_variation;
        }
        internal void Reset() { _budget = 0; _portion = 0; }
    }
}
