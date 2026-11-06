using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private static Func<ChaControl> _copiedIndependentActor;

        // Only used at capture/activation. Following uses these resolved transforms directly.
        private static ChaControl GetCopiedIndependentActor()
        {
            if (_copiedIndependentActor == null)
            {
                var type = HarmonyLib.AccessTools.TypeByName("MainGameVMDPlay.IndependentActorApi");
                var method = type == null ? null : HarmonyLib.AccessTools.Method(type, "GetReadyActor");
                if (method != null)
                    _copiedIndependentActor = (Func<ChaControl>)Delegate.CreateDelegate(typeof(Func<ChaControl>), method);
            }
            return _copiedIndependentActor?.Invoke();
        }

        private static IList GetCopiedSceneFemales()
        {
            var scene = SessionGate.HScene;
            return scene == null ? null : HSceneFemaleListField?.GetValue(scene) as IList;
        }

        private static string CopiedBonePath(Transform root, Transform bone)
        {
            if (root == null || bone == null) return null;
            var names = new List<string>();
            for (var current = bone; current != null; current = current.parent)
            {
                if (current == root)
                {
                    names.Reverse();
                    return string.Join("/", names.ToArray());
                }
                names.Add(current.name);
            }
            return null;
        }

        private static bool CaptureCopiedBinding(CopiedBoneCamera shot, ChaControl owner, Transform bone)
        {
            if (owner == null || bone == null) return false;
            string path = CopiedBonePath(owner.transform, bone);
            if (path == null) return false;
            bool independent = owner == GetCopiedIndependentActor();
            int slot = -1;
            var females = GetCopiedSceneFemales();
            if (!independent && females != null)
                for (int i = 0; i < females.Count; i++)
                    if (females[i] as ChaControl == owner) { slot = i; break; }
            if (!independent && slot < 0) return false;
            shot.HasOwnerBinding = true;
            shot.OwnerIndependentActor = independent;
            shot.OwnerFemaleSlot = slot;
            shot.BonePath = path;
            shot.RuntimeOwner = owner;
            shot.RuntimeBone = bone;
            return true;
        }

        private static bool ResolveCopiedBinding(CopiedBoneCamera shot)
        {
            if (shot == null || !shot.HasOwnerBinding) return true;
            ChaControl owner = null;
            if (shot.OwnerIndependentActor) owner = GetCopiedIndependentActor();
            else
            {
                var females = GetCopiedSceneFemales();
                if (females != null && shot.OwnerFemaleSlot >= 0 && shot.OwnerFemaleSlot < females.Count)
                    owner = females[shot.OwnerFemaleSlot] as ChaControl;
            }
            shot.RuntimeOwner = owner;
            shot.RuntimeBone = FindCopiedBone(owner, shot.BonePath);
            return owner != null && shot.RuntimeBone != null;
        }

        private static Transform FindCopiedBone(ChaControl owner, string path)
        {
            if (owner == null || path == null) return null;
            return path.Length == 0 ? owner.transform : owner.transform.Find(path);
        }

        private bool PrepareCopiedBinding(SubCameraPreset preset)
        {
            var shot = preset?.BoneCamera;
            if (preset?.UseBoneLink == true && shot != null
                && (!shot.HasOwnerBinding || shot.LookAtWorldY || !HasArray(shot.FixedCameraPosition)
                    || !HasArray(preset.LookAtOffsetLocal) || (shot.FormatVersion != 0 && shot.FormatVersion != 2)))
            {
                SetStatus("呼び出し失敗: このカメラはボーン情報を復元できません。◯で再保存してください");
                return false;
            }
            if (preset?.UseBoneLink == true && preset.BoneCamera?.HasOwnerBinding == true
                && preset.BoneCamera.OwnerIndependentActor != (_actorLease != 0))
            {
                SetStatus("メインとサブの対象が異なります。本編キャラ／呼出キャラを揃えてください（設定は変更していません）");
                return false;
            }
            if (preset == null || !preset.UseBoneLink) return true;
            if (ResolveCopiedBinding(shot))
            {
                if (shot != null) shot.FormatVersion = 2;
                return true;
            }
            SetStatus("呼び出し失敗: 保存したキャラまたはボーンが見つかりません");
            return false;
        }

        private void ResetCopiedCameraLocalPose(SubCameraPreset preset)
        {
            if (preset?.BoneCamera == null || _subCamera == null) return;
            var position = _subCamera.transform.position;
            var rotation = _subCamera.transform.rotation;
            if (_cameraAnchorObject != null) _cameraAnchorObject.transform.SetPositionAndRotation(position, rotation);
            _subCamera.transform.localPosition = Vector3.zero;
            _subCamera.transform.localRotation = Quaternion.identity;
        }
    }
}
