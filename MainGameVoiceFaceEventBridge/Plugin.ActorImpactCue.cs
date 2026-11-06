namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        internal bool RequestActorImpactCue(string owner, ChaControl actor)
        {
            if (SyringeDialogueActive || !SessionGate.Allowed || _externalVoicePlayer == null || actor == null ||
                !CanonGate.GateApi.CanRun(owner)) return false;
            SessionGate.RefreshContactState();
            bool participant = false;
            foreach (var female in SessionGate.Females) if (female == actor) { participant = true; break; }
            if (!participant) return false;
            // Contact reactions do not depend on the current H motion or choose female slot zero.
            if(!TrySelectContactMoan(actor,out var clip))return false;
            return _externalVoicePlayer.RequestContactCue(owner, actor, clip);
        }
    }
}
