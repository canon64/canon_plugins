using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapEnvironmentRuntime
{
    // Bake visible ground from the source heightfield once. TerrainCollider remains the collision source.
    internal sealed class SandboxTerrainMesh : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Material _material;
        public IEnumerator Build(Terrain terrain, Transform map, Parameters p, Shader shader)
        {
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("島の地面用CTSメッシュシェーダーを取得できません。");
            var data = terrain.terrainData;
            int zero = p.I("math.zero"), one = p.I("math.one"), two = p.I("math.two");
            int resolution = data.heightmapResolution, last = resolution - one;
            int cells = Math.Min(p.I("island.groundMesh.tileCells"), last);
            if (cells < one) throw new InvalidOperationException("Terrain mesh tile size is invalid.");
            while (last % cells != zero) cells /= two;
            float[,] heights = data.GetHeights(zero, zero, resolution, resolution);
            int holeResolution = data.holesResolution;
            bool[,] surface = holeResolution > zero ? data.GetHoles(zero, zero, holeResolution, holeResolution) : null;
            var holder = new GameObject("Native walking ground mesh");
            holder.transform.SetParent(map, true);
            holder.transform.position = terrain.transform.position;
            holder.transform.rotation = Quaternion.identity;
            _material = new Material(shader) { name = "Native walking CTS ground", enableInstancing = false };
            _material.CopyPropertiesFromMaterial(terrain.materialTemplate);
            _material.enableInstancing = false;
            Vector3 size = data.size;
            for (int startZ = zero; startZ < last; startZ += cells)
                for (int startX = zero; startX < last; startX += cells)
                {
                    var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                    var uv = new List<Vector2>(); var tangents = new List<Vector4>(); var indices = new List<int>();
                    int side = cells + one;
                    for (int z = zero; z < side; z++) for (int x = zero; x < side; x++)
                    {
                        int sx = startX + x, sz = startZ + z;
                        float u = sx / (float)last, v = sz / (float)last;
                        vertices.Add(new Vector3(u * size.x, heights[sz, sx] * size.y, v * size.z));
                        int left = Math.Max(zero, sx - one), right = Math.Min(last, sx + one);
                        int down = Math.Max(zero, sz - one), up = Math.Min(last, sz + one);
                        float dx = (heights[sz, right] - heights[sz, left]) * size.y / ((right - left) * size.x / last);
                        float dz = (heights[up, sx] - heights[down, sx]) * size.y / ((up - down) * size.z / last);
                        var normal = new Vector3(-dx, p.N("math.one"), -dz).normalized;
                        var tangent = new Vector3(normal.y, -normal.x, p.N("math.zero")).normalized;
                        normals.Add(normal); tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -p.N("math.one"))); uv.Add(new Vector2(u, v));
                        if (x == cells || z == cells) continue;
                        if (surface != null && !surface[sz * holeResolution / last, sx * holeResolution / last]) continue;
                        int a = z * side + x, b = a + one, c = a + side, d = c + one;
                        indices.AddRange(new[] { a, c, b, b, c, d });
                    }
                    var mesh = new Mesh { name = "Native walking ground tile", indexFormat = IndexFormat.UInt32 };
                    _meshes.Add(mesh);
                    mesh.vertices = vertices.ToArray(); mesh.normals = normals.ToArray(); mesh.tangents = tangents.ToArray();
                    mesh.uv = uv.ToArray(); mesh.uv2 = uv.ToArray(); mesh.triangles = indices.ToArray(); mesh.RecalculateBounds();
                    var tile = new GameObject("Ground " + startX + "," + startZ); tile.layer = terrain.gameObject.layer;
                    tile.transform.SetParent(holder.transform, false);
                    tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = tile.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    yield return null;
                }
        }
        private void OnDestroy()
        {
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
