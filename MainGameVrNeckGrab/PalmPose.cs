using System;
using System.Numerics;

namespace MainGameVrNeckGrab
{
    internal static class PalmPose
    {
        internal static Quaternion CorrectRotation(Quaternion previousPalm)
        {
            // Apply in the requested order: Y 180 degrees, THEN roll sideways Z 90 degrees.
            var flipY = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);
            var rollZ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(Math.PI * .5));
            return Quaternion.Normalize(rollZ * flipY * previousPalm);
        }
        internal static Vector3 Center(Vector3 wrist, Vector3 indexBase, Vector3 littleBase)
            => wrist * .5f + indexBase * .25f + littleBase * .25f;
        internal static Vector3 AlignmentDelta(Vector3 palmCenter, Vector3 palmInward, Vector3 skinSurface)
            => skinSurface - (palmCenter + Vector3.Normalize(palmInward) * .006f);
    }
}
