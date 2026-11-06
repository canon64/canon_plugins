using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
using Hip = MainGirlHipHijack.Plugin;
namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        [Serializable] private sealed class HotelCuffs { public int[] wrists; public Vector3 bend; }
        public bool HotelGadgetAllowed => CanAct;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!CanAct) throw new InvalidOperationException("手錠はこのHでは許可されていません。");
            if (visible && _rig == null) CreateRig(); else if (!visible) DeleteRig();
        }
        public string CaptureHotelGadget(Transform room)
        {
            var roots = _rig == null ? null : new[] { _rig.transform }.Concat(_parts).Concat(_wristTargets).ToArray();
            var data = GadgetRoomSnapshot.Capture(room, roots);
            data.runtime = JsonUtility.ToJson(new HotelCuffs { wrists = new[] { _state.Wrist(0), _state.Wrist(1) },
                bend = Quaternion.Inverse(room.rotation) * _chainBend });
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!CanAct) throw new InvalidOperationException("手錠はこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            var state = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<HotelCuffs>(data.runtime);
            if (data.visible && (data.poses.Length != 1 + _parts.Length + _wristTargets.Length || state?.wrists == null
                || state.wrists.Length != _bindings.Length || state.wrists.Any(w => w < -1 || w > 1)
                || (state.wrists[0] >= 0 && state.wrists[0] == state.wrists[1])))
                throw new ArgumentException("手錠の保存状態が不正です。");
            DeleteRig(); if (!data.visible) return;
            try
            {
                CreateRig();
                var targets = new[] { _rig.transform }.Concat(_parts).Concat(_wristTargets).ToArray();
                for (int i = 0; i < targets.Length; ++i) data.poses[i].Apply(room, targets[i]);
                _chainBend = room.rotation * state.bend;
                for (int i = 0; i < _bindings.Length; ++i)
                {
                    int wrist = state.wrists[i]; if (wrist < 0) continue;
                    // The saved target is the wrist's fixed position, not the new
                    // animation's current hand pose. Rebuild the fixed cuff mount
                    // around it, retaining the target when its parent cuff moves.
                    var targetPosition = _wristTargets[i].position;
                    var targetRotation = _wristTargets[i].rotation;
                    HandcuffMountPose.Calculate(i, wrist, targetPosition, targetRotation, out var mountPosition, out var mountRotation);
                    _parts[i].SetPositionAndRotation(mountPosition, mountRotation);
                    _wristTargets[i].SetPositionAndRotation(targetPosition, targetRotation);
                    string key = wrist == 0 ? "left_hand" : "right_hand";
                    if (!Hip.TryGetRuntimeWrist(key, out var female, out var unused)
                        || !Hip.TryBindRuntimeWrist(Guid + "/" + i, female, key, _wristTargets[i], out var binding))
                        throw new InvalidOperationException("保存した手錠の手首IKを復元できません。");
                    _bindings[i] = binding; binding.Invalidated += BindingInvalidated;
                    _state.TryGrab(i); _state.CommitBinding(i, wrist); _state.Release();
                }
            }
            catch { DeleteRig(); throw; }
        }

        public bool VerifyHotelGadgetPlacement(Transform room, string json)
        {
            var saved = GadgetRoomSnapshot.Decode(json);
            if (!saved.visible) return _rig == null;
            if (_rig == null || saved.poses.Length != 1 + _parts.Length + _wristTargets.Length) return false;
            var state = string.IsNullOrEmpty(saved.runtime) ? null : JsonUtility.FromJson<HotelCuffs>(saved.runtime);
            if (state?.wrists == null || state.wrists.Length != _bindings.Length) return false;
            var targets = new[] { _rig.transform }.Concat(_parts).Concat(_wristTargets).ToArray();
            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] == null) return false;
                var pose = saved.poses[i];
                var position = room.TransformPoint(pose.position);
                var rotation = room.rotation * pose.rotation.normalized;
                // Bound cuff bodies use the fixed mount around the saved wrist
                // target; legacy records may contain an older body offset.
                if (i >= 1 && i <= _bindings.Length && state.wrists[i - 1] >= 0)
                {
                    var wristPose = saved.poses[1 + _parts.Length + i - 1];
                    HandcuffMountPose.Calculate(i - 1, state.wrists[i - 1], room.TransformPoint(wristPose.position),
                        room.rotation * wristPose.rotation.normalized, out position, out rotation);
                }
                var scale = pose.worldScale ? targets[i].lossyScale : targets[i].localScale;
                if (Vector3.Distance(targets[i].position, position) > .001f
                    || Quaternion.Angle(targets[i].rotation, rotation) > .1f
                    || Vector3.Distance(scale, pose.scale) > .001f) return false;
            }
            for (int i = 0; i < _bindings.Length; ++i)
            {
                if (_state.Wrist(i) != state.wrists[i]) return false;
                if (state.wrists[i] >= 0 && (_bindings[i] == null || !_bindings[i].IsValid)) return false;
                if (state.wrists[i] < 0 && _bindings[i] != null) return false;
            }
            return Vector3.Distance(_chainBend, room.rotation * state.bend) <= .001f;
        }
    }
}
