using System;

namespace MainGameIPhoneCamera
{
    internal static class ViewerLayout
    {
        internal const float Width = 560, Height = 1166;
        internal static float Zoom(float value) => Math.Max(1, Math.Min(20, value));
        internal static float Speed(float axis) => Math.Abs(axis) <= .2f ? 0 : Math.Sign(axis) * Math.Min(1, (Math.Abs(axis) - .2f) / .8f);
        internal static float Fit(int width, int height, float viewportWidth = Width, float viewportHeight = Height) => Math.Min(viewportWidth / Math.Max(1, width), viewportHeight / Math.Max(1, height));
        internal static float Limit(float fittedSize, float viewport, float zoom) => Math.Max(0, (fittedSize * Zoom(zoom) - viewport) * .5f);
        internal static float Clamp(float value, float limit) => Math.Max(-limit, Math.Min(limit, value));
    }
}
