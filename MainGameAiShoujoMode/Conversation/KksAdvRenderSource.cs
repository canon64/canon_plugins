using System;
using System.Reflection;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace MainGameAiShoujoMode.Conversation
{
    // Read installed prefab settings without instantiating its camera/controller scripts.
    internal sealed class KksAdvRenderSource : IDisposable
    {
        private readonly AssetBundleData _asset;
        private GameObject _prefab;
        private Material _crossFadeMaterial;
        private float _crossFadeDuration, _lightX, _lightY;
        private Light _light;
        private DepthOfField _depthOfField;
        private Transform _dofTarget;
        private bool _disposed;
        private bool _correctionEnabled;
        internal KksAdvRenderSource(KksConversationRuntimeSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(settings.advCameraBundle) || string.IsNullOrEmpty(settings.advCameraAsset)) throw new InvalidOperationException("KKS camera template is not configured.");
            _asset = new AssetBundleData(settings.advCameraBundle, settings.advCameraAsset);
            try
            {
                _prefab = _asset.GetAsset<GameObject>();
                if (_prefab == null) throw new InvalidOperationException("Installed KKS camera prefab is missing.");
                var fade = _prefab.GetComponent<global::CrossFade>();
                var correction = _prefab.GetComponent<CorrectLightAngle>();
                var effector = _prefab.GetComponent<CameraEffector>();
                if (fade == null || fade.materiaEffect == null || correction == null || effector == null || effector.dof == null)
                    throw new InvalidOperationException("Installed KKS camera prefab lacks CrossFade/light/DOF settings.");
                _crossFadeMaterial = fade.materiaEffect; _crossFadeDuration = fade.time;
                _light = Field<Light>(correction, "dlight"); _lightX = Field<float>(correction, "correctRX"); _lightY = Field<float>(correction, "correctRY");
                _correctionEnabled = correction.enabled;
                if (_light == null) throw new InvalidOperationException("Installed KKS correction light is missing.");
                _depthOfField = effector.dof; _dofTarget = effector.dofTarget;
            }
            catch { Dispose(); throw; }
        }
        internal GameObject CameraPrefab { get { Check(); return _prefab; } }
        internal Material CrossFadeMaterial { get { Check(); return _crossFadeMaterial; } }
        internal float CrossFadeDuration { get { Check(); return _crossFadeDuration; } }
        internal Light CorrectionLight { get { Check(); return _light; } }
        internal float CorrectionX { get { Check(); return _lightX; } }
        internal float CorrectionY { get { Check(); return _lightY; } }
        internal bool CorrectionEnabled { get { Check(); return _correctionEnabled; } }
        internal DepthOfField DepthOfField { get { Check(); return _depthOfField; } }
        internal Transform DepthOfFieldTarget { get { Check(); return _dofTarget; } }
        private static T Field<T>(CorrectLightAngle source, string name)
        {
            var field = typeof(CorrectLightAngle).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(CorrectLightAngle).FullName, name);
            return (T)field.GetValue(source);
        }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(KksAdvRenderSource)); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _prefab = null; _crossFadeMaterial = null; _light = null; _depthOfField = null; _dofTarget = null;
            _asset.UnloadBundle();
        }
    }
}
