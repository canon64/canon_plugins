using System;
using System.Threading;

namespace MainGameRhythmSync
{
    // C# port of analysis/wavetone_bpm/{measure_audio,bpm_core}.py.
    // The spectral features are our approximation; the BPM scoring follows the reproduced core.
    internal static class AudioBpmAnalyzer
    {
        internal const int SampleRate = 22050, Rate = 40, Window = 4096, Bands = 72;
        private const int TableSize = 4096;
        private const double Tau = 6.28318530717958;
        private static readonly double[] Sine = new double[TableSize], Cosine = new double[TableSize];
        static AudioBpmAnalyzer()
        {
            for (int i = 0; i < TableSize; i++) { Sine[i] = Math.Sin(i * Tau / TableSize); Cosine[i] = Math.Cos(i * Tau / TableSize); }
        }

        internal static int Analyze(float[] audio, CancellationToken cancel)
        {
            if (audio == null || audio.Length < SampleRate * 8) throw new ArgumentException("8秒以上の音声が必要です");
            int count = audio.Length * Rate / SampleRate;
            var features = new float[count * Bands];
            var real = new double[Window]; var imaginary = new double[Window];
            var taper = new double[Window]; var groups = new int[Window / 2 + 1];
            for (int i = 0; i < Window; i++) taper[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Window - 1));
            for (int i = 0; i < groups.Length; i++)
            {
                double hz = i * (double)SampleRate / Window;
                groups[i] = hz < 45 || hz >= 9000 ? -1 : Math.Min(Bands - 1, (int)(Math.Log(hz / 45) / Math.Log(9000d / 45) * Bands));
            }
            float peak = 0;
            for (int frame = 0; frame < count; frame++)
            {
                cancel.ThrowIfCancellationRequested();
                int start = (int)Math.Round(frame * (double)SampleRate / Rate) - Window / 2;
                for (int i = 0; i < Window; i++)
                {
                    int source = start + i;
                    float sample = source >= 0 && source < audio.Length ? audio[source] : 0;
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new ArgumentException("不正な音声値です");
                    real[i] = sample * taper[i]; imaginary[i] = 0;
                }
                Fft(real, imaginary);
                int offset = frame * Bands;
                for (int i = 0; i < groups.Length; i++)
                    if (groups[i] >= 0) features[offset + groups[i]] += (float)Math.Sqrt(real[i] * real[i] + imaginary[i] * imaginary[i]);
                for (int band = 0; band < Bands; band++)
                {
                    float value = (float)Math.Log(1 + features[offset + band]);
                    features[offset + band] = value;
                    if (value > peak) peak = value;
                }
            }
            if (peak <= 1e-7f) throw new ArgumentException("無音のためBPMを解析できません");
            var quantized = new ushort[features.Length];
            float scale = 65535f / peak;
            for (int i = 0; i < features.Length; i++) quantized[i] = (ushort)Math.Min(65535, Math.Max(0, Math.Round(features[i] * scale)));
            return Estimate(Onsets(quantized, count), cancel);
        }

        internal static int[] Onsets(ushort[] frames, int count)
        {
            if (frames.Length != count * Bands) throw new ArgumentException("feature size");
            var raw = new int[count];
            for (int i = 0; i < count - 1; i++)
                for (int band = 0; band < Bands; band++) raw[i] += Math.Max(0, frames[(i + 1) * Bands + band] - frames[i * Bands + band]);
            var onset = (int[])raw.Clone();
            const int window = Rate / 4;
            for (int i = 0; i < count; i++)
            {
                int start = Math.Max(0, i - window / 2), end = Math.Min(count, i - window / 2 + window), best = start;
                for (int j = start + 1; j < end; j++) if (raw[j] > raw[best]) best = j;
                if (best == i) onset[i] += onset[i] / 2;
            }
            return onset;
        }

        internal static double[] Scores(int[] onset, CancellationToken cancel)
        {
            var scores = new double[180]; double maximum = 0;
            for (int candidate = 0; candidate < scores.Length; candidate++)
            {
                cancel.ThrowIfCancellationRequested();
                double step = TableSize / ((60d / (2 * (candidate + 60))) * Rate), phase = 0;
                double fc = 0, fs = 0, dc = 0, ds = 0;
                foreach (int value in onset)
                {
                    int index = (int)phase, fundamental = (index >> 1) & (TableSize - 1), harmonic = index & (TableSize - 1);
                    fc += value * Cosine[fundamental]; fs += value * Sine[fundamental];
                    dc += value * Cosine[harmonic]; ds += value * Sine[harmonic]; phase += step;
                }
                scores[candidate] = Math.Sqrt(fc * fc + fs * fs) + 0.5 * Math.Sqrt(dc * dc + ds * ds);
                maximum = Math.Max(maximum, scores[candidate]);
            }
            if (maximum > 0) for (int i = 0; i < scores.Length; i++) scores[i] /= maximum;
            return scores;
        }

        internal static int Estimate(int[] onset, CancellationToken cancel)
        {
            int events = 0; foreach (int value in onset) if (value > 0) events++;
            if (events < 4) throw new ArgumentException("拍の手掛かりが不足しています");
            double[] scores = Scores(onset, cancel); int best = 0;
            for (int i = 1; i < scores.Length; i++) if (scores[i] > scores[best]) best = i;
            if (scores[best] <= 0) throw new ArgumentException("BPM候補がありません");
            return best + 60;
        }

        private static void Fft(double[] real, double[] imaginary)
        {
            for (int i = 1, j = 0; i < Window; i++)
            {
                int bit = Window >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i >= j) continue;
                double t = real[i]; real[i] = real[j]; real[j] = t;
            }
            for (int size = 2; size <= Window; size <<= 1)
            {
                double angle = -2 * Math.PI / size, stepR = Math.Cos(angle), stepI = Math.Sin(angle);
                for (int start = 0; start < Window; start += size)
                {
                    double wr = 1, wi = 0;
                    for (int j = 0; j < size / 2; j++)
                    {
                        int a = start + j, b = a + size / 2;
                        double tr = wr * real[b] - wi * imaginary[b], ti = wr * imaginary[b] + wi * real[b];
                        real[b] = real[a] - tr; imaginary[b] = imaginary[a] - ti;
                        real[a] += tr; imaginary[a] += ti;
                        double next = wr * stepR - wi * stepI; wi = wr * stepI + wi * stepR; wr = next;
                    }
                }
            }
        }
    }
}
