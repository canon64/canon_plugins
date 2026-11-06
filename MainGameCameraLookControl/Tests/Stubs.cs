using N = System.Numerics;

// Pure CPU geometry fixture. This does not load or run Unity/game code.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 forward => new Vector3(0, 0, 1);
        internal N.Vector3 Numeric => new N.Vector3(x, y, z);
    }

    public struct Quaternion
    {
        internal N.Quaternion Value;
        public static Quaternion identity => new Quaternion { Value = N.Quaternion.Identity };
        public static Quaternion AngleAxis(float angle, Vector3 axis) => new Quaternion
        {
            Value = N.Quaternion.CreateFromAxisAngle(N.Vector3.Normalize(axis.Numeric), angle * MathF.PI / 180f)
        };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion { Value = N.Quaternion.Normalize(a.Value * b.Value) };
        public static Quaternion Inverse(Quaternion q) => new Quaternion { Value = N.Quaternion.Inverse(q.Value) };
        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Clamp(MathF.Abs(N.Quaternion.Dot(a.Value, b.Value)), 0, 1);
            return dot > 0.999999f ? 0f : 2 * MathF.Acos(dot) * 180f / MathF.PI;
        }
        internal N.Vector3 Forward => N.Vector3.Transform(N.Vector3.UnitZ, Value);
    }

    public sealed class Transform
    {
        public Transform parent;
        public Camera camera;
        public T GetComponent<T>() where T : class => camera as T;
        public Quaternion localRotation = Quaternion.identity;
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
    }

    public static class Mathf { public static float Abs(float value) => MathF.Abs(value); }
    public sealed class Camera { }
    public static class Time { public static int frameCount; public static float unscaledTime; }
}

public sealed class NeckLimitParameter
{
    public string name;
    public float upBendingAngle, downBendingAngle, minBendingAngle, maxBendingAngle;
}

public sealed class NeckTypeStateVer2
{
    public NeckLimitParameter[] aParam;
    public float hAngleLimit, vAngleLimit, leapSpeed;
    public bool isLimitBreakBackup;
}
