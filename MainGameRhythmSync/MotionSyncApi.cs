using System;
using UnityEngine;

namespace MainGameRhythmSync
{
    // Published by the existing group clock. Native animations run between changes.
    public static class MotionSyncApi
    {
        private static Action<bool, double, double> _listeners;
        private static bool _active;
        private static double _phase, _rate, _at;

        public static IDisposable Subscribe(Action<bool, double, double> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            _listeners += listener;
            listener(_active, _phase + (Time.time - _at) * _rate, _rate);
            return new Subscription(listener);
        }

        internal static void Publish(bool active, double phase = 0, double rate = 0)
        {
            double now = Time.time;
            if (_active == active && (!active ||
                (Math.Abs(rate - _rate) < 0.000001 && Math.Abs(phase - (_phase + (now - _at) * _rate)) < 0.02))) return;
            _active = active; _phase = phase; _rate = rate; _at = now;
            _listeners?.Invoke(active, phase, rate);
        }

        private sealed class Subscription : IDisposable
        {
            private Action<bool, double, double> _listener;
            internal Subscription(Action<bool, double, double> listener) { _listener = listener; }
            public void Dispose() { _listeners -= _listener; _listener = null; }
        }
    }
}
