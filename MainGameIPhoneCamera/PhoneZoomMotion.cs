using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    // A finite native animation owns interpolation. No Update, polling timer or pose listener.
    internal sealed class PhoneZoomMotion : MonoBehaviour
    {
        private Camera _camera;
        private Animation _animation;
        private AnimationClip _clip;
        private Action<float> _changed;
        private float _baseFov, _input;
        internal void Initialize(Camera camera, Action<float> changed)
        {
            _camera = camera; _changed = changed;
            _animation = gameObject.AddComponent<Animation>();
            _animation.playAutomatically = false;
            _animation.cullingType = AnimationCullingType.AlwaysAnimate;
        }
        internal void Drive(float input, float baseFov)
        {
            if (_clip != null && Math.Abs(_input - input) < .03f && _baseFov == baseFov) return;
            StopMotion();
            float speed = PhoneZoomPlan.Speed(input);
            if (speed == 0) return;
            _baseFov = baseFov; _input = input;
            float from = PhoneZoomPlan.Zoom(baseFov, _camera.fieldOfView);
            float to = speed > 0 ? PhoneZoomPlan.Maximum : PhoneZoomPlan.Minimum;
            if (Math.Abs(to - from) < .001f) return;
            float seconds = Math.Abs((to - from) / speed);
            int count = Math.Max(1, (int)Math.Ceiling(Math.Abs(to - from) / .05f));
            var keys = new Keyframe[count + 1];
            var events = new AnimationEvent[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float fraction = (float)i / count, zoom = from + (to - from) * fraction;
                float time = seconds * fraction;
                float tangent = PhoneZoomPlan.FovTangent(baseFov, zoom, speed);
                keys[i] = new Keyframe(time, PhoneZoomPlan.Fov(baseFov, zoom), tangent, tangent);
                // Push known timeline values to the text; no callback reads or polls input.
                events[i] = new AnimationEvent { time = time, functionName = nameof(ZoomValue), floatParameter = zoom };
            }
            _clip = new AnimationClip { legacy = true, wrapMode = WrapMode.ClampForever };
            _clip.SetCurve("", typeof(Camera), "field of view", new AnimationCurve(keys));
            _clip.events = events;
            _animation.AddClip(_clip, "Zoom"); _animation.Play("Zoom");
        }
        public void ZoomValue(float value) { _changed?.Invoke(value); }
        internal void Sample()
        { if (_clip != null && _camera != null) _changed?.Invoke(PhoneZoomPlan.Zoom(_baseFov, _camera.fieldOfView)); }
        internal void StopMotion()
        {
            if (_clip == null) return;
            float fov = _camera.fieldOfView;
            _animation.Stop(); _animation.RemoveClip("Zoom");
            Destroy(_clip); _clip = null; _input = 0;
            _camera.fieldOfView = fov;
            _changed?.Invoke(PhoneZoomPlan.Zoom(_baseFov, fov));
        }
        internal void Dispose()
        { StopMotion(); _changed = null; }
    }
}
