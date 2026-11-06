using System;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    // AQUAS's baked water materials expect a planar texture and projected source-camera matrix.
    internal sealed class AiIslandWaterReflection : MonoBehaviour
    {
        internal int Resolution = 256;
        internal Camera SourceCamera;
        internal bool Realtime;
        internal float DrawDistance = 80f;
        private Renderer _renderer;
        private Camera _reflection;
        private RenderTexture _texture;
        private MaterialPropertyBlock _properties;
        private static bool _rendering;
        private int _frame = -1;
        private Camera _source;
        private bool _failed;
        private Texture2D _fallback;
        private float _nextRefresh;

        private void OnWillRenderObject()
        {
            Camera source = Camera.current;
            if (_failed || _rendering || source == null || source != SourceCamera || !enabled || source == _reflection ||
                source.cameraType == CameraType.Reflection || !gameObject.activeInHierarchy) return;
            if (_frame == Time.frameCount && _source == source) return;
            _frame = Time.frameCount; _source = source;
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();
            if (!Realtime)
            {
                if (_reflection != null) { Destroy(_reflection.gameObject); _reflection = null; }
                if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
                if (_fallback == null)
                {
                    _fallback = new Texture2D(2, 32, TextureFormat.RGB24, false) { name = "AI island lightweight water sky", wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < 32; y++)
                    {
                        Color color = RenderSettings.ambientSkyColor;
                        _fallback.SetPixel(0, y, color); _fallback.SetPixel(1, y, color);
                    }
                    _fallback.Apply(false, true);
                }
                ApplyTexture(source, _fallback);
                return;
            }
            if (_texture != null && Time.unscaledTime < _nextRefresh) { ApplyTexture(source, _texture); return; }
            bool oldCulling = GL.invertCulling;
            int oldLights = QualitySettings.pixelLightCount;
            Vector3 oldPosition = Vector3.zero;
            try
            {
                if (_reflection == null)
                {
                    var cameraObject = new GameObject("__AI_Island_WaterReflection") { hideFlags = HideFlags.HideAndDontSave };
                    _reflection = cameraObject.AddComponent<Camera>();
                    _reflection.enabled = false;
                    int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Resolution), 128, 1024);
                    _texture = new RenderTexture(size, size, 16) { name = "AI island water reflection", hideFlags = HideFlags.DontSave };
                }
                _rendering = true;
                QualitySettings.pixelLightCount = 0;
                _reflection.CopyFrom(source);
                _reflection.enabled = false;
                _reflection.useOcclusionCulling = false;
                _reflection.farClipPlane = Mathf.Min(source.farClipPlane, DrawDistance);
                _reflection.ResetProjectionMatrix();
                _reflection.targetTexture = _texture;
                // Island geometry / terrain / default objects, without characters, water and recursive reflection surfaces.
                _reflection.cullingMask = source.cullingMask & ((1 << 0) | (1 << 11));
                Vector3 normal = transform.up;
                Vector3 point = transform.position;
                float distance = -Vector3.Dot(normal, point) - .05f;
                Vector4 plane = new Vector4(normal.x, normal.y, normal.z, distance);
                Matrix4x4 mirror = Matrix4x4.identity;
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4; col++) mirror[row, col] -= 2f * (row < 3 ? normal[row] : 0f) * plane[col];
                oldPosition = source.transform.position;
                _reflection.transform.position = mirror.MultiplyPoint(oldPosition);
                _reflection.transform.rotation = source.transform.rotation;
                _reflection.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
                Vector3 clipPosition = _reflection.worldToCameraMatrix.MultiplyPoint(point + normal * .05f);
                Vector3 clipNormal = _reflection.worldToCameraMatrix.MultiplyVector(normal).normalized;
                Vector4 clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPosition, clipNormal));
                _reflection.projectionMatrix = _reflection.CalculateObliqueMatrix(clipPlane);
                GL.invertCulling = !oldCulling;
                _reflection.Render();
                _nextRefresh = Time.unscaledTime + .2f;
                ApplyTexture(source, _texture);
            }
            catch (Exception ex)
            {
                _failed = true;
                Debug.LogWarning("AI island planar reflection failed: " + ex);
            }
            finally
            {
                GL.invertCulling = oldCulling;
                QualitySettings.pixelLightCount = oldLights;
                _rendering = false;
            }
        }
        private void ApplyTexture(Camera source, Texture texture)
        {
            _renderer.GetPropertyBlock(_properties);
            _properties.SetTexture("_ReflectionTex", texture);
            Matrix4x4 bias = Matrix4x4.TRS(new Vector3(.5f,.5f,.5f), Quaternion.identity, new Vector3(.5f,.5f,.5f));
            Vector3 scale = transform.lossyScale;
            Matrix4x4 local = transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(1f/scale.x, 1f/scale.y, 1f/scale.z));
            _properties.SetMatrix("_ProjMatrix", bias * source.projectionMatrix * source.worldToCameraMatrix * local);
            _renderer.SetPropertyBlock(_properties);
        }
        private void OnDestroy()
        {
            if (_reflection != null) Destroy(_reflection.gameObject);
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_fallback != null) Destroy(_fallback);
        }
    }
}
