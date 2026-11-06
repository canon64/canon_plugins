using System;
using MainGameKaleidoscope;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Near(float actual, float expected)
    {
        if (float.IsNaN(actual) || Math.Abs(actual - expected) > .00001f)
            throw new Exception("Expected " + expected + ", got " + actual);
        _checks++;
    }

    // Independent off-axis frustum, in tangents of the eye's view angles.
    private static Matrix4x4 Projection(float left, float right, float bottom, float top)
    {
        var p = new Matrix4x4();
        p.m00 = 2f / (right - left);
        p.m11 = 2f / (top - bottom);
        p.m02 = (right + left) / (right - left);
        p.m12 = (top + bottom) / (top - bottom);
        p.m22 = -1.002f;
        p.m23 = -.2002f;
        p.m32 = -1f;
        return p;
    }

    private static void CheckEye(float left, float right, float bottom, float top, Vector3 forward)
    {
        Vector2 offset = VrPatternCenter.Offset(Projection(left, right, bottom, top), forward);
        // Find the forward ray's pixel independently by interpolating its slopes
        // between the physical eye frustum edges.
        float projectedX = (forward.x / -forward.z - left) / (right - left);
        float projectedY = (forward.y / -forward.z - bottom) / (top - bottom);
        // This is the shader's patternUv = screenUv - eyeOffset. Both eyes
        // must return the same logical center at their actual forward pixel.
        Near(projectedX - offset.x, .5f);
        Near(projectedY - offset.y, .5f);
        // An intentional user center adjustment remains relative to straight ahead.
        Near((projectedX + .1f) - offset.x, .6f);
        Near((projectedY - .15f) - offset.y, .35f);
    }

    private static void Main()
    {
        Console.WriteLine("Testing per-eye kaleidoscope centers.");
        CheckEye(-1f, 1f, -1f, 1f, new Vector3(0, 0, -1));
        CheckEye(-1.3f, .7f, -.8f, 1.2f, new Vector3(0, 0, -1));
        CheckEye(-.7f, 1.3f, -.8f, 1.2f, new Vector3(0, 0, -1));
        // Canted eye views: head-forward is not the local eye-forward vector.
        CheckEye(-1.3f, .7f, -.9f, 1.1f, new Vector3(.12f, .04f, -1));
        CheckEye(-.7f, 1.3f, -.9f, 1.1f, new Vector3(-.12f, .04f, -1));
        // Projection translation/near-plane terms must not affect a direction.
        var p = Projection(-1.3f, .7f, -.8f, 1.2f);
        Vector2 before = VrPatternCenter.Offset(p, new Vector3(0, 0, -1));
        p.m03 = 150f; p.m13 = -90f; p.m23 = -500f;
        Vector2 after = VrPatternCenter.Offset(p, new Vector3(0, 0, -1));
        Near(after.x, before.x); Near(after.y, before.y);
        Vector2 invalid = VrPatternCenter.Offset(new Matrix4x4(), new Vector3(0, 0, -1));
        Near(invalid.x, 0); Near(invalid.y, 0);
        Console.WriteLine("PASS " + _checks + " center checks.");
    }
}
