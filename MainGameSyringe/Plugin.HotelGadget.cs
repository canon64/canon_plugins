using System;
using System.Linq;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameSyringe
{
    public sealed partial class Plugin
    {
        public bool HotelGadgetAllowed => Allowed;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!Allowed) throw new InvalidOperationException("注射器は無効、またはこのHでは許可されていません。");
            if (visible && _model == null) Activate(); else if (!visible && _model != null) Despawn();
            if (visible && _model == null) throw new InvalidOperationException("注射器のモデルを読み込めません。");
        }
        public string CaptureHotelGadget(Transform room)
        {
            if (Refilling || InjectionBusy) throw new InvalidOperationException("投与・補充の終了後に客室を保存してください。");
            var data = GadgetRoomSnapshot.Capture(room, _model == null ? null : _model.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config);
            data.runtime = new HotelNeedleSnapshot { spent = _spent,
                actor = _actors.IndexOf(_insertedActor), bone = _insertedBone == null ? null : _insertedBone.name,
                surface = _surfaceLocal, localPose = _insertedBone == null || _model == null ? null : GadgetRoomPose.Read(_insertedBone, _model.transform) }.Encode();
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("注射器はこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            var state = !data.visible || string.IsNullOrEmpty(data.runtime) ? null : HotelNeedleSnapshot.Decode(data.runtime);
            ChaControl actor = null; Transform bone = null;
            if (state != null && state.actor >= 0 && !string.IsNullOrEmpty(state.bone))
            {
                if (state.actor >= _actors.Count) throw new InvalidOperationException("注射器の刺入先キャラがいません。");
                actor = _actors[state.actor];
                bone = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == state.bone);
                if (bone == null) throw new InvalidOperationException("注射器の刺入先ボーンが見つかりません：" + state.bone);
            }
            GadgetConfigSnapshot.Apply(Config, data.settings); SetHotelGadgetVisible(data.visible);
            if (!data.visible) return;
            ReleaseGrab(); DetachInsertion(false); data.poses.Single().Apply(room, _model.transform);
            if (state == null) return;
            _spent = state.spent; RebuildInjectionAnimation();
            if (bone == null) return;
            // Old records lost localPose but still retain the complete room-relative placement above.
            // Attach that restored world pose without inventing a new offset or moving the needle.
            state.localPose?.Apply(bone, _model.transform);
            _model.transform.SetParent(bone, true); _insertedActor = actor; _insertedBone = bone; _surfaceLocal = state.surface;
            // Restore the actual insertion, not just its old model transform. Do this once at restore.
            RepositionInsertion();
            _boneLifetime = bone.gameObject.AddComponent<GripLifetime>(); _boneLifetime.Released = () => DetachInsertion(false);
            SyncGizmoVisibility();
        }
        public bool VerifyHotelGadgetPlacement(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible) return _model == null;
            if (_model == null) return false;
            var state = string.IsNullOrEmpty(data.runtime) ? null : HotelNeedleSnapshot.Decode(data.runtime);
            if (state == null || state.actor < 0 || string.IsNullOrEmpty(state.bone)) return _insertedBone == null;
            if (_insertedBone == null || _tip == null || _actors.IndexOf(_insertedActor) != state.actor || _insertedBone.name != state.bone) return false;
            var target = _insertedBone.TransformPoint(state.surface) + _model.transform.forward * _insertionDepth.Value;
            return _tip.position == target;
        }
    }
}
