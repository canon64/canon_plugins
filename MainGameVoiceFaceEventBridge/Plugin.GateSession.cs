using System;
using HarmonyLib;
using System.Threading;
using CanonGate;
namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private Harmony _nativeAdmissionHarmony;
        private CancellationTokenSource _gateOutgoing;
        private readonly object _gateInboundLock = new object();
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession()
        {
            CurrentProc = SessionGate.HScene;
            BeginAiModeFace();
            _gateOutgoing?.Dispose();
            _gateOutgoing = new CancellationTokenSource();
            BeginContactMoans();
            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin));
            if (RuntimeLoggingEnabled) FacePresetProbeHooks.Apply(_harmony, Log, LogWarn, LogError);
            if (CurrentProc != null) EnsurePoseClassificationFilesFromProc(CurrentProc);
            StartOrRestartPipeServer(true, "gate_start");
        }

        private void EndGateSession()
        {
            GateStopStep(EndSyringeDialogues);
            GateStopStep(EndContactMoans);
            GateStopStep(() => _gateOutgoing?.Cancel());
            GateStopStep(() => StopPipeServer("gate_stop"));
            GateStopStep(() => { lock (_gateInboundLock) _inboundCommandQueue.Clear(); });
            GateStopStep(() => ResetResponseTextQueue("gate_stop"));
            GateStopStep(() => _sequenceSessions.Deactivate());
            GateStopStep(() => ClearDelayedActions("gate_stop"));
            GateStopStep(StopAllCoroutines);
            GateStopStep(() => _externalVoicePlayer.Stop("gate_stop"));
            GateStopStep(EndAiModeFace);
            GateStopStep(ResetNativeVoiceAdmission);
            // Restore while CurrentProc and the captured original value are still held.
            GateStopStep(RestoreVoiceProcStopIfNeeded);
            _voiceGuardState.Reset();
            GateStopStep(() => SetKaleidoscopeThought(false));
            _thoughtMarkedForSession = false;
            GateStopStep(() => _harmony?.UnpatchSelf());
            _harmony = null;
            CurrentProc = null;
            _facePresetProbe = null;
        }

        private void GateStopStep(Action action)
        {
            try { action(); }
            catch (Exception ex) { LogWarn("[gate-stop] " + ex); }
        }

        internal bool RequestOwnedTransientVolumeBoost(string owner, float peak, float attack, float hold, float release,
            float silence, float fadeOut, float fadeIn, PublicApi.EasingShape easing)
            => SessionGate.Allowed && _externalVoicePlayer != null &&
               _externalVoicePlayer.RequestOwnedTransientBoost(owner, peak, attack, hold, release, silence, fadeOut, fadeIn, easing);

        internal bool CancelTransientVolumeBoost(string owner) => _externalVoicePlayer != null &&
            _externalVoicePlayer.CancelTransientVolumeBoost(owner);
        
    }
}
