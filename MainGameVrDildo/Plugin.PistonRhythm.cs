using System;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private IDisposable _pistonRhythmSubscription;
        private bool _pistonRhythmAvailable;
        private double _pistonRhythmPhase, _pistonRhythmRate, _pistonRhythmAt;

        private void ConnectPistonRhythm()
        {
            // Optional integration: no hard assembly reference or runtime discovery loop.
            var api = Type.GetType("MainGameRhythmSync.MotionSyncApi, MainGameRhythmSync", false);
            var subscribe = api?.GetMethod("Subscribe", new[] { typeof(Action<bool, double, double>) });
            if (subscribe == null) return;
            _pistonRhythmSubscription = (IDisposable)subscribe.Invoke(null,
                new object[] { new Action<bool, double, double>(ReceivePistonRhythm) });
        }

        private void ReceivePistonRhythm(bool active, double phase, double rate)
        {
            _pistonRhythmAvailable = active;
            _pistonRhythmPhase = phase; _pistonRhythmRate = rate; _pistonRhythmAt = Time.time;
            ApplyPistonRhythm();
        }

        private void ApplyPistonRhythm()
        {
            if (_settings == null || !_settings.PistonRhythmSync || !_settings.Enabled ||
                !isActiveAndEnabled || !CanonPlugins.Shared.PluginSessionGate.Allowed ||
                !_pistonEnabled || _pistonAnimation == null) return;
            var state = _pistonAnimation[PistonClipName];
            if (state == null) return;
            // Keep the synchronization preference. Without playing music, use the manual rate
            // and preserve the current phase/position so stopping music never freezes the item.
            if (!_pistonRhythmAvailable)
            {
                state.speed = PistonPolicy.Speed(_settings.PistonSpeed);
                return;
            }
            bool valid = _pistonRhythmAvailable && !double.IsNaN(_pistonRhythmRate) &&
                !double.IsInfinity(_pistonRhythmRate) && _pistonRhythmRate > 0 &&
                _pistonRhythmRate <= 5 && !double.IsNaN(_pistonRhythmPhase) && !double.IsInfinity(_pistonRhythmPhase);
            state.speed = valid ? (float)_pistonRhythmRate : 0f;
            if (!valid) return;
            double phase = _pistonRhythmPhase + (Time.time - _pistonRhythmAt) * _pistonRhythmRate;
            state.normalizedTime = (float)(phase - Math.Floor(phase));
            _pistonAnimation.Sample();
        }

        private void ChangePistonRhythmSetting()
        {
            if (_settings.PistonRhythmSync) ApplyPistonRhythm();
            else if (_pistonEnabled && _pistonAnimation != null && _pistonAnimation[PistonClipName] != null)
                _pistonAnimation[PistonClipName].speed = PistonPolicy.Speed(_settings.PistonSpeed);
        }
    }
}
