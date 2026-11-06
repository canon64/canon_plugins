using System;

namespace MainGameVrDildo
{
    // Units: full cycles/second and end-to-end centimetres (not half-amplitude).
    internal static class PistonPolicy
    {
        internal static float Speed(float value) => Clamp(value, 0f, 5f, 0f);
        internal static float Stroke(float value) => Clamp(value, 0f, 20f, 5f);
        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));

        internal static void Wheel(float steps, bool controlHeld, ref float speed, ref float stroke)
        {
            if (float.IsNaN(steps) || float.IsInfinity(steps)) return;
            if (controlHeld) stroke = Stroke(stroke + steps * 0.5f);
            else speed = Speed(speed + steps * 0.1f);
            // Avoid tiny residual speeds after ten 0.1 decrements from 1.
            if (speed < 0.0001f) speed = 0f;
        }

        internal static void Stick(float x, float y, float seconds, ref float speed, ref float stroke)
        {
            seconds = Clamp(seconds, 0f, 0.05f, 0f);
            x = Clamp(x, -1f, 1f, 0f); y = Clamp(y, -1f, 1f, 0f);
            bool horizontal = Math.Abs(x) > Math.Abs(y);
            float axis = horizontal ? x : y;
            if (Math.Abs(axis) <= 0.25f) return;
            float delta = Math.Sign(axis) * (Math.Abs(axis) - 0.25f) / 0.75f * seconds;
            if (horizontal) speed = Speed(speed + delta);
            else stroke = Stroke(stroke + delta * 5f);
        }
    }
}
