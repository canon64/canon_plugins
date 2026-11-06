using System;
using System.Collections;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    public sealed class HotelMapManifest
    {
        public string initialFloor;
        public HotelEntryDefinition initialEntry;
        public HotelFloorDefinition[] floors;
        public void Validate()
        {
            if (floors == null || !floors.Any() || floors.Select(f => f.id).Distinct().Count() != floors.Length)
                throw new InvalidDataException("ホテルの階データが不正です。");
            foreach (var f in floors)
            {
                if (f.id != null && f.id.StartsWith("room", StringComparison.Ordinal) && f.room == null)
                    throw new InvalidDataException("客室のフリーH開始定義がありません：" + f.id);
                if (string.IsNullOrWhiteSpace(f.id) || string.IsNullOrWhiteSpace(f.bundleName) || string.IsNullOrWhiteSpace(f.elevatorObject)
                    || string.IsNullOrEmpty(f.bundle) || Path.GetFileName(f.bundle) != f.bundle || string.IsNullOrEmpty(f.scene)
                    || !Finite(f.arrival) || !Finite(f.elevator) || float.IsNaN(f.yaw) || float.IsInfinity(f.yaw)
                    || !floors.Any(t => t.id == f.destination) || f.destination == f.id)
                    throw new InvalidDataException("ホテルの階接続が不正です。");
                foreach (var portal in f.portals ?? Enumerable.Empty<HotelPortalDefinition>())
                    if (string.IsNullOrWhiteSpace(portal.colliderObject) || !floors.Any(t => t.id == portal.destination)
                        || !Finite(portal.arrival) || !Finite(portal.interaction) || float.IsNaN(portal.yaw) || float.IsInfinity(portal.yaw))
                        throw new InvalidDataException("客室の接続が不正です。");
                if (f.room != null && (string.IsNullOrWhiteSpace(f.room.id) || string.IsNullOrWhiteSpace(f.room.label)
                    || !HotelRoomCards.Rooms.Contains(f.room.cardSlot) || string.IsNullOrWhiteSpace(f.room.rootObject) || string.IsNullOrWhiteSpace(f.room.bedObject)))
                    throw new InvalidDataException("客室セッションの定義が不正です。");
            }
            if (!floors.Any(f => f.id == initialFloor)) throw new InvalidDataException("ホテルの開始階がありません。");
            if (initialEntry == null || !Finite(initialEntry.position) || float.IsNaN(initialEntry.yaw) || float.IsInfinity(initialEntry.yaw))
                throw new InvalidDataException("ホテル入口の開始位置が不正です。");
        }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    public sealed class HotelEntryDefinition
    {
        public Vector3 position;
        public float yaw;
    }
    public sealed class HotelFloorDefinition
    {
        public string id, bundle, bundleName, scene, destination, elevatorObject;
        public bool lazyLoad;
        public string[] disabledColliderObjects;
        public Vector3 arrival, elevator;
        public float yaw;
        public HotelPortalDefinition[] portals;
        public HotelSessionRoomDefinition room;
    }
    public sealed class HotelSessionRoomDefinition
    {
        public string id, label, cardSlot, rootObject, bedObject;
    }
    public sealed class HotelPortalDefinition
    {
        public string destination, colliderObject;
        public string[] colliderObjects;
        public Vector3 interaction, arrival;
        public float yaw;
    }
    // One floor owns its scene, but only owns a bundle when it loaded that bundle itself.
    internal sealed partial class HotelFloorRuntime : IDisposable
    {
        internal readonly HotelFloorDefinition Definition;
        internal Scene Scene;
        internal GameObject[] Roots;
        private GameObject _walkingRoot;
        internal GameObject WalkingRoot
        {
            get
            {
                if (_disposed || Roots == null) throw new InvalidOperationException("The hotel floor is not available.");
                if (_walkingRoot != null) return _walkingRoot;
                _walkingRoot = new GameObject("Hotel native walking floor");
                SceneManager.MoveGameObjectToScene(_walkingRoot, Scene);
                foreach (var root in Roots) if (root != null) root.transform.SetParent(_walkingRoot.transform, true);
                return _walkingRoot;
            }
        }
        internal Collider[] ElevatorColliders;
        internal readonly System.Collections.Generic.Dictionary<HotelPortalDefinition,Collider[]> PortalColliders = new System.Collections.Generic.Dictionary<HotelPortalDefinition,Collider[]>();
        private AssetBundle _bundle;
        internal AssetBundle LoadedBundle => _bundle;
        private AssetBundleCreateRequest _bundleRequest;
        private AsyncOperation _sceneRequest;
        private string _scenePath;
        private int[] _scenesBeforeLoad;
        private bool _ownsBundle, _disposed, _pendingBundle, _pendingScene, _sceneRequested;
        private HotelEnvironment _environment;
        private bool[] _rootStates;
        private readonly System.Collections.Generic.List<Material> _roomMaterials = new System.Collections.Generic.List<Material>();
        internal HotelStageRuntime Stage { get; private set; }
        internal HotelFloorRuntime(HotelFloorDefinition definition) { Definition = definition; }
        internal IEnumerator Load(string content, AssetBundle sharedBundle = null, Action<string> trace = null)
        {
            trace?.Invoke("load-start");
            _bundle = sharedBundle != null ? sharedBundle : AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b => b.name == Definition.bundleName
                || b.GetAllScenePaths().Any(path => Path.GetFileNameWithoutExtension(path) == Definition.scene));
            if (_bundle == null)
            {
                _ownsBundle = true; _pendingBundle = true;
                AssetBundleCreateRequest request;
                trace?.Invoke("bundle-request-start");
                try { request = AssetBundle.LoadFromFileAsync(Path.Combine(content, Definition.bundle)); }
                catch { _pendingBundle = false; throw; }
                _bundleRequest = request;
                request.completed += ignored => { trace?.Invoke("bundle-completion-event"); CompleteBundle(request); };
                // Redirectors may return an already-resolved virtual request whose
                // native completed event is not raised. Read the actual result at
                // the operation boundary as well as in the cancellation callback.
                if (!request.isDone) yield return request;
                CompleteBundle(request);
                trace?.Invoke("bundle-wait-complete");
            }
            if (_disposed) yield break;
            if (_bundle == null) throw new InvalidDataException("ホテルのマップを読み込めません：" + Definition.id);
            var path = _bundle.GetAllScenePaths().Single(p => Path.GetFileNameWithoutExtension(p) == Definition.scene);
            _scenePath = path;
            // Guest rooms reuse the same scene asset, but own separate loaded instances.
            _scenesBeforeLoad = Enumerable.Range(default(int), SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).handle).ToArray();
            _pendingScene = true; _sceneRequested = true;
            AsyncOperation operation;
            trace?.Invoke("scene-request-start");
            try { operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive); }
            catch { _pendingScene = false; _sceneRequested = false; throw; }
            _sceneRequest = operation;
            operation.completed += ignored => { trace?.Invoke("scene-completion-event"); CompleteScene(path); };
            if (!operation.isDone) yield return operation;
            CompleteScene(path);
            trace?.Invoke("scene-wait-complete");
            if (_disposed) yield break;
            if (!Scene.IsValid() || !Scene.isLoaded) throw new InvalidDataException("ホテルのシーンがありません。");
            SceneManager.SetActiveScene(Scene); _environment = new HotelEnvironment();
            Roots = Scene.GetRootGameObjects();
            PrepareThirdFloorPortal(content);
            if (Definition.id == "third")
                Stage = new HotelStageRuntime(Roots.Single(r => r.name == "Map").transform, Path.Combine(content, "hotel-stage.json"));
            if (Definition.room?.rootObject == "HotelRoomProps__201")
            {
                // Lighting edits must not change the other rooms using this bundle's materials.
                var copies = new System.Collections.Generic.Dictionary<Material, Material>();
                foreach (var renderer in Roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        if (material == null) return null;
                        if (!copies.TryGetValue(material, out var copy))
                        {
                            copy = new Material(material); copies.Add(material, copy); _roomMaterials.Add(copy);
                        }
                        return copy;
                    }).ToArray();
            }
            _rootStates = Roots.Select(r => r.activeSelf).ToArray();
            foreach (var collider in Roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
                if (Definition.disabledColliderObjects != null && Definition.disabledColliderObjects.Contains(collider.name)) collider.enabled = false;
            var targets = Roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == Definition.elevatorObject).ToArray();
            ElevatorColliders = targets.SelectMany(t => t.GetComponents<Collider>()).ToArray();
            if (!ElevatorColliders.Any()) throw new InvalidDataException("エレベーターの当たり判定がありません：" + Definition.id);
            foreach (var portal in Definition.portals ?? Enumerable.Empty<HotelPortalDefinition>())
            {
                var colliders = Roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                    .Where(c => c.name == portal.colliderObject || (portal.colliderObjects != null && portal.colliderObjects.Contains(c.name))).ToArray();
                if (!colliders.Any()) throw new InvalidDataException("客室扉の当たり判定がありません：" + portal.colliderObject);
                PortalColliders.Add(portal,colliders);
            }
            SetActive(false);
            trace?.Invoke("load-complete");
        }
        private Scene FindScene(string path) => Enumerable.Range(default(int), SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt).FirstOrDefault(scene => scene.isLoaded
                && !_scenesBeforeLoad.Contains(scene.handle)
                && string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase));
        internal void SetActive(bool active)
        {
            if (Roots == null) return;
            if (!active) Stage?.SetActive(false);
            foreach (var root in Roots.Select((item, index) => new { item, enabled = _rootStates[index] }))
                if (root.item != null) root.item.SetActive(active && root.enabled);
            if (active) { SceneManager.SetActiveScene(Scene); _environment.Apply(); Stage?.SetActive(true); }
        }
        internal void ApplyEnvironment()
        {
            if (Scene.IsValid() && Scene.isLoaded) SceneManager.SetActiveScene(Scene);
            _environment?.Apply();
        }
        private void CompleteBundle(AssetBundleCreateRequest request)
        {
            if (!_pendingBundle) return;
            _pendingBundle = false; _bundleRequest = null; _bundle = request.assetBundle;
            if (_disposed) Release();
        }
        private void CompleteScene(string path)
        {
            if (!_pendingScene) return;
            _pendingScene = false; _sceneRequest = null; Scene = FindScene(path);
            if (_disposed) Release();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; SetActive(false);
            Stage?.Dispose(); Stage = null;
            if (_pendingBundle && _bundleRequest != null && _bundleRequest.isDone) CompleteBundle(_bundleRequest);
            if (_pendingScene && _sceneRequest != null && _sceneRequest.isDone) CompleteScene(_scenePath);
            Release();
        }
        private void Release()
        {
            if (_pendingBundle || _pendingScene) return;
            if (_sceneRequested && Scene.IsValid() && Scene.isLoaded)
            {
                _sceneRequested = false;
                var unload = SceneManager.UnloadSceneAsync(Scene);
                if (unload != null) { unload.completed += ignored => Release(); return; }
            }
            foreach (var material in _roomMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            _roomMaterials.Clear();
            if (_ownsBundle && _bundle != null) _bundle.Unload(true);
            _bundle = null;
        }
    }
    internal static class HotelElevatorInteraction
    {
        internal static bool CanUse(Vector3 eye, Vector3 direction, Vector3 elevator, float distance, Collider[] targets)
        {
            return Vector3.Distance(eye, elevator) <= distance
                && Physics.Raycast(eye, direction, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && targets.Contains(hit.collider);
        }
    }
    internal sealed class HotelEnvironment
    {
        private readonly Material _skybox = RenderSettings.skybox;
        private readonly Light _sun = RenderSettings.sun;
        private readonly AmbientMode _mode = RenderSettings.ambientMode;
        private readonly SphericalHarmonicsL2 _ambientProbe = CaptureAmbientProbe();
        private readonly DefaultReflectionMode _reflectionMode = RenderSettings.defaultReflectionMode;
        private readonly Cubemap _customReflection = RenderSettings.customReflection;
        private readonly Color _sky = RenderSettings.ambientSkyColor, _equator = RenderSettings.ambientEquatorColor,
            _ground = RenderSettings.ambientGroundColor, _light = RenderSettings.ambientLight, _fogColor = RenderSettings.fogColor;
        private readonly bool _fog = RenderSettings.fog;
        private readonly FogMode _fogMode = RenderSettings.fogMode;
        private readonly float _density = RenderSettings.fogDensity, _start = RenderSettings.fogStartDistance,
            _end = RenderSettings.fogEndDistance, _reflection = RenderSettings.reflectionIntensity, _intensity = RenderSettings.ambientIntensity;
        private static SphericalHarmonicsL2 CaptureAmbientProbe()
        {
            if (RenderSettings.ambientMode != AmbientMode.Flat) return RenderSettings.ambientProbe;
            // The scene's serialized ambient probe may be empty. H replaces the
            // active lighting context, so retain explicit flat ambient radiance.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientLight);
            return probe;
        }
        internal void Apply()
        {
            RenderSettings.skybox = _skybox; RenderSettings.sun = _sun; RenderSettings.ambientMode = _mode;
            RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _equator; RenderSettings.ambientGroundColor = _ground;
            RenderSettings.ambientLight = _light; RenderSettings.ambientIntensity = _intensity; RenderSettings.reflectionIntensity = _reflection;
            RenderSettings.ambientProbe = _ambientProbe; RenderSettings.defaultReflectionMode = _reflectionMode;
            RenderSettings.customReflection = _customReflection;
            RenderSettings.fog = _fog; RenderSettings.fogColor = _fogColor; RenderSettings.fogMode = _fogMode;
            RenderSettings.fogDensity = _density; RenderSettings.fogStartDistance = _start; RenderSettings.fogEndDistance = _end;
        }
    }
}
