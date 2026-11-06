using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace MainGameAiShoujoMode.Conversation
{
    // The owning render session supplies a KKS light and a configured engine rotation constraint.
    // Never attach the original CorrectLightAngle's LateUpdate to the AI-mode camera.
    internal sealed class KksAdvNativeCameraBackend : KksAdvCameraHost.IBackend, IDisposable
    {
        internal interface ILightCorrection { bool Enabled { get; set; } }
        private readonly KksAdvActorRegistry _actors;
        private readonly Transform _light;
        private readonly ILightCorrection _correction;
        private readonly DepthOfField _depthOfField;
        private readonly Quaternion _rotation;
        private readonly bool _correctionEnabled;
        private readonly Transform _focalTransform;
        private readonly KksConversationRuntimeSettings _settings;
        private bool _disposed;
        internal KksAdvNativeCameraBackend(KksAdvActorRegistry actors, Transform light, ILightCorrection correction, DepthOfField depthOfField, KksConversationRuntimeSettings settings)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _light = light != null ? light : throw new ArgumentNullException(nameof(light));
            _correction = correction ?? throw new ArgumentNullException(nameof(correction));
            _depthOfField = depthOfField != null ? depthOfField : throw new ArgumentNullException(nameof(depthOfField));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _rotation = _light.localRotation; _correctionEnabled = _correction.Enabled; _focalTransform = _depthOfField.focalTransform;
        }
        public void SetLightActive(bool active)
        {
            Check(); _correction.Enabled = active;
        }
        public void SetLightAngle(bool local, float x, float y)
        {
            Check(); if (_light == null) throw new InvalidOperationException("KKS ADV light was destroyed.");
            var rotation = Quaternion.Euler(x, y, _settings.zero);
            if (local) _light.localRotation = rotation; else _light.rotation = rotation;
        }
        public void SetDepthOfFieldTarget(int actor)
        {
            Check(); if (_depthOfField == null) throw new InvalidOperationException("KKS ADV depth of field was destroyed.");
            var target = _actors.Resolve(actor).Data.chaCtrl.GetReferenceInfo(ChaReference.RefObjKey.HeadParent);
            if (target == null) throw new InvalidOperationException("KKS ADV DOF actor has no HeadParent reference: " + actor);
            _depthOfField.focalTransform = target.transform;
        }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(KksAdvNativeCameraBackend)); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            try { _correction.Enabled = false; } catch (Exception exception) { errors.Add(exception); }
            try { if (_light != null) _light.localRotation = _rotation; } catch (Exception exception) { errors.Add(exception); }
            try { if (_depthOfField != null) _depthOfField.focalTransform = _focalTransform; } catch (Exception exception) { errors.Add(exception); }
            try { _correction.Enabled = _correctionEnabled; } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS ADV camera settings restoration failed.", errors);
        }
    }
}
