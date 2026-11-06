using System;
using System.IO;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    // Takes an owned TerrainData clone. Native vegetation must be detached, not only hidden.
    public static class SandboxMeshVegetation
    {
        public static GameObject Build(Terrain terrain, Transform map, Parameters settings) => Build(terrain, map, settings, null);
        public static GameObject Build(Terrain terrain, Transform map, Parameters settings, StartupTrace diagnostics)
        {
            if (terrain == null || terrain.terrainData == null) throw new InvalidDataException("VR用の植生配置に必要な地形データがありません。");
            var data = terrain.terrainData;
            var instances = data.treeInstances;
            var prototypes = data.treePrototypes;
            foreach (var instance in instances)
                if (instance.prototypeIndex < default(int) || instance.prototypeIndex >= prototypes.Length || prototypes[instance.prototypeIndex].prefab == null)
                    throw new InvalidDataException("VR用の植生Prefab参照が壊れています。");
            // This imported island has no usable detail assets. Reject other maps instead of hiding valid grass.
            foreach (var detail in data.detailPrototypes)
                if (detail.prototype != null || detail.prototypeTexture != null)
                    throw new InvalidDataException("この地形にはVR互換描画が未対応の草データがあります。");
            var root = new GameObject("AI Island Mesh Vegetation");
            root.SetActive(false);
            // Follow the map's placement/rotation, not the axis-aligned terrain heightfield.
            root.transform.SetParent(map, false);
            try
            {
                diagnostics?.Mark("vegetation.clone.begin", () => "instances=" + instances.Length + " prototypes=" + prototypes.Length);
                var parentScale = root.transform.lossyScale;
                if (parentScale.x == default(float) || parentScale.y == default(float) || parentScale.z == default(float))
                    throw new InvalidDataException("植生配置先のスケールがゼロです。");
                var block = new MaterialPropertyBlock();
                foreach (var instance in instances)
                {
                    var prefab = prototypes[instance.prototypeIndex].prefab;
                    var tree = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                    tree.transform.position = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                    var halfAngle = instance.rotation * settings.N("math.half");
                    tree.transform.rotation = terrain.transform.rotation * new Quaternion(default(float), (float)Math.Sin(halfAngle), default(float), (float)Math.Cos(halfAngle));
                    var scale = Vector3.Scale(prefab.transform.localScale, new Vector3(instance.widthScale, instance.heightScale, instance.widthScale));
                    tree.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                    // Cloned prefab colliders replace the native tree collisions detached below.
                    foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.GetPropertyBlock(block);
                        block.SetColor("_TreeInstanceColor", instance.color);
                        block.SetColor("_TreeInstanceLightmapColor", instance.lightmapColor);
                        renderer.SetPropertyBlock(block);
                    }
                    tree.SetActive(true);
                }
                // drawTreesAndFoliage=false still lets Unity prepare the native tree database.
                // Remove its render/collision inputs from the owned copy before revealing any trees.
                diagnostics?.Mark("vegetation.clone.end");
                diagnostics?.TerrainCheckpoint("vegetation.detach.begin", terrain);
                data.treeInstances = Array.Empty<TreeInstance>();
                diagnostics?.Mark("vegetation.detach.instances");
                data.treePrototypes = Array.Empty<TreePrototype>();
                diagnostics?.Mark("vegetation.detach.prototypes");
                data.detailPrototypes = Array.Empty<DetailPrototype>();
                diagnostics?.Mark("vegetation.detach.details");
                terrain.Flush();
                diagnostics?.TerrainCheckpoint("vegetation.detach.end", terrain);
                diagnostics?.RendererInventory("vegetation.before-activate", root);
                diagnostics?.Mark("vegetation.activate.begin");
                root.SetActive(true);
                diagnostics?.Mark("vegetation.activate.end");
                return root;
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }
}
