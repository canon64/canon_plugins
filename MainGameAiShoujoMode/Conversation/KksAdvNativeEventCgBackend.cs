using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    // Own for the parent conversation's lifetime, and dispose BEFORE actor presentation restoration.
    internal sealed class KksAdvNativeEventCgBackend : KksAdvEventCgHost.IBackend, IDisposable
    {
        private sealed class Graph
        {
            internal GameObject Root;
            internal ADV.EventCG.Data Data;
            internal KksAdvEventCgPlacement.Lease Placement;
            internal readonly List<Action> Restore = new List<Action>();
            internal readonly Dictionary<int, List<GameObject>> Items = new Dictionary<int, List<GameObject>>();
            internal bool Initialized;
        }
        private readonly KksAdvActorRegistry _actors;
        private readonly Camera _camera;
        private readonly KksAdvEventCgPlacement _placement;
        private readonly Func<IReadOnlyList<ChaControl>> _cameraParticipants;
        private readonly Action<bool> _setCameraCorrection;
        private readonly Action _startCrossFade;
        private readonly bool _originalCameraCorrection;
        private readonly KksConversationRuntimeSettings _settings;
        private readonly List<ADV.EventCG.Data.Scene[]> _retired = new List<ADV.EventCG.Data.Scene[]>();
        private Graph _current;
        private bool _disposed, _cameraCorrectionWritten;
        internal bool Active => !_disposed && _current?.Initialized == true;
        internal KksAdvNativeEventCgBackend(KksAdvActorRegistry actors, Camera camera, KksAdvEventCgPlacement placement,
            Func<IReadOnlyList<ChaControl>> cameraParticipants, Func<bool> getCameraCorrection, Action<bool> setCameraCorrection, Action startCrossFade, KksConversationRuntimeSettings settings)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors)); _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _placement = placement ?? throw new ArgumentNullException(nameof(placement)); _cameraParticipants = cameraParticipants ?? throw new ArgumentNullException(nameof(cameraParticipants));
            _setCameraCorrection = setCameraCorrection ?? throw new ArgumentNullException(nameof(setCameraCorrection));
            _originalCameraCorrection = (getCameraCorrection ?? throw new ArgumentNullException(nameof(getCameraCorrection)))();
            _startCrossFade = startCrossFade ?? throw new ArgumentNullException(nameof(startCrossFade)); _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        public void Load(string bundle, string asset)
        {
            Check(); if (_current != null) throw new InvalidOperationException("Previous EventCG must be released before loading another.");
            if (string.IsNullOrEmpty(bundle))
                bundle = CommonLib.GetAssetBundleNameListFromPath("adv/eventcg/", true).OrderByDescending(value => value)
                    .FirstOrDefault(candidate => AssetBundleCheck.GetAllAssetName(candidate, false).Any(name => string.Equals(name, asset, StringComparison.OrdinalIgnoreCase)));
            if (string.IsNullOrEmpty(bundle)) throw new InvalidOperationException("KKS EventCG asset was not found: " + asset);
            var source = new AssetBundleData(bundle, asset);
            try
            {
                var prefab = source.GetAsset<GameObject>();
                if (prefab == null || prefab.GetComponent<ADV.EventCG.Data>() == null) throw new InvalidOperationException("KKS EventCG prefab/data is missing: " + bundle + "/" + asset);
                // Other scripts need an explicit lifecycle adapter before their prefabs can enter this mode.
                foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour == null || (!(behaviour is ADV.EventCG.Data) && !(behaviour is ADV.EventCG.CameraData)))
                        throw new NotSupportedException("KKS EventCG contains an unadapted behaviour: " + (behaviour == null ? "missing script" : behaviour.GetType().FullName));
                var placement = _placement.Place(bundle, asset, prefab);
                try
                {
                    var root = UnityEngine.Object.Instantiate(prefab, placement.Anchor, false); root.name = prefab.name;
                    _current = new Graph { Root = root, Data = root.GetComponent<ADV.EventCG.Data>(), Placement = placement };
                    placement = null;
                }
                finally { placement?.Dispose(); }
                // Prevent native Start from searching ADV.Data.chaRoot or owning reactive subscriptions.
                foreach (var cameraData in _current.Root.GetComponentsInChildren<ADV.EventCG.CameraData>(true)) cameraData.enabled = false;
            }
            finally { source.UnloadBundle(); }
        }
        public void Initialize()
        {
            Check(); var graph = Current();
            if (graph.Initialized) throw new InvalidOperationException("EventCG cannot be initialized twice.");
            graph.Initialized = true;
            var root = graph.Root.transform;
            if (root.childCount > _settings.zero && root.GetChild(_settings.zero).name == ADV.EventCG.Data.ParentNameCamera)
            {
                var point = root.GetChild(_settings.zero); var data = point.GetComponent<ADV.EventCG.CameraData>();
                if (data == null || _camera == null) throw new InvalidOperationException("EventCG camera placement data is missing.");
                var position = _camera.transform.position; var rotation = _camera.transform.rotation; var fieldOfView = _camera.fieldOfView;
                graph.Restore.Add(() => { if (_camera != null) { _camera.fieldOfView = fieldOfView; _camera.transform.SetPositionAndRotation(position, rotation); } });
                _camera.fieldOfView = data.fieldOfView; _camera.transform.SetPositionAndRotation(point.position, point.rotation);
            }
            var positions = new Dictionary<int, Transform>(); int slot = _settings.zero;
            foreach (Transform child in root)
            {
                if (ADV.EventCG.Data.IsCharaPos(child)) { positions.Add(slot, child); slot += _settings.one; }
                else if (child.name == ADV.EventCG.Data.ParentNamePlayer) positions[_settings.advPlayerNumber] = child;
            }
            foreach (var pair in _actors.RegisteredTargets())
            {
                var key = pair.Value.Data is SaveData.Heroine ? pair.Key : _settings.advPlayerNumber;
                if (!positions.TryGetValue(key, out var point)) continue;
                var target = pair.Value.Data.transform != null ? pair.Value.Data.transform : pair.Value.Data.chaCtrl.transform;
                var position = target.position; var rotation = target.rotation;
                graph.Restore.Add(() => { if (target != null) target.SetPositionAndRotation(position, rotation); });
                target.SetPositionAndRotation(point.position, point.rotation); graph.Items.Add(key, new List<GameObject>());
            }
            Next(_settings.zero); RefreshCameraShape();
        }
        internal void RefreshCameraShape()
        {
            Check(); var graph = Current(); var root = graph.Root.transform;
            if (root.childCount <= _settings.zero || root.GetChild(_settings.zero).name != ADV.EventCG.Data.ParentNameCamera) return;
            var point = root.GetChild(_settings.zero); var data = point.GetComponent<ADV.EventCG.CameraData>();
            var participants = _cameraParticipants().Where(actor => actor != null).ToArray();
            var position = point.position; var rotation = point.rotation;
            if (participants.Any())
            {
                var shape = participants.Average(actor => actor.GetShapeBodyValue(_settings.zero));
                var offset = MotionIK.GetShapeLerpPositionValue(shape, Field(data, "_minPos"), Field(data, "_maxPos"));
                var angles = MotionIK.GetShapeLerpAngleValue(shape, Field(data, "_minAng"), Field(data, "_maxAng"));
                var anchor = graph.Placement.Anchor;
                if (anchor == null) throw new InvalidOperationException("EventCG placement frame disappeared.");
                var nativeRotation = Quaternion.Inverse(anchor.rotation) * point.rotation;
                position += anchor.TransformDirection(offset);
                rotation = anchor.rotation * Quaternion.Euler(nativeRotation.eulerAngles + angles);
            }
            if (_camera == null) throw new InvalidOperationException("EventCG target camera disappeared.");
            _camera.transform.SetPositionAndRotation(position, rotation);
        }
        private static Vector3 Field(ADV.EventCG.CameraData data, string name)
        {
            var field = typeof(ADV.EventCG.CameraData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(ADV.EventCG.CameraData).FullName, name);
            return (Vector3)field.GetValue(data);
        }
        public void Next(int scene)
        {
            Check(); var graph = Current(); var targets = _actors.RegisteredTargets();
            var plan = KksAdvEventCgScenePlan.Create(graph.Data.scenes, scene, targets.Keys, _settings.zero, _settings.one);
            foreach (var entry in plan)
            {
                var target = targets[entry.Slot]; target.Presentation.PrepareNativeEventCgMotion();
                var motion = entry.Step.motionAndItem;
                motion.ik.Create(target.Data.chaCtrl, entry.PreviousIk?.ik.motionIK);
                motion.yure.Create(target.Data.chaCtrl, entry.PreviousYure?.yure.yureCtrl);
            }
            foreach (var entry in plan)
            {
                var motion = entry.Step.motionAndItem;
                motion.ik.motionIK.SetPartners(graph.Data.scenes[scene].charas.Select(chara => chara.motionAndItem.ik.motionIK));
                entry.Step.Change(targets[entry.Slot].Data.chaCtrl, graph.Items[entry.Slot]);
            }
        }
        public void ResetHeroineMotions()
        {
            Check();
            foreach (var target in _actors.RegisteredTargets().Values)
                if (target.Data is SaveData.Heroine && target.Data.chaCtrl != null && target.Data.chaCtrl.loadEnd) target.Presentation.ApplyNativeDefaultMotion();
        }
        public void SetCameraCorrection(bool active) { Check(); _cameraCorrectionWritten = true; _setCameraCorrection(active); }
        public void StartCrossFade() { Check(); _startCrossFade(); }
        public bool Release()
        {
            Check(); var graph = _current; _current = null; if (graph == null) return false;
            _retired.Add(graph.Data.scenes); // Motion-continuation references live until the parent conversation ends.
            var errors = new List<Exception>();
            foreach (var item in graph.Items.Values.SelectMany(items => items)) try { if (item != null) UnityEngine.Object.Destroy(item); } catch (Exception exception) { errors.Add(exception); }
            foreach (var restore in graph.Restore) try { restore(); } catch (Exception exception) { errors.Add(exception); }
            try { if (graph.Root != null) { graph.Root.transform.SetParent(null, true); UnityEngine.Object.Destroy(graph.Root); } } catch (Exception exception) { errors.Add(exception); }
            try { graph.Placement?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("EventCG placement/item restoration failed.", errors);
            return true;
        }
        private Graph Current() => _current ?? throw new InvalidOperationException("No EventCG is loaded.");
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(KksAdvNativeEventCgBackend)); }
        internal void EndRun()
        {
            if (_disposed) return;
            var errors = new List<Exception>();
            try { Release(); } catch (Exception exception) { errors.Add(exception); }
            if (_cameraCorrectionWritten) try { _setCameraCorrection(_originalCameraCorrection); _cameraCorrectionWritten = false; } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("EventCG run restoration failed.", errors);
        }
        public void Dispose()
        {
            if (_disposed) return;
            var errors = new List<Exception>();
            try { Release(); } catch (Exception exception) { errors.Add(exception); }
            _disposed = true;
            if (_cameraCorrectionWritten) try { _setCameraCorrection(_originalCameraCorrection); } catch (Exception exception) { errors.Add(exception); }
            var steps = _retired.SelectMany(scenes => scenes).Where(scene => scene != null).SelectMany(scene => scene.charas).Select(chara => chara.motionAndItem).ToArray();
            foreach (var ik in steps.Select(step => step.ik.motionIK).Where(value => value != null).Distinct())
            {
                try { ik.Reset(); } catch (Exception exception) { errors.Add(exception); }
                try { ik.Release(); } catch (Exception exception) { errors.Add(exception); }
            }
            foreach (var yure in steps.Select(step => step.yure.yureCtrl).Where(value => value != null).Distinct())
                try { yure.Release(); } catch (Exception exception) { errors.Add(exception); }
            foreach (var step in steps) foreach (var loader in new AssetBundleData[] { step.motion, step.ik, step.yure })
                try { loader.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            _retired.Clear();
            if (errors.Any()) throw new AggregateException("EventCG conversation resources cleanup failed.", errors);
        }
    }
}
