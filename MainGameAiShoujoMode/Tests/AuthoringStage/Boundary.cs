using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MainGameAiShoujoMode.Authoring;

namespace Cysharp.Threading.Tasks { }
namespace MapEnvironmentRuntime { }
namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> All = new List<Object>();
        public string name;
        public Object() { All.Add(this); }
        public static T[] FindObjectsOfType<T>() where T : Object => All.OfType<T>().ToArray();
        public static void Destroy(Object item) { if (item is GameObject go) go.Destroyed = true; All.Remove(item); }
        public static void DontDestroyOnLoad(Object item) { }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => gameObject.GetComponentsInChildren<T>(includeInactive);
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour
    {
        internal static readonly List<Coroutine> Pending = new List<Coroutine>();
        public Coroutine StartCoroutine(IEnumerator routine) { var c = new Coroutine { Routine = routine }; if (routine.MoveNext()) Pending.Add(c); return c; }
        public void StopCoroutine(Coroutine c) { Pending.Remove(c); (c.Routine as IDisposable)?.Dispose(); }
        internal static void Tick() { foreach (var c in Pending.ToArray()) if (Pending.Contains(c) && !c.Routine.MoveNext()) Pending.Remove(c); }
    }
    public sealed class Coroutine { internal IEnumerator Routine; }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        public readonly Transform transform;
        public bool activeSelf = true, Destroyed;
        public GameObject(string name) { this.name = name; transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var item = new T { gameObject = this }; Components.Add(item); return item; }
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => Components.OfType<T>().Concat(transform.Children.SelectMany(c => c.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
        public void SetActive(bool active) { activeSelf = active; }
    }
    public class Transform : Component
    {
        internal readonly List<Transform> Children = new List<Transform>();
        public Transform parent;
        public Vector3 position, eulerAngles;
        public Quaternion rotation;
        public void SetParent(Transform value, bool world) { parent?.Children.Remove(this); parent = value; parent?.Children.Add(this); }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public Vector3 InverseTransformPoint(Vector3 value) => value;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default(Vector3);
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => b;
    }
    public struct Quaternion
    {
        internal Vector3 value;
        public static Quaternion Euler(Vector3 v) => new Quaternion { value = v };
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => b;
        public Vector3 eulerAngles => value;
        public static Quaternion Inverse(Quaternion q) => q;
        public static Quaternion operator *(Quaternion a, Quaternion b) => b;
    }
    public class Camera : Behaviour { public float fieldOfView; public void CopyFrom(Camera camera) { fieldOfView = camera.fieldOfView; } }
    public class Renderer : Behaviour { }
    public class Light : Behaviour { }
    public class Terrain : Behaviour { }
    public class Collider : Behaviour { }
    public class AudioListener : Behaviour { }
    public class Material : Object { }
    public struct Color { }
    public class AudioClip : Object { }
    public enum AudioType { WAV, OGGVORBIS, MPEG, UNKNOWN }
    public class AudioSource : Behaviour
    {
        public AudioClip clip; public bool playOnAwake, loop, Playing; public float spatialBlend, volume, pitch;
        public void Stop() { Playing = false; }
        public void Play() { Playing = true; }
        public void Pause() { Playing = false; }
        public void UnPause() { if (clip != null) Playing = true; }
    }
    public static class Physics { public static void SyncTransforms() { } }
    public static class RenderSettings { public static Material skybox; public static Color ambientLight, fogColor; public static bool fog; public static float fogDensity; }
    public static class Mathf { public static float LerpUnclamped(float a, float b, float t) => b; }
    public class AsyncOperation
    {
        public bool isDone;
        public event Action<AsyncOperation> completed;
        internal void Finish() { isDone = true; completed?.Invoke(this); }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name;
        public bool IsValid() => name != null;
        public bool isLoaded => SceneManager.Scenes.ContainsKey(name ?? "");
        public UnityEngine.GameObject[] GetRootGameObjects() => SceneManager.Scenes[name];
    }
    public static class SceneManager
    {
        public static event UnityEngine.Events.UnityAction<Scene> sceneUnloaded;
        internal static readonly Dictionary<string, UnityEngine.GameObject[]> Scenes = new Dictionary<string, UnityEngine.GameObject[]>();
        internal static int Unloads;
        public static Scene GetSceneByName(string name) => new Scene { name = name };
        public static UnityEngine.AsyncOperation UnloadSceneAsync(Scene scene) { Scenes.Remove(scene.name); Unloads++; sceneUnloaded?.Invoke(scene); return new UnityEngine.AsyncOperation { isDone = true }; }
    }
}
namespace UnityEngine.Events { public delegate void UnityAction<T>(T value); }
namespace UnityEngine.Networking
{
    public class UnityWebRequest : IDisposable
    {
        public bool isHttpError, isNetworkError, Aborted, Disposed;
        public string error;
        internal static UnityWebRequest Last;
        public object SendWebRequest() => this;
        public void Abort() { Aborted = true; }
        public void Dispose() { Disposed = true; }
    }
    public static class UnityWebRequestMultimedia
    {
        public static UnityWebRequest GetAudioClip(string path, UnityEngine.AudioType type) => UnityWebRequest.Last = new UnityWebRequest();
    }
    public static class DownloadHandlerAudioClip { public static UnityEngine.AudioClip GetContent(UnityWebRequest r) => new UnityEngine.AudioClip(); }
}
namespace DG.Tweening
{
    public enum Ease { Linear, InOutSine }
    public class Tween
    {
        internal Action Finish;
        internal bool Killed, Paused;
        public Tween SetEase(Ease ease) => this;
        public Tween SetUpdate(bool update) => this;
        public Tween OnComplete(Action done) { Finish = done; return this; }
        public void Kill() { Killed = true; }
        public void Pause() { Paused = true; }
        public void Play() { Paused = false; }
        internal void Complete() { if (!Killed && !Paused) Finish?.Invoke(); }
    }
    public static class DOVirtual
    {
        internal static Tween Last;
        public static Tween Float(float from, float to, float seconds, Action<float> update) => Last = new Tween { Finish = () => update(to) };
        public static Tween DelayedCall(float seconds, Action done, bool realtime) => Last = new Tween { Finish = done };
    }
}
public class ChaFileControl
{
    public sealed class Parameters { public int sex = Numbers.One; }
    public Parameters parameter = new Parameters();
    public bool LoadCharaFile(string path) => true;
}
public class ChaControl : UnityEngine.Behaviour
{
    public ChaFileControl chaFile = new ChaFileControl();
    public int chaID;
    public bool loadEnd = true, visibleAll;
    public UnityEngine.AudioSource asVoice;
    internal int eyes, mouth, brows;
    public void Load() { }
    public void SetActiveTop(bool value) { }
    public int GetEyesPtn() => eyes;
    public int GetMouthPtn() => mouth;
    public int GetEyebrowPtn() => brows;
    public int GetEyesPtnNum() => AuthoringDefaults.Current.maxExpression;
    public void ChangeEyesPtn(int value) { eyes = value; }
    public void ChangeMouthPtn(int value) { mouth = value; }
    public void ChangeEyebrowPtn(int value) { brows = value; }
    public void SetLipSync(UnityEngine.AudioSource value) { asVoice = value; }
}
namespace Manager
{
    public static class Character
    {
        public static ChaControl CreateFemale(UnityEngine.GameObject root, int id, ChaFileControl file, bool value) => root.AddComponent<ChaControl>();
        public static ChaControl CreateMale(int type, UnityEngine.GameObject root, int id, ChaFileControl file, bool value) => root.AddComponent<ChaControl>();
        public static void DeleteChara(ChaControl actor) { UnityEngine.Object.Destroy(actor); }
    }
    public static class Voice { public static UnityEngine.AudioSource Create(UnityEngine.Transform parent) { var root = new UnityEngine.GameObject("Voice"); root.transform.SetParent(parent, false); return root.AddComponent<UnityEngine.AudioSource>(); } }
}
public class AssetBundleData
{
    internal static bool Missing;
    public AssetBundleData(string bundle, string asset) { }
    public T GetAsset<T>() where T : class => Missing ? null : new UnityEngine.AudioClip() as T;
}
public static class AssetBundleManager
{
    internal static UnityEngine.AsyncOperation Pending;
    public static Task<UnityEngine.AsyncOperation> LoadLevelAsync(string bundle, string scene, bool additive, string manifest) => Task.FromResult(Pending = new UnityEngine.AsyncOperation());
}
public static class BaseMap { public const string MAP_ROOT_NAME = "Map"; }
