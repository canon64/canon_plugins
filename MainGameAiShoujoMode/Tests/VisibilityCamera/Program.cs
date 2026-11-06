using System;
using System.Collections.Generic;
using System.Reflection;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Rendering;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Checks.Add(name); }
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    private static void Hide(SandboxIslandVisibility visibility)
    {
        var entries = (System.Collections.IList)Field(visibility, "_entries");
        var hidden = Field(visibility, "_hidden");
        foreach (var entry in entries) hidden.GetType().GetMethod("Add").Invoke(hidden, new[] { entry });
    }
    private static void Main()
    {
        var settings = new Parameters();
        var visibility = new SandboxIslandVisibility();
        visibility.Configure(settings);
        var walking = new Camera(); var h = new Camera();
        var opaque = new Renderer { shadowCastingMode = ShadowCastingMode.On };
        var unlit = new Renderer { shadowCastingMode = ShadowCastingMode.Off };
        var alreadyHidden = new Renderer { shadowCastingMode = ShadowCastingMode.Off, forceRenderingOff = true };
        Call(visibility, "Add", new object[] { new[] { opaque } });
        Call(visibility, "Add", new object[] { new[] { unlit } });
        Call(visibility, "Add", new object[] { new[] { alreadyHidden } });
        visibility.BindCamera(walking); Hide(visibility);
        Call(visibility, "BeforeCamera", walking);
        Check(unlit.forceRenderingOff && opaque.shadowCastingMode == ShadowCastingMode.ShadowsOnly, "walking culling uses real renderer overrides");

        visibility.BindCamera(null);
        Check(visibility.enabled && unlit.forceRenderingOff == false && opaque.shadowCastingMode == ShadowCastingMode.On,
            "H loading restores renderers without disabling visibility component");
        Check(alreadyHidden.forceRenderingOff, "camera switch preserves preexisting forced visibility");
        Check(visibility.Snapshot().hiddenGroups == default(int), "old camera occlusion results discarded");

        visibility.BindCamera(h); Hide(visibility);
        Call(visibility, "BeforeCamera", walking);
        Check(!unlit.forceRenderingOff, "old walking camera cannot apply H culling");
        Call(visibility, "BeforeCamera", h);
        Check(unlit.forceRenderingOff && opaque.shadowCastingMode == ShadowCastingMode.ShadowsOnly, "H camera receives same culling behavior");
        Call(visibility, "AfterCamera", h);
        Check(!unlit.forceRenderingOff && opaque.shadowCastingMode == ShadowCastingMode.On, "H render completion restores geometry and shadows");

        var resets = visibility.Snapshot().viewResets;
        visibility.BindCamera(h);
        Check(visibility.Snapshot().viewResets == resets && visibility.Snapshot().hiddenGroups > default(int), "same camera bind retains valid results");
        Call(visibility, "BeforeCamera", h);
        visibility.BindCamera(walking);
        Check(!unlit.forceRenderingOff && visibility.Snapshot().hiddenGroups == default(int), "H exit restores in-flight overrides before clearing cache");
        Hide(visibility); Call(visibility, "BeforeCamera", walking);
        Check(unlit.forceRenderingOff, "walking culling resumes after H exit");
        Call(visibility, "AfterCamera", walking);

        settings.Active = false; Call(visibility, "BeforeCamera", walking);
        Check(!unlit.forceRenderingOff, "configured OFF remains OFF after camera handoff");
        settings.Active = true; visibility.enabled = false; Call(visibility, "BeforeCamera", walking);
        Check(!unlit.forceRenderingOff, "disabled component is not silently enabled by handoff");
        visibility.enabled = true; Call(visibility, "BeforeCamera", walking); Call(visibility, "OnDestroy");
        Check(!unlit.forceRenderingOff && alreadyHidden.forceRenderingOff && opaque.shadowCastingMode == ShadowCastingMode.On,
            "mode disposal restores original renderer state");
        Console.WriteLine("VISIBILITY_CAMERA_OK checks=" + Checks.Count);
        foreach (var check in Checks) Console.WriteLine("PASS " + check);
    }
}

namespace MapEnvironmentRuntime
{
    public sealed class Parameters
    {
        public bool Active = true;
        public float N(string key) => key == "math.zero" || key == "island.visibility.enabled" && !Active ? default : Math.Sign(int.MaxValue);
        public int I(string key) => key == "math.zero" ? default : Math.Sign(int.MaxValue);
    }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On, ShadowsOnly } }
namespace UnityEngine
{
    public class Object { }
    public class Component : Object
    {
        public Transform transform = new(); public GameObject gameObject = new();
        public T GetComponent<T>() where T : class => null;
        public T GetComponentInParent<T>() where T : class => null;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();
        public int GetInstanceID() => GetHashCode();
    }
    public class GameObject { public bool activeInHierarchy = true; }
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled; }
    public class MonoBehaviour : Behaviour { }
    public class Camera : Behaviour
    {
        public static event Action<Camera> onPreCull, onPostRender;
        public void Render(Action draw) { onPreCull?.Invoke(this); draw(); onPostRender?.Invoke(this); }
    }
    public class LODGroup : Component { public LOD[] GetLODs() => Array.Empty<LOD>(); }
    public struct LOD { public Renderer[] renderers; }
    public class Transform { public Matrix4x4 localToWorldMatrix; public Vector3 position; }
    public class Renderer : Component
    {
        public bool enabled = true, forceRenderingOff; public Bounds bounds;
        public ShadowCastingMode shadowCastingMode; public Material[] sharedMaterials = Array.Empty<Material>();
    }
    public class SkinnedMeshRenderer : Renderer { }
    public class ParticleSystemRenderer : Renderer { }
    public class TrailRenderer : Renderer { }
    public class Animator { }
    public class Collider : Component { public bool enabled, isTrigger; public Rigidbody attachedRigidbody; }
    public class TerrainCollider : Collider { }
    public class Rigidbody { public bool isKinematic; }
    public class Material
    {
        public int renderQueue; public bool IsKeywordEnabled(string key) => false;
        public string GetTag(string key, bool fallback, string otherwise) => otherwise;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3 normalized => this;
        public static Vector3 operator +(Vector3 a, Vector3 b) => default;
        public static Vector3 operator -(Vector3 a, Vector3 b) => default;
        public static Vector3 Scale(Vector3 a, Vector3 b) => default;
        public static float Distance(Vector3 a, Vector3 b) => default;
    }
    public struct Matrix4x4
    {
        public static bool operator ==(Matrix4x4 a, Matrix4x4 b) => true;
        public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => false;
        public override bool Equals(object value) => value is Matrix4x4;
        public override int GetHashCode() => default;
    }
    public struct Bounds
    {
        public Vector3 center, extents; public void Encapsulate(Bounds other) { } public void Expand(float distance) { }
        public float SqrDistance(Vector3 position) => default;
    }
    public struct Plane { }
    public struct RaycastHit { public Collider collider; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public static readonly int DefaultRaycastLayers;
        public static void SyncTransforms() { }
        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] hits, float distance, int layers, QueryTriggerInteraction trigger) => default;
    }
    public static class GeometryUtility
    {
        public static void CalculateFrustumPlanes(Camera camera, Plane[] planes) { }
        public static bool TestPlanesAABB(Plane[] planes, Bounds bounds) => true;
    }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sqrt(float value) => (float)Math.Sqrt(value);
    }
    public static class Debug { public static void Log(string text) { } }
}
