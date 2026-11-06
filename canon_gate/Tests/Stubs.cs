using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static object Found;
        public static int Searches;
        public static T FindObjectOfType<T>() where T : class { ++Searches; return Found as T; }
    }
    public class GameObject { public bool activeInHierarchy = true; public SceneManagement.Scene scene; public T AddComponent<T>() where T : new() => new T(); }
    public class Behaviour : Object { private bool _active = true; public bool enabled = true; public bool isActiveAndEnabled { get => _active && enabled; set => _active = value; } public GameObject gameObject = new GameObject(); }
    public class MonoBehaviour : Behaviour { }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Contains(Vector2 p) => p.x >= x && p.y >= y && p.x < x + width && p.y < y + height;
    }
    public static class Screen { public static int width = 1280, height = 720; }
    public static class Application { public static bool isFocused = true; }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); public static float Max(float a, float b) => Math.Max(a, b); }
    public class GUIStyle { public bool wordWrap, richText; public GUIStyle() { } public GUIStyle(GUIStyle other) { } }
    public class GUISkin { public GUIStyle label = new GUIStyle(), window = new GUIStyle(), button = new GUIStyle(); }
    public struct Matrix4x4 { public static Matrix4x4 identity => default; }
    public struct Color { public static Color white => default; }
    public static class GUI
    {
        public static int depth;
        public static bool enabled = true;
        public static Matrix4x4 matrix;
        public static Color color;
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect rect, string label, GUIStyle style) { }
    }
    public static class GUIUtility { public static int hotControl; }
    public enum EventType { Repaint, MouseDown, MouseUp, MouseDrag, ScrollWheel, Used, KeyDown }
    public enum KeyCode { None, Escape }
    public class Event { public static Event current = new Event(); public EventType rawType; public KeyCode keyCode; public int button; public Vector2 mousePosition; public bool Used; public void Use() { Used = true; } }
    public static class Input { public static Vector2 mousePosition; public static bool MousePressed; public static bool GetMouseButtonDown(int button) => MousePressed; public static bool GetMouseButton(int button) => MousePressed; }
    public class GUILayoutOption { }
    public static class GUILayout
    {
        public static readonly List<string> Labels = new List<string>();
        public static bool ButtonClicked;
        public static int ScrollHot;
        public static void BeginArea(Rect rect, GUIStyle style = null) { }
        public static void EndArea() { }
        public static void Label(string text, GUIStyle style) => Labels.Add(text);
        public static Vector2 BeginScrollView(Vector2 scroll) { if (ScrollHot != 0) GUIUtility.hotControl = ScrollHot; return scroll; }
        public static void EndScrollView() { }
        public static bool Button(string label, params GUILayoutOption[] options) { bool clicked = ButtonClicked; ButtonClicked = false; return clicked; }
        public static GUILayoutOption Height(float height) => new GUILayoutOption();
    }
    public static class Time { public static int frameCount; }
}
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public struct Scene
    {
        public int handle;
        public static bool operator ==(Scene a, Scene b) => a.handle == b.handle;
        public static bool operator !=(Scene a, Scene b) => a.handle != b.handle;
        public override bool Equals(object other) => other is Scene scene && this == scene;
        public override int GetHashCode() => handle;
    }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static event Action<Scene> sceneUnloaded;
        public static void Load(Scene scene) => sceneLoaded?.Invoke(scene, LoadSceneMode.Single);
        public static void Unload(Scene scene) => sceneUnloaded?.Invoke(scene);
    }
}
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public string GUID, Name; public BepInPlugin(string a, string b, string c) { GUID = a; Name = b; } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class BepInProcess : Attribute { public BepInProcess(string name) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class BepInDependency : Attribute { public BepInDependency(string guid) { } }
    public class BaseUnityPlugin : UnityEngine.Behaviour { public Log Logger = new Log(); public Configuration.ConfigFile Config = new Configuration.ConfigFile(); }
    public class Log { public void LogInfo(object message) { } public void LogError(object message) { } }
    public class PluginInfo { public BaseUnityPlugin Instance; public BepInPlugin Metadata; public string Location; }
    public static class Paths { public static string PluginPath = "F:\\kks\\BepInEx\\plugins"; }
}
namespace BepInEx.Configuration
{
    public class SettingChangedEventArgs : EventArgs { public object ChangedSetting; }
    public class ConfigEntry<T>
    {
        private T _value;
        internal Action Changed;
        public T Value { get => _value; set { _value = value; Changed?.Invoke(); } }
    }
    public class ConfigFile
    {
        public readonly Dictionary<string, object> Entries = new Dictionary<string, object>();
        public event EventHandler<SettingChangedEventArgs> SettingChanged;
        public int ReloadCount;
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description)
        {
            var entry = new ConfigEntry<T> { Value = value };
            entry.Changed = () => SettingChanged?.Invoke(this, new SettingChangedEventArgs { ChangedSetting = entry });
            Entries.Add(key, entry);
            return entry;
        }
        public void Reload() { ++ReloadCount; }
    }
}
namespace KKAPI.Maker
{
    public static class MakerAPI
    {
        public static bool Inside;
        public static int Reads;
        public static bool InsideAndLoaded { get { ++Reads; return Inside; } }
        public static event EventHandler MakerFinishedLoading;
        public static event EventHandler MakerExiting;
        public static void Enter() { Inside = true; MakerFinishedLoading?.Invoke(null, EventArgs.Empty); }
        public static void Exit() { Inside = false; MakerExiting?.Invoke(null, EventArgs.Empty); }
    }
}
namespace BepInEx.Bootstrap
{
    public static class Chainloader { public static Dictionary<string, BepInEx.PluginInfo> PluginInfos = new Dictionary<string, BepInEx.PluginInfo>(); }
}
namespace KKAPI.MainGame
{
    public static class GameAPI
    {
        public static event EventHandler StartH;
        public static event EventHandler EndH;
        public static void Begin() => StartH?.Invoke(null, EventArgs.Empty);
        public static void End() => EndH?.Invoke(null, EventArgs.Empty);
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(Type type, string name) => type?.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        public static System.Reflection.FieldInfo Field(Type type, string name) => type?.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }
    public class Harmony { public Harmony(string id) { } public void PatchAll(Type type) { } public void Patch(System.Reflection.MethodBase method, HarmonyMethod prefix = null) { } public void UnpatchSelf() { } }
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
public partial class HSceneProc : UnityEngine.MonoBehaviour { }
namespace MainGameBlankMapAdd
{
    public class Plugin : BepInEx.BaseUnityPlugin { private void OnDestroy() { } }
    public static class BlankRoomState
    {
        public static bool Inside;
        public static int Reads;
        public static bool ThrowOnRead;
        public static bool IsInBlankRoom { get { ++Reads; if (ThrowOnRead) throw new Exception("room unavailable"); return Inside; } }
        public static event Action<bool> Changed;
        public static void Set(bool inside) { Inside = inside; Changed?.Invoke(inside); }
    }
}
