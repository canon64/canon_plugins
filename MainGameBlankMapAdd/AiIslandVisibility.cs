using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameBlankMapAdd
{
    // Native distance notifications only. No polling, raycasts, or scan coroutine.
    internal sealed class AiIslandVisibility : MonoBehaviour
    {
        private sealed class Surface
        {
            internal Renderer Renderer;
            internal Material[] Materials;
            internal ShadowCastingMode Shadows;
            internal bool ReceiveShadows;
            internal MeshFilter Filter;
            internal Mesh OriginalMesh, SimpleMesh;
        }
        private sealed class Entry
        {
            internal LODGroup Group;
            internal int LowLevel;
            internal Renderer[] Renderers;
            internal Surface[] Surfaces;
            internal Bounds Bounds;
            internal bool Simple;
        }
        private Entry[] _entries;
        private BoundingSphere[] _spheres;
        private CullingGroup _group;
        private Camera _camera;
        private Matrix4x4 _matrix;
        private AiIslandLodMeshes _meshes;
        internal int SimpleCount { get; private set; }
        internal int AuthoredLodCount { get; private set; }
        internal int PreparedMeshCount { get; private set; }

        internal void Initialize(Camera camera)
        {
            _camera = camera;
            _meshes = new AiIslandLodMeshes(typeof(AiIslandVisibility).Assembly.GetManifestResourceStream("MainGameBlankMapAdd.AiIslandLod.bin"));
            var assigned = new HashSet<Renderer>();
            var entries = new List<Entry>();
            foreach (LODGroup lod in GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = lod.GetLODs();
                Renderer[] renderers = levels.SelectMany(level => level.renderers).Where(r => r != null).Distinct().ToArray();
                if (renderers.Length == 0 || !lod.enabled) continue;
                // A water LOD must retain its authored tessellation and vertex-displacement shader.
                // Mark the whole group assigned so it cannot re-enter as standalone renderers below.
                if (renderers.Any(IsWaterSurface))
                {
                    foreach (Renderer renderer in renderers) assigned.Add(renderer);
                    continue;
                }
                // White/untextured terminal cards are not a usable distant representation.
                // Keep an earlier textured model instead of forcing the last nonempty renderer.
                int last = Array.FindLastIndex(levels, IsUsableLod);
                if (last < 0)
                {
                    foreach (Renderer renderer in renderers) assigned.Add(renderer);
                    continue;
                }
                Entry entry = Create(renderers, last > 0 ? levels[last].renderers : renderers, last <= 0);
                entry.Group = lod; entry.LowLevel = last;
                if (last > 0) AuthoredLodCount++;
                entries.Add(entry);
                foreach (Renderer renderer in renderers) assigned.Add(renderer);
            }
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (!assigned.Contains(renderer) && !IsWaterSurface(renderer) && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    entries.Add(Create(new[] { renderer }, new[] { renderer }, true));
            _entries = entries.ToArray();
            _spheres = new BoundingSphere[_entries.Length];
            _group = new CullingGroup { targetCamera = camera };
            _group.SetDistanceReferencePoint(camera.transform);
            _group.SetBoundingSpheres(_spheres);
            _group.SetBoundingSphereCount(_spheres.Length);
            _group.SetBoundingDistances(new[] { 9f, 10f });
            _group.onStateChanged = Changed;
            RebuildBounds(); EvaluateDistances();
        }

        private Entry Create(Renderer[] renderers, Renderer[] lowRenderers, bool prepareMesh)
        {
            var surfaces = new List<Surface>();
            foreach (Renderer renderer in lowRenderers.Where(r => r != null).Distinct())
            {
                var surface = new Surface { Renderer = renderer, Materials = renderer.sharedMaterials,
                    Shadows = renderer.shadowCastingMode, ReceiveShadows = renderer.receiveShadows };
                surface.Filter = renderer.GetComponent<MeshFilter>();
                if (prepareMesh && surface.Filter != null)
                {
                    surface.OriginalMesh = surface.Filter.sharedMesh;
                    surface.SimpleMesh = _meshes.Get(surface.OriginalMesh);
                    if (surface.SimpleMesh != null) PreparedMeshCount++;
                }
                surfaces.Add(surface);
            }
            return new Entry { Renderers = renderers, Surfaces = surfaces.ToArray() };
        }

        internal static bool IsWaterMaterial(Material source)
        {
            if (source == null) return false;
            string shader = source.shader != null ? source.shader.name ?? string.Empty : string.Empty;
            return shader.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shader.IndexOf("aquas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.HasProperty("_GerstnerVertexIntensity") || source.HasProperty("_GFinalFrequency") ||
                source.HasProperty("_ReflectionTex");
        }

        private static bool IsWaterSurface(Renderer renderer) => renderer.sharedMaterials.Any(IsWaterMaterial);

        internal static bool HasLodTexture(Material source)
        {
            if (source == null || source.shader == null || !source.shader.isSupported) return false;
            foreach (string name in LodTextureProperties)
                if (source.HasProperty(name) && source.GetTexture(name) != null) return true;
            return false;
        }

        private static readonly string[] LodTextureProperties = { "_MainTex", "_BaseMap", "_BaseColorMap", "_AlbedoTex", "_Albedo", "_Texture_Array_Albedo" };
        private static bool IsUsableLod(LOD level)
        {
            Renderer[] renderers = level.renderers.Where(r => r != null).ToArray();
            return renderers.Length > 0 && renderers.All(renderer =>
            {
                Material[] materials = renderer.sharedMaterials;
                return materials.Length > 0 && materials.All(HasLodTexture);
            });
        }

        internal void SetCamera(Camera camera)
        {
            if (_camera == camera || camera == null || _group == null) return;
            _camera = camera; _group.targetCamera = camera;
            _group.SetDistanceReferencePoint(camera.transform); EvaluateDistances();
        }

        // Only a map placement operation needs bounds rebuilt; Unity handles camera motion.
        internal void RequestRefresh()
        {
            if (_group == null || _matrix == transform.localToWorldMatrix) return;
            RebuildBounds(); EvaluateDistances();
        }

        private void RebuildBounds()
        {
            _matrix = transform.localToWorldMatrix;
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                Bounds bounds = entry.Renderers[0].bounds;
                for (int j = 1; j < entry.Renderers.Length; j++) bounds.Encapsulate(entry.Renderers[j].bounds);
                entry.Bounds = bounds;
                _spheres[i] = new BoundingSphere(bounds.center, bounds.extents.magnitude);
            }
        }

        private void EvaluateDistances()
        {
            foreach (Entry entry in _entries)
            {
                float edge = Mathf.Max(0f, Vector3.Distance(_camera.transform.position, entry.Bounds.center) - entry.Bounds.extents.magnitude);
                Apply(entry, edge >= 10f || (entry.Simple && edge >= 9f), true);
            }
        }

        private void Changed(CullingGroupEvent state)
        {
            Entry entry = _entries[state.index];
            if (state.currentDistance >= 2) Apply(entry, true, false);
            else if (state.currentDistance == 0) Apply(entry, false, false);
            // Ignore frustum visibility: turning cannot make scenery disappear.
        }

        private void Apply(Entry entry, bool simple, bool initial)
        {
            if (!initial && entry.Simple == simple) return;
            if (entry.Simple != simple) SimpleCount += simple ? 1 : -1;
            entry.Simple = simple;
            // Force an existing nonempty level, keeping scenery visible beyond native LOD cull thresholds.
            if (entry.Group != null) entry.Group.ForceLOD(simple ? entry.LowLevel : 0);
            foreach (Surface surface in entry.Surfaces)
            {
                Renderer renderer = surface.Renderer;
                if (renderer == null) continue;
                // Preserve the original shader, texture bindings and alpha/cutout behavior.
                renderer.sharedMaterials = surface.Materials;
                renderer.shadowCastingMode = simple ? ShadowCastingMode.Off : surface.Shadows;
                renderer.receiveShadows = !simple && surface.ReceiveShadows;
                if (surface.Filter != null && surface.SimpleMesh != null)
                    surface.Filter.sharedMesh = simple ? surface.SimpleMesh : surface.OriginalMesh;
            }
        }

        internal string Snapshot() => "groups=" + (_entries?.Length ?? 0) + " simple=" + SimpleCount +
            " authoredLodGroups=" + AuthoredLodCount + " preparedMeshInstances=" + PreparedMeshCount +
            " lodStartMeters=10 restoreMeters=9 rays=0 managedScansPerFrame=0";

        private void OnDestroy()
        {
            _group?.Dispose(); _group = null;
            if (_entries != null)
                foreach (Entry entry in _entries)
                {
                    Apply(entry, false, true);
                    if (entry.Group != null) entry.Group.ForceLOD(-1);
                }
            _meshes?.Dispose(); _meshes = null;
        }
    }
}
