using System;
using System.Linq;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        public bool HotelGadgetAllowed => PluginSessionGate.Allowed && _settings != null && _settings.Enabled && isActiveAndEnabled;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ディルドは無効、またはこのHでは許可されていません。");
            if (visible && _model == null) Spawn(); else if (!visible && _model != null) DeleteModel();
            if (visible && _model == null) throw new InvalidOperationException("ディルドのモデルを読み込めません。");
        }
        [Serializable] private sealed class HotelMotion
        {
            public bool running;
            public float phase;
            public Vector3 offset;
        }
        private DildoSettings _beforeHotelSettings;
        private string _restoredHotelMotion;
        private bool _restoredHotelMotionApplied;
        public string CaptureHotelGadget(Transform room)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ディルドの保存は許可されたH内で行ってください。");
            var data = GadgetRoomSnapshot.Capture(room, _model == null ? null : _model.transform);
            data.settings = JsonUtility.ToJson(_settings);
            var state = _pistonAnimation == null ? null : _pistonAnimation[PistonClipName];
            data.runtime = JsonUtility.ToJson(new HotelMotion { running = _pistonEnabled,
                phase = state == null ? 0f : Mathf.Repeat(state.normalizedTime, 1f),
                offset = _pistonMotion == null ? Vector3.zero : _pistonMotion.localPosition });
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ディルドの復元は許可されたH内で行ってください。");
            var data = GadgetRoomSnapshot.Decode(json);
            var motion = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<HotelMotion>(data.runtime);
            if (motion != null && (float.IsNaN(motion.phase) || float.IsInfinity(motion.phase) ||
                motion.phase < 0f || motion.phase >= 1f ||
                !Finite(motion.offset.x) || !Finite(motion.offset.y) || !Finite(motion.offset.z)))
                throw new ArgumentException("自動往復の保存状態が不正です。");
            var settings = string.IsNullOrEmpty(data.settings) ? null : JsonUtility.FromJson<DildoSettings>(data.settings);
            settings?.Normalize();
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("ディルドの配置データが不正です。");
            if (data.visible && motion != null && motion.running && (settings ?? _settings).PistonSpeed <= 0f)
                throw new ArgumentException("自動往復の保存速度が停止中なのに実行中と記録されています。");
            _restoredHotelMotionApplied = false; _restoredHotelMotion = null;
            StopPiston();
            SetHotelGadgetVisible(data.visible);
            if (settings != null)
            {
                if (_beforeHotelSettings == null) _beforeHotelSettings = _settings;
                settings.Enabled = _settings.Enabled;
                settings.DiagnosticLogEnabled = _settings.DiagnosticLogEnabled;
                settings.MotionDiagnosticsEnabled = _settings.MotionDiagnosticsEnabled;
                _settings = settings;
            }
            if (!data.visible) return;
            ReleaseGrab(); TrySetComposerParent(null, false); data.poses.Single().Apply(room, _model.transform);
            // Apply settings side effects now. The generic deferred settings path rewrites
            // root scale from ModelScale on the next frame and destroys this saved placement.
            _settingsChanged = false;
            PauseAibuMotion("hotel_restore"); UnregisterColliders(); UpdateColliders();
            if (_engaged && _selectedTarget == TargetKind.Vaginal) RegisterColliders();
            if (motion == null) return; // Legacy records restore placement without starting motion.
            _hotelPistonRunning = motion.running;
            // Build at the saved phase, so the next sample does not move the centre.
            if (_pistonMotion != null && _pistonAnimation != null)
            {
                bool rhythmSync = _settings.PistonRhythmSync;
                _settings.PistonRhythmSync = false;
                try
                {
                _pistonMotion.localPosition = motion.offset;
                _pistonEnabled = true;
                RebuildPistonClip();
                var state = _pistonAnimation[PistonClipName];
                state.normalizedTime = motion.phase;
                _pistonAnimation.Sample();
                _pistonMotion.localPosition = motion.offset;
                RebuildPistonClip();
                if (!motion.running) PausePiston();
                var applied = _pistonAnimation[PistonClipName];
                _restoredHotelMotionApplied = applied != null &&
                    Mathf.Abs(Mathf.Repeat(applied.normalizedTime, 1f) - motion.phase) < .0001f &&
                    Vector3.Distance(_pistonMotion.localPosition, motion.offset) < .0001f && _pistonEnabled == motion.running;
                if (!_restoredHotelMotionApplied) throw new InvalidOperationException("自動往復の保存位相・位置を適用できません。");
                _restoredHotelMotion = data.runtime;
                }
                finally { _settings.PistonRhythmSync = rhythmSync; }
            }
            else throw new InvalidOperationException("自動往復の制御対象を生成できません。");
        }
        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible || string.IsNullOrEmpty(data.runtime)) return true;
            if (!_restoredHotelMotionApplied || _restoredHotelMotion != data.runtime || _pistonMotion == null || _pistonAnimation == null) return false;
            var motion = JsonUtility.FromJson<HotelMotion>(data.runtime);
            var state = _pistonAnimation[PistonClipName];
            if (motion == null || state == null || _pistonEnabled != motion.running || _pistonAnimation.enabled != motion.running) return false;
            if (!motion.running)
                return Mathf.Abs(Mathf.Repeat(state.normalizedTime, 1f) - motion.phase) < .0001f && Vector3.Distance(_pistonMotion.localPosition, motion.offset) < .0001f;
            // Running phase and offset naturally advance. Initialization was measured above;
            // ensure the same native clip is still driving finite phase at the selected speed.
            return _pistonClip != null && Finite(state.normalizedTime) && Finite(state.speed) && state.speed >= 0f &&
                Finite(_pistonMotion.localPosition.y) && Mathf.Abs(_pistonMotion.localPosition.x - motion.offset.x) < .0001f &&
                Mathf.Abs(_pistonMotion.localPosition.z - motion.offset.z) < .0001f;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void RestorePreHotelSettings()
        {
            _hotelPistonRunning = false;
            _restoredHotelMotionApplied = false; _restoredHotelMotion = null;
            if (_beforeHotelSettings == null) return;
            _settings = _beforeHotelSettings; _beforeHotelSettings = null;
            _settings.PistonSpeed = 0f;
        }
    }
}
