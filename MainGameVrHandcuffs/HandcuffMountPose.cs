using UnityEngine;

namespace MainGameVrHandcuffs
{
    internal static class HandcuffMountPose
    {
        // Native oo_base skeleton: left hand extends along -X, right along +X;
        // the palm side is -Y. All values below are fixed in wrist-bone space.
        internal static Vector3 LockDirection(int cuff) => new Vector3(cuff==0?.91f:-.91f,0,.414f).normalized;
        internal const float LockInsideRadius=.032f;
        internal static Vector3 LocalPosition(int hand) => new Vector3(hand==0?.008f:-.008f,.014f,0f);
        private static readonly Quaternion[] Rotations = {
            // Left wrist: cuff A, cuff B.
            new Quaternion(.691055245f,-.691055245f,.149808706f,.149808706f),
            new Quaternion(.149808706f,-.149808706f,.691055245f,.691055245f),
            // Right wrist: cuff A, cuff B.
            new Quaternion(.149808706f,.149808706f,-.691055245f,.691055245f),
            new Quaternion(.691055245f,.691055245f,-.149808706f,.149808706f)
        };
        internal static Quaternion LocalRotation(int cuff,int hand) => Rotations[hand*2+cuff];
        internal static void Calculate(int cuff,int hand,Vector3 wristPosition,Quaternion wristRotation,
            out Vector3 position,out Quaternion rotation)
        {
            position=wristPosition+wristRotation*LocalPosition(hand);
            rotation=wristRotation*LocalRotation(cuff,hand);
        }
    }
}
