using System;
using System.Collections.Generic;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private readonly HashSet<SyringeDialogueLease> _syringeDialogueLeases = new HashSet<SyringeDialogueLease>();
        internal bool SyringeDialogueActive => _syringeDialogueLeases.Count != 0;
        internal IDisposable BeginSyringeDialogue(Action interrupted)
        {
            if (!SessionGate.Allowed || interrupted == null) return null;
            var lease = new SyringeDialogueLease(this, interrupted);
            _syringeDialogueLeases.Add(lease);
            return lease;
        }
        internal void ExternalDialogueStarted(bool thought)
        {
            if (!SessionGate.Allowed || thought) return;
            EndSyringeDialogues();
        }
        private void EndSyringeDialogues()
        {
            var leases = new List<SyringeDialogueLease>(_syringeDialogueLeases);
            _syringeDialogueLeases.Clear();
            foreach (var lease in leases) lease.Interrupt();
        }
        private sealed class SyringeDialogueLease : IDisposable
        {
            private Plugin _owner;
            private Action _interrupted;
            internal SyringeDialogueLease(Plugin owner, Action interrupted) { _owner = owner; _interrupted = interrupted; }
            internal void Interrupt() { var callback = _interrupted; Dispose(); callback?.Invoke(); }
            public void Dispose()
            {
                var owner = _owner; _owner = null; _interrupted = null;
                owner?._syringeDialogueLeases.Remove(this);
            }
        }

        internal bool InterruptAllVoicesForSyringe()
        {
            if (!SessionGate.Allowed) return false;
            // Stop() aborts cues without resuming paused dialogue and invalidates WAV loads.
            _externalVoicePlayer?.Stop("syringe priority");
            ResetNativeVoiceAdmission();
            lock (_gateInboundLock) _inboundCommandQueue?.Clear();
            _sequenceSessions?.Deactivate();
            ClearDelayedActions("syringe priority");
            ResetResponseTextQueue("syringe priority");
            Manager.Voice.StopAll(false);
            return true;
        }
    }
}
