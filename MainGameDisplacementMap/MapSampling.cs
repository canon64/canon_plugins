using System;
using UnityEngine;

namespace MainGameDisplacementMap
{
    internal static class MapSampling
    {
        internal const float Tolerance = 0.00002f;
        internal struct Footprint
        {
            internal int X0, X1, Y0, Y1;
            internal float Fx, Fy;
        }

        internal static Footprint Locate(float u, float v, int width, int height, int mode)
        {
            float px = Mathf.Clamp01(u) * (mode == 1 ? width - 1 : width) - (mode == 0 ? .5f : 0f);
            float py = Mathf.Clamp01(v) * (mode == 1 ? height - 1 : height) - (mode == 0 ? .5f : 0f);
            int x = Mathf.FloorToInt(px), y = Mathf.FloorToInt(py);
            return new Footprint {
                X0 = Mathf.Clamp(x, 0, width - 1), X1 = Mathf.Clamp(x + 1, 0, width - 1),
                Y0 = Mathf.Clamp(y, 0, height - 1), Y1 = Mathf.Clamp(y + 1, 0, height - 1), Fx = px - x, Fy = py - y
            };
        }

        internal static Vector2 Interpolate(Color32 a, Color32 b, Color32 c, Color32 d, float fx, float fy)
        {
            float r = Mathf.Lerp(Mathf.Lerp(a.r, b.r, fx), Mathf.Lerp(c.r, d.r, fx), fy) / 255f;
            float g = Mathf.Lerp(Mathf.Lerp(a.g, b.g, fx), Mathf.Lerp(c.g, d.g, fx), fy) / 255f;
            return new Vector2((r - .5f) * 2f, (g - .5f) * 2f);
        }

        internal static float Error(Vector2 managed, Color native)
        {
            float x = Mathf.Abs(managed.x - (native.r - .5f) * 2f);
            float y = Mathf.Abs(managed.y - (native.g - .5f) * 2f);
            return float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)
                ? float.PositiveInfinity : Mathf.Max(x, y);
        }

        // Run once per map allocation. Ask the installed Unity runtime rather than
        // assuming that its CPU Texture2D sampler uses GPU half-texel coordinates.
        internal static bool TryCalibrate(out int mode, out float error)
        {
            mode = -1;
            error = float.PositiveInfinity;
            Texture2D texture = null;
            try
            {
                const int size = 4;
                texture = new Texture2D(size, size, TextureFormat.ARGB32, false) {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32((byte)(i * 73 % 256), (byte)(i * 151 % 256), (byte)(i * 31 % 256), 255);
                texture.SetPixels32(pixels);
                float[] coordinates = { -.2f, 0f, .03125f, .125f, .25f, .375f, .5f, .9f, 1f, 1.2f };
                var native = new Color[coordinates.Length * coordinates.Length];
                for (int y = 0; y < coordinates.Length; y++)
                    for (int x = 0; x < coordinates.Length; x++)
                        native[y * coordinates.Length + x] = texture.GetPixelBilinear(coordinates[x], coordinates[y]);
                for (int candidate = 0; candidate < 3; candidate++)
                {
                    float worst = 0;
                    for (int y = 0; y < coordinates.Length; y++)
                        for (int x = 0; x < coordinates.Length; x++)
                        {
                            Footprint p = Locate(coordinates[x], coordinates[y], size, size, candidate);
                            Vector2 value = Interpolate(pixels[p.Y0 * size + p.X0], pixels[p.Y0 * size + p.X1],
                                pixels[p.Y1 * size + p.X0], pixels[p.Y1 * size + p.X1], p.Fx, p.Fy);
                            worst = Mathf.Max(worst, Error(value, native[y * coordinates.Length + x]));
                        }
                    if (worst < error) { error = worst; mode = candidate; }
                }
                if (mode >= 0 && error <= Tolerance) return true;
                mode = -1;
                return false;
            }
            catch { mode = -1; error = float.PositiveInfinity; return false; }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        }
    }
}
