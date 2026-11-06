using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        internal System.Numerics.Vector3 N=>new System.Numerics.Vector3(x,y,z);
        internal static Vector3 From(System.Numerics.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);
        public float sqrMagnitude=>N.LengthSquared();public Vector3 normalized=>From(System.Numerics.Vector3.Normalize(N));
        public static Vector3 operator +(Vector3 a,Vector3 b)=>From(a.N+b.N);public static Vector3 operator -(Vector3 a,Vector3 b)=>From(a.N-b.N);
        public static Vector3 operator *(Vector3 a,float b)=>From(a.N*b);public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static bool operator ==(Vector3 a,Vector3 b)=>a.N==b.N;public static bool operator !=(Vector3 a,Vector3 b)=>a.N!=b.N;
        public override bool Equals(object o)=>o is Vector3 v&&v==this;public override int GetHashCode()=>N.GetHashCode();
        public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>From(v.N-System.Numerics.Vector3.Dot(v.N,n.N)*n.N);
    }
    public struct Quaternion
    {
        internal System.Numerics.Quaternion N;
        public static Quaternion identity=>new Quaternion{N=System.Numerics.Quaternion.Identity};
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion{N=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};
        public static Quaternion Euler(Vector3 v)=>Euler(v.x,v.y,v.z);
        public Vector3 eulerAngles {
            get{var q=N;float sinX=2*(q.W*q.X-q.Y*q.Z);float x=MathF.Asin(Math.Clamp(sinX,-1,1));float y=MathF.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.X*q.X+q.Y*q.Y));float z=MathF.Atan2(2*(q.W*q.Z+q.X*q.Y),1-2*(q.X*q.X+q.Z*q.Z));return new Vector3(x*180/MathF.PI,y*180/MathF.PI,z*180/MathF.PI);}
        }
        public static Quaternion Inverse(Quaternion q)=>new Quaternion{N=System.Numerics.Quaternion.Inverse(q.N)};
        public static Quaternion FromToRotation(Vector3 a,Vector3 b){a=a.normalized;b=b.normalized;float dot=System.Numerics.Vector3.Dot(a.N,b.N);if(dot<-.9999f)return Euler(0,180,0);var cross=System.Numerics.Vector3.Cross(a.N,b.N);return new Quaternion{N=System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(cross,1+dot))};}
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion{N=a.N*b.N};
        public static Vector3 operator *(Quaternion q,Vector3 v)=>Vector3.From(System.Numerics.Vector3.Transform(v.N,q.N));
    }
    public class Object
    {
        public static Canvas[] Canvases=Array.Empty<Canvas>();
        public static T[] FindObjectsOfType<T>() where T:class=>Canvases as T[]??Array.Empty<T>();
        public static void Destroy(object o){}
    }
    public class Transform
    {
        public Vector3 position,localScale=Vector3.one;public Quaternion rotation=Quaternion.identity;
        public Vector3 lossyScale=>localScale;public Vector3 forward=>rotation*new Vector3(0,0,1);
        public Vector3 TransformPoint(Vector3 v)=>position+rotation*Vector3.Scale(v,localScale);
        public Vector3 InverseTransformPoint(Vector3 v){v=Quaternion.Inverse(rotation)*(v-position);return new Vector3(v.x/localScale.x,v.y/localScale.y,v.z/localScale.z);}
        public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
    }
    public class GameObject {public Transform transform=new Transform();}
    public class WaitForEndOfFrame{}
    public enum EventType{Layout,Repaint,MouseUp}
    public class Event{public static Event current=new Event();public EventType type=EventType.Layout;}
    public class Canvas:Object{public bool enabled=true;}
    public struct Rect{public Rect(float x,float y,float w,float h){}}
    public static class Screen{public static int width=1000,height=600;}
    public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int FloorToInt(float f)=>(int)MathF.Floor(f);public static int RoundToInt(float f)=>(int)MathF.Round(f);public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);}
    public static class GUIUtility{public static int hotControl;}
    public static class Input{public static bool Held;public static bool GetMouseButton(int b)=>Held;}
    public enum RenderTextureFormat{ARGB32}public enum TextureFormat{RGB24}
    public class RenderTexture:Object
    {
        public static RenderTexture active;public int width,height;public static int Borrowed;
        public static RenderTexture GetTemporary(int w,int h,int depth,RenderTextureFormat format){Borrowed++;return new RenderTexture{width=w,height=h};}
        public static void ReleaseTemporary(RenderTexture r){Borrowed--;}
    }
    public class Texture2D:Object
    {
        public int width,height;
        public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;}public void ReadPixels(Rect r,int x,int y){}public void Apply(){}
        public bool LoadImage(byte[] data){width=1000;height=600;return true;}
        public byte[] EncodeToPNG()=>new byte[]{137,80,78,71};
    }
    public static class Graphics{public static void Blit(Texture2D source,RenderTexture render){}}
    public enum ScaleMode{ScaleToFit}
    public class GUIContent{public static GUIContent none=new GUIContent();}
    public static class GUI{public static bool enabled=true;public static bool Button(Rect rect,GUIContent content)=>false;public static void DrawTexture(Rect rect,Texture2D t,ScaleMode s){}public static void Label(Rect rect,string text){}}
    public static class GUILayout
    {
        public static int SliderCalls;public static Queue<float?> SliderValues=new Queue<float?>();public static bool AcquireSlider;
        public static List<(float min,float max)> SliderBounds=new List<(float,float)>();
        public static void BeginVertical(params object[] o){}public static void EndVertical(){}public static void BeginHorizontal(){}public static void EndHorizontal(){}
        public static string Click;
        public static void Label(string label,params object[] o){}public static bool Button(string text,params object[] o){if(!GUI.enabled||Click!=text)return false;Click=null;return true;}
        public static string TextField(string text,params object[] o)=>text;
        public static object Width(float f)=>null;public static object Height(float f)=>null;
        public static int Toolbar(int selected,string[] labels,params object[] o)=>selected;
        public static float HorizontalSlider(float value,float min,float max,params object[] o){SliderCalls++;SliderBounds.Add((min,max));if(AcquireSlider){GUIUtility.hotControl=81;AcquireSlider=false;}return SliderValues.Count==0?value:SliderValues.Dequeue()??value;}
    }
    public static class GUILayoutUtility{public static Rect GetRect(float w,float h,params object[] o)=>new Rect(0,0,w,h);}
    public class Camera:Object
    {
        public Transform transform=new Transform();public float fieldOfView=47,aspect=1.6f;public RenderTexture targetTexture;public bool FailRender;public bool RenderedWithoutUi;
        public void Render(){RenderedWithoutUi=Array.TrueForAll(Canvases,c=>!c.enabled);if(FailRender)throw new InvalidOperationException("render failure");}
    }
}
namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi{public static bool SliderActive;public static void Sync(string owner,string source,bool active){if(source=="h-location-slider")SliderActive=active;}}
}
public class BaseCameraControl_Ver2
{
    public struct CameraData{public UnityEngine.Vector3 Pos,Dir,Rot;public float Fov;}
}
public class CameraControl_Ver2:BaseCameraControl_Ver2
{
    public UnityEngine.Transform transBase=new UnityEngine.Transform();public UnityEngine.Camera thisCmaera=new UnityEngine.Camera();public CameraData Data;
    public CameraData GetCameraData()=>Data;
    public void SetCameraData(CameraData data){Data=data;var rot=transBase.rotation*UnityEngine.Quaternion.Euler(data.Rot);thisCmaera.transform.rotation=rot;thisCmaera.transform.position=transBase.TransformPoint(data.Pos)+rot*data.Dir;thisCmaera.fieldOfView=data.Fov;}
    public void SetWorldBase(UnityEngine.Transform t){transBase.position=t.position;transBase.rotation=t.rotation;}
}
public class ChaControl{public UnityEngine.Transform transform=new UnityEngine.Transform();public UnityEngine.GameObject objTop;}
public class HSceneProc{public HFlag flags=new HFlag();}
public class HFlag{public CameraControl_Ver2 ctrlCamera=new CameraControl_Ver2();}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Method)]public class HarmonyPrefix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)]public class HarmonyPostfix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)]public class HarmonyFinalizer:Attribute{}
    [AttributeUsage(AttributeTargets.Method)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}
}
namespace BepInEx.Bootstrap
{
    public class PluginInfo{public object Instance;}
    public static class Chainloader{public static Dictionary<string,PluginInfo> PluginInfos=new Dictionary<string,PluginInfo>();}
}
namespace CanonPlugins.Shared
{
    public static class PluginSessionGate
    {
        public struct Permit{internal int Generation;public bool IsValid=>Allowed&&Generation==SessionGeneration;}
        public static bool Allowed=true;public static int SessionGeneration=1;public static HSceneProc HScene=new HSceneProc();
        public static List<ChaControl> Females=new List<ChaControl>();public static ChaControl Male,Male1;
        public static Permit Capture()=>new Permit{Generation=SessionGeneration};
        public static T ReadScene<T>(string key)where T:class=>(key=="lstFemale"?(object)Females:key=="male"?Male:Male1)as T;
    }
}
namespace MainGameBlankMapAdd.Compatibility
{
    public static class PluginCompatibilityResolver{public static MainGameBlankMapAdd.Plugin Current;public static MainGameBlankMapAdd.Plugin ResolveLoadedPlugin()=>Current;}
}
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private string _pluginDir;private string MapWarpPath=>System.IO.Path.Combine(_pluginDir,"MapWarpPoints.json");
        private bool _sessionStarted=true,loading;private ChaControl _femaleChara;private string mapKey="island";private UnityEngine.Transform root=new UnityEngine.Transform();
        private IEnumerator pending;
        private bool TryMapWarpContext(out string key,out UnityEngine.Transform target,out bool hotel){key=mapKey;target=root;hotel=false;return _sessionStarted&&!loading&&CanonPlugins.Shared.PluginSessionGate.Allowed;}
        private void StartCoroutine(IEnumerator routine){pending=routine;}
        private static void Destroy(object o)=>UnityEngine.Object.Destroy(o);
    }
}
