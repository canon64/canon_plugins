using System;

namespace MainGameVoiceFaceEventBridge.Audio
{
    // The latest accepted request owns the one existing envelope. A stale caller
    // must not cancel a later caller's request or a cue-owned dialogue pause.
    internal sealed class TransientBoostOwnership
    {
        private string _owner;
        internal void Replace(string owner) { _owner = owner; }
        internal void Clear() { _owner = null; }
        internal bool Cancel(string owner, bool pausedByBoost, bool cueHoldsDialogue,
            Action restoreEnvelope, Action resumeDialogue)
        {
            if (string.IsNullOrWhiteSpace(owner) || !string.Equals(_owner, owner, StringComparison.Ordinal)) return false;
            _owner = null;
            restoreEnvelope();
            if (pausedByBoost && !cueHoldsDialogue) resumeDialogue();
            return true;
        }
    }
}
