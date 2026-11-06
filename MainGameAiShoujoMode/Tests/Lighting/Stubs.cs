using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Lighting;

namespace UnityEngine
{
    public class Object
    {
        public static void DestroyImmediate(Object value) { if (value is GameObject node) foreach (var light in node.GetComponents<Light>()) light.enabled = false; }
    }
    public class Transform : Object { public Vector3 localEulerAngles; public void SetParent(Transform parent, bool world) { } }
    public struct Vector3 { public float x, y, z; }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public struct Color { public float r, g, b, a; }
    public class GameObject : Object
    {
        private readonly List<object> _components = new List<object>();
        public Transform transform = new Transform();
        public GameObject(string name = null) { }
        public T AddComponent<T>() where T : Behaviour, new() { var component = new T { gameObject = this }; _components.Add(component); return component; }
        public T[] GetComponents<T>() => _components.OfType<T>().ToArray();
        public Scene scene = new Scene(); public bool activeInHierarchy = true;
    }
    public struct Scene { public bool IsValid() => true; public bool isLoaded => true; }
    public class Behaviour : Object
    { public bool enabled = true; public GameObject gameObject = new GameObject(); public Transform transform => gameObject.transform; }
    public class Light : Behaviour { public Color color; public float intensity; }
    public class Camera : Behaviour
    {
        public bool stereoEnabled;
        public static Camera main;
        public delegate void CameraCallback(Camera value);
        public static event CameraCallback onPreCull;
        public static int Subscribers => onPreCull?.GetInvocationList().Length ?? default(int);
        public static void Render(Camera camera) => onPreCull?.Invoke(camera);
        public T[] GetComponents<T>() => gameObject.GetComponents<T>();
    }
    public class ChaControl : Behaviour { }
    public static class Resources { public static T[] FindObjectsOfTypeAll<T>() => Array.Empty<T>(); }
    public static class GUI { public static bool enabled = true; public static Skin skin = new Skin(); public class Skin { public object button; } }
    public static class GUILayout
    {
        public static void Label(string text) { }
        public static bool Toggle(bool value, string text, params object[] options) => value;
        public static bool Button(string text, params object[] options) => false;
        public static object ExpandWidth(bool value) => null;
        public static void BeginHorizontal() { } public static void EndHorizontal() { }
    }
}
public class ChaControl : UnityEngine.ChaControl { }
public class HSceneProc
{
    public UnityEngine.Light lightCamera;
    private LightData lightData;
    public HSceneProc(UnityEngine.Light light) { lightCamera = light; lightData = new LightData { light = light, initIntensity = light.intensity, initColor = light.color }; }
    public LightData Data => lightData;
    public sealed class LightData { public UnityEngine.Light light; public UnityEngine.Color initColor; public UnityEngine.Vector2 initRot; public float initIntensity; }
}
namespace HarmonyLib
{ public static class AccessTools { public static System.Reflection.FieldInfo Field(Type type, string name) => type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); } }
namespace MapEnvironmentRuntime
{
    public sealed class Parameters { }
    public class EnvironmentWorld : UnityEngine.Behaviour
    { public Parameters Settings = new Parameters(); public bool Playing = true; public float Hour; public int Year, Month, Day; public string Weather = "clear"; }
}
namespace MainGameUnifiedUi
{
    public static class UnifiedUiApi { public static readonly Dictionary<string, Action> Tabs = new(); public static void RegisterTab(string id, string name, Action draw) => Tabs[id] = draw; public static void UnregisterTab(string id) => Tabs.Remove(id); }
}
namespace MainGameAiShoujoMode
{
    internal static class Plugin
    {
        public const string Guid = "test-lighting";
        internal static string AiLightingProfilePath, AiLightingTracePath;
        internal static bool ModeAllowed = true;
        private static bool _enabled, _trace, _native;
        internal static bool AiNativeLighting { get => _native; set { _native = value; AiLightingChanged?.Invoke(); } }
        internal static bool AiLightingEnabled { get => _enabled; set { _enabled = value; AiLightingChanged?.Invoke(); } }
        internal static bool AiLightingTrace { get => _trace; set { _trace = value; AiLightingChanged?.Invoke(); } }
        internal static event Action AiLightingChanged;
    }
}
namespace MainGameAiShoujoMode.Lighting
{
    internal sealed class NativeHLightingRenderer : IDisposable
    {
        internal static int Created, Disposed, WalkingCalls, HCalls, CameraCalls;
        internal static bool WalkingLight;
        internal static bool FailWalking;
        internal UnityEngine.Light OwnedLight => null;
        internal NativeHLightingRenderer(UnityEngine.Transform root) { ++Created; }
        internal void Walking(UnityEngine.Camera camera) { ++WalkingCalls; WalkingLight = true; if (FailWalking) throw new InvalidOperationException("partial walking setup"); }
        internal void ApplyCamera(UnityEngine.Camera camera) { ++CameraCalls; }
        internal void HReady(UnityEngine.Camera camera, HSceneProc scene, UnityEngine.Camera output = null) { ++HCalls; Suspend(); }
        internal void Suspend() { WalkingLight = false; }
        public void Dispose() { ++Disposed; Suspend(); }
    }
    internal static class LightingUnity
    {
        internal static int SnapshotCalls, LightCaptures;
        internal static LightingLight CaptureLight(UnityEngine.Light light, UnityEngine.Camera camera)
        {
            ++LightCaptures;
            return light == null ? null : new LightingLight { anchor = "world", enabled = light.enabled, active = true, settings = new[] { new LightingValue { name = "intensity", kind = "scalar", text = light.intensity.ToString(System.Globalization.CultureInfo.InvariantCulture) } } };
        }
        internal static void ApplyLight(LightingLight profile, UnityEngine.Light target, UnityEngine.Camera camera)
        { target.intensity = float.Parse(profile.settings.Single().text, System.Globalization.CultureInfo.InvariantCulture); target.enabled = profile.enabled; }
        internal static UnityEngine.Camera[] OutputCameras(UnityEngine.Camera camera) => camera == null ? Array.Empty<UnityEngine.Camera>() : new[] { camera };
        internal static bool IsImageEffect(Type type) => type == typeof(TestEffect);
        internal static LightingEffect CaptureEffect(UnityEngine.Behaviour component) => new LightingEffect { type = component.GetType().FullName, imageEffect = true, enabled = component.enabled, fields = Array.Empty<LightingValue>() };
        internal static void ValidateEffect(LightingEffect effect, UnityEngine.Behaviour component) { if (component.GetType().FullName != effect.type) throw new Exception("wrong type"); }
        internal static void ApplyEffect(LightingEffect effect, UnityEngine.Behaviour component) => component.enabled = effect.enabled;
        internal static LightingSnapshot Snapshot(string stage, UnityEngine.Camera camera, MapEnvironmentRuntime.EnvironmentWorld world, IEnumerable<ChaControl> actors)
        { ++SnapshotCalls; return new LightingSnapshot { stage = stage, hour = world.Hour, year = world.Year, month = world.Month, day = world.Day, weather = world.Weather }; }
    }
    internal sealed class TestEffect : UnityEngine.Behaviour { }
}
