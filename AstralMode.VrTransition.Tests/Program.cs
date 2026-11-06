using System;
using System.Reflection;
using AstralMode;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        ++_checks;
        if (!condition) throw new Exception(message);
    }

    private static Matrix4x4 Projection(float x, float y, float focal)
    {
        var p = new Matrix4x4();
        p.m00 = 1.2f; p.m11 = focal; p.m02 = x; p.m12 = y;
        p.m22 = -1.0002f; p.m23 = -0.020002f; p.m32 = -1;
        return p;
    }

    private static bool Same(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; ++i) if (a[i] != b[i]) return false;
        return true;
    }

    private static void KeepsOptics(Matrix4x4 original, Matrix4x4 expanded)
    {
        for (int i = 0; i < 16; ++i)
            if (i != 0 && i != 5)
                Check(original[i] == expanded[i], "only focal terms may change: " + i);
        Check(Math.Abs(original.m00 / original.m11 - expanded.m00 / expanded.m11) < 0.00001,
            "horizontal and vertical magnification stay matched");
    }

    private static void Rejects(float target, Matrix4x4 left, Matrix4x4 right)
    {
        bool rejected = false;
        try { VrWarpProjection.WithFov(left, right, target, out _, out _); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "reject invalid target " + target);
    }

    private static void Main()
    {
        try { RunChecks(); }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); Environment.ExitCode = 1; }
    }

    private static void RunChecks()
    {
        Console.WriteLine("Astral VR projection regression: start");
        var symmetric = Projection(0, 0, 1);
        Check(Math.Abs(VrWarpProjection.VerticalFov(symmetric) - 90) < 0.00001,
            "reference projection has 90 degree vertical FOV");
        VrWarpProjection.WithFov(symmetric, symmetric, 120, out var l, out var r);
        Check(Math.Abs(l.m11 - (float)(1 / Math.Tan(Math.PI / 3))) < 0.00001,
            "120 degree FOV has the independently calculated focal length");
        Check(Same(l, r), "symmetric eyes agree");

        var left = Projection(0.22f, 0.12f, 0.95f);
        var right = Projection(-0.19f, -0.08f, 1.03f);
        float baseline = VrWarpProjection.MeanFov(left, right);
        VrWarpProjection.WithFov(left, right, baseline, out l, out r);
        Check(Same(l, left) && Same(r, right), "start/restore preserves exact original matrices");
        foreach (float fov in new[] { baseline + 10, 120f, 150f, 179f })
        {
            VrWarpProjection.WithFov(left, right, fov, out l, out r);
            Check(Math.Abs(VrWarpProjection.MeanFov(l, r) - fov) < 0.0001,
                "asymmetric binocular mean reaches " + fov);
            Check(l.m11 > 0 && l.m11 < left.m11 && r.m11 > 0 && r.m11 < right.m11,
                "both eye frusta widen without flipping");
            Check(Math.Abs(l.m11 / left.m11 - r.m11 / right.m11) < 0.00001,
                "both eyes use the same magnification");
            KeepsOptics(left, l); KeepsOptics(right, r);
        }
        // Always calculate from captured optics, never from last frame's widened matrix.
        VrWarpProjection.WithFov(left, right, 150, out var firstL, out var firstR);
        VrWarpProjection.WithFov(left, right, 150, out l, out r);
        Check(Same(firstL, l) && Same(firstR, r), "repeated application does not accumulate");
        VrWarpProjection.WithFov(left, right, baseline, out l, out r);
        Check(Same(l, left) && Same(r, right), "final curve endpoint restores exact optics");
        foreach (float invalid in new[] { 0f, -1f, 180f, float.NaN, float.PositiveInfinity })
            Rejects(invalid, left, right);
        bool badProjection = false;
        try { VrWarpProjection.VerticalFov(new Matrix4x4()); }
        catch (ArgumentException) { badProjection = true; }
        Check(badProjection, "uninitialized optics are rejected before starting transition");
        CheckRendererLifecycle(left, right);
        Console.WriteLine("PASS checks=" + _checks);
    }

    private static void Callback(VrTransitionRenderer renderer, string name, params object[] args)
        => typeof(VrTransitionRenderer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, args);

    private static bool NativeOwnership(Camera camera)
        => camera.ProjectionWrites == 0 && camera.ViewWrites == 0 && camera.StereoWrites == 0;

    private static void CheckRendererLifecycle(Matrix4x4 left, Matrix4x4 right)
    {
        var camera = new GameObject("native HMD").AddComponent<Camera>();
        camera.NativeLeft = left; camera.NativeRight = right;
        var view = new Matrix4x4();
        view.m00 = view.m11 = view.m33 = 1; view.m22 = -1;
        view.m03 = .03f; camera.NativeLeftView = view;
        view.m03 = -.03f; camera.NativeRightView = view;
        int failures = 0;
        var renderer = new VrTransitionRenderer();
        renderer.Initialize(camera, new Shader(), (op, ex) => ++failures);
        Check(ReferenceEquals(camera, renderer.TargetCamera), "uses native HMD camera as source");
        // The boundary deliberately reproduces the observed outside-render API.
        Check(Same(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left),
            camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right)),
            "outside render both getters can return the same eye");
        renderer.SetFov(150);
        renderer.SetWhite(.25f);
        Callback(renderer, "OnPostRender");
        Check(GL.Vertices == 4 && GL.Material.Color.a == .25f, "white curve reaches material unchanged");
        var material = GL.Material;
        var src = new RenderTexture(1000, 900, 24, RenderTextureFormat.ARGB32);
        var dst = new RenderTexture(1000, 900, 24, RenderTextureFormat.ARGB32);
        camera.InEyeRender = true;
        foreach (var eye in new[] { Camera.MonoOrStereoscopicEye.Left, Camera.MonoOrStereoscopicEye.Right })
        {
            camera.stereoActiveEye = eye;
            Callback(renderer, "OnRenderImage", src, dst);
            Camera capture = Camera.LastRendered;
            var expectedView = eye == Camera.MonoOrStereoscopicEye.Left ? camera.NativeLeftView : camera.NativeRightView;
            var expectedProjection = eye == Camera.MonoOrStereoscopicEye.Left ? left : right;
            Check(Same(capture.worldToCameraMatrix, expectedView), "capture preserves actual " + eye + " view");
            Check(Math.Abs(capture.projectionMatrix.m02 - expectedProjection.m02) < .00001,
                "capture uses actual " + eye + " optical centre instead of cached left eye");
            Check(Math.Abs(VrWarpProjection.VerticalFov(capture.projectionMatrix) - 150) < .0001,
                "capture widens only its own eye projection");
            Check(capture.stereoTargetEye == StereoTargetEyeMask.None && !capture.enabled,
                "capture cannot become a second XR camera");
            Check(NativeOwnership(camera), "no source view/projection/pose rewrite");
            Check(ReferenceEquals(Graphics.LastDestination, dst) && Graphics.LastSource != src,
                "per-eye capture reaches current eye destination");
            Check(capture.targetTexture == null, "temporary render target detached after render");
        }
        var texture = Graphics.LastSource;
        renderer.Release();
        Check(texture.Released && material.Destroyed, "effect resources released");
        Check(NativeOwnership(camera), "native eye ownership never changes");
        renderer.Release(); Callback(renderer, "OnDisable"); Callback(renderer, "OnDestroy");
        renderer.SetFov(170); Callback(renderer, "OnRenderImage", src, dst);
        Check(ReferenceEquals(Graphics.LastSource, src), "late calls pass native image through");
        Check(failures == 0, "normal left/right capture has no failure");
        camera.InEyeRender = false;

        renderer = new VrTransitionRenderer();
        renderer.Initialize(camera, new Shader(), (op, ex) => ++failures);
        renderer.SetFov(-1);
        camera.InEyeRender = true;
        Callback(renderer, "OnRenderImage", src, dst);
        Check(failures == 1 && NativeOwnership(camera) && ReferenceEquals(Graphics.LastSource, src),
            "invalid capture projection fails back to unchanged native eye image");
        camera.InEyeRender = false;

        renderer = new VrTransitionRenderer();
        renderer.Initialize(camera, new Shader(), (op, ex) => ++failures);
        renderer.SetFov(150);
        CanonPlugins.Local.SessionGate.Allowed = false;
        Callback(renderer, "OnPostRender");
        CanonPlugins.Local.SessionGate.Allowed = true;
        Callback(renderer, "OnRenderImage", src, dst);
        Check(ReferenceEquals(Graphics.LastSource, src), "gate revocation stops all capture resources");

        foreach (var forward in new[] { new Vector3(.12f, .04f, -1), new Vector3(-.12f, -.04f, -1) })
        {
            var wide = VrWarpProjection.ForEye(left, forward, 150);
            float beforeX = left.m00 * forward.x / -forward.z - left.m02;
            float beforeY = left.m11 * forward.y / -forward.z - left.m12;
            float afterX = wide.m00 * forward.x / -forward.z - wide.m02;
            float afterY = wide.m11 * forward.y / -forward.z - wide.m12;
            Check(Math.Abs(beforeX - afterX) < .00001 && Math.Abs(beforeY - afterY) < .00001,
                "canted eye keeps head-forward at its normal screen point");
        }
        CheckConfiguredTimeline(camera);
        CheckVrViewPose();
    }

    private static void CheckVrViewPose()
    {
        // Independent local head pose and a 6 cm eye baseline.
        var origin = new Vector3(3, 4, -6);
        var originRotation = new Quaternion(0, (float)Math.Sin(.3), 0, (float)Math.Cos(.3));
        var localHead = new Vector3(.2f, 1.6f, -.1f);
        var localRotation = new Quaternion((float)Math.Sin(.1), 0, 0, (float)Math.Cos(.1));
        var head = origin + originRotation * localHead;
        var rotation = originRotation * localRotation;
        var desired = new Vector3(12, 2, 8);
        var desiredRotation = new Quaternion(0, (float)Math.Sin(.9), 0, (float)Math.Cos(.9));
        VrViewPose.Align(origin, originRotation, head, rotation, desired, desiredRotation,
            out var movedOrigin, out var movedRotation);
        Check((movedRotation * new Vector3(0, 1, 0) - new Vector3(0, 1, 0)).sqrMagnitude < .000001,
            "entry must keep tracking-space vertical aligned with world vertical");
        Check((movedOrigin + movedRotation * localHead - desired).sqrMagnitude < .000001,
            "whole-rig move places tracked head at male eye centre");
        var actualRotation = movedRotation * localRotation;
        CheckHeading(actualRotation, desiredRotation, "entry aligns female-facing heading while preserving HMD pitch/roll");
        Vector3 left = movedOrigin + movedRotation * (localHead + localRotation * new Vector3(-.03f, 0, 0));
        Vector3 right = movedOrigin + movedRotation * (localHead + localRotation * new Vector3(.03f, 0, 0));
        Check(Math.Abs((right - left).sqrMagnitude - .0036f) < .00001, "whole-rig move preserves IPD");

        // User has physically moved their head during Astral. Return still
        // aligns the current tracked pose with the saved pre-entry world view.
        var changedHead = localHead + new Vector3(.1f, -.05f, .2f);
        var changedRotation = new Quaternion(0, (float)Math.Sin(.15), 0, (float)Math.Cos(.15));
        VrViewPose.Align(movedOrigin, movedRotation, movedOrigin + movedRotation * changedHead,
            movedRotation * changedRotation, head, rotation, out var returnedOrigin, out var returnedRotation);
        Check((returnedOrigin + returnedRotation * changedHead - head).sqrMagnitude < .000001,
            "return restores exact saved viewpoint despite tracked movement");
        var returnedHeadRotation = returnedRotation * changedRotation;
        CheckHeading(returnedHeadRotation, rotation, "return restores saved heading without cancelling physical HMD pitch/roll");
        Check((returnedRotation * new Vector3(0, 1, 0) - new Vector3(0, 1, 0)).sqrMagnitude < .000001,
            "return keeps world vertical unchanged");
        CheckTiltedHeadCases();
    }

    private static void CheckHeading(Quaternion actual, Quaternion expected, string message)
    {
        Vector3 a = actual * new Vector3(0, 0, 1), e = expected * new Vector3(0, 0, 1);
        double angle = Math.Atan2(a.x, a.z) - Math.Atan2(e.x, e.z);
        Check(Math.Abs(Math.Sin(angle)) < .00001 && Math.Cos(angle) > .99999, message);
    }

    private static Quaternion Axis(int axis, double radians)
    {
        float s = (float)Math.Sin(radians * .5), c = (float)Math.Cos(radians * .5);
        return new Quaternion(axis == 0 ? s : 0, axis == 1 ? s : 0, axis == 2 ? s : 0, c);
    }

    private static void CheckTiltedHeadCases()
    {
        foreach (bool tiltedOrigin in new[] { false, true })
            foreach (float pitch in new[] { -.7f, .2f, .8f })
                foreach (float roll in new[] { -.6f, .5f })
                {
                    Quaternion originRotation = Axis(1, 2.1);
                    if (tiltedOrigin) originRotation = originRotation * Axis(0, .19) * Axis(2, .19);
                    Quaternion trackedRotation = Axis(1, .3) * Axis(0, pitch) * Axis(2, roll);
                    Vector3 origin = new Vector3(2, 4, -8), localHead = new Vector3(.1f, 1.6f, -.2f);
                    Vector3 head = origin + originRotation * localHead;
                    Quaternion target = Axis(1, -.9) * Axis(0, -.8);
                    Vector3 targetPosition = new Vector3(-4, 2, 7);
                    VrViewPose.Align(origin, originRotation, head, originRotation * trackedRotation,
                        targetPosition, target, out var p, out var q);
                    Check((q * new Vector3(0, 1, 0) - new Vector3(0, 1, 0)).sqrMagnitude < .000001,
                        "pitched/rolled headset cannot tilt tracking-space gravity");
                    Check((p + q * localHead - targetPosition).sqrMagnitude < .000001,
                        "upright alignment preserves requested eye position");
                    CheckHeading(q * trackedRotation, target, "upright alignment matches requested heading");
                    // A yaw-only origin cannot cancel the headset's own vertical look.
                    Vector3 trackedForward = trackedRotation * new Vector3(0, 0, 1);
                    Vector3 worldForward = q * trackedRotation * new Vector3(0, 0, 1);
                    Check(Math.Abs(worldForward.y - trackedForward.y) < .00001,
                        "physical HMD pitch remains the user's look angle");
                }
    }

    private static void CheckConfiguredTimeline(Camera camera)
    {
        var config = new System.Collections.Generic.Dictionary<string, string>();
        string section = "";
        foreach (string raw in System.IO.File.ReadAllLines(
            @"F:\kks\BepInEx\config\com.kks.maingame.astralmode.cfg"))
        {
            string line = raw.Trim();
            if (line.StartsWith("[")) { section = line; continue; }
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int equal = line.IndexOf('=');
            if (equal > 0) config[section + "/" + line.Substring(0, equal).Trim()] = line.Substring(equal + 1).Trim();
        }
        Func<string, string, float> number = (s, k) => float.Parse(config[s + "/" + k],
            System.Globalization.CultureInfo.InvariantCulture);
        Func<string, string, EasingType> ease = (s, k) =>
            (EasingType)Enum.Parse(typeof(EasingType), config[s + "/" + k]);
        float duration = number("[01. Warp Timing]", "Warp Duration");
        float fade = number("[01. Warp Timing]", "Fade Out Duration");
        float whiteStart = number("[10. Effect Windows (0..1)]", "WhiteCanvas Start T");
        float whiteEnd = number("[10. Effect Windows (0..1)]", "WhiteCanvas End T");
        EasingType whiteEase = ease("[11. Easing]", "WhiteCanvas Easing");
        float fromAlpha = number("[50. White Canvas]", "Start Alpha");
        float toAlpha = number("[50. White Canvas]", "End Alpha");
        float fovStart = number("[45. FOV]", "FOV StartT"), fovEnd = number("[45. FOV]", "FOV EndT");
        EasingType fovEase = ease("[45. FOV]", "FOV Easing");
        float fovTarget = number("[45. FOV]", "FOV Warp Target");
        Console.WriteLine("Installed settings: warp=" + duration + "s fade=" + fade
            + "s white=" + whiteStart + ".." + whiteEnd + " " + whiteEase
            + " fov=" + fovStart + ".." + fovEnd + " " + fovEase + " target=" + fovTarget);
        Check(duration == 3 && fade == 3, "installed active timing is 3 seconds in both directions");
        foreach (string phase in new[] { "entry", "return" })
        {
            var renderer = new VrTransitionRenderer();
            renderer.Initialize(camera, new Shader(), (op, ex) => { throw ex; });
            float baseline = renderer.OriginalFov;
            float previousAlpha = -1, previousFov = -1;
            foreach (float seconds in new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f })
            {
                float progress = seconds / duration;
                float alpha = TransitionCurve.Sample(progress, whiteStart, whiteEnd, whiteEase, fromAlpha, toAlpha);
                float fov = TransitionCurve.Sample(progress, fovStart, fovEnd, fovEase, baseline, fovTarget);
                Check(alpha >= previousAlpha && fov >= previousFov, phase + " curves progress monotonically");
                previousAlpha = alpha; previousFov = fov;
                renderer.SetFov(fov); renderer.SetWhite(alpha); Callback(renderer, "OnPostRender");
                if (seconds > 0) Check(Math.Abs(GL.Material.Color.a - alpha) < 0.000001, "configured alpha reaches white shader");
                if (seconds == 0.5f)
                {
                    Console.WriteLine(phase + " at 0.5s: whiteAlpha=" + alpha + " fov=" + fov);
                    Check(Math.Abs(alpha - 1f / 36) < 0.00001, "0.5s has only 2.78 percent white, not opaque white");
                    Check(fov > baseline && fov < fovTarget - 10, "FOV is visibly progressing before full white");
                }
                if (seconds == duration) Check(alpha == 1 && fov == fovTarget, "full white is reached only at configured endpoint");
            }
            float afterFade = 1 - Easing.Apply(ease("[11. Easing]", "FadeOut Easing"), 1);
            renderer.SetWhite(afterFade);
            Check(afterFade == 0, "configured fade finishes transparent");
            renderer.Release();
            Check(NativeOwnership(camera), phase + " configured timeline returns both eyes to XR");
        }
    }
}
