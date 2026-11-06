using System;
using System.Reflection;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private Transform _vrViewOrigin, _vrViewHead;
        private Vector3 _vrSavedHeadPosition;
        private Quaternion _vrSavedHeadRotation;
        private bool _vrViewCaptured, _vrViewMoved;

        private bool CaptureVrView()
        {
            if (!IsVrActive() || _vrViewCaptured) return true;
            try
            {
                var vr = FindType("VRGIN.Core.VR", "VRGIN_OpenXR");
                var rig = vr?.GetProperty("Camera", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
                _vrViewOrigin = rig?.GetType().GetProperty("Origin")?.GetValue(rig, null) as Transform;
                _vrViewHead = rig?.GetType().GetProperty("Head")?.GetValue(rig, null) as Transform;
                if (_vrViewOrigin == null || _vrViewHead == null)
                    throw new InvalidOperationException("VRの基準位置とHMDの視点を取得できません。");
                _vrSavedHeadPosition = _vrViewHead.position;
                _vrSavedHeadRotation = _vrViewHead.rotation;
                _vrViewCaptured = true;
                _vrViewMoved = false;
                return true;
            }
            catch (Exception ex) { ReportVrTransitionFailure("突入前のVR視点保存", ex); return false; }
        }

        private bool PlaceVrMaleView()
        {
            if (!IsVrActive()) return true;
            try
            {
                if (!SessionGate.Allowed || !_vrViewCaptured || _vrViewOrigin == null || _vrViewHead == null)
                    throw new InvalidOperationException("保存済みVR視点を使用できません。");
                ChaControl male = SessionGate.ReadScene<ChaControl>("male");
                ChaControl female = null;
                var females = SessionGate.ReadScene<System.Collections.IList>("lstFemale");
                if (females != null)
                    foreach (var item in females) if (item is ChaControl actor && actor != null) { female = actor; break; }
                Vector3 position = ActorEyeCentre(male), target = ActorEyeCentre(female);
                if ((target - position).sqrMagnitude < 0.000001f)
                    throw new InvalidOperationException("男女の視点位置が重なっています。");
                // Align heading only. Looking down/up is the user's tracked
                // head movement, never a tilt applied to the whole VR world.
                Vector3 direction = target - position;
                direction.y = 0;
                Quaternion look = direction.sqrMagnitude > .000001f
                    ? Quaternion.LookRotation(direction, Vector3.up) : _vrViewHead.rotation;
                _vrViewMoved = true;
                AlignVrHead(position, look);
                return true;
            }
            catch (Exception ex)
            {
                ReportVrTransitionFailure("男性位置から女性を見るVR視点", ex);
                return false;
            }
        }

        private static Vector3 ActorEyeCentre(ChaControl actor)
        {
            if (actor == null) throw new InvalidOperationException("視点の対象キャラがいません。");
            var eyes = actor.eyeLookCtrl != null && actor.eyeLookCtrl.eyeLookScript != null
                ? actor.eyeLookCtrl.eyeLookScript.eyeObjs : null;
            if (eyes != null && eyes.Length >= 2 && eyes[0]?.eyeTransform != null && eyes[1]?.eyeTransform != null)
                return (eyes[0].eyeTransform.position + eyes[1].eyeTransform.position) * 0.5f;
            if (actor.animBody != null && actor.animBody.isHuman)
            {
                var left = actor.animBody.GetBoneTransform(HumanBodyBones.LeftEye);
                var right = actor.animBody.GetBoneTransform(HumanBodyBones.RightEye);
                if (left != null && right != null) return (left.position + right.position) * 0.5f;
            }
            throw new InvalidOperationException(actor.name + " の左右の目を取得できません。");
        }

        private void AlignVrHead(Vector3 desiredPosition, Quaternion desiredRotation)
        {
            VrViewPose.Align(_vrViewOrigin.position, _vrViewOrigin.rotation,
                _vrViewHead.position, _vrViewHead.rotation, desiredPosition, desiredRotation,
                out var position, out var rotation);
            _vrViewOrigin.SetPositionAndRotation(position, rotation);
        }

        private void RestoreVrView(bool finish)
        {
            if (!_vrViewCaptured) return;
            bool restored = !_vrViewMoved;
            try
            {
                if (_vrViewMoved)
                {
                    if (_vrViewOrigin == null || _vrViewHead == null)
                        throw new InvalidOperationException("復元先のVR視点がありません。");
                    AlignVrHead(_vrSavedHeadPosition, _vrSavedHeadRotation);
                    _vrViewMoved = false;
                    restored = true;
                }
            }
            catch (Exception ex) { ReportVrTransitionFailure("突入前のVR視点への復元", ex); }
            if (finish && restored)
            {
                _vrViewCaptured = _vrViewMoved = false;
                _vrViewOrigin = _vrViewHead = null;
            }
        }
    }
}
