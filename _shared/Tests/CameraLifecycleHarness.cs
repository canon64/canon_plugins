using System;
using System.Collections.Generic;
using System.Reflection;
using CanonPlugins.Shared;

namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public enum RenderMode { ScreenSpaceOverlay }
    public class Object
    {
        public static void DontDestroyOnLoad(Object value) { }
        public static void Destroy(Object value)
        {
            var go = value as GameObject;
            if (go != null) { foreach (var c in go.Components.ToArray()) Destroy(c); return; }
            var component = value as Component;
            if (component == null) return;
            Send(component, "OnDisable"); Send(component, "OnDestroy");
            component.gameObject.Components.Remove(component);
        }
        public static void Send(object value, string message)
        {
            var method = value.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) method.Invoke(value, null);
        }
    }
    public class Component : Object { public GameObject gameObject; }
    public class MonoBehaviour : Component { }
    public class RectTransform : Component { }
    public class Canvas : Component { public RenderMode renderMode; }
    public class GameObject : Object
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        public readonly List<Component> Components = new List<Component>();
        public string name;
        public HideFlags hideFlags;
        public GameObject(string label, params Type[] types)
        { name = label; All.Add(this); foreach (var type in types) Add(type); }
        private Component Add(Type type)
        {
            var component = (Component)Activator.CreateInstance(type, true);
            component.gameObject = this; Components.Add(component); Send(component, "OnEnable");
            return component;
        }
        public T AddComponent<T>() where T : Component { return (T)Add(typeof(T)); }
        public T GetComponent<T>() where T : Component
        { foreach (var c in Components) if (c is T) return (T)c; return null; }
    }
    public class Camera : Component
    {
        public static Camera[] allCameras = new Camera[0];
        public static event Action<Camera> onPreCull;
        public static int Subscribers { get { return onPreCull == null ? 0 : onPreCull.GetInvocationList().Length; } }
        public static void Render(Camera camera) { if (onPreCull != null) onPreCull(camera); }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public enum LoadSceneMode { Additive }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static event Action<Scene> sceneUnloaded;
        public static void Load() { if (sceneLoaded != null) sceneLoaded(new Scene(), LoadSceneMode.Additive); }
        public static void Unload() { if (sceneUnloaded != null) sceneUnloaded(new Scene()); }
    }
}
public static class CameraLifecycleChecks
{
    private static void Check(bool condition, string reason)
    { if (!condition) throw new Exception(reason); }
    private static UnityEngine.Camera NewCamera(string name)
    { return new UnityEngine.GameObject(name).AddComponent<UnityEngine.Camera>(); }
    public static string Run()
    {
        var main = NewCamera("main");
        UnityEngine.Camera.allCameras = new[] { main };
        int changes = 0;
        var events = new CameraLifecycleEvents(() => ++changes);
        for (int i = 0; i < 600; ++i) UnityEngine.Camera.Render(main);
        Check(changes == 0, "Steady rendering must not invalidate bindings.");
        var added = NewCamera("new camera");
        UnityEngine.Camera.Render(added);
        Check(changes == 1, "A newly rendered camera must invalidate once.");
        for (int i = 0; i < 600; ++i) UnityEngine.Camera.Render(added);
        Check(changes == 1, "Known cameras must not retrigger preparation.");
        var notice = added.gameObject.GetComponent<CameraLifetimeNotice>();
        UnityEngine.Object.Send(notice, "OnDisable");
        UnityEngine.Object.Send(notice, "OnEnable");
        Check(changes == 3, "Object deactivation and reactivation must invalidate.");
        UnityEngine.SceneManagement.SceneManager.Load();
        UnityEngine.SceneManagement.SceneManager.Unload();
        Check(changes == 5, "Scene changes must invalidate.");
        foreach (var go in UnityEngine.GameObject.All)
        {
            var screen = go.GetComponent<ScreenSizeNotice>();
            if (screen != null) UnityEngine.Object.Send(screen, "OnRectTransformDimensionsChange");
        }
        Check(changes == 6, "Resolution notification must invalidate.");
        events.Dispose(); events.Dispose();
        UnityEngine.Camera.Render(NewCamera("after disposal"));
        UnityEngine.SceneManagement.SceneManager.Load();
        Check(changes == 6 && UnityEngine.Camera.Subscribers == 0, "Stop must release all callbacks without new invalidations.");
        Check(main.gameObject.GetComponent<CameraLifetimeNotice>() == null, "Stop must remove attached lifetime notices.");
        var restarted = new CameraLifecycleEvents(() => ++changes);
        Check(UnityEngine.Camera.Subscribers == 1, "Restart must not duplicate subscriptions.");
        restarted.Dispose();
        Check(UnityEngine.Camera.Subscribers == 0, "Restart cleanup must release subscriptions.");
        return "CAMERA_LIFECYCLE_PASS: steady rendering x1200, new camera, activation, scene, resize, dispose, restart";
    }
}
