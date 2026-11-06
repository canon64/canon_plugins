using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private void UpdateOgazmCueTrigger(bool mayTrigger)
        {
            if (_settings == null || !_settings.MotionGaugeEnabled)
            { _ogazmVoicePending = _ogazmCueRequestedThisEntry = false; return; }
            float gauge01 = Mathf.Clamp01(_motionGaugeCm / _settings.MotionGaugeFullCm);
            if (gauge01 <= _settings.OgazmExitGaugeFraction)
                _ogazmCueRequestedThisEntry = false;
            if (!CanStartCloneOgazm()) { _ogazmVoicePending = false; return; }
            if (!mayTrigger || gauge01 < _settings.OgazmEnterGaugeFraction ||
                _ogazmCueRequestedThisEntry) return;
            if (!_settings.OgazmVoiceEnabled) return;
            _ogazmVoicePending = true;
        }

        private void DispatchPendingSpecialVoices()
        {
            if (DildoVoiceSuppressed)
            { _pistonVoicePending = _hardVoicePending = _ogazmVoicePending = false; return; }
            if (_ogazmVoicePending && !CanStartCloneOgazm()) _ogazmVoicePending = false;
            if (_scene == null || _sceneFlags == null || _sceneFlags.mode != HFlag.EMode.aibu ||
                _model == null || _settings == null || !_settings.Enabled) return;
            if (IsClonePose() && IsClimaxAnimation(_sceneFlags.nowAnimStateName))
            { _pistonVoicePending = _hardVoicePending = _ogazmVoicePending = false; return; }
            float now = Time.unscaledTime;
            // Dispatch the strongest newly qualified condition first. A queued normal
            // line must not hold a hard/climax condition behind its retry deadline.
            if (_ogazmVoicePending)
            {
                bool accepted = MainGameVoiceFaceEventBridge.PublicApi.TryRequestOgazmCue(Guid,
                    _settings.OgazmVoiceVolume, _settings.OgazmVoiceCooldownSeconds);
                if (accepted)
                {
                    _ogazmVoicePending = false; _ogazmCueRequestedThisEntry = true;
                    _pistonVoicePending = _hardVoicePending = false;
                }
                LogMotionEvent("OGAZM_VOICE_REQUEST", "accepted=" + accepted);
                return;
            }
            if (_hardVoicePending)
            {
                if (now < _nextHardPistonVoiceAttemptAt) return;
                bool accepted = MainGameVoiceFaceEventBridge.PublicApi.TryRequestHardPistonCue(Guid,
                    _settings.HardPistonVoiceVolume, _settings.HardPistonVoiceCooldownSeconds);
                _nextHardPistonVoiceAttemptAt = now + (accepted
                    ? _settings.HardPistonAcceptedRetrySeconds : _settings.HardPistonRejectedRetrySeconds);
                if (accepted) { _hardVoicePending = false; _hardPlayedThisEntry = true; _pistonVoicePending = false; }
                LogMotionEvent("HARD_VOICE_REQUEST", "accepted=" + accepted);
                return;
            }
            if (_pistonVoicePending)
            {
                if (now < _nextPistonVoiceAttemptAt) return;
                bool accepted = MainGameVoiceFaceEventBridge.PublicApi.TryRequestContinuousPistonCue(Guid,
                    _settings.PistonVoiceVolume, _settings.PistonVoiceCooldownSeconds);
                _nextPistonVoiceAttemptAt = now + (accepted
                    ? _settings.PistonAcceptedRetrySeconds : _settings.PistonRejectedRetrySeconds);
                if (accepted) { _pistonVoicePending = false; _pistonPlayedThisEntry = true; }
                LogMotionEvent("VOICE_REQUEST", "accepted=" + accepted);
                return;
            }
        }

        private void ResetCloneVoiceCycle()
        {
            _ogazmCueRequestedThisEntry = false;
            _pistonPlayedThisEntry = _hardPlayedThisEntry = false;
            _pistonVoicePending = _hardVoicePending = _ogazmVoicePending = false;
            _nextPistonVoiceAttemptAt = _nextHardPistonVoiceAttemptAt = 0f;
        }
    }
}
