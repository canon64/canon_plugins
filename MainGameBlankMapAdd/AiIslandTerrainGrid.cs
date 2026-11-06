using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    // A finite heightfield conversion. Both LODs sample the same source grid; skirts seal tile seams.
    internal static class AiIslandTerrainGrid
    {
        internal sealed class Data
        {
            internal Vector3[] Vertices, Normals;
            internal Vector2[] UV;
            internal Vector4[] Tangents;
            internal int[] Triangles;
        }
        internal static Data Build(float[,] heights, Vector3 size, int startX, int startZ, int cells, int step)
        {
            int resolution = heights.GetLength(0), last = resolution - 1;
            if (resolution != heights.GetLength(1) || startX < 0 || startZ < 0 || startX + cells > last || startZ + cells > last || cells % step != 0)
                throw new ArgumentException("Invalid terrain tile.");
            int side = cells / step + 1;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int sx = startX + x * step, sz = startZ + z * step;
                float u = sx / (float)last, v = sz / (float)last;
                vertices.Add(new Vector3(u * size.x, heights[sz, sx] * size.y, v * size.z));
                int left = Math.Max(0, sx - 1), right = Math.Min(last, sx + 1), down = Math.Max(0, sz - 1), up = Math.Min(last, sz + 1);
                float dx = (heights[sz, right] - heights[sz, left]) * size.y / ((right - left) * size.x / last);
                float dz = (heights[up, sx] - heights[down, sx]) * size.y / ((up - down) * size.z / last);
                normals.Add(new Vector3(-dx, 1f, -dz).normalized); uv.Add(new Vector2(u, v));
                if (x == side - 1 || z == side - 1) continue;
                int a = z * side + x, b = a + 1, c = a + side, d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
            for (int edge = 0; edge < 4; edge++)
            {
                int previousTop = -1, previousBottom = -1;
                for (int i = 0; i < side; i++)
                {
                    int top = edge == 0 ? i : edge == 1 ? i * side + side - 1 : edge == 2 ? (side - 1) * side + side - 1 - i : (side - 1 - i) * side;
                    int bottom = vertices.Count;
                    Vector3 point = vertices[top]; point.y = -size.y;
                    vertices.Add(point); normals.Add(normals[top]); uv.Add(uv[top]);
                    if (i > 0) triangles.AddRange(new[] { previousTop, previousBottom, top, top, previousBottom, bottom });
                    previousTop = top; previousBottom = bottom;
                }
            }
            return new Data { Vertices = vertices.ToArray(), Normals = normals.ToArray(), UV = uv.ToArray(),
                Tangents = normals.SelectTangent(), Triangles = triangles.ToArray() };
        }
        private static Vector4[] SelectTangent(this List<Vector3> normals)
        {
            var result = new Vector4[normals.Count];
            for (int i = 0; i < result.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = new Vector3(n.y, -n.x, 0f).normalized;
                result[i] = new Vector4(t.x, t.y, t.z, -1f);
            }
            return result;
        }
    }
}
