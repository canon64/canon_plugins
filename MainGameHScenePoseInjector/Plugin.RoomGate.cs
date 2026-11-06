using System;
using System.Collections.Generic;

namespace MainGameHScenePoseInjector
{
    public sealed partial class Plugin
    {
        private bool _wasInAllPoseRoom;
        private readonly Dictionary<ChaControl, Action> _roomExpressionRestore = new Dictionary<ChaControl, Action>();

        private void RememberRoomExpression(ChaControl cha)
        {
            if (_roomExpressionRestore.ContainsKey(cha)) return;
            int eyebrow = cha.GetEyebrowPtn(), eyes = cha.GetEyesPtn(), mouth = cha.GetMouthPtn();
            _roomExpressionRestore.Add(cha, () =>
            {
                if (cha == null) return;
                cha.ChangeEyebrowPtn(eyebrow, blend: false);
                cha.ChangeEyesPtn(eyes, blend: false);
                cha.ChangeMouthPtn(mouth, blend: false);
            });
        }

        private void LeaveAllPoseRoom()
        {
            if (!_wasInAllPoseRoom && _snapshots.Count == 0 && _effectTargets.Count == 0 && _roomExpressionRestore.Count == 0) return;
            _wasInAllPoseRoom = false;
            if (_sessionEffectSuspension != null)
            {
                _settings.AutoCycle = _sessionEffectSuspension.Auto;
                foreach (var pair in _sessionEffectSuspension.Targets)
                    if (!_effectTargets.ContainsKey(pair.Key)) _effectTargets[pair.Key] = pair.Value;
                _sessionEffectSuspension = null;
            }
            ClearPoseBlend();
            RestoreAllSnapshots("AllPoseRoom-exit");
            RestoreNeckLookCamera();
            _lockedEyebrowPtn = _lockedEyesPtn = _lockedMouthPtn = null;
            _desiredHohoAka = _desiredTears = null;
            foreach (var restore in _roomExpressionRestore.Values)
                try { restore(); } catch (Exception ex) { LogWarn("expression restore: " + ex.Message); }
            _roomExpressionRestore.Clear();
            foreach (var state in _effectTargets.Values)
                for (int channel = 0; channel < EffectChannelCount; channel++)
                    if (state.Original[channel].HasValue)
                        try { WriteEffect(state.Cha, channel, state.Original[channel].Value, restoring: true); }
                        catch (Exception ex) { LogWarn("effect restore: " + ex.Message); }
            _effectTargets.Clear();
            _effectScene = null;
            _activeHSceneProc = null;
            _hSceneWasDetected = false;
            _hSceneLostCandidateSince = -1f;
            _nextHSceneProbeAt = 0f;
            _controllerChangeCandidateSince.Clear();
        }
    }
}
