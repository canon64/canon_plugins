using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapEnvironmentRuntime
{
    public sealed class SandboxIslandLoader : MonoBehaviour
    {
        public sealed class Entry { public string path, sha256; public long bytes; }
        public sealed class Manifest { public string scene, root, anchor; public Entry[] files; }
        public string Status { get; private set; }
        public StartupTrace Diagnostics { get; set; }
        public string Error { get; private set; }
        public Scene LoadedScene { get; private set; }
        private Transform _islandRoot;
        public GameObject Root => _islandRoot != null ? _islandRoot.gameObject : null;
        private Matrix4x4 _sourceWorldToLocal;
        public Vector3 FromSourcePosition(Vector3 position) => _islandRoot.TransformPoint(_sourceWorldToLocal.MultiplyPoint3x4(position));
        public Vector3 ToSourcePosition(Vector3 position) => _sourceWorldToLocal.inverse.MultiplyPoint3x4(_islandRoot.InverseTransformPoint(position));
        private readonly List<AssetBundle> _bundles = new List<AssetBundle>();
        private TerrainData _terrainData;
        private Material _terrainMaterial;
        private readonly Dictionary<Material, Vector4> _water = new Dictionary<Material, Vector4>();
        private readonly Dictionary<Material, float> _seaFrequency = new Dictionary<Material, float>();
        private Terrain _terrain;
        private GameObject _meshVegetation;
        private SandboxTerrainRenderPreparation _renderPreparation;
        private AsyncOperation _pendingScene;
        private bool _disposed;
        private SandboxTerrainShaderAsset _groundShader;
        private SandboxStagePlacement _placement;
        public Vector3 InitialWalkPosition(Vector3 position) => _placement.BaseToWorld(position);
        private readonly Dictionary<int, bool> _walkingLayers = new Dictionary<int, bool>();
        private int _groundLayer;
        public void PrepareWalkingLayer(int layer)
        {
            _groundLayer = _terrain.gameObject.layer;
            if (_walkingLayers.ContainsKey(layer)) return;
            bool ignored = Physics.GetIgnoreLayerCollision(layer, _groundLayer);
            _walkingLayers.Add(layer, ignored);
            Physics.IgnoreLayerCollision(layer, _groundLayer, false);
            Physics.SyncTransforms();
            Debug.Log("SANDBOX_WALK_COLLISION_READY playerLayer=" + layer + " groundLayer=" + _groundLayer + " previouslyIgnored=" + ignored);
        }
        public SandboxIslandVisibility Visibility { get; private set; }
        public SandboxIslandDoors Doors { get; private set; }
        public void ShutdownInteractions() { if (Doors != null) { DestroyImmediate(Doors); Doors = null; } }
        public void AttachCamera(Camera camera, Parameters settings)
        {
            Doors.BindView(camera, false);
            Visibility = _terrain.transform.root.gameObject.AddComponent<SandboxIslandVisibility>(); Visibility.Initialize(camera, settings);
            camera.useOcclusionCulling = false;
        }
        public IEnumerator Load(Parameters settings) => Load(settings, false);
        public IEnumerator Load(Parameters settings, bool useMeshVegetation)
        {
            var iterator = LoadCore(settings, useMeshVegetation);
            while (true)
            {
                bool next;
                try { next = iterator.MoveNext(); }
                catch (Exception ex) { Diagnostics?.Mark("island.failed", () => ex.ToString()); Error = ex.ToString(); Debug.LogError(Error); yield break; }
                if (!next) yield break;
                yield return iterator.Current;
            }
        }
        private IEnumerator LoadCore(Parameters settings, bool useMeshVegetation)
        {
            string directory = Path.GetFullPath(settings.T("island.directory"));
            var doorManifest = SandboxIslandDoors.Read(settings);
            AssetBundle doorBundle = null;
            var manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            if (manifest.files == null || !manifest.files.Any()) throw new InvalidDataException("Island manifest is empty");
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var paths = manifest.files.Select(file => Path.GetFullPath(Path.Combine(directory, file.path))).ToArray();
            if (paths.Any(path => !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Island file outside directory");
            Status = "AI少女マップ：抽出済みファイルのサイズ・SHA-256を照合中"; Debug.Log(Status);
            Diagnostics?.Mark("island.validation.begin", () => directory);
            var validation = Task.Run(() =>
            {
                foreach (var pair in manifest.files.Zip(paths, (entry, path) => new { entry, path }))
                {
                    if (new FileInfo(pair.path).Length != pair.entry.bytes) throw new InvalidDataException("Island size mismatch: " + pair.entry.path);
                    using (var hash = SHA256.Create()) using (var stream = File.OpenRead(pair.path))
                        if (!string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), pair.entry.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Island SHA-256 mismatch: " + pair.entry.path);
                }
            });
            while (!validation.IsCompleted) yield return null;
            if (validation.IsFaulted) throw validation.Exception.GetBaseException();
            Diagnostics?.Mark("island.validation.end");
            foreach (string path in paths)
            {
                Status = "AI少女マップ：" + Path.GetFileName(path) + "を読み込み中"; Debug.Log(Status);
                Diagnostics?.Mark("island.bundle.begin", () => path);
                var request = AssetBundle.LoadFromFileAsync(path); yield return request;
                if (request.assetBundle == null) throw new InvalidDataException("Island bundle load failed: " + path);
                Diagnostics?.Mark("island.bundle.end", () => path);
                _bundles.Add(request.assetBundle);
                if (string.Equals(Path.GetFullPath(Path.Combine(directory, doorManifest.bundle)), path, StringComparison.OrdinalIgnoreCase)) doorBundle = request.assetBundle;
            }
            string scenePath = _bundles.Last().GetAllScenePaths().Single(path => Path.GetFileNameWithoutExtension(path) == manifest.scene);
            Status = "AI少女マップ：地形・建物・植生を配置中"; Debug.Log(Status);
            Diagnostics?.Mark("island.scene.begin", () => scenePath);
            if (useMeshVegetation) _renderPreparation = new SandboxTerrainRenderPreparation(manifest.scene);
            _pendingScene = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            _pendingScene.completed += ignored =>
            {
                if (!_disposed) return;
                _renderPreparation?.Dispose();
                var lateScene = SceneManager.GetSceneByName(manifest.scene);
                if (lateScene.IsValid() && lateScene.isLoaded) SceneManager.UnloadSceneAsync(lateScene);
            };
            yield return _pendingScene;
            _pendingScene = null;
            if (_disposed) yield break;
            Diagnostics?.Snapshot("island.scene.end");
            LoadedScene = SceneManager.GetSceneByName(manifest.scene);
            var root = SceneManager.GetSceneByName(manifest.scene).GetRootGameObjects().Single(node => node.name == manifest.root);
            var terrain = root.GetComponentInChildren<Terrain>(true);
            _terrain = terrain;
            Diagnostics?.TerrainCheckpoint("terrain.original", terrain);
            // Keep native Terrain out of the VR render queue; retain its heightfield for collision.
            if (useMeshVegetation && terrain != null) terrain.drawTreesAndFoliage = false;
            Diagnostics?.TerrainCheckpoint("terrain.native-visibility.set", terrain);
            _islandRoot = root.transform; _sourceWorldToLocal = root.transform.worldToLocalMatrix;
            SandboxStaticBatchBinding.Bind(root.transform);
            float scale = settings.N("island.scale"); root.transform.localScale *= scale;
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                bool playing = particles.isPlaying; particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (playing) particles.Play(false);
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true)) { light.range *= scale; light.shadowNearPlane *= scale; }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            if (terrain == null || terrain.terrainData == null || terrain.materialTemplate == null || !terrain.materialTemplate.shader.isSupported) throw new InvalidDataException("Island terrain or CTS shader unavailable");
            Diagnostics?.Mark("terrain.clone.begin");
            _terrainData = Instantiate(terrain.terrainData); _terrainData.size *= scale;
            var trees = _terrainData.treeInstances;
            for (int index = settings.I("math.zero"); index < trees.Length; index++) { trees[index].heightScale *= scale; trees[index].widthScale *= scale; }
            _terrainData.treeInstances = trees;
            var details = _terrainData.detailPrototypes;
            foreach (var detail in details) { detail.minWidth *= scale; detail.maxWidth *= scale; detail.minHeight *= scale; detail.maxHeight *= scale; }
            _terrainData.detailPrototypes = details; terrain.terrainData = _terrainData;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
            collider.terrainData = _terrainData; collider.enabled = true;
            Diagnostics?.TerrainCheckpoint("terrain.clone.end", terrain);
            Debug.Log("SANDBOX_TERRAIN_COLLISION_READY enabled=" + collider.enabled + " sharedTerrainData=" + (collider.terrainData == terrain.terrainData));
            _terrainMaterial = Instantiate(terrain.materialTemplate);
            var properties = Enumerable.Range(settings.I("math.one"), settings.I("island.terrainLayers")).Select(index => "_Texture_" + index + "_Tiling")
                .Concat(new[] { "_Geological_Tiling_Close", "_Geological_Tiling_Far", "_Perlin_Normal_Tiling_Close", "_Perlin_Normal_Tiling_Far" });
            foreach (var property in properties) if (_terrainMaterial.HasProperty(property)) _terrainMaterial.SetFloat(property, _terrainMaterial.GetFloat(property) * scale);
            terrain.materialTemplate = _terrainMaterial; terrain.basemapDistance = settings.N("island.basemapDistance");
            terrain.treeDistance = settings.N("island.treeDistance"); terrain.detailObjectDistance = settings.N("island.detailDistance");
            Diagnostics?.Mark("terrain.flush.begin");
            terrain.Flush();
            Diagnostics?.Mark("terrain.flush.end");
            var anchor = root.GetComponentsInChildren<Transform>(true).Single(node => node.name == manifest.anchor);
            var start = anchor.position; start.y = terrain.SampleHeight(start) + terrain.transform.position.y;
            root.transform.position -= start;
            Physics.SyncTransforms();
            if (settings.T("movement.mode") == "walk")
            {
                var origin = settings.V("spawn");
                origin.y = terrain.SampleHeight(origin) + terrain.transform.position.y + settings.N("character.groundProbeHeight");
                if (!collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit ground, settings.N("character.groundProbeDistance")))
                    throw new InvalidDataException("歩行開始地点に接触可能な地面がありません。 spawn=" + settings.V("spawn"));
                settings.numbers.Single(item => item.key == "spawn.y").value = ground.point.y + settings.N("movement.bodyHeight") - settings.N("movement.bodyRadius") + settings.N("movement.skin");
                Debug.Log("SANDBOX_WALK_SPAWN_READY ground=" + ground.point + " camera=" + settings.V("spawn"));
            }
            var clones = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials; bool changed = false;
                for (int index = settings.I("math.zero"); index < materials.Length; index++)
                {
                    var source = materials[index]; if (source == null || source.shader == null || source.shader.name != settings.T("island.sea.shader")) continue;
                    if (!clones.TryGetValue(source, out Material material))
                    {
                        material = Instantiate(source); clones.Add(source, material);
                        _water.Add(material, source.GetVector("_GFinalFrequency"));
                        _seaFrequency.Add(material, source.GetFloat("_GGlobalFrequency"));
                    }
                    materials[index] = material; changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            if (useMeshVegetation)
            {
                var ground = root.AddComponent<SandboxTerrainMesh>();
                _groundShader = new SandboxTerrainShaderAsset();
                var build = ground.Build(terrain, root.transform, settings, _groundShader.Shader);
                try { while (build.MoveNext()) yield return build.Current; }
                finally { (build as IDisposable)?.Dispose(); }
            }
            var placement = new GameObject("AI Island Stage Placement");
            SceneManager.MoveGameObjectToScene(placement, root.scene);
            Diagnostics?.Mark("island.placement.begin");
            _placement = placement.AddComponent<SandboxStagePlacement>(); _placement.Initialize(root.transform, terrain);
            Diagnostics?.Mark("island.placement.end");
            Diagnostics?.Mark("vegetation.build.begin", () => "meshFallback=" + useMeshVegetation);
            if (useMeshVegetation) _meshVegetation = SandboxMeshVegetation.Build(terrain, root.transform, settings, Diagnostics);
            Diagnostics?.TerrainCheckpoint("vegetation.build.end", terrain);
            Diagnostics?.Mark("island.settings.begin");
            ApplySettings(settings);
            Diagnostics?.Mark("island.settings.end");
            if (doorBundle == null) throw new InvalidDataException("Island door animation bundle missing");
            Diagnostics?.Mark("island.doors.begin");
            Doors = gameObject.AddComponent<SandboxIslandDoors>(); Doors.Initialize(doorManifest, root.transform, doorBundle, settings);
            Diagnostics?.Snapshot("island.doors.end", root);
            Status = "AI少女・本編島 読込完了";
            Debug.Log("SANDBOX_AI_ISLAND_READY renderers=" + root.GetComponentsInChildren<Renderer>(true).Length + " terrain=" + terrain.terrainData.size + " shader=" + terrain.materialTemplate.shader.name);
        }
        public void ApplySettings(Parameters settings)
        {
            if (_placement != null) _placement.Apply(settings);
            if (Visibility != null) Visibility.Configure(settings);
            if (_terrain != null) { _terrain.treeDistance = settings.N("island.treeDistance"); _terrain.detailObjectDistance = settings.N("island.detailDistance"); _terrain.basemapDistance = settings.N("island.basemapDistance"); }
            foreach (var entry in _water)
            {
                var displacement = settings.V("island.sea.displacement");
                entry.Key.SetVector("_GerstnerVertexIntensity", new Vector4(displacement.x, displacement.y, displacement.z, settings.N("math.zero")));
                entry.Key.SetVector("_GFinalFrequency", entry.Value * settings.N("island.sea.frequencyScale"));
                entry.Key.SetFloat("_GGlobalFrequency", _seaFrequency[entry.Key] * settings.N("island.sea.frequencyScale"));
                Debug.Log("SANDBOX_LUX_SEA_APPLIED material=" + entry.Key.name + " displacement=" + entry.Key.GetVector("_GerstnerVertexIntensity").ToString("F4") + " frequency=" + entry.Key.GetVector("_GFinalFrequency") + " speed=" + entry.Key.GetVector("_GFinalSpeed"));
            }
        }
        private void OnDestroy()
        {
            _disposed = true;
            _groundShader?.Dispose(); _groundShader = null;
            if (_pendingScene == null || _pendingScene.isDone) _renderPreparation?.Dispose();
            ShutdownInteractions();
            if (_meshVegetation != null) Destroy(_meshVegetation);
            foreach (var layer in _walkingLayers) Physics.IgnoreLayerCollision(layer.Key, _groundLayer, layer.Value);
            foreach (var bundle in _bundles) if (bundle != null) bundle.Unload(false);
            foreach (var material in _water.Keys) if (material != null) Destroy(material);
            if (_terrainData != null) Destroy(_terrainData); if (_terrainMaterial != null) Destroy(_terrainMaterial);
        }
    }
}
