using System;

namespace MapEnvironmentRuntime
{
    // Heights are relative to the unchanged feet, not to the crouched camera root.
    public static class SandboxStandClearance
    {
        public static void Capsule(float crouchedHeight, float standingHeight, float radius, float skin, float epsilon,
            out float fromHeight, out float toHeight, out float queryRadius)
        {
            fromHeight = crouchedHeight - radius;
            toHeight = standingHeight - radius;
            queryRadius = Math.Max(epsilon, radius - skin);
        }
    }
}
