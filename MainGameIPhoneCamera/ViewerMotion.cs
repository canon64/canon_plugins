using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    // Only finite native clips run while the stick is held. No frame callbacks or input polling.
    internal sealed class ViewerMotion : MonoBehaviour
    {
        private Transform _rect;
        private RectTransform _image;
        private int _imageWidth, _imageHeight;
        private float _viewportWidth, _viewportHeight;
        private Animation _animation;
        private AnimationClip _clip;
        private Vector2 _fit;
        private float _x, _y;
        private bool _pan;
        internal void Initialize(Transform target, RectTransform image, int width, int height, float viewportWidth, float viewportHeight)
        {
            StopMotion(); _rect = target; _image = image; _imageWidth = width; _imageHeight = height;
            _rect.localScale = Vector3.one; _rect.localPosition = Vector3.zero;
            Resize(viewportWidth, viewportHeight);
            if (_animation == null) { _animation = gameObject.AddComponent<Animation>(); _animation.playAutomatically = false; _animation.cullingType = AnimationCullingType.AlwaysAnimate; }
        }
        internal void Resize(float width, float height)
        {
            if (_rect == null) return;
            StopMotion(); _viewportWidth = width; _viewportHeight = height;
            float fit = ViewerLayout.Fit(_imageWidth, _imageHeight, width, height);
            _fit = new Vector2(_imageWidth * fit, _imageHeight * fit); _image.sizeDelta = _fit;
            Vector3 pos = _rect.localPosition;
            _rect.localPosition = new Vector3(ViewerLayout.Clamp(pos.x, ViewerLayout.Limit(_fit.x, width, _rect.localScale.x)), ViewerLayout.Clamp(pos.y, ViewerLayout.Limit(_fit.y, height, _rect.localScale.x)), pos.z);
        }
        internal void Drive(float x, float y, bool pan)
        {
            if (_rect == null) return;
            x = ViewerLayout.Speed(x); y = ViewerLayout.Speed(y);
            if (_clip != null && Math.Abs(x - _x) < .02f && Math.Abs(y - _y) < .02f && pan == _pan) return;
            StopMotion(); _x = x; _y = y; _pan = pan;
            float zoom = _rect.localScale.x;
            Vector3 pos = _rect.localPosition;
            _clip = new AnimationClip { legacy = true, wrapMode = WrapMode.ClampForever };
            if (pan)
            {
                float lx = ViewerLayout.Limit(_fit.x, _viewportWidth, zoom), ly = ViewerLayout.Limit(_fit.y, _viewportHeight, zoom);
                float vx = x * 448, vy = y * 448;
                float tx = vx == 0 ? 0 : (Math.Sign(vx) * lx - pos.x) / vx;
                float ty = vy == 0 ? 0 : (Math.Sign(vy) * ly - pos.y) / vy;
                float duration = Math.Max(tx, ty);
                if (duration <= .001f) { Destroy(_clip); _clip = null; return; }
                _clip.SetCurve("", typeof(Transform), "localPosition.x", Move(pos.x, vx, tx, duration));
                _clip.SetCurve("", typeof(Transform), "localPosition.y", Move(pos.y, vy, ty, duration));
            }
            else
            {
                float speed = y * 1.5f, target = speed > 0 ? 20 : 1;
                if (speed == 0 || Math.Abs(zoom - target) < .001f) { Destroy(_clip); _clip = null; return; }
                float duration = Math.Abs((target - zoom) / speed);
                int count = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - zoom) * 10));
                var z = new Keyframe[count + 1]; var px = new Keyframe[count + 1]; var py = new Keyframe[count + 1];
                for (int i = 0; i <= count; i++)
                {
                    float t = duration * i / count, value = zoom + speed * t;
                    z[i] = new Keyframe(t, value, speed, speed);
                    px[i] = new Keyframe(t, ViewerLayout.Clamp(pos.x, ViewerLayout.Limit(_fit.x, _viewportWidth, value)));
                    py[i] = new Keyframe(t, ViewerLayout.Clamp(pos.y, ViewerLayout.Limit(_fit.y, _viewportHeight, value)));
                }
                _clip.SetCurve("", typeof(Transform), "localScale.x", new AnimationCurve(z));
                _clip.SetCurve("", typeof(Transform), "localScale.y", new AnimationCurve(z));
                _clip.SetCurve("", typeof(Transform), "localPosition.x", LinearKeys(px));
                _clip.SetCurve("", typeof(Transform), "localPosition.y", LinearKeys(py));
            }
            _animation.AddClip(_clip, "ViewerMotion");
            if (!_animation.Play("ViewerMotion")) throw new InvalidOperationException("閲覧画像のズーム・移動を開始できません");
        }
        private static AnimationCurve LinearKeys(Keyframe[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                float before = i == 0 ? 0 : (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
                float after = i == keys.Length - 1 ? 0 : (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time);
                keys[i].inTangent = before; keys[i].outTangent = after;
            }
            return new AnimationCurve(keys);
        }
        private static AnimationCurve Move(float from, float speed, float end, float duration)
        {
            if (end <= .001f) return AnimationCurve.Linear(0, from, duration, from);
            var keys = end >= duration ? new[] { new Keyframe(0, from), new Keyframe(duration, from + speed * duration) }
                : new[] { new Keyframe(0, from), new Keyframe(end, from + speed * end), new Keyframe(duration, from + speed * end) };
            return LinearKeys(keys);
        }
        internal void StopMotion()
        {
            if (_clip == null) return;
            Vector3 pos = _rect.localPosition; Vector3 scale = _rect.localScale;
            _animation.Stop(); _animation.RemoveClip("ViewerMotion"); Destroy(_clip); _clip = null;
            _rect.localScale = scale;
            _rect.localPosition = new Vector3(ViewerLayout.Clamp(pos.x, ViewerLayout.Limit(_fit.x, _viewportWidth, scale.x)), ViewerLayout.Clamp(pos.y, ViewerLayout.Limit(_fit.y, _viewportHeight, scale.x)), pos.z);
        }
        private void OnDestroy() { StopMotion(); }
    }
}
