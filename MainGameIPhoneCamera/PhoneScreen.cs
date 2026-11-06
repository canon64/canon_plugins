using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    internal static class PhoneScreen
    {
        // Installed bake mesh is in X/Z, its front normal is -Y; the prefab rotates it upright.
        // Keep the measured curved outline, a black bezel, and the camera/speaker notch.
        internal static Mesh Build(Mesh source, bool bezel = false)
        {
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = source.uv;
            var indices = source.triangles;
            var output = new List<Vector3>();
            var texcoords = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (!Front(a, normals, uv) || !Front(b, normals, uv) || !Front(c, normals, uv)) continue;
                if (bezel)
                {
                    Emit(new List<Vector3> {
                        vertices[a] + new Vector3(0, -.0003f, 0),
                        vertices[b] + new Vector3(0, -.0003f, 0),
                        vertices[c] + new Vector3(0, -.0003f, 0)
                    }, output, texcoords, triangles);
                    continue;
                }
                var face = new List<Vector3> { ScreenVertex(vertices[a]), ScreenVertex(vertices[b]), ScreenVertex(vertices[c]) };
                // Rounded lower corners of the camera notch; preserve the original outer screen outline.
                Emit(Clip(face, 0, -.017f, false), output, texcoords, triangles);
                Emit(Clip(face, 0, .017f, true), output, texcoords, triangles);
                const float notchRadius = .003f;
                const float innerEdge = .017f - notchRadius;
                var middle = Clip(Clip(face, 0, -innerEdge, true), 0, innerEdge, false);
                Emit(Clip(middle, 2, .0515f, false), output, texcoords, triangles);
                for (int side = -1; side <= 1; side += 2)
                for (int segment = 0; segment < 12; segment++)
                {
                    // Quarter-circle sampled from bottom tangent to vertical side tangent.
                    double angle0 = segment * Math.PI / 24;
                    double angle1 = (segment + 1) * Math.PI / 24;
                    float x0 = side * (innerEdge + notchRadius * (float)Math.Sin(angle0));
                    float x1 = side * (innerEdge + notchRadius * (float)Math.Sin(angle1));
                    float z0 = .0515f + notchRadius * (1f - (float)Math.Cos(angle0));
                    float z1 = .0515f + notchRadius * (1f - (float)Math.Cos(angle1));
                    var strip = Clip(Clip(face, 0, Math.Min(x0, x1), true), 0, Math.Max(x0, x1), false);
                    Emit(ClipBelowLine(strip, x0, z0, (z1 - z0) / (x1 - x0)), output, texcoords, triangles);
                }
            }
            if (triangles.Count == 0) throw new InvalidOperationException("導入済みiPhoneの画面形状が想定と異なります");
            var mesh = new Mesh { name = "IPhoneLiveScreen" };
            mesh.SetVertices(output);
            mesh.SetUVs(0, texcoords);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool Front(int index, Vector3[] normals, Vector2[] uv)
            => normals[index].y < -.99f && uv[index].x < .21f && uv[index].y > .39f && uv[index].y < .81f;
        private static Vector3 ScreenVertex(Vector3 v)
            => new Vector3(v.x * .96f, v.y - .0006f, v.z * .97f);

        private static List<Vector3> Clip(List<Vector3> polygon, int axis, float boundary, bool keepGreater)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0) return result;
            Vector3 previous = polygon[polygon.Count - 1];
            bool previousInside = keepGreater ? previous[axis] >= boundary : previous[axis] <= boundary;
            foreach (var current in polygon)
            {
                bool inside = keepGreater ? current[axis] >= boundary : current[axis] <= boundary;
                if (inside != previousInside)
                {
                    float t = (boundary - previous[axis]) / (current[axis] - previous[axis]);
                    result.Add(Vector3.Lerp(previous, current, t));
                }
                if (inside) result.Add(current);
                previous = current; previousInside = inside;
            }
            return result;
        }

        private static List<Vector3> ClipBelowLine(List<Vector3> polygon, float x, float z, float slope)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0) return result;
            Vector3 previous = polygon[polygon.Count - 1];
            float previousDistance = previous.z - z - slope * (previous.x - x);
            foreach (var current in polygon)
            {
                float distance = current.z - z - slope * (current.x - x);
                if ((distance <= 0f) != (previousDistance <= 0f))
                    result.Add(Vector3.Lerp(previous, current, previousDistance / (previousDistance - distance)));
                if (distance <= 0f) result.Add(current);
                previous = current;
                previousDistance = distance;
            }
            return result;
        }

        private static void Emit(List<Vector3> polygon, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            if (polygon.Count < 3) return;
            int start = vertices.Count;
            foreach (var v in polygon)
            {
                vertices.Add(v);
                // Front is viewed along +native Y: -X is the viewer's right. No selfie mirror.
                uv.Add(new Vector2(1f - Mathf.InverseLerp(-.028f, .028f, v.x), Mathf.InverseLerp(-.0583f, .0583f, v.z)));
            }
            for (int i = 1; i < polygon.Count - 1; i++)
            { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }
    }
}
