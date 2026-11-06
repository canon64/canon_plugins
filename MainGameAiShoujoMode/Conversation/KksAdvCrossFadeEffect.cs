using System;
using DG.Tweening;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    // Enabled only for a requested frame capture or an active transition. No Update/LateUpdate polling.
    public sealed class KksAdvCrossFadeEffect : MonoBehaviour, KksAdvCrossFadePlayback.IDevice
    {
        private sealed class Lease : IDisposable
        {
            internal Action Cancel;
            public void Dispose() { var cancel = Cancel; Cancel = null; cancel?.Invoke(); }
        }
        private Material _material;
        private RenderTexture _frame;
        private KksConversationRuntimeSettings _settings;
        private float _defaultDuration, _duration, _timer, _alpha;
        private Tween _timeline;
        private object _generation;
        private Action _completed;
        private Action<Exception> _failed;
        private Lease _capture;
        private Action _captured;
        private Action<Exception> _captureFailed;
        private bool _hasFrame, _animating, _disposed;
        private int _fadeTex, _alphaKey;
        public bool HasFrame => !_disposed && _hasFrame && _frame != null;
        public float DefaultDuration => _defaultDuration;
        private void Awake() { enabled = false; }
        internal void Initialize(Material nativeMaterial, float nativeDefaultDuration, KksConversationRuntimeSettings settings)
        {
            if (_settings != null || _disposed) throw new InvalidOperationException("CrossFade renderer is already initialized/disposed.");
            if (nativeMaterial == null || nativeMaterial.shader == null || !nativeMaterial.shader.isSupported
                || !nativeMaterial.HasProperty("_FadeTex") || !nativeMaterial.HasProperty("_Alpha"))
                throw new InvalidOperationException("Installed KKS CrossFade material is missing or incompatible.");
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _material = new Material(nativeMaterial); _defaultDuration = nativeDefaultDuration;
            _fadeTex = Shader.PropertyToID("_FadeTex"); _alphaKey = Shader.PropertyToID("_Alpha");
        }
        internal IDisposable CaptureBeforeChanges(Action completed, Action<Exception> failed)
        {
            Check();
            if (completed == null || failed == null) throw new ArgumentNullException(completed == null ? nameof(completed) : nameof(failed));
            var camera = GetComponent<Camera>();
            if (camera == null || !camera.isActiveAndEnabled) throw new InvalidOperationException("CrossFade capture camera is missing or inactive.");
            if (_animating) { completed(); return null; } // Native keeps the same frozen frame during a running crossfade.
            if (_capture != null) throw new InvalidOperationException("A CrossFade frame capture is already pending.");
            var lease = new Lease(); _capture = lease; _captured = completed; _captureFailed = failed;
            lease.Cancel = () =>
            {
                if (!ReferenceEquals(_capture, lease)) return;
                _capture = null; _captured = null; _captureFailed = null; enabled = _animating;
            };
            enabled = true; return lease;
        }
        public bool IsZero(float duration) => Mathf.Approximately(duration, _settings.zero);
        public IDisposable Animate(float duration, Action completed, Action<Exception> failed)
        {
            Check(); if (!HasFrame || _capture != null) throw new InvalidOperationException("CrossFade frame is not ready.");
            StopAnimation();
            var generation = new object(); _generation = generation; _duration = duration;
            _timer = _settings.zero; _alpha = _settings.zero; _animating = true;
            _completed = completed; _failed = failed; enabled = true;
            if (float.IsNaN(duration)) _alpha = float.NaN;
            else if (!float.IsPositiveInfinity(duration)) Segment(generation);
            // NaN/+Infinity have native non-completing behavior and remain explicitly cancelable.
            return new Lease { Cancel = () => { if (ReferenceEquals(_generation, generation)) StopAnimation(); } };
        }
        private void Segment(object generation)
        {
            var remaining = Mathf.Max(Mathf.Epsilon, _duration - _timer);
            var value = (float)_settings.zero;
            _timeline = DOTween.To(() => value, next => value = next, _settings.one, remaining).SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    if (!ReferenceEquals(_generation, generation)) return;
                    // Preserve native CrossFade's smooth scaled clock rather than a deltaTime approximation.
                    _timer = Mathf.Clamp(_timer + Time.smoothDeltaTime, _settings.zero, _duration);
                    _alpha = _timer >= _duration ? _settings.one : _timer / _duration;
                    if (_timer >= _duration) Finish(generation);
                }).OnComplete(() =>
                {
                    if (!ReferenceEquals(_generation, generation)) return;
                    _timeline = null;
                    // Smoothing may leave a small remainder after this finite tween segment.
                    Segment(generation);
                });
        }
        private void Finish(object generation)
        {
            if (!ReferenceEquals(_generation, generation)) return;
            var completed = _completed; StopAnimation(); _alpha = _settings.one; completed?.Invoke();
        }
        private void StopAnimation()
        {
            _generation = null; _completed = null; _failed = null; _animating = false;
            var timeline = _timeline; _timeline = null; timeline?.Kill(false);
            enabled = _capture != null;
        }
        public void ForceEnd() { Check(); StopAnimation(); _alpha = _settings.one; }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var previous = RenderTexture.active;
            var originalFailure = _captureFailed ?? _failed;
            try
            {
                if (_capture != null)
                {
                    if (_frame == null || _frame.width != source.width || _frame.height != source.height)
                    {
                        ReleaseFrame();
                        _frame = new RenderTexture(source.width, source.height, _settings.crossFadeDepthBits, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                        if (!_frame.Create()) throw new InvalidOperationException("KKS CrossFade frame texture could not be allocated.");
                    }
                    Graphics.Blit(source, _frame); _hasFrame = true;
                    var completed = _captured; _capture.Cancel = null; _capture = null; _captured = null; _captureFailed = null;
                    Graphics.Blit(source, destination); enabled = _animating; completed?.Invoke();
                }
                else if (_animating && HasFrame)
                {
                    _material.SetTexture(_fadeTex, _frame); _material.SetFloat(_alphaKey, _alpha);
                    Graphics.Blit(source, destination, _material);
                }
                else { Graphics.Blit(source, destination); enabled = false; }
            }
            catch (Exception exception)
            {
                var failed = _captureFailed ?? _failed ?? originalFailure;
                _capture?.Dispose(); StopAnimation();
                Graphics.Blit(source, destination); failed?.Invoke(exception);
            }
            finally { RenderTexture.active = previous; }
        }
        private void ReleaseFrame()
        {
            _hasFrame = false;
            if (_frame != null) { _frame.Release(); UnityEngine.Object.Destroy(_frame); _frame = null; }
        }
        private void Check()
        {
            if (_disposed || _settings == null || _material == null) throw new ObjectDisposedException(nameof(KksAdvCrossFadeEffect));
        }
        public void Dispose() { Cleanup(); if (this != null) UnityEngine.Object.Destroy(this); }
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (_disposed) return; _disposed = true;
            _capture?.Dispose(); StopAnimation(); ReleaseFrame();
            if (_material != null) UnityEngine.Object.Destroy(_material); _material = null;
        }
    }
}
