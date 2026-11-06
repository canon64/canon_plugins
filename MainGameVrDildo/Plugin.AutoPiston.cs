using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private const string PistonMotionName = "__DildoPistonMotion";
        private const string PistonClipName = "DildoAutoPiston";
        private Transform _pistonMotion;
        private Animation _pistonAnimation;
        private AnimationClip _pistonClip;
        // Ordinary H starts stopped; an explicit hotel snapshot can resume this state.
        private bool _pistonEnabled;
        private bool _hotelPistonRunning;
        private bool _pistonTriggerRunning;
        private bool CanRunPiston => _hotelPistonRunning || !VRGIN.Core.VR.Active || _pistonTriggerRunning;
        private bool IsPistonDriving => _pistonEnabled && _settings.PistonSpeed > 0f && _settings.PistonStrokeCm > 0f &&
            (!_settings.PistonRhythmSync || (_pistonAnimation != null && _pistonAnimation[PistonClipName] != null && _pistonAnimation[PistonClipName].speed > 0f));

        private void CreatePistonHierarchy(GameObject asset)
        {
            _settings.PistonSpeed = 0f;
            _pistonTriggerRunning = false;
            _pistonMotion = new GameObject(PistonMotionName).transform;
            _pistonMotion.SetParent(_model.transform, false);
            asset.transform.SetParent(_pistonMotion, false);
            asset.transform.localPosition = Vector3.zero;
            asset.transform.localRotation = Quaternion.identity;
            asset.transform.localScale = Vector3.one;
            _pistonAnimation = _model.AddComponent<Animation>();
            _pistonAnimation.playAutomatically = false;
            _pistonAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
            _pistonAnimation.enabled = false;
        }

        private void SetPistonEnabled(bool enabled)
        {
            if (!enabled) { StopPiston(); return; }
            if (!CanRunPiston || _pistonEnabled || (!Application.isFocused && !(VRGIN.Core.VR.Active && _pistonTriggerRunning)) || _model == null || _pistonAnimation == null ||
                !_settings.Enabled || _settings.PistonSpeed <= 0f || !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            _pistonEnabled = true;
            RebuildPistonClip();
        }

        // Called only at start, stroke/scale edits, or explicit parent changes.
        private void RebuildPistonClip()
        {
            if (!_pistonEnabled || _pistonAnimation == null || _pistonMotion == null) return;
            AnimationState previous = _pistonAnimation[PistonClipName];
            float phase = previous != null ? Mathf.Repeat(previous.normalizedTime, 1f) : 0f;
            float heldPosition = _pistonMotion.localPosition.y;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(_model.transform.lossyScale.y));
            float amplitude = PistonPolicy.Stroke(_settings.PistonStrokeCm) * 0.005f / scale;
            // Preserve both phase and current position, including while speed is zero.
            var keys = new Keyframe[17];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 16f;
                float value = i == 16 ? 0f : Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
                float tangent = Mathf.Cos(t * Mathf.PI * 2f) * Mathf.PI * 2f * amplitude;
                keys[i] = new Keyframe(t, value, tangent, tangent);
            }
            float centre = heldPosition - new AnimationCurve(keys).Evaluate(phase);
            for (int i = 0; i < keys.Length; i++) keys[i].value += centre;
            RemovePistonClip();
            _pistonClip = new AnimationClip { legacy = true, name = PistonClipName, wrapMode = WrapMode.Loop };
            _pistonClip.SetCurve(PistonMotionName, typeof(Transform), "localPosition.y", new AnimationCurve(keys));
            _pistonAnimation.AddClip(_pistonClip, PistonClipName);
            _pistonAnimation.enabled = true;
            _pistonAnimation.Play(PistonClipName);
            AnimationState current = _pistonAnimation[PistonClipName];
            current.normalizedTime = phase;
            current.speed = PistonPolicy.Speed(_settings.PistonSpeed);
            _pistonAnimation.Sample();
            ApplyPistonRhythm();
        }

        private void ApplyPistonSettings(float previousStroke)
        {
            _settings.PistonSpeed = PistonPolicy.Speed(_settings.PistonSpeed);
            _settings.PistonStrokeCm = PistonPolicy.Stroke(_settings.PistonStrokeCm);
            if (!_settings.Enabled) { StopPiston(); return; }
            if (!CanRunPiston || _settings.PistonSpeed <= 0f)
            {
                PausePiston();
                return;
            }
            if (!_pistonEnabled) { SetPistonEnabled(true); return; }
            if (!Mathf.Approximately(previousStroke, _settings.PistonStrokeCm)) RebuildPistonClip();
            else if (_pistonAnimation != null && _pistonAnimation[PistonClipName] != null)
                _pistonAnimation[PistonClipName].speed = _settings.PistonSpeed;
            ApplyPistonRhythm();
        }

        private void RemovePistonClip()
        {
            if (_pistonAnimation != null)
            {
                _pistonAnimation.Stop();
                _pistonAnimation.RemoveClip(PistonClipName);
            }
            if (_pistonClip != null) Destroy(_pistonClip);
            _pistonClip = null;
        }

        private void StopPiston()
        {
            _hotelPistonRunning = false;
            _pistonTriggerRunning = false;
            if (_settings != null) _settings.PistonSpeed = 0f;
            if (!_pistonEnabled && _pistonClip == null) return;
            _pistonEnabled = false;
            // Bake the visible offset before Stop resets animation bindings.
            Vector3 offset = _pistonMotion != null && _model != null
                ? _pistonMotion.position - _model.transform.position : Vector3.zero;
            RemovePistonClip();
            if (_pistonAnimation != null) _pistonAnimation.enabled = false;
            if (_pistonMotion != null) _pistonMotion.localPosition = Vector3.zero;
            if (_model == null) return;
            _model.transform.position += offset;
            if (_desktopGizmoProxy != null)
            {
                _desktopGizmoProxy.transform.position = _model.transform.position;
                _desktopGizmoProxy.transform.rotation = _model.transform.rotation;
            }
        }

        private void PausePiston()
        {
            // Unlike StopPiston, do not bake the child offset into the model or discard phase.
            _pistonEnabled = false;
            if (_pistonAnimation == null) return;
            if (_pistonAnimation[PistonClipName] != null) _pistonAnimation[PistonClipName].speed = 0f;
            _pistonAnimation.enabled = false;
        }
        private void BeginHeldPiston()
        {
            _pistonTriggerRunning = true;
            if (PistonPolicy.Speed(_settings.PistonSpeed) <= 0f) _settings.PistonSpeed = 1f;
            ApplyPistonSettings(_settings.PistonStrokeCm);
        }
        private void EndHeldPiston()
        {
            _pistonTriggerRunning = false;
            PausePiston();
        }
        private void ToggleHeldPiston()
        {
            if (_pistonTriggerRunning) EndHeldPiston();
            else BeginHeldPiston();
        }

        private void ChangePistonFromStick(Vector2 axis)
        {
            if ((!Application.isFocused && !(VRGIN.Core.VR.Active && _grabController != null)) || _model == null || !_settings.Enabled ||
                !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            float speed = _settings.PistonSpeed, stroke = _settings.PistonStrokeCm;
            PistonPolicy.Stick(axis.x, axis.y, Time.unscaledDeltaTime, ref _settings.PistonSpeed, ref _settings.PistonStrokeCm);
            if (speed == _settings.PistonSpeed && stroke == _settings.PistonStrokeCm) return;
            _edits.Remove("auto_piston_speed"); _edits.Remove("auto_piston_stroke");
            ApplyPistonSettings(stroke);
        }

        private void ChangePistonFromWheel()
        {
            if (VRGIN.Core.VR.Active || !Application.isFocused || _model == null || !_settings.Enabled || _desktopGizmo == null || !_desktopGizmo.IsDragging ||
                !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            // Reuse the exact capture owner registered by the gizmo; other UI owners still block input.
            string owner = MainGameTransformGizmo.Plugin.GUID + ".gizmo." + _desktopGizmo.GetInstanceID();
            float steps = UiInputCaptureApi.GetMouseScrollDeltaForOwner(owner).y;
            if (steps == 0f || !UiInputCaptureApi.GetMouseButtonForOwner(owner, 0)) return;
            float stroke = _settings.PistonStrokeCm;
            bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            PistonPolicy.Wheel(steps, controlHeld,
                ref _settings.PistonSpeed, ref _settings.PistonStrokeCm);
            _edits.Remove("auto_piston_speed"); _edits.Remove("auto_piston_stroke");
            ApplyPistonSettings(stroke);
        }
    }
}
