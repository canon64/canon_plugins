using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvCrossFadePlayback : KksAdvCrossFadeHost.IBackend, IDisposable
    {
        internal interface IDevice : IDisposable
        {
            bool HasFrame { get; }
            float DefaultDuration { get; }
            bool IsZero(float duration);
            IDisposable Animate(float duration, Action completed, Action<Exception> failed);
            void ForceEnd();
        }
        private sealed class Operation : KksAdvCrossFadeHost.IOperation
        {
            internal KksAdvCrossFadePlayback Owner;
            internal Action Completed;
            internal Action<Exception> Failed;
            internal bool Ended, Notified, Implicit;
            public void End(bool processEnd)
            {
                if (Ended) return; Ended = true; var owner = Owner;
                owner?._operations.Remove(this);
                if (!processEnd && owner != null && !owner._disposed) owner.ForceEnd();
            }
            public void Dispose() { Owner?._operations.Remove(this); Owner = null; Completed = null; Failed = null; }
        }
        private readonly IDevice _device;
        private readonly float _zero;
        private readonly HashSet<Operation> _operations = new HashSet<Operation>();
        private IDisposable _animation;
        private object _generation;
        private bool _active, _disposed;
        internal int PendingOperations => _operations.Count;
        internal KksAdvCrossFadePlayback(IDevice device, float zero) { _device = device ?? throw new ArgumentNullException(nameof(device)); _zero = zero; }
        public KksAdvCrossFadeHost.IOperation Start(float duration, Action completed, Action<Exception> failed) => Begin(duration, completed, failed, false);
        internal void StartImplicit(float duration, Action<Exception> failed) => Begin(duration, null, failed ?? throw new ArgumentNullException(nameof(failed)), true);
        private Operation Begin(float duration, Action completed, Action<Exception> failed, bool implicitFade)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvCrossFadePlayback));
            if (!_device.HasFrame) throw new InvalidOperationException("CrossFade requires a frame captured before presentation changes.");
            var operation = new Operation { Owner = this, Completed = completed, Failed = failed, Implicit = implicitFade };
            _operations.Add(operation);
            try
            {
                var time = duration < _zero ? _device.DefaultDuration : duration;
                // Native zero-time FadeStart leaves an already-running crossfade untouched.
                if (!_device.IsZero(time))
                {
                    var previous = _animation; _animation = null; _generation = null; previous?.Dispose();
                    var generation = new object(); _generation = generation; _active = true;
                    var animation = _device.Animate(time, () => Complete(generation), exception => Fail(generation, exception));
                    if (_disposed || !ReferenceEquals(_generation, generation)) animation?.Dispose(); else _animation = animation;
                }
                if (!_active) Notify();
                return operation;
            }
            catch
            {
                operation.Dispose(); _generation = null; _active = false;
                var animation = _animation; _animation = null;
                try { animation?.Dispose(); } finally { _device.ForceEnd(); }
                throw;
            }
        }
        private void Complete(object generation)
        {
            if (_disposed || !ReferenceEquals(_generation, generation)) return;
            _generation = null; _active = false; var animation = _animation; _animation = null; animation?.Dispose(); Notify();
        }
        private void Notify()
        {
            foreach (var operation in _operations.ToArray())
            {
                if (!_operations.Contains(operation) || operation.Notified || operation.Ended) continue;
                operation.Notified = true;
                if (operation.Implicit) { operation.End(true); operation.Dispose(); continue; }
                operation.Completed?.Invoke();
            }
        }
        private void Fail(object generation, Exception exception)
        {
            if (_disposed || !ReferenceEquals(_generation, generation)) return;
            _generation = null; _active = false; var animation = _animation; _animation = null; animation?.Dispose();
            foreach (var operation in _operations.ToArray())
            {
                if (!_operations.Contains(operation) || operation.Notified || operation.Ended) continue;
                operation.Notified = true; operation.Failed?.Invoke(exception);
                if (operation.Implicit) { operation.End(true); operation.Dispose(); }
            }
        }
        private void ForceEnd()
        {
            _generation = null; _active = false; var animation = _animation; _animation = null;
            try { animation?.Dispose(); } finally { _device.ForceEnd(); }
            Notify();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var operation in _operations.ToArray()) operation.Dispose();
            _generation = null; var animation = _animation; _animation = null;
            try { animation?.Dispose(); } finally { _device.Dispose(); }
        }
    }
}
