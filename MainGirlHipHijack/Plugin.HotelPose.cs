using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private bool _hotelRoomSession, _hotelRoomRestorePending, _hotelRoomPoseOwned;
        [Serializable] private sealed class HotelHipState
        {
            public int version = 1;
            public string[] points;
            public string headAndHands;
            public bool speedHijack, cutSpeed, bodyControl;
        }
        [Serializable] private sealed class HotelHipPoint
        {
            public bool enabled, external, configuredComposer, hasPose, hasBone;
            public float weight;
            public string composerId, bone;
            public int actor;
            public Vector3 position, followPosition;
            public Quaternion rotation = Quaternion.identity, followRotation = Quaternion.identity;
        }
        public bool HotelGadgetAllowed => _hotelRoomSession && SessionGate.Allowed && IsPluginEnabled();
        public void SetHotelGadgetVisible(bool visible)
        {
            RequireHotelPose();
            if (!visible) CompleteResetToAnimationPose();
        }
        private void RequireHotelPose()
        {
            if (!HotelGadgetAllowed || _femaleSession.Character == null)
                throw new InvalidOperationException("HipHijackの客室セッションが未準備または無効です。");
        }
        private Transform ResolveHotelFollow(HotelHipPoint point)
        {
            if (!point.hasBone) return null;
            Transform root = point.actor == 1 ? _maleSession.TargetMaleCha?.transform : _femaleSession.Character.transform;
            if (point.actor == 2) return GetOrCreateHmdFollowTarget();
            return root == null ? null : FindByRelativePath(root, point.bone);
        }
        public string CaptureHotelGadget(Transform room)
        {
            RequireHotelPose();
            var points = new HotelHipPoint[BIK_TOTAL];
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                var current = _bikEff[i];
                bool enabled = current.Running && !(i == BIK_BODY && _vibeOwnsBodyIk);
                var point = points[i] = new HotelHipPoint { enabled = enabled, external = current.ExternalOwner,
                    weight = GetBodyIKWeight(i), configuredComposer = _settings.FollowComposerEnabled[i],
                    composerId = _settings.FollowComposerObjectIds[i], hasPose = enabled && current.Proxy != null };
                if (point.hasPose)
                {
                    point.position = room.InverseTransformPoint(current.Proxy.position - ReadVibeAppliedOffset(i));
                    point.rotation = Quaternion.Inverse(room.rotation) * current.Proxy.rotation;
                }
                if (current.FollowBone == null || point.configuredComposer || point.external) continue;
                point.bone = BuildRelativePath(_femaleSession.Character.transform, current.FollowBone);
                if (point.bone == null && _maleSession.TargetMaleCha != null)
                {
                    point.actor = 1;
                    point.bone = BuildRelativePath(_maleSession.TargetMaleCha.transform, current.FollowBone);
                }
                if (IsHmdFollowTarget(current.FollowBone)) { point.actor = 2; point.bone = FollowHmdTargetName; }
                if (point.bone == null) throw new InvalidOperationException("HipHijack操作点 " + i + " の追従先を保存できません。");
                point.hasBone = true;
                point.followPosition = current.FollowBonePositionOffset;
                point.followRotation = current.FollowBoneRotationOffset;
            }
            var snapshot = GadgetRoomSnapshot.Capture(room, _femaleSession.Character.transform);
            snapshot.runtime = JsonUtility.ToJson(new HotelHipState {
                points = points.Select(p => JsonUtility.ToJson(p)).ToArray(),
                headAndHands = JsonUtility.ToJson(BuildCurrentPosePresetSnapshot("hotel-room")),
                speedHijack = _settings.SpeedHijackEnabled, cutSpeed = _settings.CutFemaleAnimSpeedEnabled, bodyControl = _bodyCtrlLinkEnabled
            });
            return snapshot.Encode();
        }
        private static HotelHipState DecodeHotelHip(string json, out HotelHipPoint[] points)
        {
            var snapshot = GadgetRoomSnapshot.Decode(json);
            var state = JsonUtility.FromJson<HotelHipState>(snapshot.runtime);
            if (state == null || state.version != 1 || state.points == null || state.points.Length != BIK_TOTAL || string.IsNullOrEmpty(state.headAndHands))
                throw new ArgumentException("HipHijackの客室保存形式が不正です。");
            points = state.points.Select(p => JsonUtility.FromJson<HotelHipPoint>(p)).ToArray();
            foreach (var p in points)
            {
                if (p == null || float.IsNaN(p.weight) || float.IsInfinity(p.weight) || p.weight < 0 || p.weight > 1)
                    throw new ArgumentException("HipHijackの操作点または重みが不正です。");
                if (p.hasPose) new GadgetRoomPose { position = p.position, rotation = p.rotation, scale = Vector3.one }.Validate();
                if (p.hasBone) new GadgetRoomPose { position = p.followPosition, rotation = p.followRotation, scale = Vector3.one }.Validate();
                if (p.enabled && !p.external && !p.hasPose) throw new ArgumentException("有効なHipHijack操作点の座標がありません。");
            }
            return state;
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            RequireHotelPose();
            var saved = DecodeHotelHip(json, out var points);
            var head = JsonUtility.FromJson<PosePresetRuntime>(saved.headAndHands)
                ?? throw new ArgumentException("HipHijackの頭・手の保存データが不正です。");
            var follow = new Transform[BIK_TOTAL];
            // Resolve every dependency before changing any existing effector.
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                var p = points[i];
                if (p.external || _bikEff[i].ExternalOwner || !p.enabled) continue;
                if (p.configuredComposer)
                {
                    if (string.IsNullOrEmpty(p.composerId) || !_objectComposerGateway.TryGetTransform(p.composerId, out follow[i]))
                        throw new InvalidOperationException("HipHijack操作点 " + i + " のComposer追従先が復元されていません：" + p.composerId);
                }
                else if (p.hasBone && (follow[i] = ResolveHotelFollow(p)) == null)
                    throw new InvalidOperationException("HipHijack操作点 " + i + " の追従ボーンが見つかりません：" + p.bone);
            }
            TryResolveRuntimeRefs();
            StopPoseTransitionIfRunning();
            ResetVibeReactionState();
            _pendingAbandonByPostureChange = false;
            _abandonedByPostureChange = false;
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                var p = points[i];
                if (p.external || _bikEff[i].ExternalOwner) continue;
                _settings.FollowComposerEnabled[i] = p.configuredComposer;
                _settings.FollowComposerObjectIds[i] = p.composerId;
                SetBodyIK(i, p.enabled, saveSettings: false, reason: "hotel-room-restore");
                ClearBodyIKFollowBone(i);
                SetBodyIKWeight(i, p.weight, saveSettings: false);
                if (!p.enabled) continue;
                var current = _bikEff[i];
                if (current.Proxy == null) throw new InvalidOperationException("HipHijack操作点 " + i + " を生成できません。");
                current.Proxy.SetPositionAndRotation(room.TransformPoint(p.position), room.rotation * p.rotation);
                current.FollowBone = follow[i];
                current.FollowBonePositionOffset = p.configuredComposer ? Vector3.zero : p.followPosition;
                current.FollowBoneRotationOffset = p.configuredComposer ? Quaternion.identity : p.followRotation;
            }
            SetFemaleHeadAngleValues(head.femaleHeadAngleX, head.femaleHeadAngleY, head.femaleHeadAngleZ);
            _settings.FemaleHeadAngleEnabled = head.hasFemaleHeadAngle;
            SetFemaleHeadAdditiveFromPreset(head);
            SetFemaleHandPoseIndex(0, head.femaleLeftHandPoseIndex, false, false);
            SetFemaleHandPoseIndex(1, head.femaleRightHandPoseIndex, false, false);
            SetFemaleHandPoseWeight(0, head.femaleLeftHandPoseWeight, false, false);
            SetFemaleHandPoseWeight(1, head.femaleRightHandPoseWeight, false, false);
            SetFemaleHandPoseEnabled(0, head.femaleLeftHandPoseEnabled, true, false);
            SetFemaleHandPoseEnabled(1, head.femaleRightHandPoseEnabled, true, false);
            _settings.SpeedHijackEnabled = saved.speedHijack;
            _settings.CutFemaleAnimSpeedEnabled = saved.cutSpeed;
            if (!saved.bodyControl) DisableBodyCtrlLink();
            else if (!_bodyCtrlLinkEnabled) ToggleBodyCtrlLink();
            CompleteHotelRoomRestore();
        }
        public void CompleteHotelRoomRestore()
        {
            RequireHotelPose();
            TryResolveRuntimeRefs();
            _autoPosePendingApply = false;
            _autoPoseLoopReady = false;
            _hotelRoomRestorePending = false;
            _hotelRoomPoseOwned = true;
        }
        // The map/pose location owns the actor root. HipHijack owns its effectors,
        // follow offsets and head/hand controls; VerifyHotelGadgetState checks their
        // actual placement as well as their enabled/weight settings.
        public bool VerifyHotelGadgetPlacement(Transform room, string json)
            => VerifyHotelGadgetState(room, json);
        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            RequireHotelPose();
            var saved = DecodeHotelHip(json, out var points);
            var head = JsonUtility.FromJson<PosePresetRuntime>(saved.headAndHands);
            if (head == null || _settings.FemaleHeadAngleEnabled != head.hasFemaleHeadAngle
                || Mathf.Abs(_settings.FemaleHeadAngleX - head.femaleHeadAngleX) > 0.001f
                || Mathf.Abs(_settings.FemaleHeadAngleY - head.femaleHeadAngleY) > 0.001f
                || Mathf.Abs(_settings.FemaleHeadAngleZ - head.femaleHeadAngleZ) > 0.001f
                || _settings.FemaleLeftHandPoseEnabled != head.femaleLeftHandPoseEnabled
                || _settings.FemaleRightHandPoseEnabled != head.femaleRightHandPoseEnabled
                || _settings.FemaleLeftHandPoseIndex != head.femaleLeftHandPoseIndex
                || _settings.FemaleRightHandPoseIndex != head.femaleRightHandPoseIndex
                || Mathf.Abs(_settings.FemaleLeftHandPoseWeight - head.femaleLeftHandPoseWeight) > 0.001f
                || Mathf.Abs(_settings.FemaleRightHandPoseWeight - head.femaleRightHandPoseWeight) > 0.001f) return false;
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                var p = points[i]; var current = _bikEff[i];
                if (p.external != current.ExternalOwner) return false;
                if (p.external) continue;
                bool enabled = current.Running && !(i == BIK_BODY && _vibeOwnsBodyIk);
                if (enabled != p.enabled || Mathf.Abs(GetBodyIKWeight(i) - p.weight) > 0.001f) return false;
                if (_settings.FollowComposerEnabled[i] != p.configuredComposer || _settings.FollowComposerObjectIds[i] != p.composerId) return false;
                if (!p.enabled) continue;
                if (current.Proxy == null) return false;
                if (p.configuredComposer)
                {
                    if (!_settings.FollowComposerEnabled[i] || _settings.FollowComposerObjectIds[i] != p.composerId
                        || !_objectComposerGateway.TryGetTransform(p.composerId, out var target) || current.FollowBone != target) return false;
                }
                else if (p.hasBone)
                {
                    if (current.FollowBone != ResolveHotelFollow(p) || Vector3.Distance(current.FollowBonePositionOffset, p.followPosition) > 0.001f
                        || Quaternion.Angle(current.FollowBoneRotationOffset, p.followRotation) > 0.1f) return false;
                }
                else if (Vector3.Distance(current.Proxy.position - ReadVibeAppliedOffset(i), room.TransformPoint(p.position)) > 0.001f
                    || Quaternion.Angle(current.Proxy.rotation, room.rotation * p.rotation) > 0.1f) return false;
            }
            return _settings.SpeedHijackEnabled == saved.speedHijack && _settings.CutFemaleAnimSpeedEnabled == saved.cutSpeed
                && _bodyCtrlLinkEnabled == saved.bodyControl;
        }
    }
}
