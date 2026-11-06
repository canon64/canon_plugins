using UnityEngine;

namespace AstralMode
{
    internal static class TransitionCurve
    {
        // The desktop and HMD renderers receive this same configured sample.
        internal static float Sample(float progress, float startT, float endT,
            EasingType easing, float startValue, float endValue)
            => Mathf.Lerp(startValue, endValue, Easing.Window(easing, progress, startT, endT));
    }
}
