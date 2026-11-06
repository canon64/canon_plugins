using System;
using UnityEngine;

namespace AstralMode
{
    internal static class VrViewPose
    {
        internal static void Align(Vector3 originPosition, Quaternion originRotation,
            Vector3 headPosition, Quaternion headRotation,
            Vector3 desiredPosition, Quaternion desiredRotation,
            out Vector3 position, out Quaternion rotation)
        {
            // Tracking-space up is world up. Pitch/roll belong to the physical
            // HMD; cancelling them by rotating the origin tilts the entire world.
            float norm = originRotation.x * originRotation.x + originRotation.y * originRotation.y
                + originRotation.z * originRotation.z + originRotation.w * originRotation.w;
            if (!(norm > .000001f)) throw new ArgumentException("Invalid VR origin rotation.");
            var inverseOrigin = new Quaternion(-originRotation.x / norm, -originRotation.y / norm,
                -originRotation.z / norm, originRotation.w / norm);
            Vector3 localHead = inverseOrigin * (headPosition - originPosition);
            Quaternion localHeadRotation = inverseOrigin * headRotation;
            double localYaw = Heading(localHeadRotation, 0);
            double desiredYaw = Heading(desiredRotation, Heading(headRotation, Heading(originRotation, 0) + localYaw));
            double yaw = (desiredYaw - localYaw) * .5;
            rotation = new Quaternion(0, (float)Math.Sin(yaw), 0, (float)Math.Cos(yaw));
            position = desiredPosition - rotation * localHead;
        }

        private static double Heading(Quaternion rotation, double fallback)
        {
            Vector3 forward = rotation * new Vector3(0, 0, 1);
            if (forward.x * forward.x + forward.z * forward.z < .000001f) return fallback;
            return Math.Atan2(forward.x, forward.z);
        }
    }
}
