using UnityEngine;

namespace SimpleAfterimage
{
    public sealed partial class Plugin
    {
        // 描画用 MonoBehaviour
        private sealed class OverlayDrawer : MonoBehaviour
        {
            internal Plugin Owner;
            private void OnPostRender() { Owner?.DrawOnPostRender(GetComponent<Camera>()); }
        }

        private void AgeThenBuildDrawList()
        {
            int fadeFrames = Mathf.Max(1, _cfgFadeFrames.Value);
            float alphaScale = Mathf.Clamp01(_cfgAlphaScale.Value);
            float tintA = Mathf.Clamp01(_cfgTintA.Value);

            for (int i = 0; i < _slots.Length; i++)
                if (_life[i] > 0) _life[i]--;

            _drawCount = 0;
            int newest = (_writeIndex - 1 + _slots.Length) % _slots.Length;
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = (newest - i + _slots.Length) % _slots.Length;
                if (_life[slot] <= 0) continue;

                float t = (float)_life[slot] / fadeFrames;
                t = ApplyCurve(t, _cfgFadeCurve.Value);
                float alpha = tintA * alphaScale * t;
                if (alpha <= 0.0001f) continue;

                _drawSlots[_drawCount] = _slots[slot];
                _drawAlpha[_drawCount] = alpha;
                _drawCount++;
            }
        }

        private static float ApplyCurve(float t, string curve)
        {
            switch (curve)
            {
                case "EaseIn":  return t * t;
                case "EaseOut": return Mathf.Sqrt(t);
                case "Square":  return t * t * t;
                default:        return t;
            }
        }

        internal void DrawOnPostRender(Camera cam)
        {
            if (!_cfgEnabled.Value || _drawCount == 0) return;
            float r = Mathf.Clamp01(_cfgTintR.Value);
            float g = Mathf.Clamp01(_cfgTintG.Value);
            float b = Mathf.Clamp01(_cfgTintB.Value);
            float w = (cam != null) ? cam.pixelWidth  : Screen.width;
            float h = (cam != null) ? cam.pixelHeight : Screen.height;
            Rect rect = new Rect(0, 0, w, h);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, h, 0f);
            try
            {
                for (int i = 0; i < _drawCount; i++)
                {
                    if (_drawSlots[i] == null) continue;
                    float alpha = _drawAlpha[i];
                    if (alpha <= 0.0001f) continue;
                    Graphics.DrawTexture(rect, _drawSlots[i], new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0,
                        new Color(r, g, b, alpha));
                }
            }
            finally
            {
                GL.PopMatrix();
            }
        }
    }
}
