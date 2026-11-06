using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    // A single finite native clip rotates the whole visible page and interpolates its layout.
    internal sealed class PhoneOrientationMotion : MonoBehaviour
    {
        private Animation _animation;
        private AnimationClip _clip;
        private sealed class State
        {
            internal RectTransform Rect;
            internal Vector3 Position;
            internal Vector2 Size;
            internal State(RectTransform rect) { Rect = rect; Position = rect.localPosition; Size = rect.sizeDelta; }
            internal void Restore() { if (Rect != null) { Rect.localPosition = Position; Rect.sizeDelta = Size; } }
        }
        internal void Transition(int turns, Action layout, bool animate = true)
        {
            Cancel();
            var before = new List<State>();
            foreach (var rect in GetComponentsInChildren<RectTransform>(true)) before.Add(new State(rect));
            float start = transform.localEulerAngles.z;
            layout();
            float target = PhoneOrientationPlan.TargetAngle(start, turns);
            if (!animate || !gameObject.activeInHierarchy)
            { transform.localRotation = Quaternion.Euler(0, 0, target); return; }
            if (_animation == null)
            { _animation = gameObject.AddComponent<Animation>(); _animation.playAutomatically = false; _animation.cullingType = AnimationCullingType.AlwaysAnimate; }
            _clip = new AnimationClip { legacy = true, wrapMode = WrapMode.ClampForever };
            var z = new Keyframe[17]; var w = new Keyframe[17];
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f, eased = t * t * (3 - 2 * t);
                float half = (start + (target - start) * eased) * Mathf.Deg2Rad * .5f;
                z[i] = new Keyframe(t * PhoneOrientationPlan.Duration, Mathf.Sin(half));
                w[i] = new Keyframe(t * PhoneOrientationPlan.Duration, Mathf.Cos(half));
            }
            _clip.SetCurve("", typeof(Transform), "localRotation.x", AnimationCurve.Linear(0, 0, PhoneOrientationPlan.Duration, 0));
            _clip.SetCurve("", typeof(Transform), "localRotation.y", AnimationCurve.Linear(0, 0, PhoneOrientationPlan.Duration, 0));
            _clip.SetCurve("", typeof(Transform), "localRotation.z", new AnimationCurve(z));
            _clip.SetCurve("", typeof(Transform), "localRotation.w", new AnimationCurve(w));
            foreach (var old in before)
            {
                if (old.Rect == null) continue;
                var after = new State(old.Rect); string path = Path(old.Rect);
                if ((old.Position - after.Position).sqrMagnitude > .000001f)
                {
                    PositionCurve(path, "localPosition.x", old.Position.x, after.Position.x);
                    PositionCurve(path, "localPosition.y", old.Position.y, after.Position.y);
                    PositionCurve(path, "localPosition.z", old.Position.z, after.Position.z);
                }
                if ((old.Size - after.Size).sqrMagnitude > .000001f)
                {
                    _clip.SetCurve(path, typeof(RectTransform), "m_SizeDelta.x", AnimationCurve.EaseInOut(0, old.Size.x, PhoneOrientationPlan.Duration, after.Size.x));
                    _clip.SetCurve(path, typeof(RectTransform), "m_SizeDelta.y", AnimationCurve.EaseInOut(0, old.Size.y, PhoneOrientationPlan.Duration, after.Size.y));
                }
                old.Restore();
            }
            _animation.AddClip(_clip, "PhoneOrientation");
            if (!_animation.Play("PhoneOrientation")) throw new InvalidOperationException("画面の縦横切替を開始できません");
        }
        private void PositionCurve(string path, string property, float from, float to)
            => _clip.SetCurve(path, typeof(Transform), property, AnimationCurve.EaseInOut(0, from, PhoneOrientationPlan.Duration, to));
        private string Path(Transform target)
        {
            string result = target.name;
            for (var parent = target.parent; parent != null && parent != transform; parent = parent.parent) result = parent.name + "/" + result;
            return result;
        }
        internal void Cancel()
        {
            if (_clip == null) return;
            var rotation = transform.localRotation;
            var states = new List<State>();
            foreach (var rect in GetComponentsInChildren<RectTransform>(true)) states.Add(new State(rect));
            _animation.Stop(); _animation.RemoveClip("PhoneOrientation"); Destroy(_clip); _clip = null;
            transform.localRotation = rotation;
            foreach (var state in states) state.Restore();
        }
        private void OnDestroy() { Cancel(); }
    }
}
