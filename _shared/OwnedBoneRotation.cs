using UnityEngine;

namespace CanonPlugins.ShoulderPose
{
    internal sealed class OwnedBoneRotation
    {
        private Transform bone;
        private Quaternion before, after;
        private bool owned;
        internal void Capture(Transform value) { bone = value; before = value.localRotation; owned = false; }
        internal void Commit() { if (bone != null) { after = bone.localRotation; owned = true; } }
        internal void Restore()
        {
            // Only remove our exact output. Animation/another owner wins otherwise.
            if (owned && bone != null && SameRotation(bone.localRotation, after))
                bone.localRotation = before;
            owned = false;
        }
        private static bool SameRotation(Quaternion a, Quaternion b)
        {
            // Dot(q,q) can round below one even for the same stored quaternion.
            float sign = Quaternion.Dot(a, b) < 0f ? -1f : 1f;
            return Mathf.Abs(a.x - sign * b.x) < 1e-6f && Mathf.Abs(a.y - sign * b.y) < 1e-6f &&
                Mathf.Abs(a.z - sign * b.z) < 1e-6f && Mathf.Abs(a.w - sign * b.w) < 1e-6f;
        }
    }
}
