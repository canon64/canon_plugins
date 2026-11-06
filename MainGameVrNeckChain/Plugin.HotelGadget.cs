using System;
using System.Linq;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameVrNeckChain
{
    public sealed partial class Plugin
    {
        [Serializable] private sealed class HotelChainAttachment
        {
            public string schema = "chain-v1";
            public Vector3 rootLocal;
        }
        public bool HotelGadgetAllowed => PluginSessionGate.Allowed && isActiveAndEnabled;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("鎖はこのHでは許可されていません。");
            if (visible && _chain == null) SpawnChain(); else if (!visible && _chain != null) ClearChain();
            if (visible && _chain == null) throw new InvalidOperationException("鎖のモデルを読み込めません。");
        }
        public string CaptureHotelGadget(Transform room)
        {
            var data = GadgetRoomSnapshot.Capture(room, _chain == null ? null : _chain.transform, _collar == null ? null : _collar.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config);
            if (data.visible)
            {
                data.collarAttached = _collarAttached; data.rootAttached = _rootAttached;
                data.tipPinned = _releasedTipPinned || _grabController != null || _desktopGizmoProxy != null;
                data.tipPosition = room.InverseTransformPoint(TipTarget);
                data.points = _bones.Select(b => room.InverseTransformPoint(b.position)).ToArray();
                data.runtime = JsonUtility.ToJson(new HotelChainAttachment { rootLocal = _attachedRootLocal });
            }
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!HotelGadgetAllowed) throw new InvalidOperationException("鎖はこのHでは許可されていません。");
            GadgetConfigSnapshot.Apply(Config, data.settings);
            if (data.visible && (data.points == null || data.points.Length != _bones.Length || data.poses.Length != new[] { "chain", "collar" }.Length))
                throw new ArgumentException("鎖の形状データが不正です。");
            SetHotelGadgetVisible(data.visible); if (!data.visible) return;
            DetachDesktopGizmo(); DetachRootGizmo(); DetachCollarGizmo(); ReleaseGrip(); ReleaseRootGrip(); ReleaseCollarGrip();
            _releasedTipPinned = false;
            using (var poses = data.poses.AsEnumerable().GetEnumerator())
            { poses.MoveNext(); poses.Current.Apply(room, _chain.transform); poses.MoveNext(); poses.Current.Apply(room, _collar.transform); }
            // Resolve the saved root relative to the saved collar BEFORE attaching
            // the collar to the current character. A world-space root applied to
            // a newly posed neck otherwise moves again on the next LateUpdate.
            var savedCollarPosition = _collar.transform.position;
            var savedCollarRotation = _collar.transform.rotation;
            var savedCollarScale = _collar.transform.lossyScale;
            _collarAttached = data.collarAttached; _rootAttached = data.rootAttached;
            if (_collarAttached) UpdateCollarPlacement();
            else _collar.transform.SetParent(null, true);
            _attachedRootLocal = SavedRootLocal(room, data);
            _collarPreviousPosition = _collar.transform.position;
            _rootSnapArmed = !_rootAttached; _collarSnapArmed = !_collarAttached;

            // Copy every saved point, using the attachment as its frame when the
            // chain belongs to the collar. This also supports old v1 snapshots.
            for (int i = 0; i < data.points.Length; ++i)
            {
                var world = room.TransformPoint(data.points[i]);
                if (_collarAttached && _rootAttached)
                    world = _collar.transform.TransformPoint(ChainRestoreGeometry.ToLocal(
                        world, savedCollarPosition, savedCollarRotation, savedCollarScale));
                _points[i] = _previous[i] = world;
            }
            _chainLength = 0f;
            for (int i = 0; i < _segments.Length; ++i)
            {
                _segments[i] = Vector3.Scale(_baseSegmentVectors[i], _chain.transform.localScale).magnitude;
                _chainLength += _segments[i];
            }
            _customSimulation = true; if (_dynamicBone != null) _dynamicBone.enabled = false;
            _rootAttachPending = false; _collarAttachPending = false;
            _releasedTipPosition = room.TransformPoint(data.tipPosition); _releasedTipPinned = data.tipPinned;
            // A room-fixed tip stays room-fixed while the collar is reattached.
            // Project once with zero time; restoration must not add gravity or
            // carry the previous room's Verlet velocity into the new scene.
            ChainSimulation.Step(_points, _previous, _segments,
                _rootAttached ? _collar.transform.TransformPoint(_attachedRootLocal) : _points[0],
                _releasedTipPosition, _rootAttached, _releasedTipPinned, 0f, 0f,
                null, _releasedTipPinned);
            Array.Copy(_points, _previous, _points.Length);
            ApplyChainPose();
        }

        // Rope particles and their world root are animated, so raw JSON equality
        // cannot establish whether a saved attachment was restored. Verify the
        // actual neck/root/tip constraints and the rendered particle positions.
        public bool VerifyHotelGadgetPlacement(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible) return _chain == null && _collar == null;
            if (_chain == null || _collar == null || data.poses.Length != 2 ||
                data.points == null || data.points.Length != _bones.Length ||
                _collarAttached != data.collarAttached || _rootAttached != data.rootAttached ||
                _releasedTipPinned != data.tipPinned) return false;
            const float tolerance = .003f;
            if ((_chain.transform.localScale - ChainScaleVector()).magnitude > tolerance ||
                (_collar.transform.localScale - CollarScaleVector()).magnitude > tolerance) return false;
            if (_collarAttached && (_collar.transform.parent != _collarBone ||
                (_collar.transform.position - CollarHome()).magnitude > tolerance ||
                Quaternion.Angle(_collar.transform.localRotation, Quaternion.Euler(
                    _collarRotationX.Value, _collarRotationY.Value, _collarRotationZ.Value)) > .2f)) return false;
            var savedRootLocal = SavedRootLocal(room, data);
            if (_rootAttached && ((_attachedRootLocal - savedRootLocal).magnitude > tolerance ||
                (_bones[0].position - _collar.transform.TransformPoint(_attachedRootLocal)).magnitude > tolerance)) return false;
            if (data.tipPinned && ((_releasedTipPosition - room.TransformPoint(data.tipPosition)).magnitude > tolerance ||
                (_bones[BoneCount - 1].position - _releasedTipPosition).magnitude > tolerance)) return false;
            for (int i = 0; i < _bones.Length; ++i)
                if (_bones[i] == null || !ChainRestoreGeometry.Finite(_bones[i].position) ||
                    (_customSimulation && (_bones[i].position - _points[i]).magnitude > tolerance)) return false;
            return true;
        }

        private Vector3 SavedRootLocal(Transform room, GadgetRoomSnapshot data)
        {
            if (!string.IsNullOrEmpty(data.runtime))
            {
                var saved = JsonUtility.FromJson<HotelChainAttachment>(data.runtime);
                if (saved == null || saved.schema != "chain-v1" || !ChainRestoreGeometry.Finite(saved.rootLocal))
                    throw new ArgumentException("鎖の保存接続位置が不正です。");
                return saved.rootLocal;
            }
            var pose = data.poses[1];
            var savedScale = pose.scale;
            if (!pose.worldScale && data.collarAttached)
                savedScale = Vector3.Scale(savedScale, _collarBone.lossyScale);
            var local = ChainRestoreGeometry.ToLocal(room.TransformPoint(data.points[0]),
                room.TransformPoint(pose.position), room.rotation * pose.rotation.normalized, savedScale);
            // Old captures could mix a post-IK collar with pre-IK rope points.
            // An attached root metres from the collar is not a valid attachment.
            // Recover it from the saved ring/placement settings, not that stale
            // particle. Valid legacy contact points retain their exact offset.
            var anchor = NeckAnchor();
            return ChainRestoreGeometry.ResolveLegacyRoot(local, _collar.transform.TransformPoint(local),
                _collar.transform.InverseTransformPoint(anchor), anchor, _chainAttachRadius.Value, data.rootAttached);
        }

        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible) return _chain == null && _collar == null;
            if (_chain == null || _collar == null || _rootAttached != data.rootAttached) return false;
            return !data.rootAttached || (_attachedRootLocal - SavedRootLocal(room, data)).magnitude <= .003f;
        }
    }
}
