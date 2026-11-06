using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object obj) { } }
    public class MonoBehaviour : Object { public bool enabled = true; public bool isActiveAndEnabled => enabled; public Transform transform = new(); }
    public class Transform
    {
        public Vector3 forward = new(0, 0, 1), localPosition, localScale = new(1,1,1);
        public Transform parent;
        public bool FailSetParent;
        private Quaternion localRotation;
        public Quaternion rotation { get => new() { Yaw = localRotation.Yaw + (parent?.rotation.Yaw ?? 0) }; set => localRotation = new() { Yaw = value.Yaw - (parent?.rotation.Yaw ?? 0) }; }
        public Vector3 position { get=>parent!=null ? parent.TransformPoint(localPosition) : localPosition; set=>localPosition=parent!=null ? parent.InverseTransformPoint(value) : value; }
        Vector3 Scale => parent==null ? localScale : new(localScale.x*parent.Scale.x,localScale.y*parent.Scale.y,localScale.z*parent.Scale.z);
        public readonly List<Transform> Children = new();
        public void SetParent(Transform next, bool keep) { if(FailSetParent)throw new Exception("injected parent failure");var p=position;var r=rotation;parent?.Children.Remove(this);parent=next;parent?.Children.Add(this);if(keep){position=p;rotation=r;} }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public Transform GetChild(int index) => Children[index];
        public Vector3 TransformPoint(Vector3 p) { var s=Scale;var q=Rotate(new(p.x*s.x,p.y*s.y,p.z*s.z),rotation.Yaw);return position+q; }
        public Vector3 InverseTransformPoint(Vector3 p) { var q=Rotate(p-position,-rotation.Yaw);var s=Scale;return new(q.x/s.x,q.y/s.y,q.z/s.z); }
        static Vector3 Rotate(Vector3 p,float degrees) { double a=degrees*Math.PI/180;return new((float)(p.x*Math.Cos(a)+p.z*Math.Sin(a)),p.y,(float)(-p.x*Math.Sin(a)+p.z*Math.Cos(a))); }
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public float sqrMagnitude=>x*x+y*y+z*z; public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z); public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z); public static Vector3 operator *(Vector3 a,float b) => new(a.x*b,a.y*b,a.z*b); }
    public struct Quaternion { public float Yaw; public static Vector3 operator *(Quaternion q,Vector3 p) {double a=q.Yaw*Math.PI/180;return new((float)(p.x*Math.Cos(a)+p.z*Math.Sin(a)),p.y,(float)(-p.x*Math.Sin(a)+p.z*Math.Cos(a)));} }
    public static class Mathf { public static float Abs(float x)=>Math.Abs(x);public static float Clamp(float x,float min,float max)=>Math.Clamp(x,min,max); }
    public struct Color { public Color(float r,float g,float b) { } }
    public enum PrimitiveType { Cube, Quad }
    public class GameObject : Object
    {
        public Transform transform = new(); public bool Active = true;
        public GameObject(string name) { this.name = name; }
        readonly Dictionary<Type, object> components = new();
        public T AddComponent<T>() where T : new() { var value = new T(); components[typeof(T)] = value; return value; }
        public T GetComponent<T>() where T : new() { if (!components.TryGetValue(typeof(T), out var value)) { value = new T(); components[typeof(T)] = value; } return (T)value; }
        public static GameObject CreatePrimitive(PrimitiveType kind) => new(kind.ToString());
        public void SetActive(bool active) { Active = active; }
    }
    public class Collider : Object { }
    public class Renderer : Object { public Material sharedMaterial; public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; }
    public class LineRenderer : Renderer { public bool useWorldSpace;public int positionCount,PositionWrites;public float startWidth,endWidth;public readonly Vector3[] Positions=new Vector3[2];public void SetPosition(int index,Vector3 point){Positions[index]=point;PositionWrites++;} }
    public class Shader : Object { public static bool Available = true; public static Shader Find(string name) => Available ? new() : null; }
    public class Material : Object { public Color color; public Texture2D mainTexture; public Material(Shader shader) { } }
    public enum TextureFormat { BGRA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public class Texture2D : Object
    {
        public static Texture2D blackTexture = new(1,1,TextureFormat.BGRA32,false);
        public FilterMode filterMode; public TextureWrapMode wrapMode; public int Uploads;
        public Texture2D(int w,int h,TextureFormat f,bool mip) { }
        public void LoadRawTextureData(IntPtr ptr,int bytes) { Uploads++; }
        public void Apply(bool mip,bool read) { }
    }
    public class Camera : MonoBehaviour { public static Camera main = new(); }
    public static class Time { public static float realtimeSinceStartup;public static int frameCount; }
    public class GUILayoutOption {}
    public class GUIStyle {}
    public class GUISkin {public GUIStyle box=new();}
    public static class GUI {public static bool enabled=true;public static GUISkin skin=new();public static string Focus="";public static void SetNextControlName(string n){}public static string GetNameOfFocusedControl()=>Focus;}
    public static class GUIUtility {public static int hotControl;}
    public enum EventType{Layout,Repaint,MouseUp,KeyDown,Used}
    public enum KeyCode{None,Return,KeypadEnter}
    public class Event {public static Event current=new();public EventType type,rawType;public int button;public KeyCode keyCode;public void Use(){type=EventType.Used;}}
    public static class GUILayout
    {
        public static void BeginVertical(GUIStyle style=null){}public static void EndVertical(){}public static void BeginHorizontal(){}public static void EndHorizontal(){}
        public static void Label(string text,params GUILayoutOption[] options){}public static GUILayoutOption Width(float w)=>new();
        public static float HorizontalSlider(float value,float min,float max,params GUILayoutOption[] options)=>value;
        public static string TextField(string text,params GUILayoutOption[] options)=>text;
        public static bool Button(string text,params GUILayoutOption[] options)=>false;
    }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off } }
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string a,string b,string c) { } }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInDependency : Attribute { public BepInDependency(string a,string b=null) { } }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInProcess : Attribute { public BepInProcess(string a) { } }
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Configuration.ConfigFile Config = new(); }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { private T value;public int Writes; public ConfigEntry(T value) { this.value=value; } public T Value { get=>value; set {if(EqualityComparer<T>.Default.Equals(this.value,value))return;this.value=value;Writes++; SettingChanged?.Invoke(this,EventArgs.Empty); } } public event EventHandler SettingChanged; }
    public class ConfigDescription { public ConfigDescription(string s,object values) { } }
    public class AcceptableValueList<T> { public AcceptableValueList(params T[] values) { } }
    public class AcceptableValueRange<T> { public AcceptableValueRange(T min,T max) { } }
    public class ConfigFile {internal readonly Dictionary<string,object> values=new();public ConfigEntry<T> Bind<T>(string a,string b,T value,object desc){string key=a+"/"+b;if(!values.TryGetValue(key,out var entry))values[key]=entry=new ConfigEntry<T>(value);return (ConfigEntry<T>)entry;} }
}
namespace CanonGate
{
    public static class GateApi
    {
        public const string PluginGuid="canon.gate";
        public enum SessionKind { None,H,Maker }
        public class SessionContext { public SessionKind Kind; public UnityEngine.MonoBehaviour Scene=new(); public ulong Id; }
        public static SessionContext Current;
        public static bool Allowed; public static int Generation; public static Action Start,Stop;
        public static IDisposable Register(string guid,Action start,Action stop) { Start=start; Stop=stop; return new Registration(); }
        private class Registration : IDisposable { public void Dispose() { Start=Stop=null; } }
        public static Permit Capture(string guid) => new(Generation);
        public readonly struct Permit { readonly int generation; public Permit(int g) { generation=g; } public bool IsValid => Allowed && generation==Generation; }
    }
}
namespace MainGameUnifiedUi { public static class UnifiedUiApi { public static void RegisterTab(string id,string label,Action draw,int order) { } public static void UnregisterTab(string id) { } } }
namespace VRGIN.Core { public static class VR { public static bool Active; public static HeadCamera Camera=new(); public class HeadCamera { public UnityEngine.Transform Head=new(); } public static ModeState Mode=new();public class ModeState{public VRGIN.Controls.Controller Left=new(),Right=new();} } }
namespace MainGameTransformGizmo
{
    public enum GizmoMode { Move,Rotate }
    public class TransformGizmo { public bool enabled,IsVisible; public void SetVisible(bool visible) { IsVisible=visible; } public void SetMode(GizmoMode mode) { } }
    public static class TransformGizmoApi
    {
        public static bool IsAvailable=true,Allowed=true,Capture=true;
        public static TransformGizmo Attach(UnityEngine.GameObject obj) => Allowed ? new() : null;
        public static bool EnableCameraInputCaptureOnDrag(TransformGizmo g) => Capture;
        public static bool DisableCameraInputCaptureOnDrag(TransformGizmo g) => true;
    }
}
namespace MainGameDesktopDisplay
{
    // Native producer is separately tested on real Windows; inject failure/counts here.
    internal sealed class DesktopCapture : IDisposable
    {
        internal struct Bounds {internal int Left,Top,Right,Bottom;internal int Width=>Right-Left;internal int Height=>Bottom-Top;}
        internal class Monitor { internal string Device; internal Bounds Rect; }
        internal static int Captures,Live; internal static bool Fail;
        internal static Monitor[] Enumerate() => new[] { new Monitor { Device="primary", Rect=new(){Right=1920,Bottom=1080} }, new Monitor { Device="secondary", Rect=new(){Left=-1920,Right=0,Bottom=1080} } };
        internal int Width {get;} internal int Height {get;} internal int ByteCount=>Width*Height*4; internal IntPtr Pixels=>new(1);
        internal Bounds Source;internal static int Created;
        bool disposed;
        internal DesktopCapture(Bounds rect,int max) { Live++;Created++;Source=rect;double ratio=Math.Min(1d,(double)max/Math.Max(rect.Width,rect.Height));Width=Math.Max(1,(int)Math.Round(rect.Width*ratio));Height=Math.Max(1,(int)Math.Round(rect.Height*ratio)); }
        internal void Capture(bool cursor) { if (Fail) throw new InvalidOperationException("injected capture failure"); Captures++; }
        public void Dispose() { if (!disposed) Live--; disposed=true; }
    }
    public sealed partial class Plugin { private string _widthText; private void DrawTab() { } }
    internal sealed class DesktopButtonInput : IDisposable
    {
        internal bool Enabled;readonly Action<bool,bool> button;readonly Action<UnityEngine.Vector2> axis;
        internal DesktopButtonInput(Action<bool,bool> button,Action<UnityEngine.Vector2> axis){this.button=button;this.axis=axis;}
        internal void Connect(){}internal void Active(bool active){Enabled=active;}
        internal void Button(bool right,bool down){if(Enabled||!down)button(right,down);}
        internal void Axis(float x,float y){if(Enabled)axis(new(x,y));}
        public void Dispose(){Enabled=false;}
    }
}
