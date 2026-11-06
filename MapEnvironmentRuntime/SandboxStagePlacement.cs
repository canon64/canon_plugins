using System;
using System.Linq;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    // Rotate heightfield data while the native Terrain transform remains axis aligned.
    public sealed class SandboxStagePlacement : MonoBehaviour
    {
        private Transform _map;
        private Terrain _terrain;
        private TerrainCollider _collider;
        private TerrainData _original, _rotated;
        private Vector3 _mapPosition, _terrainPosition, _size;
        private Quaternion _mapRotation;
        private float _yaw;
        private Quaternion _placementRotation;
        private bool _applied;
        public Vector3 BaseToWorld(Vector3 position) => transform.position + _placementRotation * position;
        public void Initialize(Transform map, Terrain terrain)
        {
            _map = map; _terrain = terrain; _collider = terrain.GetComponent<TerrainCollider>();
            _original = terrain.terrainData; _size = _original.size;
            _mapPosition = map.position; _mapRotation = map.rotation; _terrainPosition = terrain.transform.position;
            map.SetParent(transform, true); terrain.transform.SetParent(transform, true);
            terrain.transform.localScale = Vector3.one; terrain.transform.rotation = Quaternion.identity;
        }
        public void Apply(Parameters p)
        {
            float yaw = p.N("island.stage.yaw");
            var rotation = Quaternion.Euler(p.N("math.zero"), yaw, p.N("math.zero"));
            if (!_applied || yaw != _yaw)
            {
                if (yaw == p.N("math.zero"))
                {
                    _terrain.terrainData = _original; _collider.terrainData = _original;
                    _terrain.transform.localPosition = _terrainPosition;
                    if (_rotated != null) { Release(_rotated); _rotated = null; }
                }
                else
                {
                    var corners = new[] { _terrainPosition, _terrainPosition + new Vector3(_size.x, p.N("math.zero"), p.N("math.zero")),
                        _terrainPosition + new Vector3(p.N("math.zero"), p.N("math.zero"), _size.z), _terrainPosition + new Vector3(_size.x, p.N("math.zero"), _size.z) }.Select(point => rotation * point).ToArray();
                    Vector3 min = corners.Aggregate(Vector3.Min), max = corners.Aggregate(Vector3.Max);
                    var size = new Vector3(max.x - min.x, _size.y, max.z - min.z);
                    TerrainData next = RotateData(p, min, size, Quaternion.Inverse(rotation), yaw);
                    _terrain.terrainData = next; _collider.terrainData = next;
                    _terrain.transform.localPosition = min;
                    var old = _rotated; _rotated = next; if (old != null) Release(old);
                }
                _yaw = yaw;
                var textures = _terrain.terrainData.alphamapTextures;
                for (int index = p.I("math.zero"); index < textures.Length; index++)
                {
                    string property = p.T("island.stage.splatPropertyPrefix") + (index + p.I("math.one"));
                    if (_terrain.materialTemplate.HasProperty(property)) _terrain.materialTemplate.SetTexture(property, textures[index]);
                }
            }
            transform.position = p.V("island.stage.position");
            _map.localPosition = rotation * _mapPosition; _map.localRotation = rotation * _mapRotation;
            _applied = true; _terrain.Flush(); Physics.SyncTransforms();
            _placementRotation = rotation;
        }
        private Vector2 SourceUv(int x, int z, int width, int height, Vector3 min, Vector3 size, Quaternion inverse, Parameters p, bool centers)
        {
            float offset = centers ? p.N("math.half") : p.N("math.zero");
            float tail = centers ? p.N("math.zero") : p.N("math.one");
            var world = min + new Vector3((x + offset) / (width - tail) * size.x, p.N("math.zero"), (z + offset) / (height - tail) * size.z);
            var point = inverse * world - _terrainPosition;
            return new Vector2(point.x / _size.x, point.z / _size.z);
        }
        private bool Inside(Vector2 uv, Parameters p) => uv.x >= p.N("math.zero") && uv.y >= p.N("math.zero") && uv.x <= p.N("math.one") && uv.y <= p.N("math.one");
        private static int Nearest(float uv, int count, Parameters p) => Mathf.Clamp(Mathf.FloorToInt(uv * count), p.I("math.zero"), count - p.I("math.one"));
        private TerrainData RotateData(Parameters p, Vector3 min, Vector3 size, Quaternion inverse, float yaw)
        {
            var next = Instantiate(_original);
            try
            {
                next.size = size;
                int resolution = _original.heightmapResolution;
                var heights = _original.GetHeights(p.I("math.zero"), p.I("math.zero"), resolution, resolution);
                var rotatedHeights = new float[resolution, resolution];
                for (int z = p.I("math.zero"); z < resolution; z++) for (int x = p.I("math.zero"); x < resolution; x++)
                {
                    var uv = SourceUv(x, z, resolution, resolution, min, size, inverse, p, false);
                    rotatedHeights[z, x] = Inside(uv, p) ? Sample(heights, uv, p) : p.N("math.zero");
                }
                next.SetHeights(p.I("math.zero"), p.I("math.zero"), rotatedHeights);
                int holesResolution = _original.holesResolution;
                var holes = _original.GetHoles(p.I("math.zero"), p.I("math.zero"), holesResolution, holesResolution);
                var rotatedHoles = new bool[holesResolution, holesResolution];
                for (int z = p.I("math.zero"); z < holesResolution; z++) for (int x = p.I("math.zero"); x < holesResolution; x++)
                {
                    var uv = SourceUv(x, z, holesResolution, holesResolution, min, size, inverse, p, true);
                    rotatedHoles[z, x] = Inside(uv, p) && holes[Nearest(uv.y, holesResolution, p), Nearest(uv.x, holesResolution, p)];
                }
                next.SetHoles(p.I("math.zero"), p.I("math.zero"), rotatedHoles);
                var alpha = _original.GetAlphamaps(p.I("math.zero"), p.I("math.zero"), _original.alphamapWidth, _original.alphamapHeight);
                var rotatedAlpha = new float[_original.alphamapHeight, _original.alphamapWidth, _original.alphamapLayers];
                for (int z = p.I("math.zero"); z < _original.alphamapHeight; z++) for (int x = p.I("math.zero"); x < _original.alphamapWidth; x++)
                {
                    var uv = SourceUv(x, z, _original.alphamapWidth, _original.alphamapHeight, min, size, inverse, p, true);
                    int sourceX = Nearest(uv.x, _original.alphamapWidth, p), sourceZ = Nearest(uv.y, _original.alphamapHeight, p);
                    for (int layer = p.I("math.zero"); layer < _original.alphamapLayers; layer++) rotatedAlpha[z, x, layer] = alpha[sourceZ, sourceX, layer];
                }
                next.SetAlphamaps(p.I("math.zero"), p.I("math.zero"), rotatedAlpha);
                for (int layer = p.I("math.zero"); layer < _original.detailPrototypes.Length; layer++)
                {
                    var detail = _original.GetDetailLayer(p.I("math.zero"), p.I("math.zero"), _original.detailWidth, _original.detailHeight, layer);
                    var rotatedDetail = new int[_original.detailHeight, _original.detailWidth];
                    for (int z = p.I("math.zero"); z < _original.detailHeight; z++) for (int x = p.I("math.zero"); x < _original.detailWidth; x++)
                    {
                        var uv = SourceUv(x, z, _original.detailWidth, _original.detailHeight, min, size, inverse, p, true);
                        if (Inside(uv, p)) rotatedDetail[z, x] = detail[Nearest(uv.y, _original.detailHeight, p), Nearest(uv.x, _original.detailWidth, p)];
                    }
                    next.SetDetailLayer(p.I("math.zero"), p.I("math.zero"), layer, rotatedDetail);
                }
                var rotation = Quaternion.Inverse(inverse); var trees = _original.treeInstances;
                for (int index = p.I("math.zero"); index < trees.Length; index++)
                {
                    var tree = trees[index];
                    var position = rotation * (_terrainPosition + Vector3.Scale(tree.position, _size)) - min;
                    tree.position = new Vector3(position.x / size.x, position.y / size.y, position.z / size.z);
                    tree.rotation += yaw * p.N("math.pi") / p.N("math.degreesHalfTurn"); trees[index] = tree;
                }
                next.SetTreeInstances(trees, false);
                return next;
            }
            catch { Release(next); throw; }
        }
        private static float Sample(float[,] values, Vector2 uv, Parameters p)
        {
            float x = uv.x * (values.GetLength(p.I("math.one")) - p.I("math.one")), z = uv.y * (values.GetLength(p.I("math.zero")) - p.I("math.one"));
            int left = Mathf.FloorToInt(x), bottom = Mathf.FloorToInt(z), right = Mathf.Min(left + p.I("math.one"), values.GetLength(p.I("math.one")) - p.I("math.one")), top = Mathf.Min(bottom + p.I("math.one"), values.GetLength(p.I("math.zero")) - p.I("math.one"));
            return Mathf.Lerp(Mathf.Lerp(values[bottom, left], values[bottom, right], x - left), Mathf.Lerp(values[top, left], values[top, right], x - left), z - bottom);
        }
        private static void Release(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() { if (_rotated != null) Release(_rotated); }
    }
}
