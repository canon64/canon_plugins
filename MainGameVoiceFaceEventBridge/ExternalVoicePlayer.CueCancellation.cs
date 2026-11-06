namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class ExternalVoicePlayer
    {
        internal void CancelImpactCueForMotion()
        {
            // Motion eligibility controls the next cue, never the lifetime of an accepted dildo line.
            // Explicit owner OFF, syringe priority, session end and disposal keep their own cleanup.
            if (_priorityDeepCueActive || _priorityPistonCueActive || _explicitActorCueActive) return;
            ApplyCueActions(_cueState.Disable());
        }
    }
}
