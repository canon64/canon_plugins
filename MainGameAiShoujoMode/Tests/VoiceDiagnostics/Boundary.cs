using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;

internal static class TestValues
{
    private static readonly JsonDocument Data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures.json")));
    internal static float N(string key)=>Data.RootElement.GetProperty(key).GetSingle();
    internal static int I(string key)=>Data.RootElement.GetProperty(key).GetInt32();
}

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static int Searches;
        public static readonly List<Object> Objects = new List<Object>();
        public static T[] FindObjectsOfType<T>() where T : Object { Searches++; return Objects.OfType<T>().ToArray(); }
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
    }
    public class GameObject { public bool activeInHierarchy=true; }
    public class Transform : Object
    {
        public Transform parent;
        public Vector3 position,eulerAngles;
        public Vector3 right=new Vector3(TestValues.N("one"),default(float),default(float)),up=new Vector3(default(float),TestValues.N("one"),default(float)),forward=new Vector3(default(float),default(float),TestValues.N("one"));
    }
    public class Component : Object
    {
        public Transform transform=new Transform();
        public GameObject gameObject=new GameObject();
        public Object Attached;
        public T GetComponent<T>() where T: Object=>Attached as T;
    }
    public class AudioListener : Component { public bool enabled=true; }
    public class AudioClip : Object { public int channels; }
    public class AudioSource : Component
    {
        public bool isPlaying,spatialize;
        public float panStereo,spatialBlend;
        public AudioClip clip;
        public Object outputAudioMixerGroup;
    }
    public class Camera : Component { public static Camera main; }
}
namespace Manager
{
    public static class Sound { public static UnityEngine.Transform Listener; }
    public class Voice
    {
        public class Loader { public UnityEngine.Transform voiceTrans; }
        private static void Play_Standby(UnityEngine.AudioSource source, Loader loader) { }
    }
}
public class Voice_Component : UnityEngine.Object { public UnityEngine.Transform voiceTrans; }
namespace HarmonyLib
{
    public enum HarmonyPatchType { All }
    public class HarmonyMethod
    {
        internal readonly MethodInfo Method;
        public HarmonyMethod(Type type,string name) { Method=type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic); }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type,string name,Type[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static,null,args,null);
    }
    public class Harmony
    {
        public static int Hooks;
        public static MethodInfo Callback;
        public Harmony(string id) { }
        public void Patch(MethodInfo method,HarmonyMethod postfix) { Hooks++; Callback=postfix.Method; }
        public void Unpatch(MethodInfo method,HarmonyPatchType type,string id) { Hooks--;Callback=null; }
        public static void Play(UnityEngine.AudioSource source,Manager.Voice.Loader loader)=>Callback?.Invoke(null,new object[]{source,loader});
    }
}
namespace MainGameAiShoujoMode
{
    internal static class Plugin { internal const string Guid="test.hotel"; }
    public class RoomFlags { public UnityEngine.Transform[] transVoiceMouth; }
    public class HotelRoomSelection { public string name="test pose"; }
    public class AiModeHSession
    {
        public bool Busy=true;
        public RoomFlags RoomFlags;
        public UnityEngine.Camera ActiveCamera;
        public HotelRoomSelection ReadRoomSelection()=>new HotelRoomSelection();
    }
}
