using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // One displayed score and animation, with shared waits matching the native global point UI.
    internal sealed class KksPointDisplayPlayback : IDisposable
    {
        internal interface IDevice
        {
            IDisposable Play(int now, int target, Action beganAdding, Action completed, Action<Exception> failed);
            void Reset(int score);
        }
        private sealed class Wait : IDisposable
        {
            internal KksPointDisplayPlayback Owner;
            internal Action<int> Award;
            internal Action Completed;
            internal Action<Exception> Failed;
            public void Dispose() { var owner = Owner; Owner = null; owner?.Remove(this); }
        }
        private sealed class Run
        {
            internal IDisposable Animation;
            internal int Target;
            internal bool Began;
        }
        private readonly IDevice _device;
        private readonly List<Wait> _waits = new List<Wait>();
        private Run _run;
        private int _score;
        private bool _disposed;
        internal int DisplayedScore => _score;
        internal bool Active => _run != null;
        internal KksPointDisplayPlayback(IDevice device, int initialScore)
        { _device = device ?? throw new ArgumentNullException(nameof(device)); _score = initialScore; _device.Reset(_score); }
        internal IDisposable Present(int target, Action<int> award, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksPointDisplayPlayback));
            var wait = new Wait { Owner = this, Award = award, Completed = completed, Failed = failed };
            _waits.Add(wait);
            var previous = _run; var run = new Run { Target = target }; _run = run;
            try
            {
                previous?.Animation?.Dispose();
                if (_disposed || !ReferenceEquals(_run, run)) return wait;
                if (target <= _score) { _score = target; Finish(run, null); return wait; }
                var animation = _device.Play(_score, target, () => Begin(run), () => Finish(run, null), exception => Finish(run, exception));
                if (ReferenceEquals(_run, run)) run.Animation = animation; else animation?.Dispose();
            }
            catch (Exception exception) { if (ReferenceEquals(_run, run)) Finish(run, exception); else throw; }
            return wait;
        }
        private void Begin(Run run)
        {
            if (_disposed || !ReferenceEquals(_run, run) || run.Began) return;
            run.Began = true; var delta = run.Target - _score; _score = run.Target;
            var owner = _waits.LastOrDefault();
            try { owner?.Award?.Invoke(delta); }
            catch (Exception exception) { Finish(run, exception); }
        }
        private void Finish(Run run, Exception error)
        {
            if (_disposed || !ReferenceEquals(_run, run)) return;
            _run = null; var waits = _waits.ToArray(); _waits.Clear();
            foreach (var wait in waits) wait.Owner = null;
            try { run.Animation?.Dispose(); } catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
            // Reset before notifying callers, which may synchronously open the next point display.
            try { _device.Reset(_score); } catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
            var errors = new List<Exception>();
            foreach (var wait in waits)
                try { if (error == null) wait.Completed?.Invoke(); else wait.Failed?.Invoke(error); }
                catch (Exception exception) { errors.Add(exception); }
            if (errors.Count != default(int)) throw new AggregateException("Point display notifications failed.", errors);
        }
        private void Remove(Wait wait)
        {
            _waits.Remove(wait);
            if (_waits.Count != default(int) || _run == null) return;
            var run = _run; _run = null;
            try { run.Animation?.Dispose(); } finally { _device.Reset(_score); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var run = _run; _run = null;
            foreach (var wait in _waits) wait.Owner = null;
            _waits.Clear();
            try { run?.Animation?.Dispose(); } finally { _device.Reset(_score); }
        }
    }
}
