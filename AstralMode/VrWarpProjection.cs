using System;
using UnityEngine;

namespace AstralMode
{
    // Scale the two optical frusta together. Never replace their asymmetric
    // projection centres or the XR-owned view matrices / eye positions.
    internal static class VrWarpProjection
    {
        internal static float VerticalFov(Matrix4x4 projection)
        {
            double focal = projection.m11;
            if (focal <= 0 || double.IsNaN(focal) || double.IsInfinity(focal))
                throw new ArgumentException("Invalid HMD projection.");
            return (float)((Math.Atan((1 + projection.m12) / focal)
                + Math.Atan((1 - projection.m12) / focal)) * 180 / Math.PI);
        }

        internal static float MeanFov(Matrix4x4 left, Matrix4x4 right)
            => (VerticalFov(left) + VerticalFov(right)) * 0.5f;

        internal static Matrix4x4 ForEye(Matrix4x4 projection, Vector3 headForward, float target)
        {
            if (float.IsNaN(target) || float.IsInfinity(target) || target <= 0 || target >= 180)
                throw new ArgumentOutOfRangeException(nameof(target));
            if (!(headForward.z < -0.000001f)) throw new ArgumentException("Invalid head-forward direction.");
            if (Math.Abs(target - VerticalFov(projection)) < 0.00001f) return projection;
            double low = 0.000001, high = 1000000;
            for (int i = 0; i < 64; ++i)
            {
                double scale = Math.Sqrt(low * high);
                if (VerticalFov(ScaleAroundForward(projection, headForward, scale)) > target) low = scale;
                else high = scale;
            }
            return ScaleAroundForward(projection, headForward, Math.Sqrt(low * high));
        }

        private static Matrix4x4 ScaleAroundForward(Matrix4x4 projection, Vector3 forward, double scale)
        {
            // Same head-forward reference as Kaleidoscope's VrPatternCenter.
            // Scaling about local eye-forward alone moves the centre on canted
            // displays. Keep its original screen point for each eye instead.
            float ratio = (float)scale;
            projection.m02 += (ratio - 1) * projection.m00 * forward.x / -forward.z;
            projection.m12 += (ratio - 1) * projection.m11 * forward.y / -forward.z;
            projection.m00 *= ratio;
            projection.m11 *= ratio;
            return projection;
        }

        internal static void WithFov(Matrix4x4 left, Matrix4x4 right, float target,
            out Matrix4x4 expandedLeft, out Matrix4x4 expandedRight)
        {
            if (float.IsNaN(target) || float.IsInfinity(target) || target <= 0 || target >= 180)
                throw new ArgumentOutOfRangeException(nameof(target));
            float original = MeanFov(left, right);
            expandedLeft = left;
            expandedRight = right;
            if (Math.Abs(target - original) < 0.00001f) return;

            // A common scale preserves matching magnification for both eyes,
            // including HMDs whose left/right optical centres are asymmetric.
            double low = 0.000001, high = 1000000;
            for (int i = 0; i < 64; ++i)
            {
                double scale = Math.Sqrt(low * high);
                Matrix4x4 l = Scale(left, scale), r = Scale(right, scale);
                if (MeanFov(l, r) > target) low = scale;
                else high = scale;
            }
            double result = Math.Sqrt(low * high);
            expandedLeft = Scale(left, result);
            expandedRight = Scale(right, result);
        }

        private static Matrix4x4 Scale(Matrix4x4 projection, double scale)
        {
            projection.m00 *= (float)scale;
            projection.m11 *= (float)scale;
            return projection;
        }
    }
}
