using System;
using System.Collections.Generic;
using System.Reflection;
using CanonGate;
using CanonPlugins.LobbyCharacterLight;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Checks
{
    private static int count;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    private static void Call(Plugin p, string name) => typeof(Plugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, null);
    private static HSceneProc Scene(int map = 299000, string name = Plugin.LobbyScene) => new HSceneProc(map, name);
    private static void Start(HSceneProc scene, ulong id, bool allowed = true, GateApi.SessionKind kind = GateApi.SessionKind.H)
    { GateApi.Current = new GateApi.SessionContext { HScene = scene, Id = id, Kind = kind }; GateApi.Valid = allowed; GateApi.Start(); }
    private static void Main()
    {
        Console.WriteLine("Checking actual Plugin.cs lifecycle with managed event/light doubles.");
        var p = new Plugin(); Call(p, "Awake");
        var scene = Scene(); Start(scene, 1, false);
        Check(scene.lightCamera.intensity == 1f && SceneManager.Listeners == 0, "denied session changed light");
        Start(scene, 2);
        Check(scene.lightCamera.intensity == .32f, "lobby light intensity");
        Check(scene.lightCamera.color.g == .90f && scene.Data.initColor.b == .78f, "warm color");
        Check(SceneManager.Listeners == 2 && scene.Data.initIntensity == .32f, "defaults and events");
        Start(scene, 2);
        Check(SceneManager.Listeners == 2, "duplicate start subscribed twice");
        scene.lightCamera.intensity = .65f;
        scene.sprite.LightColorAllInit();
        Check(scene.lightCamera.intensity == .32f, "native reset lost override");
        SceneManager.Unload("OtherScene");
        Check(scene.lightCamera.intensity == .32f, "unrelated unload restored lobby");
        p.Config.Set("Intensity", .24f);
        Check(scene.lightCamera.intensity == .24f && scene.Data.initIntensity == .24f, "setting event not applied");
        SceneManager.Unload(Plugin.LobbyScene);
        Check(scene.lightCamera.intensity == 1f && scene.Data.initIntensity == 1.2f, "unload failed exact restoration");
        Check(scene.lightCamera.color.b == 1f && scene.Data.initColor.g == .8f, "unload failed color restoration");
        SceneManager.Load(Plugin.LobbyScene);
        Check(scene.lightCamera.intensity == .24f, "reentry failed");
        p.Config.Set("Enabled", false);
        Check(scene.lightCamera.intensity == 1f, "config disable failed restoration");
        p.Config.Set("Enabled", true);
        Check(scene.lightCamera.intensity == .24f, "config enable failed");
        GateApi.Valid = false; GateApi.Stop();
        Check(scene.lightCamera.intensity == 1f && SceneManager.Listeners == 0, "revoked cleanup failed");
        SceneManager.Load(Plugin.LobbyScene);
        Check(scene.lightCamera.intensity == 1f, "stale scene event reapplied");
        var other = Scene(100, "OtherScene"); Start(other, 3);
        Check(other.lightCamera.intensity == 1f, "other map changed"); GateApi.Stop();
        other = Scene(299000, "WrongBundle"); Start(other, 4);
        Check(other.lightCamera.intensity == 1f, "ID collision changed wrong bundle"); GateApi.Stop();
        Start(other, 5, true, GateApi.SessionKind.Maker);
        Check(SceneManager.Listeners == 0, "Maker started scene hooks"); GateApi.Stop();
        var incomplete = Scene(); incomplete.Data.light = null; Start(incomplete, 6);
        Check(incomplete.lightCamera.intensity == 1f, "partial scene changed light"); GateApi.Stop();
        var final = Scene(); Start(final, 7); Call(p, "OnDisable");
        Check(final.lightCamera.intensity == 1f && final.Data.initIntensity == 1.2f, "component disable failed restore");
        Check(SceneManager.Listeners == 0, "disable leaked event subscriptions");
        GateApi.Stop(); Call(p, "OnDestroy");
        Check(SceneManager.Listeners == 0, "double cleanup failed");
        Console.WriteLine("PASS: " + count + " lifecycle assertions; not a Unity rendering test.");
    }
}

namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a) {this.r=r;this.g=g;this.b=b;this.a=a;} }
    public class Light { public Color color = new Color(1,1,1,1); public float intensity = 1f; }
    public class GameObject { public UnityEngine.SceneManagement.Scene scene; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public enum LoadSceneMode { Additive }
    public static class SceneManager
    {
        public static event Action<Scene,LoadSceneMode> sceneLoaded;
        public static event Action<Scene> sceneUnloaded;
        public static int Listeners => (sceneLoaded?.GetInvocationList().Length ?? 0) + (sceneUnloaded?.GetInvocationList().Length ?? 0);
        public static void Load(string name) => sceneLoaded?.Invoke(new Scene { name=name }, LoadSceneMode.Additive);
        public static void Unload(string name) => sceneUnloaded?.Invoke(new Scene { name=name });
    }
}
public class BaseMap { public int no; public GameObject mapRoot; }
public class HSceneProc : MonoBehaviour
{
    public class LightData { public Light light; public Color initColor; public float initIntensity; }
    private readonly BaseMap map;
    private readonly LightData lightData;
    public LightData Data => lightData;
    public Light lightCamera = new Light(); public HSprite sprite;
    public HSceneProc(int number,string name)
    {
        map = new BaseMap { no=number, mapRoot=new GameObject { scene=new Scene { name=name } } };
        lightData = new LightData { light=lightCamera, initColor=new Color(1,.8f,.7f,1), initIntensity=1.2f };
        sprite = new HSprite(); sprite.SetLightData(lightData);
    }
}
public class HSprite
{
    private HSceneProc.LightData data;
    public void SetLightData(HSceneProc.LightData value) { data=value; }
    public void LightColorAllInit() { data.light.color=data.initColor; data.light.intensity=data.initIntensity; }
}
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string guid,string name,string version) {} }
    public class BepInDependency : Attribute { public BepInDependency(string guid,string version) {} }
    public class BepInProcess : Attribute { public BepInProcess(string process) {} }
    public class BaseUnityPlugin : MonoBehaviour { public bool isActiveAndEnabled=true; public Configuration.ConfigFile Config=new Configuration.ConfigFile(); }
}
namespace BepInEx.Configuration
{
    public class AcceptableValueRange<T> { public AcceptableValueRange(T low,T high) {} }
    public class ConfigDescription { public ConfigDescription(string text,object range) {} }
    public class ConfigEntry<T>
    {
        private T value; public ConfigEntry(T value) { this.value=value; }
        public event EventHandler SettingChanged;
        public T Value { get=>value; set { this.value=value; SettingChanged?.Invoke(this,EventArgs.Empty); } }
    }
    public class ConfigFile
    {
        private readonly Dictionary<string,object> items=new Dictionary<string,object>();
        public ConfigEntry<T> Bind<T>(string section,string key,T value,object description) { var e=new ConfigEntry<T>(value); items[key]=e; return e; }
        public void Set<T>(string key,T value) { ((ConfigEntry<T>)items[key]).Value=value; }
    }
}
namespace CanonGate
{
    public static class GateApi
    {
        public const string PluginGuid="canon.gate";
        public enum SessionKind { None,H,Maker }
        public class SessionContext { public HSceneProc HScene; public ulong Id; public SessionKind Kind; }
        public static SessionContext Current; public static bool Valid; public static Action Start,Stop;
        public readonly struct Permit { public bool IsValid=>Valid; }
        public static Permit Capture(string guid)=>new Permit();
        public static IDisposable Register(string guid,Action start,Action stop) { Start=start;Stop=stop;return new Registration(); }
        private class Registration : IDisposable { public void Dispose() { Stop();Start=null;Stop=null; } }
    }
}
