using System.Runtime.Serialization;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    [DataContract]
    internal sealed class CopiedBoneCamera
    {
        [DataMember] public int FormatVersion;
        [DataMember] public float[] FixedCameraPosition;
        [DataMember] public bool HasOwnerBinding;
        [DataMember] public bool OwnerIndependentActor;
        [DataMember] public int OwnerFemaleSlot;
        [DataMember] public string BonePath;
        // Read only to reject old world-Y offsets without the saved bone pose.
        [DataMember(EmitDefaultValue = false)] public bool LookAtWorldY;
        [IgnoreDataMember] public ChaControl RuntimeOwner;
        [IgnoreDataMember] public Transform RuntimeBone;
    }

    public sealed partial class Plugin
    {
        private CopiedBoneCamera _activeCopiedBoneCamera;

        private static CopiedBoneCamera ReadCopiedBoneCamera(object[] data)
        {
            if (data == null || data.Length != 13 || !(data[9] is int version) || version != 2
                || !(bool)data[3]) return null;
            var shot = new CopiedBoneCamera { FormatVersion = 2, FixedCameraPosition = ToArray((Vector3)data[0]) };
            return CaptureCopiedBinding(shot, data[11] as ChaControl, data[10] as Transform) ? shot : null;
        }

        private static CopiedBoneCamera CloneBoneCamera(CopiedBoneCamera shot)
        {
            if (shot == null) return null;
            return new CopiedBoneCamera {
                FormatVersion = shot.FormatVersion, FixedCameraPosition = CloneArray(shot.FixedCameraPosition, null),
                HasOwnerBinding = shot.HasOwnerBinding, OwnerIndependentActor = shot.OwnerIndependentActor,
                OwnerFemaleSlot = shot.OwnerFemaleSlot, BonePath = shot.BonePath, LookAtWorldY = shot.LookAtWorldY,
                RuntimeOwner = shot.RuntimeOwner, RuntimeBone = shot.RuntimeBone
            };
        }

        private static Vector3 CopiedLookAt(Transform bone, Vector3 offset, CopiedBoneCamera shot)
        {
            return bone.TransformPoint(offset);
        }

        // Shared by immediate application, transition targets and following.
        private static void ResolveSubBoneView(CopiedBoneCamera shot, Transform bone, Vector3 offset,
            Vector3 legacyPosition, Quaternion fallbackRotation, out Vector3 position, out Quaternion rotation)
        {
            position = shot != null ? ToVector3(shot.FixedCameraPosition, legacyPosition) : legacyPosition;
            Vector3 forward = bone.TransformPoint(offset) - position;
            if (forward.sqrMagnitude <= .000001f) { rotation = fallbackRotation; return; }
            Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > .999f
                ? new Vector3(0, 0, 1) : Vector3.up;
            rotation = Quaternion.LookRotation(forward.normalized, up);
        }

        // Only an explicit camera movement changes the fixed world position.
        private static void SetCopiedCameraPosition(CopiedBoneCamera shot, Vector3 position)
        {
            if (shot != null) shot.FixedCameraPosition = ToArray(position);
        }
    }
}
