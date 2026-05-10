using UnityEngine;

namespace MainGameGlow
{
    public sealed partial class Plugin
    {
        private sealed class OverlayDrawer : MonoBehaviour
        {
            internal Plugin Owner;
            private void OnPostRender()
            {
                Owner?.DrawOverlay(GetComponent<Camera>());
            }
        }

        private void SyncOverlayDrawer(Camera srcCamera)
        {
            if (!_cfgEnabled.Value || srcCamera == null)
            {
                if (_overlayDrawer != null)
                {
                    Destroy(_overlayDrawer);
                    _overlayDrawer = null;
                }

                _lastSourceCamera = null;
                return;
            }

            if (_lastSourceCamera != srcCamera)
            {
                if (_overlayDrawer != null)
                {
                    Destroy(_overlayDrawer);
                    _overlayDrawer = null;
                }

                _overlayDrawer = srcCamera.gameObject.AddComponent<OverlayDrawer>();
                _overlayDrawer.Owner = this;
                _lastSourceCamera = srcCamera;
            }
        }

        internal void DrawOverlay(Camera cam)
        {
            if (!_cfgEnabled.Value || _captureRt == null)
                return;

            float r = Mathf.Clamp01(_cfgTintR.Value);
            float g = Mathf.Clamp01(_cfgTintG.Value);
            float b = Mathf.Clamp01(_cfgTintB.Value);
            float a = Mathf.Clamp01(_cfgTintA.Value) * Mathf.Clamp01(_cfgOverlayAlpha.Value);

            if (a <= 0.0001f)
                return;

            float w = cam != null ? cam.pixelWidth : Screen.width;
            float h = cam != null ? cam.pixelHeight : Screen.height;
            Rect rect = new Rect(0, 0, w, h);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, h, 0f);
            try
            {
                Graphics.DrawTexture(
                    rect,
                    _captureRt,
                    new Rect(0f, 0f, 1f, 1f),
                    0, 0, 0, 0,
                    new Color(r, g, b, a));
            }
            finally
            {
                GL.PopMatrix();
            }
        }
    }
}
