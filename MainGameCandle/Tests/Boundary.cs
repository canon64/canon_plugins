using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using N=System.Numerics;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static readonly List<GameObject> Objects=new List<GameObject>();
        public static T[] FindObjectsOfType<T>() where T:Component=>Objects.Where(o=>o.activeInHierarchy).SelectMany(o=>o.Components).OfType<T>().ToArray();
        public static void Destroy(Object o){if(o is GameObject g){g.SetActive(false);foreach(var c in g.Components)c.GetType().GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(c,null);}else if(o is Behaviour b)b.enabled=false;}
    }
    public class GameObject:Object
    {
        public readonly Transform transform;public int layer;bool _activeSelf=true;public bool activeInHierarchy=>_activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);public readonly List<Component> Components=new List<Component>();
        public GameObject(string name="object",params Type[] types){this.name=name;transform=new Transform{gameObject=this};Components.Add(transform);Objects.Add(this);foreach(var t in types)AddComponent(t);}
        public T AddComponent<T>() where T:Component,new()=>(T)AddComponent(typeof(T));
        Component AddComponent(Type t){var c=(Component)Activator.CreateInstance(t);c.gameObject=this;Components.Add(c);if(t==typeof(ParticleSystem))AddComponent(typeof(ParticleSystemRenderer));return c;}
        public T GetComponent<T>() where T:Component=>Components.OfType<T>().FirstOrDefault();
        public Component GetComponent(Type t)=>Components.FirstOrDefault(t.IsInstanceOfType);
        public T GetComponentInParent<T>() where T:Component=>GetComponent<T>()??transform.parent?.gameObject.GetComponentInParent<T>();
        public void SetActive(bool v){_activeSelf=v;}
    }
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
        public Component GetComponent(Type t)=>gameObject.GetComponent(t);
        public T GetComponentInParent<T>() where T:Component=>gameObject.GetComponentInParent<T>();
    }
    public class Behaviour:Component{public bool enabled=true;public bool isActiveAndEnabled=>enabled;}
    public class MonoBehaviour:Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine{Routine=e};e.MoveNext();return c;}
        public void StopCoroutine(Coroutine c){c.Stopped=true;}
    }
    public class Coroutine{public IEnumerator Routine;public bool Stopped;}
    public class WaitForSecondsRealtime{public WaitForSecondsRealtime(float f){}}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>-up;public static Vector3 right=>new Vector3(1,0,0);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>1e-12?this/magnitude:zero;
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator-(Vector3 a)=>new Vector3(-a.x,-a.y,-a.z);
        public static Vector3 operator*(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);public static Vector3 operator/(Vector3 a,float f)=>a*(1/f);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;public static float Angle(Vector3 a,Vector3 b)=>MathF.Acos(Math.Clamp(Dot(a.normalized,b.normalized),-1,1))*180/MathF.PI;
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 normal)=>a-normal*Dot(a,normal);
        internal N.Vector3 N=>new N.Vector3(x,y,z);internal static Vector3 From(N.Vector3 p)=>new Vector3(p.X,p.Y,p.Z);
    }
    public struct Quaternion
    {
        public float x=>Q.X;public float y=>Q.Y;public float z=>Q.Z;public float w=>Q.W;
        internal N.Quaternion Q;public static Quaternion identity=>new Quaternion{Q=N.Quaternion.Identity};
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion{Q=N.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};
        public static Vector3 operator*(Quaternion q,Vector3 p)=>Vector3.From(N.Vector3.Transform(p.N,q.Q));
        public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion{Q=a.Q*b.Q};
    }
    public struct Matrix4x4
    {
        // Unity column-vector convention, backed by a transposed Numerics matrix.
        internal N.Matrix4x4 M;public static Matrix4x4 identity=>new Matrix4x4{M=N.Matrix4x4.Identity};
        public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4{M=b.M*a.M};
        public float determinant=>M.GetDeterminant();public Matrix4x4 inverse{get{N.Matrix4x4.Invert(M,out var m);return new Matrix4x4{M=m};}}
        public Vector3 MultiplyPoint3x4(Vector3 v)=>Vector3.From(N.Vector3.Transform(v.N,M));public Vector3 MultiplyVector(Vector3 v)=>Vector3.From(N.Vector3.TransformNormal(v.N,M));
        public float this[int i]{get=>Get(i);set=>Set(i,value);}
        float Get(int i)=>i switch{0=>M.M11,1=>M.M12,2=>M.M13,3=>M.M14,4=>M.M21,5=>M.M22,6=>M.M23,7=>M.M24,8=>M.M31,9=>M.M32,10=>M.M33,11=>M.M34,12=>M.M41,13=>M.M42,14=>M.M43,_=>M.M44};
        void Set(int i,float v){switch(i){case 0:M.M11=v;break;case 1:M.M12=v;break;case 2:M.M13=v;break;case 3:M.M14=v;break;case 4:M.M21=v;break;case 5:M.M22=v;break;case 6:M.M23=v;break;case 7:M.M24=v;break;case 8:M.M31=v;break;case 9:M.M32=v;break;case 10:M.M33=v;break;case 11:M.M34=v;break;case 12:M.M41=v;break;case 13:M.M42=v;break;case 14:M.M43=v;break;case 15:M.M44=v;break;}}
    }
    public class Transform:Component
    {
        public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
        public bool ThrowParent;
        public Matrix4x4 localToWorldMatrix {get{var local=new Matrix4x4{M=N.Matrix4x4.CreateScale(localScale.N)*N.Matrix4x4.CreateFromQuaternion(localRotation.Q)*N.Matrix4x4.CreateTranslation(localPosition.N)};return parent==null?local:parent.localToWorldMatrix*local;}}
        public Matrix4x4 worldToLocalMatrix=>localToWorldMatrix.inverse;
        public Vector3 position{get=>localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);set=>localPosition=parent==null?value:parent.InverseTransformPoint(value);}
        public Quaternion rotation{get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:new Quaternion{Q=N.Quaternion.Inverse(parent.rotation.Q)*value.Q};}
        public Vector3 up=>rotation*Vector3.up;public Vector3 forward=>rotation*new Vector3(0,0,1);
        public void SetParent(Transform p,bool world=true){if(ThrowParent)throw new InvalidOperationException("parent fault");var pos=position;var rot=rotation;parent=p;if(world){position=pos;rotation=rot;}}
        public void SetPositionAndRotation(Vector3 p,Quaternion r){position=p;rotation=r;}
        public Vector3 TransformPoint(Vector3 p)=>localToWorldMatrix.MultiplyPoint3x4(p);public Vector3 InverseTransformPoint(Vector3 p)=>worldToLocalMatrix.MultiplyPoint3x4(p);
        public bool IsChildOf(Transform p)=>ReferenceEquals(this,p)||(parent!=null&&parent.IsChildOf(p));
    }
    public struct Bounds
    {
        public Vector3 min=>center-extents;public Vector3 max=>center+extents;
        public void Encapsulate(Vector3 p)=>Encapsulate(new Bounds(p,Vector3.zero));
        public float SqrDistance(Vector3 p){var d=N.Vector3.Max(N.Vector3.Zero,N.Vector3.Max(min.N-p.N,p.N-max.N));return d.LengthSquared();}
        public Vector3 center,size;public Vector3 extents=>size*.5f;public Bounds(Vector3 c,Vector3 s){center=c;size=s;}
        public void Encapsulate(Bounds b){var lo=Min(center-extents,b.center-b.extents);var hi=Max(center+extents,b.center+b.extents);center=(lo+hi)*.5f;size=hi-lo;}
        static Vector3 Min(Vector3 a,Vector3 b)=>new Vector3(MathF.Min(a.x,b.x),MathF.Min(a.y,b.y),MathF.Min(a.z,b.z));static Vector3 Max(Vector3 a,Vector3 b)=>new Vector3(MathF.Max(a.x,b.x),MathF.Max(a.y,b.y),MathF.Max(a.z,b.z));
        public bool IntersectRay(Ray ray,out float distance){float lo=0,hi=float.MaxValue;for(int i=0;i<3;i++){float o=i==0?ray.origin.x:i==1?ray.origin.y:ray.origin.z,d=i==0?ray.direction.x:i==1?ray.direction.y:ray.direction.z,c=i==0?center.x:i==1?center.y:center.z,e=i==0?extents.x:i==1?extents.y:extents.z;if(MathF.Abs(d)<1e-8){if(o<c-e||o>c+e){distance=0;return false;}}else{float a=(c-e-o)/d,b=(c+e-o)/d;lo=MathF.Max(lo,MathF.Min(a,b));hi=MathF.Min(hi,MathF.Max(a,b));}}distance=lo;return hi>=lo;}
    }
    public struct Ray{public Vector3 origin,direction;public Ray(Vector3 o,Vector3 d){origin=o;direction=d;}public Vector3 GetPoint(float t)=>origin+direction*t;}
    public struct BoneWeight{public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public class Mesh:Object{public Vector3[] vertices=Array.Empty<Vector3>();public Vector2[] uv=Array.Empty<Vector2>();public Color[] colors=Array.Empty<Color>();public int[] triangles=Array.Empty<int>();public BoneWeight[] boneWeights=Array.Empty<BoneWeight>();public Matrix4x4[] bindposes=Array.Empty<Matrix4x4>();public bool isReadable=true;public int vertexCount=>vertices.Length;public void RecalculateNormals(){}public void RecalculateBounds(){}}
    public class Shader:Object{public static readonly HashSet<string> Unavailable=new HashSet<string>();public static Shader Find(string name)=>Unavailable.Contains(name)?null:new Shader{name=name};public static int PropertyToID(string s)=>s.GetHashCode();}
    public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
    public class Material:Object
    {
        public string[] shaderKeywords=>Keywords.ToArray();
        public Shader shader;public Color color;public Texture mainTexture;public int renderQueue;
        public readonly Dictionary<string,float> Numbers=new Dictionary<string,float>();public readonly Dictionary<string,string> Tags=new Dictionary<string,string>();public readonly HashSet<string> Keywords=new HashSet<string>();
        public Material(){}public Material(Material m){shader=m.shader;color=m.color;foreach(var p in m.Numbers)Numbers[p.Key]=p.Value;foreach(var p in m.Tags)Tags[p.Key]=p.Value;foreach(var k in m.Keywords)Keywords.Add(k);renderQueue=m.renderQueue;}
        public Material(Shader s){shader=s;}public bool HasProperty(string s)=>true;public void SetFloat(string s,float f){Numbers[s]=f;}public void SetInt(string s,int i){Numbers[s]=i;}
        public void SetColor(string s,Color c){if(s=="_Color")color=c;}public void SetOverrideTag(string s,string t){Tags[s]=t;}
        public void EnableKeyword(string s){Keywords.Add(s);}public void DisableKeyword(string s){Keywords.Remove(s);}
        public bool HasProperty(int id)=>true;public Texture GetTexture(int id)=>mainTexture;public bool SetPass(int pass){GL.Material=this;return true;}
    }
    public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float magnitude=>MathF.Sqrt(x*x+y*y);}
    public struct Color32{public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}}
    public enum TextureWrapMode{Clamp}public enum TextureFormat{RGBA32}
    public class Texture:Object{public int width,height;public Color32[] Pixels;public TextureWrapMode wrapMode;}
    public class Texture2D:Texture
    {
        public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;Pixels=new Color32[w*h];}
        public void SetPixels(Color[] p){Pixels=p.Select(c=>new Color32((byte)(Math.Clamp(c.r,0,1)*255),(byte)(Math.Clamp(c.g,0,1)*255),(byte)(Math.Clamp(c.b,0,1)*255),(byte)(Math.Clamp(c.a,0,1)*255))).ToArray();}
        public void SetPixels32(Color32[] p){Pixels=(Color32[])p.Clone();}
        public void SetPixels32(int x,int y,int w,int h,Color32[] p){for(int row=0;row<h;row++)Array.Copy(p,row*w,Pixels,(row+y)*width+x,w);}
        public void Apply(bool a,bool b){}
    }
    public enum RenderTextureFormat{ARGB32}
    public class RenderTexture:Texture{public static RenderTexture active;public RenderTexture(int w,int h,int depth,RenderTextureFormat f){width=w;height=h;Pixels=new Color32[w*h];}public bool Create()=>true;public void Release(){}public static RenderTexture GetTemporary(int w,int h,int d,RenderTextureFormat f)=>new RenderTexture(w,h,d,f);public static void ReleaseTemporary(RenderTexture t){}}
    public class MaterialPropertyBlock
    {
        readonly Dictionary<int,Texture> _textures=new Dictionary<int,Texture>();
        public bool isEmpty=>_textures.Count==0;public Texture GetTexture(int id)=>_textures.TryGetValue(id,out var t)?t:null;public void SetTexture(int id,Texture t){_textures[id]=t;}
        public void Copy(MaterialPropertyBlock b){_textures.Clear();if(b!=null)foreach(var item in b._textures)_textures[item.Key]=item.Value;}
    }
    public static class Graphics{public static void Blit(Texture source,RenderTexture target){target.Pixels=(Color32[])source.Pixels.Clone();}}
    public static class GL
    {
        public const int QUADS=7;public static Material Material;public static void PushMatrix(){}public static void PopMatrix(){}public static void LoadOrtho(){}public static void Begin(int mode){}public static void Color(Color color){}public static void TexCoord2(float x,float y){}public static void Vertex3(float x,float y,float z){}
        public static void End()
        {
            var target=RenderTexture.active;var layer=Material.mainTexture;
            for(int y=0;y<target.height;y++)for(int x=0;x<target.width;x++)
            {
                int at=y*target.width+x;var s=layer.Pixels[(y*layer.height/target.height)*layer.width+x*layer.width/target.width];var d=target.Pixels[at];float a=s.a/255f;
                target.Pixels[at]=new Color32((byte)(s.r*a+d.r*(1-a)),(byte)(s.g*a+d.g*(1-a)),(byte)(s.b*a+d.b*(1-a)),255);
            }
        }
    }
    public enum ParticleSystemStopBehavior{StopEmittingAndClear}public enum ParticleSystemSimulationSpace{World,Local}public enum ParticleSystemShapeType{Sphere}public enum ParticleSystemRenderMode{Stretch,VerticalBillboard}public enum ParticleSystemRenderSpace{Velocity}
    public class ParticleSystem:Component
    {
        public bool isPlaying;public MainModule main=new MainModule();public EmissionModule emission=new EmissionModule();public ShapeModule shape=new ShapeModule();public VelocityModule velocityOverLifetime=new VelocityModule();public SizeModule sizeOverLifetime=new SizeModule();public ColorModule colorOverLifetime=new ColorModule();
        public void Stop(bool b,ParticleSystemStopBehavior s){isPlaying=false;}public void Play(){isPlaying=true;}
        public struct MinMaxCurve{public float constantMin,constantMax;public MinMaxCurve(float a,float b){constantMin=a;constantMax=b;}public MinMaxCurve(float f,AnimationCurve c){constantMin=constantMax=f;}public static implicit operator MinMaxCurve(float f)=>new MinMaxCurve(f,f);}
        public class MainModule{public bool playOnAwake,loop,startRotation3D,startSize3D;public MinMaxCurve startLifetime,startSpeed,startSize,startSizeX,startSizeY,startSizeZ,startRotation;public int maxParticles;public ParticleSystemSimulationSpace simulationSpace;}
        public class EmissionModule{public float rateOverTime;}public class ShapeModule{public bool enabled;public ParticleSystemShapeType shapeType;public float radius;}
        public class VelocityModule{public bool enabled;public ParticleSystemSimulationSpace space;public float x,y,z;}public class SizeModule{public bool enabled;public MinMaxCurve size;}public class ColorModule{public bool enabled;public Gradient color;}
    }
    public class ParticleSystemRenderer:Renderer{public ParticleSystemRenderMode renderMode;public ParticleSystemRenderSpace alignment;public Vector3 pivot;public float lengthScale,velocityScale,cameraVelocityScale;}
    public class AnimationCurve{public AnimationCurve(params Keyframe[] frames){}}public struct Keyframe{public Keyframe(float t,float v){}}
    public class Gradient{public void SetKeys(GradientColorKey[] c,GradientAlphaKey[] a){}}public struct GradientColorKey{public GradientColorKey(Color c,float t){}}public struct GradientAlphaKey{public GradientAlphaKey(float a,float t){}}
    public enum LightType{Point}public enum LightShadows{None}public class Light:Behaviour{public LightType type;public Color color;public float intensity,range;public LightShadows shadows;}
    public class Renderer:Behaviour
    {
        public Bounds bounds=new Bounds(Vector3.zero,new Vector3(10,10,10));public bool forceRenderingOff;public Rendering.ShadowCastingMode shadowCastingMode;public Material sharedMaterial;
        public Material[] sharedMaterials=>new[]{sharedMaterial};
        readonly Dictionary<int,MaterialPropertyBlock> _blocks=new Dictionary<int,MaterialPropertyBlock>();
        public void GetPropertyBlock(MaterialPropertyBlock b,int index=-1){_blocks.TryGetValue(index,out var old);b.Copy(old);}
        public void SetPropertyBlock(MaterialPropertyBlock b,int index=-1){var copy=new MaterialPropertyBlock();copy.Copy(b);_blocks[index]=copy;}
    }
    public class MeshRenderer:Renderer{}
    public class MeshFilter:Component{public Mesh sharedMesh;}
    public class SkinnedMeshRenderer:Renderer
    {
        public Mesh sharedMesh;public Transform[] bones=Array.Empty<Transform>();public Transform rootBone;public Bounds localBounds=new Bounds(Vector3.zero,new Vector3(10,10,10));public bool updateWhenOffscreen;public int Bakes;
        public void BakeMesh(Mesh m){Bakes++;m.vertices=sharedMesh.vertices.Select(transform.TransformPoint).Select(p=>QuaternionInversePoint(p)).ToArray();m.triangles=sharedMesh.triangles;m.uv=sharedMesh.uv;}
        Vector3 QuaternionInversePoint(Vector3 p)=>Vector3.From(N.Vector3.Transform((p-transform.position).N,N.Quaternion.Inverse(transform.rotation.Q)));
    }
    public class Camera:Behaviour{public static Camera main;}
    public class Collider:Component{}
    public struct RaycastHit{public Collider collider;public float distance;public Vector3 point,normal;public Transform transform=>collider.transform;}
    public enum QueryTriggerInteraction{Ignore}
    public static class Physics{public static Vector3 gravity=new Vector3(0,-9.81f,0);public static int RaycastNonAlloc(Ray ray,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction query)=>0;}
    public static class Mathf{public const float PI=MathF.PI;public static float Abs(float f)=>MathF.Abs(f);public static float Min(float a,float b)=>MathF.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static float Cos(float f)=>MathF.Cos(f);public static float Sin(float f)=>MathF.Sin(f);public static float Pow(float a,float b)=>MathF.Pow(a,b);public static float Clamp01(float f)=>Math.Clamp(f,0,1);}
    public static class Random{public static float Range(float a,float b)=>(a+b)*.5f;}
    public static class Time{public static int frameCount;public static float unscaledTime,deltaTime;}
    public static class Application{public static bool isFocused=true;}
}
namespace UnityEngine.Rendering{public enum ShadowCastingMode{On,ShadowsOnly}public enum BlendMode{Zero=0,One=1,SrcAlpha=5}public enum CullMode{Back=2}public enum CompareFunction{LessEqual=4,Always=8}public enum RenderQueue{Geometry=2000}}
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute{public BepInPlugin(string a,string b,string c){}}
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class BepInProcess:Attribute{public BepInProcess(string s){}}
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class BepInDependency:Attribute{public enum DependencyFlags{SoftDependency}public BepInDependency(string s){}public BepInDependency(string s,string version){}public BepInDependency(string s,DependencyFlags f){}}
    public class BaseUnityPlugin:UnityEngine.MonoBehaviour{public readonly Configuration.ConfigFile Config=new Configuration.ConfigFile();public BaseUnityPlugin(){gameObject=new UnityEngine.GameObject("Plugin");}}
}
namespace BepInEx.Configuration
{
    public class SettingChangedEventArgs:EventArgs{}
    public class ConfigEntry<T>{T _value;public event EventHandler SettingChanged;public ConfigEntry(T v){_value=v;}public T Value{get=>_value;set{if(Equals(_value,value))return;_value=value;SettingChanged?.Invoke(this,EventArgs.Empty);}}}
    public class ConfigFile{public bool SaveOnConfigSet;public event EventHandler<SettingChangedEventArgs> SettingChanged;public ConfigEntry<T> Bind<T>(string a,string b,T value,object d)=>new ConfigEntry<T>(value);public void Save(){}}
    public class ConfigDescription{public ConfigDescription(string s,object range){}}
    public class AcceptableValueRange<T>{public AcceptableValueRange(T a,T b){}}
}
namespace CanonGate
{
    public static class GateApi
    {
        public const string PluginGuid="canon.gate";public enum SessionKind{H,Maker}public class SessionContext{public SessionKind Kind;public UnityEngine.MonoBehaviour Scene;public ulong Id;}
        public static SessionContext Current;public static bool Allowed=true;public static bool CanRun(string owner)=>Allowed;
        public struct Permit{internal bool Captured;public bool IsValid=>Captured&&Allowed;}
        public static Permit Capture(string guid)=>new Permit{Captured=true};
        public static IDisposable Register(string g,Action start,Action stop)=>new Lease();class Lease:IDisposable{public void Dispose(){}}
    }
}
namespace MainGameUnifiedUi{public static class UnifiedUiApi{public static void RegisterTab(string g,string n,Action a,int o){}public static void UnregisterTab(string g){}}}
namespace MainGameUiInputCapture{public static class UiInputCaptureApi{public static void EndOwner(string g){}public static void ClearInputBlockRects(string g){}public static void SetIdleCursorUnlock(string g,bool b){}}}
namespace MainGameTransformGizmo
{
    public enum GizmoMode{Move,Rotate}public class TransformGizmo:UnityEngine.Behaviour{public bool IsVisible;public void SetVisible(bool b){IsVisible=b;}public void SetMode(GizmoMode m){}}
    public static class TransformGizmoApi{public static TransformGizmo Attach(UnityEngine.GameObject o)=>o.AddComponent<TransformGizmo>();public static bool EnableCameraInputCaptureOnDrag(TransformGizmo g)=>true;public static void DisableCameraInputCaptureOnDrag(TransformGizmo g){}public static void SetSizeMultiplier(TransformGizmo g,float f){}}
}
namespace Valve.VR
{
    public enum SteamVR_Input_Sources{Any,LeftHand,RightHand}public enum ETrackingResult{Uninitialized,Running_OK}
    public class SteamVR_Action_Boolean
    {
        public void AddOnStateDownListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s){}public void RemoveOnStateDownListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s){}
        public void AddOnStateUpListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s){}public void RemoveOnStateUpListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s){}
        public void AddOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> a,SteamVR_Input_Sources s){}public void RemoveOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> a,SteamVR_Input_Sources s){}
    }
    public static class SteamVR_Actions{public static class legacy_emulate{public static SteamVR_Action_Boolean Grip_Press=new SteamVR_Action_Boolean(),Axis1_Press=new SteamVR_Action_Boolean();}}
    public static class SteamVR_Events{public class Action{public bool enabled;}public static Action InputFocusAction(System.Action<bool> action)=>new Action();}
    public class SteamVR_Behaviour_Pose
    {
        public event Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
        public event Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
    }
}
namespace VRGIN.Controls
{
    public class Controller:UnityEngine.MonoBehaviour
    {
        private Lock _Lock;public Lock Current=>_Lock;public bool IsTracking=true;public Valve.VR.SteamVR_Behaviour_Pose Tracking=new Valve.VR.SteamVR_Behaviour_Pose();
        public Controller(){gameObject=new UnityEngine.GameObject("hand");}
        public bool TryAcquireFocus(out Lock focus){focus=null;if(_Lock!=null&&_Lock.IsValid)return false;focus=_Lock=new Lock();return true;}
        public class Lock{public bool IsValid=true,IsInvalidating;public void SafeRelease(){IsInvalidating=true;}public void Release(){IsValid=false;}public void NativeButtonsReleased(){if(IsInvalidating)IsValid=false;}}
    }
}
namespace VRGIN.Core
{
    public static class VR{public static bool Active;public static Rig Mode=new Rig();public static CameraRig Camera=new CameraRig();public class Rig{public VRGIN.Controls.Controller Left=new VRGIN.Controls.Controller(),Right=new VRGIN.Controls.Controller();}public class CameraRig{public UnityEngine.Transform Head=new UnityEngine.GameObject("head").transform;}}
}
public class ChaControl:UnityEngine.MonoBehaviour
{
    public ChaFileParameter fileParam=new ChaFileParameter();
    public byte sex=1;
    public UnityEngine.Renderer rendBody,rendFace;public int Eyes,Brows,Mouth;public int GetEyesPtn()=>Eyes;public int GetEyebrowPtn()=>Brows;public int GetMouthPtn()=>Mouth;public void ChangeEyesPtn(int n,bool b){Eyes=n;}public void ChangeEyebrowPtn(int n,bool b){Brows=n;}public void ChangeMouthPtn(int n,bool b){Mouth=n;}
}
namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        void DrawTab(){}
        internal void Begin(){Awake();StartSession();}
        internal void Stop()=>OnDisable();internal void Focus(bool b){if(VRGIN.Core.VR.Active)OnVrInputFocus(b);else OnApplicationFocus(b);}
        internal void DesktopFocus(bool b)=>OnApplicationFocus(b);
        internal void Trigger(Valve.VR.SteamVR_Input_Sources s)=>IgnitionTriggerDown(null,s);
        internal bool Burning=>_lit.Value;
        internal void Grip(Valve.VR.SteamVR_Input_Sources s)=>GripDown(null,s);internal void Up(Valve.VR.SteamVR_Input_Sources s)=>GripUp(null,s);
        internal bool Held=>_hand!=null;internal CandleModel Model=>_model;internal int Flying=>_flying.Count;internal int Deposits=>_deposits.Count;internal bool Loop=>_loop!=null&&_loop.enabled;
        internal void Lit(bool b){_lit.Value=b;}internal void Voice(bool b){_voice.Value=b;}
        internal void ReactTo(ChaControl c)=>React(c);internal int Faces=>_faces.Count;internal int Reactions=>_nextReaction.Count;
        internal void FinishFaceNow(ChaControl actor){var face=_faces[actor];face.Finish.Routine.MoveNext();}
        internal void AddDeposit(WaxHit hit,float volume=1)=>Deposit(hit,volume);internal void Erase()=>ClearWax();
        internal void FireVolume(float volume)=>SpawnDrop(volume);
        internal void Fire(int n){for(int i=0;i<n;i++)SpawnDrop();}internal void Fly(float dt)=>AdvanceFlying(dt);
        internal bool Hit(UnityEngine.Ray ray,float length,out WaxHit hit)=>FindWaxHit(ray,length,out hit);
        internal void Refresh()=>RefreshSurfaces();internal void Track(WaxSurface s)=>_surfaces.Add(s);
        internal int SkinInk=>_waxLayers.Values.Sum(layer=>layer.InkPixels);
        internal void AdjustWax(float frequency,float volume,float variation){_flowMultiplier.Value=frequency;_dropVolume.Value=volume;_variation.Value=variation;}
        internal void AdjustHeight(float millimeters){_wickHeight.Value=millimeters;}
        internal float DefaultDropVolume=>_dropVolume.Value;
        internal float DefaultVariation=>_variation.Value;
        internal void AdjustThickness(float mm){_waxThickness.Value=mm;}
    }
}
namespace MainGameMagicPaint{internal class PaintActor{} public static class Plugin{public const string Guid="com.kks.maingame.magicpaint";}}
namespace MainGameVoiceFaceEventBridge
{
    internal static partial class SessionGate{internal static bool Allowed=true;static readonly List<ChaControl> _females=new List<ChaControl>();internal static List<ChaControl> Females=>_females;internal static HFlag Flags;internal static HVoiceCtrl Voice;internal static UnityEngine.MonoBehaviour Scene;}
    internal sealed partial class Plugin
    {
        internal bool SyringeDialogueActive;internal FakePlayer _externalVoicePlayer=new FakePlayer();
        internal bool PrepareContact(ChaControl actor)=>PrepareActorContactVoice(actor);
        internal void LoadContacts()=>BeginContactMoans();
        internal void ClearContacts()=>EndContactMoans();
        internal int PoolCount=>_contactPools.Count;
        internal sealed class FakePlayer{internal ChaControl Target;internal bool Explicit;internal int Calls;internal UnityEngine.AudioClip Clip;internal bool RequestContactCue(string o,ChaControl a,UnityEngine.AudioClip clip){Calls++;Target=a;Explicit=true;Clip=clip;return true;}}
    }
}
public class HFlag{public UnityEngine.Transform[] transVoiceMouth;public static List<string> lstABName=new List<string>();}
namespace Manager
{
    public static class Voice
    {
        public static readonly List<UnityEngine.Transform> Stopped=new List<UnityEngine.Transform>();
        public static void Stop(UnityEngine.Transform target){Stopped.Add(target);}
    }
}
