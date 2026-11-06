using System;
using System.Threading;

namespace MainGameIPhoneCamera
{
    // The user's explicit exception permits orientation polling once per second only.
    internal sealed class PhoneOrientationClock : IDisposable
    {
        private readonly PhoneDeadline _deadline;
        private readonly Func<bool> _tick;
        private bool _running;
        internal PhoneOrientationClock(SynchronizationContext context, Func<bool> tick)
        { _deadline = new PhoneDeadline(context); _tick = tick; }
        internal void SetActive(bool active)
        {
            if (_running == active) return;
            _running = active;
            _deadline.Cancel();
            if (active) Schedule();
        }
        private void Schedule()
        {
            _deadline.Arm(TimeSpan.FromSeconds(1), () =>
            {
                if (!_running) return;
                if (!_tick()) { SetActive(false); return; }
                if (_running) Schedule();
            });
        }
        public void Dispose() { SetActive(false); _deadline.Dispose(); }
    }
}
