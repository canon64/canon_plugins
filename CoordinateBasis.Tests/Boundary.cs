using System;
using N = System.Numerics;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal N.Vector3 Value => new N.Vector3(x, y, z);
        internal Vector3(N.Vector3 v) : this(v.X, v.Y, v.Z) { }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.Value + b.Value);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.Value - b.Value);
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        internal Quaternion(N.Quaternion v) : this(v.X, v.Y, v.Z, v.W) { }
        internal N.Quaternion Value => new N.Quaternion(x, y, z, w);
        public static Quaternion identity => new Quaternion(N.Quaternion.Identity);
        public static Quaternion Inverse(Quaternion q) => new Quaternion(N.Quaternion.Inverse(q.Value));
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(a.Value * b.Value);
        public static Quaternion Euler(Vector3 e) => new Quaternion(N.Quaternion.CreateFromYawPitchRoll(e.y * MathF.PI / 180, e.x * MathF.PI / 180, e.z * MathF.PI / 180));
        public Vector3 eulerAngles => new Vector3(0, MathF.Atan2(2 * (w * y + x * z), 1 - 2 * (y * y + x * x)) * 180 / MathF.PI, 0);
    }
    public sealed class Transform
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = new Vector3(1, 1, 1);
        public Vector3 TransformPoint(Vector3 p)
            => new Vector3(N.Vector3.Transform(p.Value * scale.Value, rotation.Value) + position.Value);
        public Vector3 InverseTransformPoint(Vector3 p)
            => new Vector3(N.Vector3.Transform(p.Value - position.Value, N.Quaternion.Inverse(rotation.Value)) / scale.Value);
    }
    public static class GUI { public static bool enabled = true; }
    public static class GUILayout
    {
        public static void BeginVertical(string style) { }
        public static void EndVertical() { }
        public static void Label(string text) { }
        public static int SelectionGrid(int selected, string[] labels, int count, object width) => selected;
        public static object Width(float width) => null;
    }
}

namespace MainGameObjectComposer
{
    internal sealed class ManagedObjectData
    {
        public string parentKind, parentRefId, parentId;
        public UnityEngine.Vector3 localPosition, localEulerAngles, worldPosition;
        public UnityEngine.Quaternion worldRotation = UnityEngine.Quaternion.identity;
        public CanonPlugins.Shared.RootRelativePose femaleRootPose;
    }
    public sealed partial class Plugin
    {
        private const string ParentKindExternal = "external", ParentKindRoot = "root";
        private sealed class Actor { public UnityEngine.Transform transform; }
        private sealed class Runtime { public Actor MainFemale; }
        private sealed class Settings { public bool UseFemaleRootCoordinates = true; }
        private readonly Runtime _runtime = new Runtime();
        private readonly Settings _settings = new Settings();
        private object _presetDraft, _movementEditPreset;
        private void SaveSettings() { }
        internal Plugin(UnityEngine.Transform root) { if (root != null) _runtime.MainFemale = new Actor { transform = root }; }
        internal void Capture(ManagedObjectData data) => CaptureObjectCoordinates(data);
        internal void Resolve(ManagedObjectData data, bool local) { _settings.UseFemaleRootCoordinates = local; ResolveObjectCoordinates(data); }
    }
}
namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        internal sealed class PosePresetEntryRuntime
        {
            public UnityEngine.Vector3 proxyPosition;
            public UnityEngine.Quaternion proxyRotation;
            public CanonPlugins.Shared.RootRelativePose femaleRootPose;
        }
        private sealed class Actor { public UnityEngine.Transform transform; }
        private sealed class Session { public Actor Character; }
        private sealed class Settings { public bool UseFemaleRootCoordinates; }
        private readonly Session _femaleSession = new Session();
        private readonly Settings _settings = new Settings();
        private object _gaugePoseBlend, _poseTransitionCoroutine;
        private void SaveSettings() { }
        internal Plugin(UnityEngine.Transform root) { if (root != null) _femaleSession.Character = new Actor { transform = root }; }
        internal void Resolve(PosePresetEntryRuntime entry, bool local, out UnityEngine.Vector3 p, out UnityEngine.Quaternion q)
        { _settings.UseFemaleRootCoordinates = local; ResolvePoseCoordinates(entry, out p, out q); }
    }
}
