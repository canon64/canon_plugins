using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Native Text.Next choreography: voice-group starts drive performance rows.
    // With no voices, motion completion events drive the corresponding rows instead.
    internal sealed class KksAdvTextPlayback : KksAdvMachine.ICommand
    {
        internal sealed class Performance
        {
            private readonly Action _expressions, _icons, _motions;
            internal bool HasMotion { get; }
            internal Func<Action, Action<Exception>, IDisposable> WaitMotion { get; }
            internal Performance(Action expressions, Action icons, Action motions,
                Func<Action, Action<Exception>, IDisposable> waitMotion = null)
            {
                _expressions = expressions; _icons = icons; _motions = motions;
                HasMotion = motions != null; WaitMotion = waitMotion;
                if (HasMotion && waitMotion == null) throw new ArgumentNullException(nameof(waitMotion));
            }
            internal void Apply() { _expressions?.Invoke(); _icons?.Invoke(); _motions?.Invoke(); }
        }
        private sealed class Wait
        {
            internal int Index;
            internal bool Completed;
            internal IDisposable Lease;
        }
        private readonly Performance[] _performances;
        private readonly int _motionCount, _zero, _one;
        private readonly Func<Action, Action, Action<Exception>, IDisposable> _playVoices;
        private readonly Func<bool> _cancelVoices;
        private readonly HashSet<int> _motionReady = new HashSet<int>();
        private IDisposable _voices;
        private Wait _wait;
        private Action _completed;
        private Action<Exception> _failed;
        private int _current;
        private bool _started, _ended, _reported, _pumping, _voicesEnded;
        private bool _pendingSuccess;
        private Exception _pendingFailure;
        internal KksAdvTextPlayback(IEnumerable<Performance> performances, int motionCount,
            Func<Action, Action, Action<Exception>, IDisposable> playVoices, Func<bool> cancelVoices, int zero, int one)
        {
            _performances = (performances ?? throw new ArgumentNullException(nameof(performances))).ToArray();
            if (_performances.Any(item => item == null) || motionCount < zero || motionCount > _performances.Length)
                throw new ArgumentException("KKS ADV performance/motion row counts are inconsistent.");
            _motionCount = motionCount; _playVoices = playVoices; _cancelVoices = cancelVoices ?? throw new ArgumentNullException(nameof(cancelVoices));
            _zero = zero; _one = one; _current = zero;
        }
        public void Start(Action completed, Action<Exception> failed)
        {
            if (_started || _ended) throw new InvalidOperationException("KKS ADV text playback cannot be started twice.");
            _started = true; _completed = completed; _failed = failed;
            if (_playVoices != null)
            {
                var lease = _playVoices(VoiceStarted, VoiceEnded, Failed);
                // A synchronous failure may have canceled the command before Play returned.
                if (_ended && _cancelVoices()) lease?.Dispose(); else _voices = lease;
            }
            Pump();
        }
        private void VoiceStarted()
        {
            if (_ended || _reported) return;
            try { ApplyNext(); } catch (Exception exception) { Failed(exception); return; }
            Pump();
        }
        private void VoiceEnded() { if (!_ended) { _voicesEnded = true; Pump(); } }
        private void ApplyNext()
        {
            var index = _current++;
            if (index < _performances.Length) _performances[index].Apply();
        }
        private void Pump()
        {
            if (_pumping || _ended || _reported) return;
            _pumping = true;
            try
            {
                if (_playVoices != null)
                {
                    // Native voice playback never substitutes motion timers for missing voice groups.
                    if (_voicesEnded && _current >= _performances.Length) ReportCompleted();
                    return;
                }
                while (!_ended && !_reported)
                {
                    if (_wait != null)
                    {
                        if (!_wait.Completed) return;
                        var wait = _wait; _wait = null; _motionReady.Add(wait.Index); wait.Lease?.Dispose();
                        if (_ended) return;
                    }
                    if (_current >= _performances.Length)
                    {
                        if (_motionCount > _zero && NeedsMotion(_motionCount - _one)) { BeginWait(_motionCount - _one); continue; }
                        ReportCompleted(); return;
                    }
                    // Text.Next waits for the preceding motion only if the NEXT motion row is nonempty.
                    if (_current > _zero && _current < _motionCount && _performances[_current].HasMotion && NeedsMotion(_current - _one))
                    { BeginWait(_current - _one); continue; }
                    ApplyNext();
                }
            }
            catch (Exception exception) { Failed(exception); }
            finally { _pumping = false; Deliver(); }
        }
        private bool NeedsMotion(int index) => _performances[index].HasMotion && !_motionReady.Contains(index);
        private void BeginWait(int index)
        {
            var wait = new Wait { Index = index }; _wait = wait;
            var lease = _performances[index].WaitMotion(() =>
            {
                if (_ended || !ReferenceEquals(_wait, wait) || wait.Completed) return;
                wait.Completed = true; Pump();
            }, exception => { if (!_ended && ReferenceEquals(_wait, wait)) Failed(exception); });
            if (!_ended && ReferenceEquals(_wait, wait)) wait.Lease = lease; else lease?.Dispose();
        }
        private void ReportCompleted()
        {
            if (_ended || _reported) return;
            _reported = true; _pendingSuccess = true;
        }
        private void Failed(Exception exception)
        {
            if (_ended || _reported) return;
            _reported = true; _pendingFailure = exception ?? new InvalidOperationException("KKS ADV text playback failed without diagnostics.");
            if (!_pumping) Deliver();
        }
        private void Deliver()
        {
            var success = _pendingSuccess; var failure = _pendingFailure;
            _pendingSuccess = false; _pendingFailure = null;
            if (failure != null) _failed?.Invoke(failure);
            else if (success)
            {
                var failed = _failed;
                try { _completed?.Invoke(); } catch (Exception exception) { failed?.Invoke(exception); }
            }
        }
        public void End(bool processEnd)
        {
            if (_ended) return; _ended = true; _completed = null; _failed = null;
            var wait = _wait; _wait = null;
            var failures = new List<Exception>();
            try { wait?.Lease?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            // Ordinary text advance detaches its callbacks; the presentation host still owns speech.
            // Whole-session cancellation must stop it. The next Text applies NextVoiceStop itself.
            try { if (_cancelVoices()) _voices?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            _voices = null;
            if (_started)
                while (_current < _performances.Length)
                    try { ApplyNext(); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Any()) throw new AggregateException("KKS ADV text playback cleanup failed.", failures);
        }
    }
}
