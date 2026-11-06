using System;
using System.Threading;
using CanonGate;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
namespace MainGameVoiceFaceEventBridge
{
    // One permit per H session. No location queries or scene searches.
    internal static partial class SessionGate
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
        internal static UnityEngine.MonoBehaviour Scene { get; private set; }
        internal static HFlag Flags { get; private set; }
        internal static HVoiceCtrl Voice { get; private set; }
        private static readonly List<ChaControl> _females = new List<ChaControl>();
        internal static IReadOnlyList<ChaControl> Females => _females;
        internal static ChaControl Actor { get; private set; }
        internal static bool Allowed => _active && (Scene != null ? _permit.IsValid : Actor != null && _activityPermit.IsValid);
        // Worker threads only inspect this admission snapshot and epoch.
        internal static bool Accepting => _accepting;
        internal static int Epoch => Volatile.Read(ref _epoch);
        internal static void Open(string guid, Action start, Action stop, Func<bool> enabled)
        {
            _stop = stop;
            _activity = GateApi.RegisterActivity(guid, GateApi.ActivityKind.AiShoujoMode, () =>
            {
                if (!enabled()) return;
                Scene = GateApi.Current?.Kind == GateApi.SessionKind.H ? GateApi.Current.Scene : null;
                HScene = Scene as HSceneProc;
                var runtime = new CanonPlugins.Shared.HSceneRuntime(Scene);
                Flags = runtime.Flags;
                Voice = runtime.Voice;
                Actor = GateApi.Current == null ? GateApi.AiShoujoMode?.Actor as ChaControl : null;
                if (Scene == null && Actor == null) return;
                _females.Clear();
                var participants = Scene == null ? null : AccessTools.Field(Scene.GetType(), "lstFemale")?.GetValue(Scene) as IEnumerable;
                if (participants != null)
                    foreach (var value in participants)
                        if (value is ChaControl female && female != null && female.sex == 1) _females.Add(female);
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
            try { _stop?.Invoke(); } finally { _females.Clear(); Scene = null; HScene = null; Flags = null; Voice = null; Actor = null; }
        }
        internal static void Close() { Stop(); _registration?.Dispose(); _registration = null; }
    }
}
