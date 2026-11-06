using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapEnvironmentRuntime
{
    public sealed partial class StartupTrace
    {
        public void AssemblyIdentity(Assembly assembly)
        {
            Mark("assembly.identity", () =>
            {
                string location = assembly.Location;
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(location))
                    return assembly.FullName + " mvid=" + assembly.ManifestModule.ModuleVersionId + " path=" + location
                        + " bytes=" + stream.Length + " sha256=" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            });
        }
        public void ObserveUnityErrors()
        {
            if (!Enabled) return;
            Application.LogCallback callback = (message, stack, kind) =>
            {
                if (!Enabled || kind == LogType.Log) return;
                Mark("unity." + kind, () => message + "\n" + stack);
            };
            Application.logMessageReceivedThreaded += callback;
            Own(() => Application.logMessageReceivedThreaded -= callback);
        }
        private static string ObjectName(UnityEngine.Object value) => value == null ? "null" : value.name + "#" + value.GetInstanceID()
            + " native=" + NativeAddressReader.Read(value, typeof(UnityEngine.Object));
        private static string Hierarchy(Transform transform)
        {
            var names = new List<string>();
            for (var node = transform; node != null; node = node.parent) names.Add(node.name);
            names.Reverse(); return string.Join("/", names.ToArray());
        }
        private static string CameraState(Camera camera)
        {
            if (camera == null) return "null";
            return ObjectName(camera) + " path=" + Hierarchy(camera.transform) + " scene=" + camera.gameObject.scene.name
                + " active=" + camera.gameObject.activeInHierarchy + " enabled=" + camera.enabled + " stereo=" + camera.stereoTargetEye
                + " mask=" + camera.cullingMask + " target=" + ObjectName(camera.targetTexture) + " position=" + camera.transform.position
                + " rotation=" + camera.transform.rotation + " near=" + camera.nearClipPlane + " far=" + camera.farClipPlane;
        }
        public void CameraCheckpoint(string stage, Camera camera) => Mark(stage, () => "frame=" + Time.frameCount + " " + CameraState(camera));
        public void TerrainCheckpoint(string stage, Terrain terrain)
        {
            Mark(stage, () =>
            {
                if (terrain == null) return "terrain=null frame=" + Time.frameCount;
                var data = terrain.terrainData;
                var collider = terrain.GetComponent<TerrainCollider>();
                var material = terrain.materialTemplate;
                return "frame=" + Time.frameCount + " terrain=" + ObjectName(terrain) + " path=" + Hierarchy(terrain.transform)
                    + " scene=" + terrain.gameObject.scene.name + " enabled=" + terrain.enabled + " active=" + terrain.gameObject.activeInHierarchy
                    + " nativeVegetation=" + terrain.drawTreesAndFoliage + " data=" + ObjectName(data)
                    + " treeInstances=" + (data == null ? "null" : data.treeInstanceCount.ToString())
                    + " treePrototypes=" + (data == null ? "null" : data.treePrototypes.Length.ToString())
                    + " detailPrototypes=" + (data == null ? "null" : data.detailPrototypes.Length.ToString())
                    + " colliderData=" + ObjectName(collider == null ? null : collider.terrainData)
                    + " material=" + ObjectName(material) + " shader=" + ObjectName(material == null ? null : material.shader)
                    + " shaderSupported=" + (material != null && material.shader != null && material.shader.isSupported)
                    + " position=" + terrain.transform.position + " scale=" + terrain.transform.lossyScale;
            });
        }
        public void Snapshot(string stage, GameObject root = null)
        {
            if (!Enabled) return;
            Mark(stage, () => "frame=" + Time.frameCount + " activeScene=" + SceneManager.GetActiveScene().name
                + " unity=" + Application.unityVersion + " root=" + ObjectName(root));
            if (!Enabled) return;
            var cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(camera => camera.gameObject.scene.IsValid()).ToArray();
            foreach (var camera in cameras) CameraCheckpoint(stage + ".camera", camera);
            if (!Enabled) return;
            var terrains = root == null ? Resources.FindObjectsOfTypeAll<Terrain>().Where(terrain => terrain.gameObject.scene.IsValid()).ToArray() : root.GetComponentsInChildren<Terrain>(true);
            foreach (var terrain in terrains) TerrainCheckpoint(stage + ".terrain", terrain);
            foreach (string name in new[] { "Hidden/Nature/Tree Creator Albedo Rendertex", "Hidden/Nature/Tree Creator Normal Rendertex",
                "Hidden/TerrainEngine/CameraFacingBillboardTree", "Nature/Terrain/CameraFacingBillboardTree", "Hidden/TerrainEngine/BillboardTree" })
                Mark(stage + ".shader", () => { var shader = Shader.Find(name); return "requested=" + name + " found=" + ObjectName(shader) + " supported=" + (shader != null && shader.isSupported); });
        }
        public void RendererInventory(string stage, GameObject root)
        {
            Artifact(stage, () => RendererRows(root, null));
        }
        public void RenderInventory(string stage, Camera camera)
        {
            if (!Enabled) return;
            CameraCheckpoint(stage + ".camera", camera);
            // Capture all loaded scene renderers, including the player and other plugins' objects.
            Artifact(stage, () => RendererRows(null, camera));
        }
        private static string StaticBatchDetails(Renderer renderer)
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var root = typeof(Renderer).GetProperty("staticBatchRootTransform", flags)?.GetValue(renderer, null) as Transform;
                var start = renderer.GetType().GetProperty("subMeshStartIndex", flags)?.GetValue(renderer, null);
                return "root=" + ObjectName(root) + " subMeshStartIndex=" + (start?.ToString() ?? "unavailable");
            }
            catch (Exception error) { return "unavailable:" + error.GetType().Name; }
        }
        private IEnumerable<Dictionary<string, string>> RendererRows(GameObject root, Camera camera)
        {
            if (!Enabled) yield break;
            var planes = camera == null ? null : GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers = root == null ? Resources.FindObjectsOfTypeAll<Renderer>().Where(renderer => renderer.gameObject.scene.IsValid()).ToArray() : root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!Enabled) yield break;
                var meshFilter = renderer.GetComponent<MeshFilter>();
                var skin = renderer as SkinnedMeshRenderer;
                var mesh = skin != null ? skin.sharedMesh : meshFilter != null ? meshFilter.sharedMesh : null;
                var materials = renderer.sharedMaterials;
                var bounds = renderer.bounds;
                var maskIncluded = camera != null && (camera.cullingMask & (Convert.ToInt32(true) << renderer.gameObject.layer)) != default(int);
                var candidate = planes != null && maskIncluded && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && !renderer.forceRenderingOff && GeometryUtility.TestPlanesAABB(planes, bounds);
                yield return new Dictionary<string, string> {
                    { "id", renderer.GetInstanceID().ToString() }, { "type", renderer.GetType().FullName }, { "path", Hierarchy(renderer.transform) },
                    { "native", NativeAddressReader.Read(renderer, typeof(UnityEngine.Object)) },
                    { "frame", Time.frameCount.ToString() }, { "camera", ObjectName(camera) },
                    { "cameraMaskIncludesLayer", maskIncluded.ToString() }, { "approximateFrustumCandidate", candidate.ToString() },
                    { "boundsCenter", bounds.center.ToString("R") }, { "boundsExtents", bounds.extents.ToString("R") },
                    { "position", renderer.transform.position.ToString("R") }, { "lossyScale", renderer.transform.lossyScale.ToString("R") },
                    { "shadowCasting", renderer.shadowCastingMode.ToString() }, { "receiveShadows", renderer.receiveShadows.ToString() },
                    { "lightmapIndex", renderer.lightmapIndex.ToString() }, { "lightmapScaleOffset", renderer.lightmapScaleOffset.ToString("R") },
                    { "motionVectors", renderer.motionVectorGenerationMode.ToString() },
                    { "staticBatchDetails", StaticBatchDetails(renderer) },
                    { "meshBounds", mesh == null ? "null" : mesh.bounds.ToString("R") },
                    { "meshIndexFormat", mesh == null ? "null" : mesh.indexFormat.ToString() },
                    { "skinRoot", skin == null ? "null" : ObjectName(skin.rootBone) },
                    { "skinBones", skin == null ? "null" : skin.bones.Length.ToString() },
                    { "scene", renderer.gameObject.scene.name }, { "active", renderer.gameObject.activeInHierarchy.ToString() },
                    { "enabled", renderer.enabled.ToString() }, { "forceOff", renderer.forceRenderingOff.ToString() },
                    { "layer", renderer.gameObject.layer.ToString() }, { "staticBatch", renderer.isPartOfStaticBatch.ToString() },
                    { "mesh", ObjectName(mesh) }, { "vertices", mesh == null ? "null" : mesh.vertexCount.ToString() },
                    { "submeshes", mesh == null ? "null" : mesh.subMeshCount.ToString() },
                    { "materials", string.Join("\n", materials.Select(material => material == null ? "null" : ObjectName(material)
                        + " shader=" + ObjectName(material.shader) + " supported=" + (material.shader != null && material.shader.isSupported)
                        + " queue=" + material.renderQueue + " instancing=" + material.enableInstancing + " keywords=" + (material.shaderKeywords ?? Array.Empty<string>()).Aggregate(new StringBuilder(), (builder, word) => builder.Append(word).Append(' '))).ToArray()) }
                };
            }
        }
    }
}
