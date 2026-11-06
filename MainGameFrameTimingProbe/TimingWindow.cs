using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MainGameFrameTimingProbe
{
    // No Unity, file I/O, locks, formatting or per-sample object creation in this class.
    internal sealed class TimingWindow
    {
        internal readonly struct Ticket
        {
            internal readonly TimingWindow Owner;
            internal readonly int Method, Depth;
            internal readonly long Started;
            internal Ticket(TimingWindow owner, int method, int depth, long started)
            { Owner = owner; Method = method; Depth = depth; Started = started; }
        }

        private struct Total
        {
            internal long Inclusive, Exclusive, Max;
            internal int Calls;
        }

        private readonly long _frequency;
        private readonly string[] _methods;
        private readonly Total[] _totals;
        private readonly double[] _frames;
        private readonly long[] _children = new long[64];
        private int _depth, _frameCount, _framesStored;
        private double _frameSum, _frameMax;
        private int _overBudget, _droppedFrames, _depthOverflows, _unbalancedCalls;
        private double _gpuSum, _cpuSum;
        private int _gpuCount, _cpuCount;
        private int _overBudgetStreak, _maxOverBudgetStreak, _over20Ms, _over33Ms, _over50Ms;
        private const int SlowFrameCapacity = 8;
        private readonly long[] _frameExclusive;
        private readonly int[] _frameCalls;
        private readonly long[][] _slowExclusive = new long[SlowFrameCapacity][];
        private readonly int[][] _slowCalls = new int[SlowFrameCapacity][];
        private readonly double[] _slowFrameMs = new double[SlowFrameCapacity];
        private readonly int[] _slowFrameIds = new int[SlowFrameCapacity];
        private int _slowFramesStored;
        internal bool Recording { get; private set; }

        internal TimingWindow(string[] methods, long frequency, int frameCapacity = 8192)
        {
            if (frequency <= 0 || frameCapacity <= 0) throw new ArgumentOutOfRangeException();
            _frequency = frequency;
            _methods = (string[])methods.Clone();
            _totals = new Total[methods.Length];
            _frames = new double[frameCapacity];
            _frameExclusive = new long[methods.Length];
            _frameCalls = new int[methods.Length];
            for (int i = 0; i < SlowFrameCapacity; i++)
            {
                _slowExclusive[i] = new long[methods.Length];
                _slowCalls[i] = new int[methods.Length];
            }
        }

        internal void Start() { Recording = true; }
        internal void Stop()
        {
            Recording = false;
            _overBudgetStreak = 0;
            ClearPendingFrame();
        }

        internal Ticket Begin(int method, long now)
        {
            if (!Recording || (uint)method >= (uint)_totals.Length) return default;
            if (_depth == _children.Length) { _depthOverflows++; return default; }
            int depth = _depth++;
            _children[depth] = 0;
            return new Ticket(this, method, depth, now);
        }

        internal void End(Ticket ticket, long now)
        {
            if (!ReferenceEquals(ticket.Owner, this)) return;
            if (_depth != ticket.Depth + 1) _unbalancedCalls++;
            _depth = ticket.Depth;
            long inclusive = Math.Max(0, now - ticket.Started);
            long exclusive = Math.Max(0, inclusive - _children[ticket.Depth]);
            if (ticket.Depth > 0) _children[ticket.Depth - 1] += inclusive;
            if (!Recording) return;
            ref Total total = ref _totals[ticket.Method];
            total.Inclusive += inclusive;
            total.Exclusive += exclusive;
            total.Max = Math.Max(total.Max, inclusive);
            total.Calls++;
            _frameExclusive[ticket.Method] += exclusive;
            _frameCalls[ticket.Method]++;
        }

        internal void Frame(long elapsed)
        {
            if (!Recording || elapsed <= 0) return;
            double ms = elapsed * 1000.0 / _frequency;
            _frameCount++;
            _frameSum += ms;
            _frameMax = Math.Max(_frameMax, ms);
            // Account for timer quantisation at a fixed 60 Hz boundary.
            if (elapsed > _frequency / 60.0 + 1)
            {
                _overBudget++;
                _maxOverBudgetStreak = Math.Max(_maxOverBudgetStreak, ++_overBudgetStreak);
                KeepSlowFrame(ms);
            }
            else _overBudgetStreak = 0;
            if (elapsed > _frequency / 50.0 + 1) _over20Ms++;
            if (elapsed > _frequency / 30.0 + 1) _over33Ms++;
            if (elapsed > _frequency / 20.0 + 1) _over50Ms++;
            if (_framesStored < _frames.Length) _frames[_framesStored++] = ms;
            else _droppedFrames++;
            ClearPendingFrame();
        }

        private void ClearPendingFrame()
        {
            Array.Clear(_frameExclusive, 0, _frameExclusive.Length);
            Array.Clear(_frameCalls, 0, _frameCalls.Length);
        }

        private void KeepSlowFrame(double ms)
        {
            int slot = _slowFramesStored;
            if (slot == SlowFrameCapacity)
            {
                slot = 0;
                for (int i = 1; i < SlowFrameCapacity; i++) if (_slowFrameMs[i] < _slowFrameMs[slot]) slot = i;
                if (ms <= _slowFrameMs[slot]) return;
            }
            else _slowFramesStored++;
            _slowFrameMs[slot] = ms;
            _slowFrameIds[slot] = _frameCount;
            Array.Copy(_frameExclusive, _slowExclusive[slot], _frameExclusive.Length);
            Array.Copy(_frameCalls, _slowCalls[slot], _frameCalls.Length);
        }

        internal void EngineTiming(double cpuMs, double gpuMs)
        {
            if (!Recording) return;
            if (cpuMs > 0 && !double.IsInfinity(cpuMs) && !double.IsNaN(cpuMs)) { _cpuSum += cpuMs; _cpuCount++; }
            if (gpuMs > 0 && !double.IsInfinity(gpuMs) && !double.IsNaN(gpuMs)) { _gpuSum += gpuMs; _gpuCount++; }
        }

        internal WindowReport Snapshot()
        {
            if (Recording) throw new InvalidOperationException("Stop sampling before formatting a report.");
            var sorted = new double[_framesStored];
            Array.Copy(_frames, sorted, sorted.Length);
            Array.Sort(sorted);
            var methods = new List<MethodReport>();
            for (int i = 0; i < _totals.Length; i++)
            {
                Total total = _totals[i];
                if (total.Calls == 0) continue;
                double unit = 1000.0 / _frequency;
                methods.Add(new MethodReport {
                    Method = _methods[i], Calls = total.Calls,
                    InclusiveMs = total.Inclusive * unit, ExclusiveMs = total.Exclusive * unit,
                    ExclusiveMsPerFrame = _frameCount > 0 ? total.Exclusive * unit / _frameCount : 0,
                    MaxCallMs = total.Max * unit
                });
            }
            methods.Sort((a, b) => b.ExclusiveMs.CompareTo(a.ExclusiveMs));
            var slowFrames = new SlowFrameReport[_slowFramesStored];
            for (int slot = 0; slot < slowFrames.Length; slot++)
            {
                var costs = new List<SlowMethodReport>();
                for (int i = 0; i < _methods.Length; i++)
                    if (_slowCalls[slot][i] > 0) costs.Add(new SlowMethodReport {
                        Method = _methods[i], Calls = _slowCalls[slot][i], ExclusiveMs = _slowExclusive[slot][i] * 1000.0 / _frequency
                    });
                costs.Sort((a, b) => b.ExclusiveMs.CompareTo(a.ExclusiveMs));
                slowFrames[slot] = new SlowFrameReport { FrameIndexInWindow = _slowFrameIds[slot], FrameMs = _slowFrameMs[slot], Methods = costs.ToArray() };
            }
            Array.Sort(slowFrames, (a, b) => b.FrameMs.CompareTo(a.FrameMs));
            return new WindowReport {
                Frames = _frameCount, FrameSamplesStored = _framesStored, DroppedFrameSamples = _droppedFrames,
                MeanFps = _frameSum > 0 ? _frameCount * 1000.0 / _frameSum : 0,
                MeanFrameMs = _frameCount > 0 ? _frameSum / _frameCount : 0,
                P95FrameMs = Percentile(sorted, .95), P99FrameMs = Percentile(sorted, .99),
                MaxFrameMs = _frameMax, FramesOver60FpsBudget = _overBudget,
                LongestOverBudgetRun = _maxOverBudgetStreak, FramesOver20Ms = _over20Ms, FramesOver33Ms = _over33Ms, FramesOver50Ms = _over50Ms,
                EngineCpuTimingSamples = _cpuCount, EngineGpuTimingSamples = _gpuCount,
                EngineCpuMeanMs = _cpuCount > 0 ? (double?)(_cpuSum / _cpuCount) : null,
                EngineGpuMeanMs = _gpuCount > 0 ? (double?)(_gpuSum / _gpuCount) : null,
                DepthOverflows = _depthOverflows, UnbalancedCalls = _unbalancedCalls, Methods = methods.ToArray(), SlowFrames = slowFrames
            };
        }

        private static double Percentile(double[] sorted, double p)
            => sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * p) - 1)];
    }

    [DataContract]
    internal sealed class WindowReport
    {
        [DataMember] public int Frames, FrameSamplesStored, DroppedFrameSamples, FramesOver60FpsBudget;
        [DataMember] public double MeanFps, MeanFrameMs, P95FrameMs, P99FrameMs, MaxFrameMs;
        [DataMember] public int EngineCpuTimingSamples, EngineGpuTimingSamples, DepthOverflows, UnbalancedCalls;
        [DataMember] public double? EngineCpuMeanMs, EngineGpuMeanMs;
        [DataMember] public MethodReport[] Methods;
        [DataMember] public int LongestOverBudgetRun, FramesOver20Ms, FramesOver33Ms, FramesOver50Ms;
        [DataMember] public SlowFrameReport[] SlowFrames;
    }

    [DataContract]
    internal sealed class SlowFrameReport
    {
        [DataMember] public int FrameIndexInWindow;
        [DataMember] public double FrameMs;
        [DataMember] public SlowMethodReport[] Methods;
    }

    [DataContract]
    internal sealed class SlowMethodReport
    {
        [DataMember] public string Method;
        [DataMember] public int Calls;
        [DataMember] public double ExclusiveMs;
    }

    [DataContract]
    internal sealed class MethodReport
    {
        [DataMember] public string Method;
        [DataMember] public int Calls;
        [DataMember] public double InclusiveMs, ExclusiveMs, ExclusiveMsPerFrame, MaxCallMs;
    }
}
