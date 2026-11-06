using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;

namespace MainGameAiShoujoMode
{
    // Bake the imported floor once, then let the native PlayerMover own all walking.
    internal sealed class NativeActionMapScope : IDisposable
    {
        private readonly ActionScene _action;
        private readonly MethodInfo _setRoot, _setLoading, _setNo, _setLock;
        private readonly GameObject _previousRoot;
        private readonly bool _previousActive, _previousLoading, _previousLock;
        private readonly int _previousNo, _previousPlayerNo;
        private GameObject _root;
        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        private AsyncOperation _build;
        private bool _disposed, _attached, _locked;

        internal NativeActionMapScope(ActionScene action)
        {
            if (action == null || action.Player == null || !action.Player.initialized || action.Map == null)
                throw new InvalidOperationException("The native walking scene is not initialized.");
            if (!NativeActionWorldScope.Owns(Manager.Game.saveData)) throw new InvalidOperationException("The native walking world is not owned.");
            _action = action;
            _setRoot = AccessTools.PropertySetter(typeof(BaseMap), "mapRoot") ?? throw new MissingMethodException("BaseMap.mapRoot setter");
            _setLoading = AccessTools.PropertySetter(typeof(BaseMap), "isMapLoading") ?? throw new MissingMethodException("BaseMap.isMapLoading setter");
            _setNo = AccessTools.PropertySetter(typeof(BaseMap), "no") ?? throw new MissingMethodException("BaseMap.no setter");
            _setLock = AccessTools.PropertySetter(typeof(BaseMap), "_isLock") ?? throw new MissingMethodException("BaseMap map lock setter");
            _previousLock = (bool)(AccessTools.PropertyGetter(typeof(BaseMap), "_isLock") ?? throw new MissingMethodException("BaseMap map lock getter")).Invoke(action.Map, null);
            _previousRoot = action.Map.mapRoot;
            _previousActive = _previousRoot != null && _previousRoot.activeSelf;
            _previousLoading = action.Map.isMapLoading;
            _previousNo = action.Map.no; _previousPlayerNo = action.Player.mapNo;
        }
        internal IEnumerator Attach(GameObject root, Parameters settings, Vector3 feet, Quaternion rotation, IEnumerable<Transform> exclusions = null)
        {
            if (_disposed || _attached || _build != null) throw new InvalidOperationException("Native floor attachment is not available.");
            if (root == null) throw new ArgumentNullException(nameof(root));
            _root = root;
            var colliders = root.GetComponentsInChildren<Collider>(true).Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
            if (!colliders.Any()) throw new InvalidOperationException("The imported map has no active floor collision.");
            var bounds = colliders.First().bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
            bounds.Expand(settings.N("movement.bodyHeight"));
            var sources = new List<NavMeshBuildSource>();
            int mask = ~default(int), cameraCollision = LayerMask.NameToLayer("CameraCollision");
            if (cameraCollision >= default(int)) mask &= ~(settings.I("math.one") << cameraCollision);
            var markups = (exclusions ?? Enumerable.Empty<Transform>()).Where(item => item != null).Distinct()
                .Select(item => new NavMeshBuildMarkup { root = item, ignoreFromBuild = true }).ToList();
            NavMeshBuilder.CollectSources(root.transform, mask, NavMeshCollectGeometry.PhysicsColliders,
                NavMesh.GetAreaFromName("Walkable"), markups, sources);
            if (!sources.Any()) throw new InvalidOperationException("No native walking sources were collected from the imported map.");
            var agentSettings = NavMesh.GetSettingsByID(_action.Player.agent.agentTypeID);
            agentSettings.agentHeight = settings.N("movement.bodyHeight");
            agentSettings.agentRadius = settings.N("movement.bodyRadius");
            agentSettings.agentClimb = settings.N("movement.step");
            agentSettings.agentSlope = settings.N("movement.slopeLimit");
            agentSettings.overrideVoxelSize = true; agentSettings.voxelSize = settings.N("navigation.voxelSize");
            agentSettings.minRegionArea = settings.N("navigation.minRegionArea");
            _data = new NavMeshData(agentSettings.agentTypeID) { name = "Mode native walking floor" };
            _build = NavMeshBuilder.UpdateNavMeshDataAsync(_data, agentSettings, sources, bounds);
            if (_build == null) throw new InvalidOperationException("Native walking floor construction did not start.");
            _build.completed += ignored => { if (_disposed) ReleaseData(); };
            if (!_build.isDone) yield return _build;
            if (_disposed) { ReleaseData(); yield break; }
            if (!NativeActionWorldScope.Owns(Manager.Game.saveData)) throw new InvalidOperationException("The native walking world changed while the floor was loading.");
            _instance = NavMesh.AddNavMeshData(_data);
            if (!_instance.valid) throw new InvalidOperationException("The imported native walking floor could not be registered.");
            if (!NavMesh.SamplePosition(feet, out var start, settings.N("movement.bodyHeight"), NavMesh.AllAreas))
                throw new InvalidOperationException("The selected arrival point is not on the native walking floor.");
            _setLock.Invoke(_action.Map, new object[] { true }); _locked = true;
            _setRoot.Invoke(_action.Map, new object[] { root });
            _attached = true;
            // Keep native clothing/input metadata valid; the floor itself is the imported map.
            var metadata = _action.Map.GetParam(_action.Player.mapNo) ?? _action.Map.infoDic.Values.OrderBy(item => item.No).First();
            _setNo.Invoke(_action.Map, new object[] { metadata.No });
            _action.Player.mapNo = metadata.No;
            _setLoading.Invoke(_action.Map, new object[] { false });
            // Hotel's native Reserve callback can already refer to a child of this floor.
            // That child is part of the incoming map, not an outgoing map to hide.
            if (_previousRoot != null && _previousRoot != root && !_previousRoot.transform.IsChildOf(root.transform)) _previousRoot.SetActive(false);
            _action.Player.agent.radius = agentSettings.agentRadius;
            _action.Player.agent.height = agentSettings.agentHeight;
            _action.Player.SetActive(true);
            if (!_action.Player.agent.Warp(start.position)) throw new InvalidOperationException("The native player could not enter the imported floor.");
            _action.Player.transform.rotation = rotation;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_attached && _action != null && _action.Map != null && _action.Map.mapRoot == _root)
            {
                _setRoot.Invoke(_action.Map, new object[] { _previousRoot });
                _setNo.Invoke(_action.Map, new object[] { _previousNo });
                if (_action.Player != null && _action.Map.GetParam(_previousPlayerNo) != null) _action.Player.mapNo = _previousPlayerNo;
                _setLoading.Invoke(_action.Map, new object[] { _previousLoading });
                if (_previousRoot != null) _previousRoot.SetActive(_previousActive);
            }
            _attached = false; _root = null;
            if (_locked && _action != null && _action.Map != null) _setLock.Invoke(_action.Map, new object[] { _previousLock });
            _locked = false;
            if (_instance.valid) _instance.Remove();
            if (_build == null || _build.isDone) ReleaseData();
        }
        private void ReleaseData()
        {
            if (_data != null) UnityEngine.Object.Destroy(_data);
            _data = null;
        }
    }
}
