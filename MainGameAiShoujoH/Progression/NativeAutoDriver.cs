using System;
using System.Collections.Generic;

namespace MainGameAiShoujoH.Progression
{
    /// <summary>Native HAuto table timers advance only in their original dispatched state.</summary>
    public sealed class NativeAutoDriver
    {
        private readonly Func<float, float, float> random;
        private readonly Func<int, int, int> integer;
        private float start, restart, speed, loop, motion, pull;
        private float center, lerpStart, lerpTarget, lerpTime;
        private bool interpolating, pullJudged;
        public NativeAutoDriver(Func<float, float, float> random, Func<int, int, int> integer)
        { this.random = random; this.integer = integer; Reset(); pull = random(10, 40); }
        public void Reset()
        {
            start = 5; restart = random(10, 15); speed = random(3, 8); loop = random(10, 15); motion = random(10, 40); pull = 8;
            center = 0; interpolating = false; pullJudged = false; lerpTime = 0;
        }
        public void EnterState(string previous, string state, ProcKind family)
        {
            bool fromWait = previous.EndsWith("_A", StringComparison.Ordinal) || previous == "Idle";
            if (fromWait && (state == "WLoop" || state == "Insert")) Reset();
            if (state == "OLoop" && (family == ProcKind.Aibu || family == ProcKind.Les)) { center = 0; interpolating = false; }
        }
        public void Advance(NativeProgression engine, DriveSample sample, float low, float high)
        {
            sample.AutoStart = sample.AutoRestart = sample.AutoPull = sample.AutoLoopChange = false;
            sample.AutoChangeMotion = false;
            sample.AutoSpeed = float.NaN;
            if (!engine.IsAutomatic || engine.Definition.Kind == ProcKind.Masturbation) return;
            string state = engine.State; float delta = sample.Delta;
            if (state == "Idle") { if (!engine.WaitingForStartVoice) start -= delta; sample.AutoStart = start <= 0; return; }
            if (state.EndsWith("_A", StringComparison.Ordinal))
            {
                if (state == "Orgasm_IN_A" && !pullJudged)
                {
                    pull -= delta;
                    if (pull <= 0) { pullJudged = true; sample.AutoPull = integer(0, 100) < (engine.HasInsideResult ? 70 : 30); }
                }
                if (!engine.WaitingForStartVoice) restart -= delta;
                sample.AutoRestart = restart <= 0;
                if (sample.AutoRestart && state != "Orgasm_IN_A") sample.AutoChangeMotion = integer(0, 100) < 40;
                return;
            }
            bool strong = state == "SLoop", climax = state == "OLoop";
            if (state != "WLoop" && !strong && !climax) return;
            bool wasInterpolating = interpolating;
            if (interpolating)
            {
                lerpTime = Math.Min(2, lerpTime + delta);
                center = lerpStart + (lerpTarget - lerpStart) * (float)Evaluate(lerpTime / 2) + (strong ? 1 : 0);
                if (lerpTime >= 2) { interpolating = false; lerpTime = 0; }
            }
            else
            {
                speed -= delta;
                if (!climax) loop -= delta;
                if (!climax && loop <= 0)
                {
                    loop = random(10, 15);
                    if ((integer(0, 100) > 50) != strong) { sample.AutoLoopChange = true; center = 1; strong = !strong; }
                }
                if (!sample.AutoLoopChange && speed <= 0)
                {
                    speed = random(3, 8); lerpStart = center - (strong ? 1 : 0);
                    if (low >= 0 && integer(0, 100) < 50) lerpTarget = random(low, high);
                    else { int chosen; do { chosen = integer(0, 100); } while (chosen >= (int)(low * 100) && chosen <= (int)(high * 100)); lerpTarget = chosen * .01f; }
                    interpolating = true; lerpTime = 0;
                }
                else if (sample.Wheel != 0)
                {
                    center += Math.Sign(sample.Wheel) * engine.Definition.WheelStep;
                    if (!climax && (strong ? center < 1 : center > 1)) { sample.AutoLoopChange = true; strong = !strong; }
                    center = Math.Max(0, Math.Min(climax ? 1 : 2, center)); speed = random(3, 8);
                }
            }
            if (!wasInterpolating && !climax && !sample.PendingMotion) { motion -= delta; if (motion <= 0) { motion = random(10, 40); sample.AutoChangeMotion = true; } }
            sample.AutoSpeed = center - (strong ? 1 : 0);
        }
        public float SecondsToBoundary(NativeProgression engine, float low, float high)
        {
            if (!engine.IsAutomatic || engine.Definition.Kind == ProcKind.Masturbation || engine.WaitingForStartVoice) return float.PositiveInfinity;
            string state = engine.State;
            if (state == "Idle") return Math.Max(0, start);
            if (state.EndsWith("_A", StringComparison.Ordinal)) return Math.Max(0, state == "Orgasm_IN_A" && !pullJudged ? Math.Min(pull, restart) : restart);
            if (state != "WLoop" && state != "SLoop" && state != "OLoop") return float.PositiveInfinity;
            if (interpolating)
            {
                float result = 2 - lerpTime;
                if (Math.Abs(lerpTarget - lerpStart) > 1e-8)
                    foreach (float boundary in new[] { low, high })
                    {
                        double value = (boundary - lerpStart) / (lerpTarget - lerpStart);
                        double crossing = NextCrossing(lerpTime / 2, value);
                        if (!double.IsInfinity(crossing)) result = Math.Min(result, (float)(crossing * 2 - lerpTime) + .00001f);
                    }
                return Math.Max(0, result);
            }
            return Math.Max(0, state == "OLoop" ? speed : Math.Min(speed, Math.Min(loop, motion)));
        }
        public static double Evaluate(double time)
        {
            var keys = NativeAutoCurve.Keys;
            if (time <= keys[0][0]) return keys[0][1];
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                var a = keys[i]; var b = keys[i + 1]; if (time > b[0]) continue;
                double dt = b[0] - a[0], t = (time - a[0]) / dt, t2 = t * t, t3 = t2 * t;
                return (2*t3-3*t2+1)*a[1] + (t3-2*t2+t)*dt*a[3] + (-2*t3+3*t2)*b[1] + (t3-t2)*dt*b[2];
            }
            return keys[keys.Length - 1][1];
        }
        private static double NextCrossing(double after, double value)
        {
            var intervals = new List<double> { Math.Max(0, after) };
            var keys = NativeAutoCurve.Keys;
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                var a = keys[i]; var b = keys[i+1]; double dt = b[0]-a[0];
                double aa = 2*a[1]-2*b[1]+dt*(a[3]+b[2]), bb = -3*a[1]+3*b[1]-dt*(2*a[3]+b[2]), cc = dt*a[3];
                double discriminant = 4*bb*bb-12*aa*cc;
                if (Math.Abs(aa) > 1e-14 && discriminant >= 0)
                    foreach (double root in new[] { (-2*bb-Math.Sqrt(discriminant))/(6*aa), (-2*bb+Math.Sqrt(discriminant))/(6*aa) })
                        if (root > 0 && root < 1 && a[0]+root*dt > after) intervals.Add(a[0]+root*dt);
                if (b[0] > after) intervals.Add(b[0]);
            }
            intervals.Sort();
            for (int i = 0; i + 1 < intervals.Count; i++)
            {
                double low = intervals[i], high = intervals[i+1], lv = Evaluate(low)-value, hv = Evaluate(high)-value;
                if (lv*hv > 0) continue;
                for (int pass = 0; pass < 48; pass++) { double middle = (low+high)/2, mv = Evaluate(middle)-value; if (lv*mv <= 0) high = middle; else { low = middle; lv = mv; } }
                double result = (low+high)/2; if (result > after+1e-7) return result;
            }
            return double.PositiveInfinity;
        }
    }
}
