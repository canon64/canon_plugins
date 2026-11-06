using System;
using System.Threading;
using CanonGate;
namespace MainGameHandySync
{
    // One permit per H session. No location queries or scene searches.
    internal static class SessionGate
    {
        private static IDisposable _registration;
        private static GateApi.Permit _permit;
        private static Action _stop;
        private static bool _active;
        private static int _epoch;
        private static volatile bool _accepting;
        internal static HSceneProc HScene { get; private set; }
        internal static bool Allowed => _active && _permit.IsValid && HScene != null;
        // Worker threads only inspect this admission snapshot and epoch.
        internal static bool Accepting => _accepting;
        internal static int Epoch => Volatile.Read(ref _epoch);
        internal static void Open(string guid, Action start, Action stop, Func<bool> enabled)
        {
            _stop = stop;
            _registration = GateApi.Register(guid, () =>
            {
                if (!enabled()) return;
                HScene = GateApi.Current?.HScene;
                if (HScene == null) return;
                Interlocked.Increment(ref _epoch);
                _permit = GateApi.Capture(guid);
                _active = true;
                _accepting = true;
                start();
            }, Stop);
        }
        internal static void Stop()
        {
            if (!_active) return;
            _active = false;
            _accepting = false;
            _permit = default(GateApi.Permit);
            Interlocked.Increment(ref _epoch);
            try { _stop?.Invoke(); } finally { HScene = null; }
        }
        internal static void Close() { Stop(); _registration?.Dispose(); _registration = null; }
    }
}
