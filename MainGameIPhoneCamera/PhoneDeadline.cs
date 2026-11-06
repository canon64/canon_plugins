using System;
using System.Threading;

namespace MainGameIPhoneCamera
{
    // One deadline notification, not a timer that polls game/room/input state.
    internal sealed class PhoneDeadline : IDisposable
    {
        private readonly SynchronizationContext _context;
        private Timer _timer;
        private int _generation;
        internal PhoneDeadline(SynchronizationContext context) { _context = context ?? throw new ArgumentNullException(nameof(context)); }
        internal void Arm(TimeSpan delay, Action callback)
        {
            Cancel(); int generation = _generation;
            _timer = new Timer(_ => _context.Post(__ =>
            {
                if (generation != _generation) return;
                _timer?.Dispose(); _timer = null;
                callback();
            }, null), null, delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
        }
        internal void Cancel() { ++_generation; _timer?.Dispose(); _timer = null; }
        public void Dispose() => Cancel();
    }
}
