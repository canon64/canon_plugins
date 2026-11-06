using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapEnvironmentRuntime
{
    public sealed class IslandVisibilityStatus
    {
        public bool enabled;
        public int groups, occluders, hiddenGroups, hiddenRenderers;
        public long rays, tested, viewResets;
        public double scanCpuMilliseconds;
    }
    public sealed class SandboxIslandVisibility : MonoBehaviour
    {
        private sealed class Entry { public Renderer[] renderers; public ShadowCastingMode[] shadows; public bool[] forced; public Bounds bounds; public int blocked; }
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly HashSet<int> _occluders = new HashSet<int>();
        private readonly HashSet<Entry> _hidden = new HashSet<Entry>();
        private Parameters _settings;
        private Camera _camera;
        private Plane[] _planes;
        private RaycastHit[] _hits;
        private Matrix4x4 _matrix;
        private Vector3 _origin;
        private int _cursor;
        private bool _applied;
        private readonly IslandVisibilityStatus _status = new IslandVisibilityStatus();
        public void BindCamera(Camera camera)
        {
            if (ReferenceEquals(_camera, camera)) return;
            _camera = camera;
            _cursor = default(int);
            // Restore the previous camera's temporary overrides before forgetting its occlusion results.
            ResetView();
        }
        public void Initialize(Camera camera, Parameters settings)
        {
            _camera = camera; Configure(settings);
            var assigned = new HashSet<Renderer>();
            foreach (var lod in GetComponentsInChildren<LODGroup>(true))
            {
                var renderers = lod.GetLODs().SelectMany(level => level.renderers).Where(item => item != null).Distinct().ToArray();
                Add(renderers); foreach (var renderer in renderers) assigned.Add(renderer);
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) if (!assigned.Contains(renderer)) Add(new[] { renderer });
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger || collider.GetComponentInParent<Animator>() != null || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)) continue;
                if (collider is TerrainCollider) { _occluders.Add(collider.GetInstanceID()); continue; }
                var renderer = collider.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var materials = renderer.sharedMaterials;
                if (materials.Any() && materials.All(material => material != null && material.renderQueue <= settings.N("island.visibility.opaqueQueue") && !material.IsKeywordEnabled("_ALPHATEST_ON") && !material.IsKeywordEnabled("_ALPHABLEND_ON") && (material.GetTag("RenderType", false, "") == "Opaque" || string.IsNullOrEmpty(material.GetTag("RenderType", false, ""))))) _occluders.Add(collider.GetInstanceID());
            }
            Rebuild(); Camera.onPreCull += BeforeCamera; Camera.onPostRender += AfterCamera;
            Debug.Log("SANDBOX_VISIBILITY_READY groups=" + _entries.Count + " occluders=" + _occluders.Count);
        }
        private void Add(Renderer[] renderers)
        {
            if (!renderers.Any() || renderers.Any(renderer => renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer.GetComponentInParent<Animator>() != null)) return;
            _entries.Add(new Entry { renderers = renderers, shadows = renderers.Select(renderer => renderer.shadowCastingMode).ToArray(), forced = renderers.Select(renderer => renderer.forceRenderingOff).ToArray() });
        }
        public void Configure(Parameters settings)
        {
            Restore(); _settings = settings;
            _planes = new Plane[settings.I("island.visibility.planeCount")]; _hits = new RaycastHit[settings.I("island.visibility.hitCapacity")];
            ResetView();
            if (_entries.Any()) Rebuild();
        }
        private void Rebuild()
        {
            _matrix = transform.localToWorldMatrix;
            foreach (var entry in _entries)
            {
                entry.bounds = entry.renderers.First().bounds;
                foreach (var renderer in entry.renderers) entry.bounds.Encapsulate(renderer.bounds);
            }
            Physics.SyncTransforms(); ResetView();
        }
        private void ResetView()
        {
            Restore(); _hidden.Clear(); foreach (var entry in _entries) entry.blocked = default(int);
            if (_camera != null) _origin = _camera.transform.position;
            _status.viewResets++;
        }
        private void LateUpdate()
        {
            if (_camera == null || _settings == null) return;
            long started = Stopwatch.GetTimestamp();
            if (_matrix != transform.localToWorldMatrix) Rebuild();
            if (Vector3.Distance(_origin, _camera.transform.position) > _settings.N("island.visibility.viewTolerance")) ResetView();
            if (_settings.N("island.visibility.enabled") > _settings.N("math.zero")) Scan();
            _status.scanCpuMilliseconds = (Stopwatch.GetTimestamp() - started) * _settings.N("units.millisecondsPerSecond") / Stopwatch.Frequency;
        }
        private void Scan()
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
            int scans = default(int); long initialRays = _status.rays;
            while (_entries.Count > default(int) && scans++ < Mathf.Min(_entries.Count, _settings.I("island.visibility.scanBudget")) && _status.rays - initialRays <= _settings.I("island.visibility.rayBudget") - _settings.I("island.visibility.probeCount"))
            {
                var entry = _entries[_cursor]; _cursor = (_cursor + _settings.I("math.one")) % _entries.Count;
                if (_hidden.Contains(entry) || !GeometryUtility.TestPlanesAABB(_planes, entry.bounds) || entry.bounds.SqrDistance(_origin) < Mathf.Pow(_settings.N("island.visibility.nearDistance"), _settings.N("math.two"))) continue;
                _status.tested++;
                if (!Covered(entry)) { entry.blocked = default(int); continue; }
                if (++entry.blocked >= _settings.I("island.visibility.confirmations")) _hidden.Add(entry);
            }
        }
        private bool Covered(Entry entry)
        {
            var bounds = entry.bounds; bounds.Expand(_settings.N("island.visibility.viewTolerance") * _settings.N("math.two"));
            float distance = Mathf.Sqrt(bounds.SqrDistance(_origin)) - _settings.N("island.visibility.margin");
            if (distance <= _settings.N("math.zero")) return false;
            for (int index = _settings.I("math.zero"); index < _settings.I("island.visibility.probeCount"); index++)
            {
                var target = bounds.center;
                if (index < _settings.I("island.visibility.cornerCount")) target += Vector3.Scale(bounds.extents, new Vector3((index & _settings.I("math.one")) == default(int) ? -_settings.N("math.one") : _settings.N("math.one"), (index & _settings.I("math.two")) == default(int) ? -_settings.N("math.one") : _settings.N("math.one"), (index & _settings.I("island.visibility.cornerZMask")) == default(int) ? -_settings.N("math.one") : _settings.N("math.one")));
                _status.rays++;
                int count = Physics.RaycastNonAlloc(_origin, (target - _origin).normalized, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == _hits.Length) return false;
                bool blocked = false;
                for (int hit = _settings.I("math.zero"); hit < count; hit++)
                {
                    var collider = _hits[hit].collider;
                    if (collider != null && _occluders.Contains(collider.GetInstanceID()) && !entry.renderers.Contains(collider.GetComponent<Renderer>())) { blocked = true; break; }
                }
                if (!blocked) return false;
            }
            return true;
        }
        private void BeforeCamera(Camera camera)
        {
            Restore();
            if (!isActiveAndEnabled || camera != _camera || _settings.N("island.visibility.enabled") <= _settings.N("math.zero")) return;
            foreach (var entry in _hidden) for (int index = _settings.I("math.zero"); index < entry.renderers.Length; index++)
            {
                var renderer = entry.renderers[index]; if (renderer == null) continue;
                if (entry.shadows[index] == ShadowCastingMode.Off) renderer.forceRenderingOff = true;
                else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            _applied = true;
        }
        private void AfterCamera(Camera camera) { if (camera == _camera) Restore(); }
        private void Restore()
        {
            if (!_applied) return;
            foreach (var entry in _hidden) for (int index = _settings.I("math.zero"); index < entry.renderers.Length; index++) if (entry.renderers[index] != null) { entry.renderers[index].shadowCastingMode = entry.shadows[index]; entry.renderers[index].forceRenderingOff = entry.forced[index]; }
            _applied = false;
        }
        public IslandVisibilityStatus Snapshot()
        {
            _status.enabled = _settings.N("island.visibility.enabled") > _settings.N("math.zero"); _status.groups = _entries.Count; _status.occluders = _occluders.Count;
            _status.hiddenGroups = _hidden.Count; _status.hiddenRenderers = _hidden.Sum(entry => entry.renderers.Length); return _status;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Camera.onPreCull -= BeforeCamera; Camera.onPostRender -= AfterCamera; Restore(); }
    }
}
