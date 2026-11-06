using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameBlankMapAdd
{
    // Owned by the additive island root. All construction is finite and runs during loading only.
    internal sealed class AiIslandTerrainMesh : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private Terrain _source;
        private TerrainCollider _collider;
        private bool _heightmap, _foliage, _collision;

        private Mesh MakeMesh(AiIslandTerrainGrid.Data data, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            _meshes.Add(mesh);
            mesh.vertices = data.Vertices; mesh.normals = data.Normals; mesh.uv = data.UV; mesh.uv2 = data.UV;
            mesh.tangents = data.Tangents; mesh.triangles = data.Triangles; mesh.RecalculateBounds();
            return mesh;
        }
        private GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name); child.layer = _source.gameObject.layer;
            child.transform.SetParent(parent, false); return child;
        }
        private Renderer RenderMesh(GameObject child, Mesh mesh, Material material)
        {
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        internal IEnumerator Build(Terrain source, float scale, Func<bool> valid)
        {
            _source = source; _collider = source.GetComponent<TerrainCollider>();
            _heightmap = source.drawHeightmap; _foliage = source.drawTreesAndFoliage;
            _collision = _collider != null && _collider.enabled;
            TerrainData data = source.terrainData;
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            // TerrainData was converted to KKS units, but mesh vertices inherit the scaled island root.
            // Undo that data-only conversion here so the root supplies exactly one unit conversion.
            Vector3 size = data.size / scale;
            var material = new Material(source.materialTemplate) { name = "AI island rotating terrain CTS", enableInstancing = false };
            _materials.Add(material);
            var holder = Child("__AiIslandRotatingGround", source.transform);
            int cells = Math.Min(64, resolution - 1);
            while ((resolution - 1) % cells != 0) cells /= 2;
            int lowStep = cells >= 4 ? 4 : 1;
            for (int z = 0; z < resolution - 1; z += cells) for (int x = 0; x < resolution - 1; x += cells)
            {
                if (!valid()) yield break;
                var tile = Child("Ground " + x + "," + z, holder.transform);
                Mesh high = MakeMesh(AiIslandTerrainGrid.Build(heights, size, x, z, cells, 1), "AI terrain detailed tile");
                Mesh low = MakeMesh(AiIslandTerrainGrid.Build(heights, size, x, z, cells, lowStep), "AI terrain distant tile");
                var near = RenderMesh(Child("Detailed", tile.transform), high, material);
                var far = RenderMesh(Child("Distant", tile.transform), low, material);
                var lod = tile.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.1f, new[] { near }), new LOD(0f, new[] { far }) }); lod.RecalculateBounds();
                tile.AddComponent<MeshCollider>().sharedMesh = high;
                yield return null;
            }
            TreePrototype[] prototypes = data.treePrototypes;
            TreeInstance[] trees = data.treeInstances;
            for (int i = 0; i < trees.Length; i++)
            {
                if (!valid()) yield break;
                TreeInstance tree = trees[i];
                if (tree.prototypeIndex >= 0 && tree.prototypeIndex < prototypes.Length && prototypes[tree.prototypeIndex].prefab != null)
                {
                    GameObject prefab = prototypes[tree.prototypeIndex].prefab;
                    var instance = Instantiate(prefab, holder.transform, false);
                    instance.transform.localPosition = Vector3.Scale(tree.position, size);
                    instance.transform.localRotation = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
                    instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(tree.widthScale / scale, tree.heightScale / scale, tree.widthScale / scale));
                    var properties = new MaterialPropertyBlock(); properties.SetColor("_TreeInstanceColor", tree.color);
                    properties.SetColor("_TreeInstanceLightmapColor", tree.lightmapColor);
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(properties);
                }
                if (i % 128 == 0) yield return null;
            }
            // Keep native density and textures, baking small static detail batches once per prototype.
            foreach (var step in BuildDetails(data, holder.transform, size, scale, valid)) yield return step;
            if (!valid()) yield break;
            source.drawHeightmap = false; source.drawTreesAndFoliage = false;
            if (_collider != null) _collider.enabled = false;
        }

        private IEnumerable<object> BuildDetails(TerrainData data, Transform parent, Vector3 size, float scale, Func<bool> valid)
        {
            int resolution = data.detailResolution;
            DetailPrototype[] prototypes = data.detailPrototypes;
            var random = new System.Random(0);
            for (int layer = 0; layer < prototypes.Length; layer++)
            {
                if (!valid()) yield break;
                DetailPrototype prototype = prototypes[layer];
                Mesh template = null; Material material = null; Matrix4x4 prefabMatrix = Matrix4x4.identity;
                if (prototype.usePrototypeMesh && prototype.prototype != null)
                {
                    var filter = prototype.prototype.GetComponentInChildren<MeshFilter>();
                    var renderer = prototype.prototype.GetComponentInChildren<MeshRenderer>();
                    if (filter != null && renderer != null)
                    { template = filter.sharedMesh; material = renderer.sharedMaterial; prefabMatrix = prototype.prototype.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix; }
                }
                else if (prototype.prototypeTexture != null)
                {
                    var shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
                    if (shader == null) throw new InvalidOperationException("草の描画シェーダーを取得できません。");
                    material = new Material(shader) { name = "AI island rotating grass", mainTexture = prototype.prototypeTexture };
                    _materials.Add(material);
                    template = new Mesh { name = "AI island grass crossed cards" }; _meshes.Add(template);
                    template.vertices = new[] { new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(-.5f,1,0),new Vector3(.5f,1,0),
                        new Vector3(0,0,-.5f),new Vector3(0,0,.5f),new Vector3(0,1,-.5f),new Vector3(0,1,.5f) };
                    template.uv = new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one,Vector2.zero,Vector2.right,Vector2.up,Vector2.one };
                    template.triangles = new[] { 0,2,1,1,2,3,1,2,0,3,2,1,4,6,5,5,6,7,5,6,4,7,6,5 }; template.RecalculateNormals();
                }
                if (template == null || material == null || resolution == 0) continue;
                int[,] density = data.GetDetailLayer(0,0,resolution,resolution,layer);
                var combines = new List<CombineInstance>();
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++) for (int item = 0; item < density[z,x]; item++)
                {
                    if (random.NextDouble() > _source.detailObjectDensity) continue;
                    float u = (x + (float)random.NextDouble()) / resolution, v = (z + (float)random.NextDouble()) / resolution;
                    float variation = (float)random.NextDouble();
                    float width = Mathf.Lerp(prototype.minWidth,prototype.maxWidth,variation) / scale;
                    float height = Mathf.Lerp(prototype.minHeight,prototype.maxHeight,variation) / scale;
                    var position = new Vector3(u * size.x, data.GetInterpolatedHeight(u,v) / scale, v * size.z);
                    combines.Add(new CombineInstance { mesh = template, transform = Matrix4x4.TRS(position,Quaternion.Euler(0,variation*360f,0),new Vector3(width,height,width)) * prefabMatrix });
                }
                if (combines.Count > 0)
                {
                    var mesh = new Mesh { name = "AI island static grass batch", indexFormat = IndexFormat.UInt32 }; _meshes.Add(mesh);
                    mesh.CombineMeshes(combines.ToArray(),true,true);
                    RenderMesh(Child("Ground details " + layer,parent),mesh,material);
                }
                yield return null;
            }
        }
        private void OnDestroy()
        {
            if (_source != null) { _source.drawHeightmap = _heightmap; _source.drawTreesAndFoliage = _foliage; }
            if (_collider != null) _collider.enabled = _collision;
            foreach (Mesh mesh in _meshes) Destroy(mesh);
            foreach (Material material in _materials) Destroy(material);
        }
    }
}
