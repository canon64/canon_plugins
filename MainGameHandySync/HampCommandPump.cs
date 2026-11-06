using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameHandySync
{
    // One complete width/velocity pair at a time. Only the next, not the in-flight,
    // pair may be replaced. Stop invalidates every unsent command immediately.
    internal sealed class HampCommandPump
    {
        private readonly object _gate = new object();
        private readonly Func<string, string, CancellationToken, Task<bool>> _send;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        private HampTarget? _pending;
        private bool _worker, _stop, _enabled, _modeReady, _started;
        private long _generation;
        private volatile bool _failed;
        internal bool Failed => _failed;
        internal Task Completion { get; private set; } = Task.CompletedTask;
        internal HampCommandPump(Func<string, string, CancellationToken, Task<bool>> send) { _send = send; }

        internal void Request(HampTarget target)
        {
            if (target.Width <= 0 || target.Velocity <= 0) { Stop(); return; }
            bool launch;
            lock (_gate)
            {
                _enabled = true; _pending = target; _failed = false;
                launch = !_worker; _worker = true;
            }
            if (launch) Completion = Run();
        }

        internal void Stop()
        {
            bool launch;
            lock (_gate)
            {
                _enabled = false; _pending = null; _modeReady = _started = false;
                _generation++; _cancel.Cancel(); _cancel.Dispose(); _cancel = new CancellationTokenSource();
                _stop = true;
                launch = !_worker; _worker = true;
            }
            if (launch) Completion = Run();
        }

        private bool Current(long generation) { lock (_gate) return _enabled && generation == _generation; }
        private bool MarkReady(long generation, bool started)
        {
            lock (_gate)
            {
                if (!_enabled || generation != _generation) return false;
                if (started) _started = true; else _modeReady = true;
                return true;
            }
        }
        private async Task Run()
        {
            while (true)
            {
                HampTarget target = default;
                bool stop, mode, start; long generation; CancellationToken token;
                lock (_gate)
                {
                    stop = _stop; _stop = false;
                    if (!stop && (!_enabled || !_pending.HasValue)) { _worker = false; return; }
                    if (!stop) { target = _pending.Value; _pending = null; }
                    generation = _generation; token = _cancel.Token;
                    mode = !_modeReady; start = !_started;
                }
                try
                {
                    if (stop)
                    {
                        bool ok = await _send("/hamp/stop", "{}", CancellationToken.None);
                        if (!ok) _failed = true;
                        continue;
                    }
                    if (!Current(generation)) continue;
                    if (mode)
                    {
                        if (!await _send("/mode2", "{\"mode\":0}", token)) throw new InvalidOperationException();
                        if (!MarkReady(generation, false)) continue;
                    }
                    string stroke = "{\"min\":" + Number(target.Min) + ",\"max\":" + Number(target.Max) + "}";
                    if (!await _send("/slider/stroke", stroke, token)) throw new InvalidOperationException();
                    if (!Current(generation)) continue;
                    if (!await _send("/hamp/velocity", "{\"velocity\":" + Number(target.Velocity) + "}", token)) throw new InvalidOperationException();
                    if (!Current(generation)) continue;
                    if (start)
                    {
                        if (!await _send("/hamp/start", "{}", token)) throw new InvalidOperationException();
                        if (!MarkReady(generation, true)) continue;
                    }
                    lock (_gate) { if (_enabled && generation == _generation) _failed = false; }
                }
                catch (OperationCanceledException) { }
                catch
                {
                    lock (_gate)
                    {
                        if (generation != _generation) continue;
                        _failed = true;
                        if (stop) continue; // A new explicit request may already be waiting.
                        _enabled = false; _pending = null;
                        _modeReady = _started = false; _stop = true;
                    }
                }
            }
        }
        private static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
