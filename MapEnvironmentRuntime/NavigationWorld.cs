using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace MapEnvironmentRuntime
{
    public sealed class NavigationWaypoint
    {
        public string Id { get; internal set; }
        public string SetName { get; internal set; }
        public int MapId { get; internal set; }
        public int AreaId { get; internal set; }
        public Vector3 Position { get; internal set; }
    }
    public sealed class NavigationStatus
    {
        public bool ready;
        public int sourceMeshes, excludedMeshes, acceptedWaypoints, rejectedWaypoints, buildSerial;
        public string error;
    }
    // Builds only on entry or an explicit placement/settings change. Native Unity owns path following.
    public sealed class NavigationWorld : IDisposable
    {
        public sealed class Reference { public long m_PathID; }
        public sealed class Rotation { public float x, y, z, w; }
        public sealed class SourceTransform
        {
            public Vector3 m_LocalPosition, m_LocalScale;
            public Rotation m_LocalRotation;
            public Reference m_Father;
        }
        public sealed class SourceNode
        {
            public long id, meshId;
            public string name;
            public bool active, tagged, ignore;
            public int area;
            public SourceTransform transform;
        }
        public sealed class SourceMesh { public long pathId; public string name; public Vector3[] vertices; public int[] triangles; }
        public sealed class SourceDocument { public long rootTransform; public SourceNode[] nodes; public SourceMesh[] meshes; }
        public sealed class WaypointSet { public string name; public int mapId, areaId; public Vector3[] points; }
        public sealed class WaypointDocument { public WaypointSet[] sets; }
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<NavigationWaypoint> _waypoints = new List<NavigationWaypoint>();
        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        private NavMeshBuildSettings _build;
        private bool _ownsSettings;
        private Parameters _settings;
        private Func<IReadOnlyList<Bounds>> _blockedVolumes;
        private Bounds[] _movementBlockers = Array.Empty<Bounds>();
        private Predicate<Collider> _collisionFilter;
        private Collider[] _overlaps;
        private RaycastHit[] _hits;
        public NavigationStatus Status { get; } = new NavigationStatus();
        public IReadOnlyList<NavigationWaypoint> Waypoints => _waypoints;
        public int AgentTypeId => _build.agentTypeID;
        public int AreaMask => _settings.I("navigation.areaMask");
        public event Action Rebuilding;
        public event Action Rebuilt;
        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = AreaMask };
        public void SetBlockedVolumes(Func<IReadOnlyList<Bounds>> blockedVolumes) { _blockedVolumes = blockedVolumes; RefreshBlockedVolumes(); }
        public void RefreshBlockedVolumes()
        {
            Physics.SyncTransforms();
            _movementBlockers = _blockedVolumes?.Invoke()?.ToArray() ?? Array.Empty<Bounds>();
        }
        public Vector3 ConstrainMovement(Vector3 origin, Vector3 target, float radius, float height, out bool blocked)
        {
            blocked = false;
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance <= _settings.N("render.epsilon")) return target;
            Vector3 center = origin + Vector3.up * height * _settings.N("math.half");
            var ray = new Ray(center, delta / distance);
            float allowed = distance;
            foreach (var source in _movementBlockers)
            {
                var bounds = source;
                bounds.Expand(new Vector3(radius * _settings.N("math.two"), height, radius * _settings.N("math.two")));
                if (bounds.Contains(center))
                {
                    if (Vector3.Dot(delta, center - bounds.center) >= _settings.N("math.zero")) continue;
                    allowed = _settings.N("math.zero"); blocked = true; continue;
                }
                if (!bounds.IntersectRay(ray, out float along) || along > allowed) continue;
                allowed = Mathf.Max(_settings.N("math.zero"), along - _settings.N("movement.skin"));
                blocked = true;
            }
            return blocked ? origin + delta * (allowed / distance) : target;
        }
        public void SetCollisionFilter(Predicate<Collider> collisionFilter) { _collisionFilter = collisionFilter; }
        public void Build(Parameters p, string sourcePath, string waypointPath, Func<Vector3, Vector3> fromSource)
        {
            Rebuilding?.Invoke(); Clear(); _settings = p;
            try
            {
                var source = JsonCodec.FromJson<SourceDocument>(File.ReadAllText(sourcePath));
                var points = JsonCodec.FromJson<WaypointDocument>(File.ReadAllText(waypointPath));
                if (source?.nodes == null || source.meshes == null || points?.sets == null) throw new InvalidDataException("Incomplete navigation source document");
                var nodes = source.nodes.ToDictionary(item => item.id);
                var meshes = source.meshes.ToDictionary(item => item.pathId);
                var matrices = new Dictionary<long, Matrix4x4>(); var active = new Dictionary<long, bool>();
                var visiting = new HashSet<long>();
                foreach (var node in source.nodes) Resolve(node, nodes, matrices, active, visiting);
                var sources = new List<NavMeshBuildSource>(); Bounds bounds = default(Bounds); bool haveBounds = false;
                foreach (var node in source.nodes.Where(item => item.tagged && active[item.id] && !item.ignore))
                {
                    if (!meshes.TryGetValue(node.meshId, out var geometry) || geometry.vertices == null || !geometry.vertices.Any() || geometry.triangles == null || !geometry.triangles.Any()) throw new InvalidDataException("Navigation mesh missing: " + node.name);
                    if (node.area != p.I("navigation.walkableArea") && node.area != p.I("navigation.sourceSpecialArea")) throw new InvalidDataException("Unknown source navigation area: " + node.area);
                    var vertices = geometry.vertices.Select(point => fromSource(matrices[node.id].MultiplyPoint3x4(point))).ToArray();
                    foreach (var vertex in vertices)
                    {
                        if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) || float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z)) throw new InvalidDataException("Non-finite navigation vertex");
                        if (!haveBounds) { bounds = new Bounds(vertex, Vector3.zero); haveBounds = true; } else bounds.Encapsulate(vertex);
                    }
                    var mesh = new Mesh { name = "AI Navigation " + geometry.name, vertices = vertices, triangles = geometry.triangles };
                    mesh.RecalculateBounds(); _meshes.Add(mesh);
                    bool special = node.area == p.I("navigation.sourceSpecialArea");
                    int area = special ? p.I("navigation.sourceSpecialTargetArea") : p.I("navigation.walkableArea");
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity, area = area });
                    if (area == p.I("navigation.excludedArea")) Status.excludedMeshes++;
                }
                if (!haveBounds) throw new InvalidDataException("No active navigation geometry");
                _build = NavMesh.CreateSettings(); _ownsSettings = true;
                _build.agentHeight = p.N("navigation.agentHeight"); _build.agentRadius = p.N("navigation.agentRadius");
                _build.agentClimb = p.N("navigation.agentClimb"); _build.agentSlope = p.N("navigation.agentSlope");
                _build.overrideVoxelSize = true; _build.voxelSize = p.N("navigation.voxelSize");
                _build.overrideTileSize = true; _build.tileSize = p.I("navigation.tileSize"); _build.minRegionArea = p.N("navigation.minRegionArea");
                _overlaps = new Collider[p.I("navigation.physical.hitCapacity")]; _hits = new RaycastHit[_overlaps.Length];
                bounds.Expand(p.N("navigation.projectRadius") * p.N("math.two"));
                _data = NavMeshBuilder.BuildNavMeshData(_build, sources, bounds, Vector3.zero, Quaternion.identity);
                if (_data == null) throw new InvalidOperationException("Native navigation bake failed");
                _instance = NavMesh.AddNavMeshData(_data);
                if (!_instance.valid) throw new InvalidOperationException("Native navigation registration failed");
                Status.sourceMeshes = sources.Count; Status.ready = true;
                foreach (var set in points.sets)
                {
                    if (set.points == null) throw new InvalidDataException("Navigation waypoint set missing points: " + set.name);
                    int index = p.I("math.zero");
                    foreach (var original in set.points)
                    {
                        string id = set.name + ":" + index; index++;
                        if (!TryProject(fromSource(original), out var position)) { Status.rejectedWaypoints++; continue; }
                        _waypoints.Add(new NavigationWaypoint { Id = id, SetName = set.name, MapId = set.mapId, AreaId = set.areaId, Position = position });
                    }
                }
                Status.acceptedWaypoints = _waypoints.Count; Status.buildSerial++;
                if (!_waypoints.Any()) throw new InvalidOperationException("No valid navigation waypoints after projection");
                RefreshBlockedVolumes();
                Debug.Log("AI_NAVIGATION_READY sources=" + Status.sourceMeshes + " excluded=" + Status.excludedMeshes + " waypoints=" + Status.acceptedWaypoints + " rejected=" + Status.rejectedWaypoints + " agentType=" + AgentTypeId);
                Rebuilt?.Invoke();
            }
            catch (Exception ex) { Clear(); Status.error = ex.ToString(); throw; }
        }
        private static void Resolve(SourceNode node, Dictionary<long, SourceNode> nodes, Dictionary<long, Matrix4x4> matrices, Dictionary<long, bool> active, HashSet<long> visiting)
        {
            if (matrices.ContainsKey(node.id)) return;
            if (!visiting.Add(node.id)) throw new InvalidDataException("Navigation source transform cycle");
            var t = node.transform; if (t?.m_LocalRotation == null || t.m_Father == null) throw new InvalidDataException("Incomplete source transform: " + node.name);
            var rotation = t.m_LocalRotation;
            var matrix = Matrix4x4.TRS(t.m_LocalPosition, new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w), t.m_LocalScale);
            bool enabled = node.active;
            if (nodes.TryGetValue(t.m_Father.m_PathID, out var parent)) { Resolve(parent, nodes, matrices, active, visiting); matrix = matrices[parent.id] * matrix; enabled &= active[parent.id]; }
            matrices.Add(node.id, matrix); active.Add(node.id, enabled); visiting.Remove(node.id);
        }
        public bool TryProject(Vector3 position, out Vector3 projected)
        {
            projected = position;
            if (!Status.ready || !NavMesh.SamplePosition(position, out var hit, _settings.N("navigation.projectRadius"), Filter)) return false;
            if (Mathf.Abs(hit.position.y - position.y) > _settings.N("navigation.maxVerticalProjection")) return false;
            projected = hit.position; return true;
        }
        public bool TryCalculatePath(Vector3 start, Vector3 target, out NavMeshPath path)
        {
            path = new NavMeshPath();
            if (!TryProject(start, out var from) || !TryProject(target, out var to) || !NavMesh.CalculatePath(from, to, Filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!ClearPhysicalPath(path.corners)) return false;
            var blockers = _blockedVolumes?.Invoke();
            if (blockers != null)
            {
                var corners = path.corners;
                for (int index = _settings.I("math.one"); index < corners.Length; index++)
                {
                    Vector3 delta = corners[index] - corners[index - _settings.I("math.one")];
                    float distance = delta.magnitude;
                    if (distance <= _settings.N("render.epsilon")) continue;
                    var ray = new Ray(corners[index - _settings.I("math.one")] + Vector3.up * _settings.N("navigation.agentHeight") * _settings.N("math.half"), delta / distance);
                    foreach (var sourceBounds in blockers)
                    {
                        var block = sourceBounds; block.Expand(_settings.N("navigation.agentRadius") * _settings.N("math.two"));
                        if (block.IntersectRay(ray, out float along) && along <= distance) return false;
                    }
                }
            }
            return true;
        }
        private bool ClearPhysicalPath(Vector3[] corners)
        {
            if (_collisionFilter == null) return true;
            float radius = _settings.N("navigation.agentRadius");
            float lower = radius + _settings.N("navigation.agentClimb") + _settings.N("navigation.physical.clearance");
            float upper = _settings.N("navigation.agentHeight") - radius;
            if (lower > upper) throw new InvalidDataException("Navigation physical probe does not fit the configured body");
            int mask = _settings.I("navigation.physical.layerMask");
            for (int index = _settings.I("math.zero"); index < corners.Length; index++)
            {
                var bottom = corners[index] + Vector3.up * lower;
                var top = corners[index] + Vector3.up * upper;
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
                if (count == _overlaps.Length) return false;
                for (int hit = _settings.I("math.zero"); hit < count; hit++) if (_overlaps[hit] != null && _collisionFilter(_overlaps[hit])) return false;
                if (index == _settings.I("math.zero")) continue;
                var delta = corners[index] - corners[index - _settings.I("math.one")]; float distance = delta.magnitude;
                if (distance <= _settings.N("render.epsilon")) continue;
                count = Physics.CapsuleCastNonAlloc(bottom - delta, top - delta, radius, delta / distance, _hits, distance, mask, QueryTriggerInteraction.Ignore);
                if (count == _hits.Length) return false;
                for (int hit = _settings.I("math.zero"); hit < count; hit++) if (_hits[hit].collider != null && _collisionFilter(_hits[hit].collider)) return false;
            }
            return true;
        }
        public bool ChooseReachableWaypoint(Vector3 origin, ISet<string> reserved, System.Random random, out NavigationWaypoint destination)
        {
            destination = null;
            var candidates = _waypoints.Where(point => (reserved == null || !reserved.Contains(point.Id)) && Vector3.Distance(point.Position, origin) >= _settings.N("navigation.minDestinationDistance") && Vector3.Distance(point.Position, origin) <= _settings.N("navigation.maxDestinationDistance")).ToList();
            int remaining = _settings.I("navigation.candidateBudget");
            while (candidates.Any() && remaining > _settings.I("math.zero"))
            {
                int index = random.Next(candidates.Count); var point = candidates[index]; candidates.RemoveAt(index); remaining--;
                if (!TryCalculatePath(origin, point.Position, out _)) continue;
                destination = point; return true;
            }
            return false;
        }
        private void Clear()
        {
            Status.ready = false; Status.error = null; Status.sourceMeshes = default(int); Status.excludedMeshes = default(int); Status.acceptedWaypoints = default(int); Status.rejectedWaypoints = default(int);
            _waypoints.Clear();
            if (_instance.valid) _instance.Remove();
            if (_data != null) { UnityEngine.Object.DestroyImmediate(_data); _data = null; }
            foreach (var mesh in _meshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            _meshes.Clear();
            if (_ownsSettings) { NavMesh.RemoveSettings(_build.agentTypeID); _ownsSettings = false; }
        }
        public void Dispose() { Rebuilding?.Invoke(); Clear(); _blockedVolumes = null; _movementBlockers = Array.Empty<Bounds>(); _collisionFilter = null; _overlaps = null; _hits = null; }
    }
}
