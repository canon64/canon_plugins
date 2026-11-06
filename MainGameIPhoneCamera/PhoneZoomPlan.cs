using System;

namespace MainGameIPhoneCamera
{
    internal static class PhoneZoomPlan
    {
        internal const float Minimum = .1f, Maximum = 20f;
        internal static float Clamp(float zoom) => Math.Max(Minimum, Math.Min(Maximum, zoom));
        internal static float Speed(float input)
            => Math.Abs(input) <= .2f ? 0 : Math.Sign(input) * Math.Min(1f, (Math.Abs(input) - .2f) / .8f) * 1.5f;
        internal static float Fov(float baseFov, float zoom)
            => (float)(2 * Math.Atan(Math.Tan(baseFov * Math.PI / 360) / Clamp(zoom)) * 180 / Math.PI);
        internal static float Zoom(float baseFov, float fov)
            => Clamp((float)(Math.Tan(baseFov * Math.PI / 360) / Math.Tan(fov * Math.PI / 360)));
        internal static float FovTangent(float baseFov, float zoom, float speed)
        {
            double tangent = Math.Tan(baseFov * Math.PI / 360);
            return (float)(-2 * tangent / (zoom * zoom + tangent * tangent) * 180 / Math.PI * speed);
        }
    }
}
