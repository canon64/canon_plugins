using System;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace AstralMode
{
    // Follow SimpleAfterimage's per-eye OnRenderImage capture path. The HMD
    // camera's view, projection and pose are never overwritten by this effect.
    internal sealed class VrTransitionRenderer : MonoBehaviour
    {
        private Camera _camera, _captureCamera;
        private GameObject _captureRoot;
        private VrTransitionFade _captureFade;
        private RenderTexture _texture;
        private Material _fadeMaterial;
        private bool _active, _renderingCapture;
        private float _targetFov;
        private Color _fade = new Color(1, 1, 1, 0);
        private Action<string, Exception> _failure;
        internal float OriginalFov { get; private set; }
        internal Camera TargetCamera => _camera;

        internal void Initialize(Camera camera, Shader shader, Action<string, Exception> failure)
        {
            _camera = camera;
            _failure = failure;
            // This is only the scalar animation start value, never an eye pose.
            OriginalFov = VrWarpProjection.VerticalFov(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
            _targetFov = OriginalFov;
            _fadeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _active = true;
        }

        internal void SetWhite(float alpha) { _fade.a = Mathf.Clamp01(alpha); }
        internal void SetFov(float target)
        {
            if (!_active) return;
            if (!SessionGate.Allowed) { Release(); return; }
            _targetFov = target;
        }

        // Native source rendering already contains the fade when no extra
        // capture is necessary. The scratch camera uses the identical draw.
        private void OnPostRender() => DrawWhite();
        internal void DrawWhite()
        {
            if (!_active) return;
            if (!SessionGate.Allowed) { Release(); return; }
            if (_fade.a <= 0 || _fadeMaterial == null) return;
            try
            {
                _fadeMaterial.SetColor("fadeColor", _fade);
                if (!_fadeMaterial.SetPass(0)) throw new InvalidOperationException("VR fade shader pass unavailable.");
                GL.Begin(GL.QUADS);
                try
                {
                    GL.Vertex3(-1, -1, 0); GL.Vertex3(1, -1, 0);
                    GL.Vertex3(1, 1, 0); GL.Vertex3(-1, 1, 0);
                }
                finally { GL.End(); }
            }
            catch (Exception ex) { Fail("VR白飛びの描画", ex); }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!_active || !SessionGate.Allowed || Mathf.Abs(_targetFov - OriginalFov) < 0.0001f)
            {
                Graphics.Blit(source, destination);
                return;
            }
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Read the currently rendered eye here, exactly as Afterimage
                // does. Outside this callback the runtime can return one eye's
                // values for both GetStereo* requests.
                Camera.MonoOrStereoscopicEye activeEye = _camera.stereoActiveEye;
                Matrix4x4 view, projection;
                if (activeEye == Camera.MonoOrStereoscopicEye.Left || activeEye == Camera.MonoOrStereoscopicEye.Right)
                {
                    var eye = activeEye == Camera.MonoOrStereoscopicEye.Right
                        ? Camera.StereoscopicEye.Right : Camera.StereoscopicEye.Left;
                    view = _camera.GetStereoViewMatrix(eye);
                    projection = _camera.GetStereoProjectionMatrix(eye);
                }
                else
                {
                    view = _camera.worldToCameraMatrix;
                    projection = _camera.projectionMatrix;
                }
                Vector3 headForward = view.MultiplyVector(_camera.transform.forward);
                Matrix4x4 wide = VrWarpProjection.ForEye(projection, headForward, _targetFov);
                EnsureCapture(source);
                _captureCamera.CopyFrom(_camera);
                _captureCamera.enabled = false;
                _captureCamera.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);
                _captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
                _captureCamera.rect = new Rect(0, 0, 1, 1);
                _captureCamera.allowMSAA = false;
                _captureCamera.worldToCameraMatrix = view;
                _captureCamera.projectionMatrix = wide;
                _captureCamera.ResetCullingMatrix();
                _captureCamera.targetTexture = _texture;
                _renderingCapture = true;
                try { _captureCamera.Render(); }
                finally { _renderingCapture = false; }
                if (!_active)
                {
                    Release();
                    Graphics.Blit(source, destination);
                    return;
                }
                Graphics.Blit(_texture, destination);
            }
            catch (Exception ex)
            {
                Fail("VR各眼の画角演出", ex);
                Graphics.Blit(source, destination);
            }
            finally
            {
                if (_captureCamera != null) _captureCamera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        private void EnsureCapture(RenderTexture source)
        {
            if (_captureRoot == null)
            {
                _captureRoot = new GameObject("AstralMode_VrEyeCapture") { hideFlags = HideFlags.HideAndDontSave };
                _captureCamera = _captureRoot.AddComponent<Camera>();
                _captureCamera.enabled = false;
                _captureFade = _captureRoot.AddComponent<VrTransitionFade>();
                _captureFade.Owner = this;
            }
            if (_texture != null && _texture.width == source.width && _texture.height == source.height
                && _texture.format == source.format) return;
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            _texture = new RenderTexture(source.width, source.height, 24, source.format)
                { hideFlags = HideFlags.HideAndDontSave };
            _texture.Create();
        }

        private void Fail(string operation, Exception ex)
        {
            _active = false;
            _fade.a = 0;
            // Never destroy a render target while its camera is drawing it.
            // OnRenderImage releases it immediately after that render unwinds.
            if (!_renderingCapture) Release();
            _failure?.Invoke(operation, ex);
        }

        internal void Release()
        {
            _active = false;
            _fade.a = 0;
            if (_captureFade != null) _captureFade.Owner = null;
            if (_captureCamera != null) { _captureCamera.enabled = false; _captureCamera.targetTexture = null; }
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
            if (_captureRoot != null) Destroy(_captureRoot);
            _captureRoot = null; _captureCamera = null; _captureFade = null;
            if (_fadeMaterial != null) Destroy(_fadeMaterial);
            _fadeMaterial = null;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }

    internal sealed class VrTransitionFade : MonoBehaviour
    {
        internal VrTransitionRenderer Owner;
        private void OnPostRender() => Owner?.DrawWhite();
    }
}
