using System;
using System.Collections.Generic;
using System.Reflection;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class AstralVrProjectionProbe : IDevelopmentCommand
{
    public string Name => "astral-vr-projection-probe";
    private Camera _target;
    private readonly List<object> _frames = new List<object>();
    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object Get(object target, string member)
    {
        var type = target as Type ?? target.GetType();
        return type.GetProperty(member, Flags).GetValue(target is Type ? null : target, null);
    }
    private static float[] Matrix(Matrix4x4 matrix)
    {
        var values = new float[16];
        for (int i = 0; i < 16; ++i) values[i] = matrix[i];
        return values;
    }
    private static float[] Vector(Vector3 vector) => new[] { vector.x, vector.y, vector.z };
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var vr = ReflectionCommands.ResolveType("VRGIN.Core.VR");
        var rig = Get(vr, "Camera");
        var steam = Get(rig, "SteamCam") as Component;
        var camera = steam != null ? steam.GetComponent<Camera>() : null;
        if (camera == null) throw new InvalidOperationException("No SteamCam camera");
        string action = ReflectionCommands.Text(arguments, "action", "sample");
        if (action == "arm")
        {
            Dispose();
            _target = camera;
            _frames.Clear();
            Camera.onPreRender += ObserveRender;
            return new { armed = true, camera = camera.name };
        }
        if (action == "result")
        {
            Dispose();
            return _frames.ToArray();
        }
        return Sample(camera);
    }

    private void ObserveRender(Camera camera)
    {
        if (camera != _target) return;
        _frames.Add(Sample(camera));
        if (_frames.Count >= 2) Dispose();
    }

    private static object Sample(Camera camera)
    {
        // One operation, one frame: no callbacks, capture camera, state changes,
        // persistent subscriptions or projection writes.
        Vector3 forward = camera.transform.forward;
        var leftProjection = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
        var rightProjection = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
        var leftView = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
        var rightView = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
        return new {
            frame = Time.frameCount, camera = camera.name,
            stereo = camera.stereoEnabled, activeEye = camera.stereoActiveEye.ToString(),
            projection = Matrix(camera.projectionMatrix), view = Matrix(camera.worldToCameraMatrix),
            forward = Vector(forward), position = Vector(camera.transform.position),
            left = new { projection = Matrix(leftProjection), view = Matrix(leftView), headForwardInEye = Vector(leftView.MultiplyVector(forward)) },
            right = new { projection = Matrix(rightProjection), view = Matrix(rightView), headForwardInEye = Vector(rightView.MultiplyVector(forward)) }
        };
    }
    public void Dispose() { Camera.onPreRender -= ObserveRender; _target = null; }
}
