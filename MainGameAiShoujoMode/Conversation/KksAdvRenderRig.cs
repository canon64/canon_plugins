using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityStandardAssets.ImageEffects;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvRenderRig : KksAdvNativeCameraBackend.ILightCorrection, KksAdvCrossFadeHost.IBackend, IDisposable
    {
        private readonly Camera _camera;
        private readonly KksConversationRuntimeSettings _settings;
        private readonly Func<IReadOnlyList<ChaControl>> _participants;
        private readonly DepthTextureMode _depthMode;
        private readonly List<KeyValuePair<DepthOfField, bool>> _borrowedDepth = new List<KeyValuePair<DepthOfField, bool>>();
        private readonly Dictionary<Transform, Transform> _transforms = new Dictionary<Transform, Transform>();
        private KksAdvRenderSource _source;
        private GameObject _root;
        private Light _light;
        private Transform _proxy, _defaultDepthTarget;
        private Quaternion _restRotation;
        private RotationConstraint _constraint;
        private DepthOfField _depth;
        private KksAdvCrossFadeEffect _effect;
        private KksAdvCrossFadePlayback _crossFade;
        private Func<IReadOnlyList<KksAdvTape.Entry>, Action, Action<Exception>, IDisposable> _prepare;
        private bool _correctionEnabled, _disposed;
        internal KksAdvNativeCameraBackend Commands { get; private set; }
        internal KksAdvRenderRig(Camera camera, KksAdvActorRegistry actors, Func<IReadOnlyList<ChaControl>> participants,
            bool depthOfFieldEnabled, KksConversationRuntimeSettings settings)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _participants = participants ?? throw new ArgumentNullException(nameof(participants)); _depthMode = camera.depthTextureMode;
            if (camera.GetComponent<KksAdvCrossFadeEffect>() != null) throw new InvalidOperationException("AI camera already has an owned conversation renderer.");
            try
            {
                _source = new KksAdvRenderSource(settings);
                _root = new GameObject("AI mode KKS conversation render"); _root.transform.SetParent(camera.transform, false);
                _transforms.Add(_source.CameraPrefab.transform, _root.transform);
                foreach (var existing in camera.GetComponents<DepthOfField>())
                { _borrowedDepth.Add(new KeyValuePair<DepthOfField, bool>(existing, existing.enabled)); existing.enabled = false; }
                var order = _source.CameraPrefab.GetComponents<Component>();
                var fadeIndex = Array.FindIndex(order, component => component is global::CrossFade);
                var depthIndex = Array.FindIndex(order, component => component == _source.DepthOfField);
                if (fadeIndex < settings.zero || depthIndex < settings.zero) throw new NotSupportedException("KKS effect order is not on the source camera root.");
                if (fadeIndex < depthIndex) { CreateCrossFade(); CreateDepth(depthOfFieldEnabled); }
                else { CreateDepth(depthOfFieldEnabled); CreateCrossFade(); }
                CreateLight(); _correctionEnabled = _source.CorrectionEnabled; RefreshLightTarget();
                _constraint.enabled = _correctionEnabled;
                Commands = new KksAdvNativeCameraBackend(actors, _light.transform, this, _depth, settings);
                _prepare = KksAdvFramePreparation.Create(_effect.CaptureBeforeChanges);
            }
            catch (Exception exception)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(exception, cleanup); }
                throw;
            }
        }
        private void CreateCrossFade()
        {
            _effect = _camera.gameObject.AddComponent<KksAdvCrossFadeEffect>();
            _effect.Initialize(_source.CrossFadeMaterial, _source.CrossFadeDuration, _settings);
            _crossFade = new KksAdvCrossFadePlayback(_effect, _settings.zero);
        }
        private void CreateDepth(bool enabled)
        {
            _depth = _camera.gameObject.AddComponent<DepthOfField>(); _depth.enabled = false;
            KksAdvComponentPreset.Copy(_source.DepthOfField, _depth);
            _depth.focalTransform = _source.DepthOfField.focalTransform == null ? null : CopyTransform(_source.DepthOfField.focalTransform);
            _defaultDepthTarget = _source.DepthOfFieldTarget == null ? null : CopyTransform(_source.DepthOfFieldTarget);
            if (enabled)
            {
                _depth.enabled = true;
                if (!_depth.CheckResources()) throw new InvalidOperationException("KKS depth-of-field shaders are unsupported on this camera.");
            }
        }
        private Transform CopyTransform(Transform source)
        {
            if (_transforms.TryGetValue(source, out var existing)) return existing;
            if (source.parent == null) throw new NotSupportedException("KKS render point lies outside the source camera hierarchy.");
            var parent = CopyTransform(source.parent);
            var node = new GameObject(source.name).transform; node.SetParent(parent, false);
            node.localPosition = source.localPosition; node.localRotation = source.localRotation; node.localScale = source.localScale;
            _transforms.Add(source, node); return node;
        }
        private void CreateLight()
        {
            var source = _source.CorrectionLight;
            foreach (var component in source.GetComponentsInChildren<Component>(true))
                if (!(component is Transform) && !(component is Light)) throw new NotSupportedException("KKS light has an unadapted component: " + (component == null ? "missing script" : component.GetType().FullName));
            var parent = CopyTransform(source.transform.parent);
            _light = UnityEngine.Object.Instantiate(source, parent, false); _light.name = source.name;
            _restRotation = _light.transform.localRotation;
            _proxy = new GameObject("KKS light rotation source").transform; _proxy.SetParent(parent, false);
            _constraint = _light.gameObject.AddComponent<RotationConstraint>();
            _constraint.constraintActive = false; _constraint.locked = false; _constraint.weight = _settings.one;
            _constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z; _constraint.rotationOffset = Vector3.zero;
            _constraint.rotationAtRest = _restRotation.eulerAngles;
            _constraint.AddSource(new ConstraintSource { sourceTransform = _proxy, weight = _settings.one });
            _constraint.locked = true; _constraint.constraintActive = true;
        }
        internal void RefreshLightTarget()
        {
            Check();
            if (_constraint == null || _light == null) throw new InvalidOperationException("Conversation light was destroyed.");
            if (_proxy == null)
            {
                _proxy = new GameObject("KKS light rotation source").transform;
                _constraint.SetSource(_settings.zero, new ConstraintSource { sourceTransform = _proxy, weight = _settings.one });
            }
            var visible = (_participants() ?? throw new InvalidOperationException("Conversation participants are unavailable.")).Where(actor => actor != null && actor.visibleAll).ToArray();
            var actor = visible.Length == _settings.one ? visible[_settings.zero] : null;
            var reference = actor != null && actor.hiPoly ? actor.GetReferenceInfo(ChaReference.RefObjKey.HeadParent) : null;
            _proxy.SetParent(reference != null ? reference.transform : _light.transform.parent, false);
            _proxy.localPosition = Vector3.zero; _proxy.localScale = Vector3.one;
            _proxy.localRotation = reference != null ? Quaternion.Euler(_source.CorrectionX, _source.CorrectionY, _settings.zero) : _restRotation;
        }
        bool KksAdvNativeCameraBackend.ILightCorrection.Enabled
        {
            get { Check(); return _correctionEnabled; }
            set { Check(); if (_constraint == null) throw new InvalidOperationException("Conversation light constraint is missing."); _correctionEnabled = value; _constraint.enabled = value; }
        }
        internal IDisposable PrepareBatch(IReadOnlyList<KksAdvTape.Entry> entries, Action ready, Action<Exception> failed)
        { Check(); return _prepare(entries, ready, failed); }
        internal KksAdvCrossFadeHost.IOperation StartCrossFade(float duration, Action completed, Action<Exception> failed)
        { Check(); return _crossFade.Start(duration, completed, failed); }
        KksAdvCrossFadeHost.IOperation KksAdvCrossFadeHost.IBackend.Start(float duration, Action completed, Action<Exception> failed)
            => StartCrossFade(duration, completed, failed);
        internal void StartImplicitCrossFade(float duration, Action<Exception> failed) { Check(); _crossFade.StartImplicit(duration, failed); }
        internal void ResetDepthTarget() { Check(); if (_depth != null) _depth.focalTransform = _defaultDepthTarget; }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(KksAdvRenderRig)); }
        public void Dispose()
        {
            if (_disposed) return;
            var errors = new List<Exception>();
            try { Commands?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            _disposed = true;
            try { if (_constraint != null) _constraint.enabled = false; } catch (Exception exception) { errors.Add(exception); }
            try { if (_crossFade != null) _crossFade.Dispose(); else if (_effect != null) _effect.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            try { if (_depth != null) { _depth.enabled = false; UnityEngine.Object.Destroy(_depth); } } catch (Exception exception) { errors.Add(exception); }
            try { if (_proxy != null) UnityEngine.Object.Destroy(_proxy.gameObject); } catch (Exception exception) { errors.Add(exception); }
            try { if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); } } catch (Exception exception) { errors.Add(exception); }
            foreach (var existing in _borrowedDepth) try { if (existing.Key != null) existing.Key.enabled = existing.Value; } catch (Exception exception) { errors.Add(exception); }
            try { if (_camera != null) _camera.depthTextureMode = _depthMode; } catch (Exception exception) { errors.Add(exception); }
            try { _source?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS conversation render cleanup failed.", errors);
        }
    }
}
