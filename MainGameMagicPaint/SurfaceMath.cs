using System;
using V = System.Numerics.Vector3;
namespace MainGameMagicPaint
{
    internal static class SurfaceMath
    {
        internal static V TriangleNormal(V a, V b, V c)
        {
            V cross = V.Cross(b - a, c - a); float length = cross.Length();
            // Face triangles can have area below Unity Vector3.normalized's cutoff.
            // Match the raster/BVH degeneracy limit, rather than zeroing valid small faces.
            return length < 1e-10f ? V.Zero : cross / length;
        }
        internal static bool RayTriangle(V origin, V direction, V a, V b, V c, out float t, out V bary)
        {
            t = 0; bary = default; V e1 = b - a, e2 = c - a, p = V.Cross(direction, e2);
            float det = V.Dot(e1, p); if (Math.Abs(det) < 1e-9f) return false;
            float inv = 1 / det; V s = origin - a; float u = V.Dot(s, p) * inv; if (u < 0 || u > 1) return false;
            V q = V.Cross(s, e1); float v = V.Dot(direction, q) * inv; if (v < 0 || u + v > 1) return false;
            t = V.Dot(e2, q) * inv; bary = new V(1 - u - v, u, v); return t >= 0;
        }
        internal static V Closest(V p, V a, V b, V c)
        {
            V ab = b - a, ac = c - a, ap = p - a; float d1 = V.Dot(ab, ap), d2 = V.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            V bp = p - b; float d3 = V.Dot(ab, bp), d4 = V.Dot(ac, bp); if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            V cp = p - c; float d5 = V.Dot(ab, cp), d6 = V.Dot(ac, cp); if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float sum = va + vb + vc; return Math.Abs(sum) < 1e-15 ? a : a + ab * (vb / sum) + ac * (vc / sum);
        }
    }
}
