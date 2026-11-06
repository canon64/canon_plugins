using System;
using System.Threading;
using CanonGate;
namespace MainGameSubtitleCore
{
    // One permit per H session. No location queries or scene searches.
    internal static class SessionGate
    {
        private static IDisposable _registration;
        private static GateApi.Permit _permit;
        private static GateApi.ActivityRegistration _activity;
        private static GateApi.ActivityPermit _activityPermit;
        private static Action _stop;
        private static bool _active;
        private static int _epoch;
        private static volatile bool _accepting;
        internal static HSceneProc HScene { get; private set; }
        internal static bool Allowed => _active && (HScene != null ? _permit.IsValid : _activityPermit.IsValid);
        // Worker threads only inspect this admission snapshot and epoch.
        internal static bool Accepting => _accepting;
        internal static int Epoch => Volatile.Read(ref _epoch);
        internal static void Open(string guid, Action start, Action stop, Func<bool> enabled)
        {
            _stop = stop;
            _activity = GateApi.RegisterActivity(guid, GateApi.ActivityKind.AiShoujoMode, () =>
            {
                if (!enabled()) return;
                HScene = GateApi.Current?.HScene;
                if (HScene == null && (GateApi.Current != null || GateApi.AiShoujoMode == null)) return;
                Interlocked.Increment(ref _epoch);
                _permit = GateApi.Capture(guid);
                _activityPermit = _activity.Capture();
                _active = true;
                _accepting = true;
                start();
            }, Stop);
            _registration = _activity;
        }
        internal static void Stop()
        {
            if (!_active) return;
            _active = false;
            _accepting = false;
            _permit = default(GateApi.Permit);
            _activityPermit = default(GateApi.ActivityPermit);
            Interlocked.Increment(ref _epoch);
            try { _stop?.Invoke(); } finally { HScene = null; }
        }
        internal static void Close() { Stop(); _registration?.Dispose(); _registration = null; }
    }
}
