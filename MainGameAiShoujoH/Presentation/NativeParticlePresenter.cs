using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.Animations;

namespace MainGameAiShoujoH.Presentation
{
public sealed class ParticleRenderState { public bool ShaderSupportVerified; public int BoundRenderers; public string[] MaterialShaders; }
#pragma warning disable CS0649
    [DataContract] internal sealed class ParticleEntry { [DataMember] public int id, parentKind; [DataMember] public string asset, sourceBundle, manifest, bone; [DataMember] public float[] position, rotation; }
    [DataContract] internal sealed class ParticleRenderRow { [DataMember] public string asset, path; [DataMember] public string[] materials; }
    [DataContract] internal sealed class ParticleRenderData { [DataMember] public string bundle, sourceHash; [DataMember] public ParticleRenderRow[] rows; }
    [DataContract] internal sealed class ParticleCatalog { [DataMember] public string bundle; [DataMember] public ParticleEntry[] entries; [DataMember] public string[] actorRoles; }
#pragma warning restore CS0649
    internal sealed class NativeParticlePresenter : IDisposable
    {
        private readonly SessionContext context;
        private readonly Func<string, Transform> itemAnchor;
        private readonly List<GameObject> anchors = new List<GameObject>();
        private readonly string directory;
        private readonly ParticleCatalog catalog;
        private readonly Dictionary<int, GameObject> instances = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, Dictionary<string, Transform>> bones = new Dictionary<string, Dictionary<string, Transform>>(StringComparer.Ordinal);
        private AssetBundle bundle, renderBundle;
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, ParticleRenderRow> renderers;
        internal bool ShaderSupportVerified { get; private set; }
        internal ParticleRenderState CaptureRenderState() => new ParticleRenderState {
            ShaderSupportVerified = ShaderSupportVerified,
            BoundRenderers = instances.Values.Where(r => r != null).Sum(r => r.GetComponentsInChildren<ParticleSystemRenderer>(true).Length),
            MaterialShaders = materials.Values.Select(m => m.name + " => " + (m.shader == null ? "<missing>" : m.shader.name) + " supported=" + (m.shader != null && m.shader.isSupported)).ToArray() };
        private bool disposed;
        internal NativeParticlePresenter(SessionContext value, string directory, Func<string, Transform> itemAnchor)
        {
            context = value; this.directory = directory; this.itemAnchor = itemAnchor;
            catalog = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<ParticleCatalog>(Path.Combine(directory, "catalog.json"));
            if (catalog?.entries == null) throw new InvalidDataException("Native H particle catalog is missing.");
            var renderData = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<ParticleRenderData>(Path.Combine(directory, "render-assets.json"));
            if (renderData?.rows == null) throw new InvalidDataException("Original particle render mapping is missing.");
            renderers = renderData.rows.ToDictionary(r => r.path, StringComparer.Ordinal);
            string nativePath = Path.Combine(directory, renderData.bundle);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(nativePath))).Replace("-", ""), renderData.sourceHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Original particle render asset hash differs.");
            try
            {
                renderBundle = AssetBundle.LoadFromFile(nativePath);
                if (renderBundle == null) throw new IOException("Original compiled particle shaders could not be loaded.");
                var originals = renderBundle.LoadAllAssets<Material>().ToDictionary(m => m.name, StringComparer.Ordinal);
                foreach (string name in renderData.rows.SelectMany(r => r.materials).Where(n => n != null).Distinct())
                {
                    if (!originals.TryGetValue(name, out var original) || original.shader == null || !original.shader.isSupported) throw new InvalidDataException("Original particle material/shader is unsupported: " + name);
                    materials.Add(name, new Material(original)); // Session clone; original compiled Shader/Texture references are retained.
                }
                bundle = AssetBundle.LoadFromFile(Path.Combine(directory, catalog.bundle));
                if (bundle == null) throw new IOException("Converted particle structure is unavailable.");
                foreach (string asset in catalog.entries.Select(e => e.asset).Distinct())
                {
                    var prefab = bundle.LoadAsset<GameObject>(asset);
                    if (prefab == null || prefab.GetComponent<ParticleSystem>() == null) throw new InvalidDataException("Converted particle prefab is missing: " + asset);
                    prefabs.Add(asset, prefab);
                }
                ShaderSupportVerified = true;
            }
            catch { Dispose(); throw; }
            foreach (var actor in context.Request.Participants)
            {
                var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in actor.Character.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(node.name)) map.Add(node.name, node);
                bones.Add(actor.Role, map);
            }
        }
        internal void Play(int id)
        {
            if (disposed || !context.IsCurrent()) return;
            var entry = catalog.entries.FirstOrDefault(e => e.id == id);
            if (entry == null) throw new InvalidDataException("Native H particle index is missing: " + id);
            if (!instances.TryGetValue(id, out var root))
            {
                if (catalog.actorRoles == null || entry.parentKind < default(int) || entry.parentKind >= catalog.actorRoles.Length) throw new InvalidDataException("Native particle actor role is invalid.");
                string role = catalog.actorRoles[entry.parentKind]; Transform parent;
                if (role == "item") parent = itemAnchor(entry.bone);
                else if (!bones.TryGetValue(role, out var actor) || !actor.TryGetValue(entry.bone, out parent)) throw new InvalidDataException("Native particle bone is unavailable: " + role + ":" + entry.bone);
                if (!prefabs.TryGetValue(entry.asset, out var prefab)) throw new InvalidDataException("Converted native particle prefab is missing: " + entry.asset);
                root = UnityEngine.Object.Instantiate(prefab); instances.Add(id, root);
                foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    string path = RenderPath(root.transform, renderer.transform, entry.asset);
                    if (!renderers.TryGetValue(path, out var row)) throw new InvalidDataException("Original particle renderer mapping is missing: " + path);
                    renderer.sharedMaterials = row.materials.Select(n => n == null ? null : materials[n]).ToArray();
                    if (renderer.sharedMaterials.Where(m => m != null).Any(m => m.shader == null || !m.shader.isSupported)) throw new InvalidDataException("Particle renderer lost its original compiled shader reference.");
                }
                var anchor = new GameObject("Native particle anchor " + id); anchors.Add(anchor); anchor.transform.SetParent(parent, false);
                anchor.transform.localPosition = new Vector3(entry.position[0], entry.position[1], entry.position[2]);
                anchor.transform.localRotation = Quaternion.Euler(entry.rotation[0], entry.rotation[1], entry.rotation[2]);
                root.transform.position = anchor.transform.position; root.transform.rotation = anchor.transform.rotation; root.transform.localScale = Vector3.one;
                var constraint = root.AddComponent<ParentConstraint>(); constraint.AddSource(new ConstraintSource { sourceTransform = anchor.transform, weight = Vector3.one.x });
                constraint.weight = Vector3.one.x; constraint.translationAxis = Axis.X | Axis.Y | Axis.Z; constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z;
                constraint.locked = true; constraint.constraintActive = true;
            }
            root.SetActive(true); var particle = root.GetComponent<ParticleSystem>();
            if (particle == null) throw new InvalidDataException("Converted native particle root has no ParticleSystem.");
            particle.Simulate(0); particle.Play();
        }
        private static string RenderPath(Transform root, Transform node, string asset)
        { return node == root ? asset : RenderPath(root, node.parent, asset) + "/" + node.name + "#" + node.GetSiblingIndex(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var root in instances.Values) if (root != null) UnityEngine.Object.Destroy(root);
            instances.Clear(); foreach (var anchor in anchors) if (anchor != null) UnityEngine.Object.Destroy(anchor);
            anchors.Clear(); bones.Clear(); prefabs.Clear();
            foreach (var material in materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear(); if (bundle != null) bundle.Unload(false); if (renderBundle != null) renderBundle.Unload(false);
            bundle = null; renderBundle = null; ShaderSupportVerified = false;
        }
    }
}
