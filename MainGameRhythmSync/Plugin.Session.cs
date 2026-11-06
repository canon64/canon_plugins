using System;
using CanonGate;
using UnityEngine;
using BeatController = MainGameBeatSyncSpeed.Plugin;
using SpeedController = MainGameSpeedLimitBreak.Plugin;

namespace MainGameRhythmSync
{
    public sealed partial class Plugin
    {
        private IDisposable _gateRegistration;
        private GateApi.Permit _permit;
        private ulong _lastSessionId;
        internal SessionScene SessionScene { get; private set; }
        public bool SessionActive => isActiveAndEnabled && _permit.IsValid && SessionScene != null;

        private void BeginRhythmSession()
        {
            var context = GateApi.Current;
            if (context == null || context.Kind != GateApi.SessionKind.H || context.Scene == null) return;
            if (_lastSessionId == context.Id) return;
            _lastSessionId = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            SessionScene = new SessionScene(context.Scene);
            PrepareEffectSource();
            if (LoggingEnabled) Logger.LogInfo("[canon_gate] RhythmSync started session=" + context.Id);
        }
        private void EndRhythmSession()
        {
            bool wasActive = SessionScene != null;
            _permit = default;
            BeatController.Instance?.EndSession();
            SpeedController.Instance?.EndSession();
            _effectSource = null;
            ReleaseGroup();
            SessionScene = null;
            if (wasActive && LoggingEnabled) Logger.LogInfo("[canon_gate] RhythmSync stopped");
        }

        // Called by the real compatibility plugins. Facets have independent GUID policies.
        public void BeginBeatSession() => BeatController.Instance?.BeginSession();
        public void EndBeatSession() { BeatController.Instance?.EndSession(); ReleaseGroup(); }
        public void BeginSpeedSession() => SpeedController.Instance?.BeginSession();
        public void EndSpeedSession() { SpeedController.Instance?.EndSession(); ReleaseGroup(); }
    }
}
