using System;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class ExternalVoicePlayer
    {
        private readonly Audio.TransientBoostOwnership _boostOwnership = new Audio.TransientBoostOwnership();

        internal bool RequestOwnedTransientBoost(string owner, float peak, float attack, float hold, float release,
            float silence, float fadeOut, float fadeIn, PublicApi.EasingShape easing)
        {
            if (!SessionGate.Allowed || string.IsNullOrWhiteSpace(owner)) return false;
            if (!RequestTransientBoost(peak, attack, hold, release, silence, fadeOut, fadeIn, easing)) return false;
            _boostOwnership.Replace(owner);
            return true;
        }

        internal bool CancelTransientVolumeBoost(string owner)
        {
            // Cancellation is cleanup and is valid after the requesting owner lost its permit.
            return _boostOwnership.Cancel(owner, _silencePhase == SilencePhase.Paused, _cueState.HoldsDialogue,
                () =>
                {
                    _boostActive = false;
                    _boostSilenceSec = 0f;
                    ResetSilenceState();
                    if (_boostFilter != null) _boostFilter.Gain = 1f;
                },
                () =>
                {
                    if (_source == null || _currentClip == null) return;
                    _source.UnPause();
                    TryBindLipSync(_dialogueFemale);
                });
        }
    }
}
