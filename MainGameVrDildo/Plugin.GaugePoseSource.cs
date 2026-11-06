using System;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        // Main-thread notifications from the existing gauge calculations, never a polling loop.
        public static event Action<ChaControl, float> MotionGaugeChanged;
        public static event Action MotionGaugeEnded;
        private bool _gaugePosePublished;
        private bool _gaugePoseAvailable;
        private ChaControl _gaugePoseActor;
        private float _gaugePoseValue;

        public static bool TryGetMotionGauge(out ChaControl actor, out float weight)
        {
            actor = null;
            weight = 0f;
            var p = Instance;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || p == null || !p.isActiveAndEnabled ||
                p._settings == null || !p._settings.Enabled || !p._settings.MotionGaugeEnabled ||
                p._model == null || p._female == null || p._scene == null || p._sceneFlags == null ||
                p._sceneFlags.mode != HFlag.EMode.aibu) return false;
            actor = p._female;
            weight = Mathf.Clamp01(p._motionGaugeCm / p._settings.MotionGaugeFullCm);
            p._gaugePoseAvailable = true;
            return true;
        }

        private void NotifyGaugePoseChanged()
        {
            if (!TryGetMotionGauge(out var actor, out float value)) { EndGaugePoseSource(); return; }
            if (_gaugePosePublished && _gaugePoseActor == actor && _gaugePoseValue == value) return;
            _gaugePosePublished = true;
            _gaugePoseActor = actor;
            _gaugePoseValue = value;
            InvokeGaugeSubscribers(MotionGaugeChanged, callback => callback(actor, value));
        }

        private void EndGaugePoseSource()
        {
            // Also notify a consumer that subscribed at an empty gauge before the first movement.
            if (!_gaugePoseAvailable) return;
            _gaugePoseAvailable = false;
            _gaugePosePublished = false;
            _gaugePoseActor = null;
            InvokeGaugeSubscribers(MotionGaugeEnded, callback => callback());
        }

        private void InvokeGaugeSubscribers<T>(T handlers, Action<T> invoke) where T : Delegate
        {
            if (handlers == null) return;
            foreach (T callback in handlers.GetInvocationList())
                try { invoke(callback); }
                catch (Exception ex) { Logger.LogWarning("Gauge pose subscriber failed: " + ex.Message); }
        }
    }
}
