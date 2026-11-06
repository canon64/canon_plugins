using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MainGameLgsMode { public static class Plugin { public const string Guid = "test.kks.lgs"; } }
namespace UnityEngine
{
    public class Object { }
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject(); }
    public class GameObject
    {
        public bool activeInHierarchy = true;
        public SceneManagement.Scene scene;
        public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
        public T GetComponentInParent<T>() where T : class => Components.TryGetValue(typeof(T), out var item) ? item as T : null;
    }
    public enum KeyCode { None = 0, M = 109, F8 = 289, F9 = 290, Alpha1 = 49 }
    public enum EventType { KeyDown, KeyUp, Repaint, Layout }
    [Flags] public enum EventModifiers { Shift = 1, Control = 2, Alt = 4, Command = 8, CapsLock = 16 }
    public sealed class Event { public EventType type; public KeyCode keyCode; public EventModifiers modifiers; }
    public static class Application { public static bool isFocused = true; }
    public static class GUIUtility { public static int keyboardControl; }
}
namespace UnityEngine.UI { public class InputField { } }
namespace TMPro { public class TMP_InputField { } }
namespace UnityEngine.EventSystems
{ public class EventSystem { public static EventSystem current; public UnityEngine.GameObject currentSelectedGameObject; } }
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public int handle;
        public static bool operator ==(Scene a, Scene b) => a.handle == b.handle;
        public static bool operator !=(Scene a, Scene b) => a.handle != b.handle;
        public override bool Equals(object value) => value is Scene other && this == other;
        public override int GetHashCode() => handle;
    }
    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static event Action<Scene> sceneUnloaded;
        public static void Activate(Scene next) => activeSceneChanged?.Invoke(default, next);
        public static void Unload(Scene next) => sceneUnloaded?.Invoke(next);
    }
}
namespace BepInEx.Bootstrap
{
    public sealed class PluginInfo { public UnityEngine.MonoBehaviour Instance; }
    public static class Chainloader { public static readonly Dictionary<string, PluginInfo> PluginInfos = new Dictionary<string, PluginInfo>(); }
}
namespace KKAPI.MainGame
{
    public static class GameAPI
    {
        public static event EventHandler StartH, EndH;
        public static void Begin() => StartH?.Invoke(null, EventArgs.Empty);
        public static void End() => EndH?.Invoke(null, EventArgs.Empty);
    }
}
namespace CanonGate
{
    public static class GateApi
    {
        public static AiShoujoModeScope AiShoujoMode;
        public sealed class AiShoujoModeScope
        {
            public UnityEngine.MonoBehaviour Owner;
            internal bool Suspended { get; private set; } = true;
            [MethodImpl(MethodImplOptions.NoInlining)] public void Resume() { Suspended = false; }
            [MethodImpl(MethodImplOptions.NoInlining)] public void Dispose() { Suspended = true; }
        }
    }
}
namespace ActionGame.Chara
{
    public class Base : UnityEngine.MonoBehaviour
    { [MethodImpl(MethodImplOptions.NoInlining)] public void SetActive(bool value) { gameObject.activeInHierarchy = value; } }
    public class Player : Base { }
}
public class ActionScene : UnityEngine.MonoBehaviour
{
    public ActionGame.Chara.Player Player = new ActionGame.Chara.Player();
    [MethodImpl(MethodImplOptions.NoInlining)] public IEnumerator Start() { yield return null; }
}
public class HFlag { public bool isFreeH; }
public class HSceneProc : UnityEngine.MonoBehaviour
{
    public HFlag flags = new HFlag();
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual IEnumerator Start() { return Empty(); }
    private static IEnumerator Empty() { yield break; }
}
public class VRHScene : HSceneProc
{ [MethodImpl(MethodImplOptions.NoInlining)] public override IEnumerator Start() { return base.Start(); } }
namespace MapEnvironmentRuntime
{
    public sealed class Parameters { public string Mode = "walk"; public string T(string key) => Mode; }
    public sealed class SandboxSession
    {
        public Parameters Settings { get; private set; } = new Parameters();
        [MethodImpl(MethodImplOptions.NoInlining)] public void Apply(Parameters next, bool persist) { Settings = next; }
    }
    public sealed class SandboxHost { public SandboxSession Session { get; } = new SandboxSession(); }
}
namespace MainGameAiShoujoMode
{
    public sealed class Plugin : UnityEngine.MonoBehaviour { public UnityEngine.Object _mode, _hotel; }
    public sealed class AiModeSession : UnityEngine.MonoBehaviour { public MapEnvironmentRuntime.SandboxHost _host = new MapEnvironmentRuntime.SandboxHost(); }
    public sealed class HotelModeSession : UnityEngine.MonoBehaviour { }
    public sealed class NativeActionWalkingSession
    {
        public bool Ready { get; [MethodImpl(MethodImplOptions.NoInlining)] set; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void Resume() { }
    }
    public sealed class HotelWalkingContext { public bool WalkingReady { get; [MethodImpl(MethodImplOptions.NoInlining)] set; } }
}
