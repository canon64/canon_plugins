// Native API boundary doubles. These tests exercise production ownership/conversion decisions, not Unity rendering.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public bool Destroyed;
        public static T Instantiate<T>(T value) where T : Object => (T)((ICloneable)value).Clone();
        public static void Destroy(Object value) { value.Destroyed = true; }
        public int GetInstanceID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class GameObject : Object { public int layer;public bool activeInHierarchy=true; }
    public class Component : Object
    {
        public Transform transform = new Transform();
        public GameObject gameObject = new GameObject();
        public List<Component> Components = new List<Component>();
        public T GetComponent<T>() where T:Component => Components.OfType<T>().FirstOrDefault();
        public T GetComponentInChildren<T>(bool include) where T:Component => GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool include) where T:Component => Components.OfType<T>().ToArray();
    }
    public class Coroutine { internal System.Collections.IEnumerator Routine; }
    public class MonoBehaviour : Component
    {
        public bool isActiveAndEnabled = true;
        private readonly List<Coroutine> jobs = new();
        public Coroutine StartCoroutine(System.Collections.IEnumerator routine)
        { var job = new Coroutine { Routine = routine }; if (routine.MoveNext()) jobs.Add(job); return job; }
        public void StopCoroutine(Coroutine job) => jobs.Remove(job);
        public void DrainJobs()
        { int frames=0; while(jobs.Count>0) { if(++frames>1000) throw new Exception("culling job never finishes"); foreach(var job in jobs.ToArray()) if(!job.Routine.MoveNext()) jobs.Remove(job); } }
        public int PendingJobs => jobs.Count;
    }
    public class Transform
    {
        public Vector3 localScale = Vector3.one, position;
        public Quaternion rotation = Quaternion.identity;
        public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position,rotation,localScale);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 one=>new Vector3(1,1,1);
        public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);
        public Vector3 normalized=>this*(1/magnitude);
        public static Vector3 operator *(Vector3 v,float n)=>new Vector3(v.x*n,v.y*n,v.z*n);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
    }
    public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
    public struct Vector4
    {
        public float x,y,z,w;
        public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
        public static Vector4 operator /(Vector4 value,float scale)=>new Vector4(value.x/scale,value.y/scale,value.z/scale,value.w/scale);
    }
    public struct Quaternion
    {
        public float angle;
        public static Quaternion identity=>new Quaternion();
        public static float Angle(Quaternion a,Quaternion b)=>MathF.Abs(a.angle-b.angle);
    }
    public struct Matrix4x4
    {
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new Matrix4x4{Position=p,Rotation=q,Scale=s};
        public static bool operator ==(Matrix4x4 a,Matrix4x4 b)=>a.Equals(b);
        public static bool operator !=(Matrix4x4 a,Matrix4x4 b)=>!a.Equals(b);
        public override bool Equals(object other)=>other is Matrix4x4 m && Position.Equals(m.Position) && Scale.Equals(m.Scale) && Rotation.Equals(m.Rotation);
        public override int GetHashCode()=>HashCode.Combine(Position,Scale,Rotation);
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);
    }
    public static class Mathf
    {
        public const float PI=MathF.PI;
        public static float Cos(float x)=>MathF.Cos(x);
        public static float Sin(float x)=>MathF.Sin(x);
        public static float Sqrt(float x)=>MathF.Sqrt(x);
        public static float Min(float x,float y)=>MathF.Min(x,y);
        public static float Max(float x,float y)=>MathF.Max(x,y);
    }
    public class Mesh:Object
    {
        public Vector3[] vertices,normals;public Vector2[] uv,uv2;public Vector4[] tangents;public Color[] colors;
        public int[] triangles;public Bounds bounds;public int VertexCountOverride;
        public int vertexCount=>VertexCountOverride>0?VertexCountOverride:vertices?.Length??0;
        public int subMeshCount=1;public Rendering.IndexFormat indexFormat;
        public Dictionary<int,int[]> Indices=new();
        public void SetTriangles(int[] value,int sub,bool bounds){Indices[sub]=value;triangles=value;}
        public void UploadMeshData(bool discard){}
        public void RecalculateBounds(){}
    }
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class Shader:Object{public bool isSupported=true;public static Shader Find(string name)=>new Shader{name=name};}
    public class Cubemap:Object{}
    public enum FogMode{Linear}
    public static class RenderSettings
    {
        public static Material skybox;public static Light sun;public static Rendering.AmbientMode ambientMode;
        public static Color ambientLight,ambientSkyColor,ambientEquatorColor,ambientGroundColor,fogColor;
        public static float ambientIntensity,reflectionIntensity,fogDensity,fogStartDistance,fogEndDistance;
        public static Rendering.DefaultReflectionMode defaultReflectionMode;public static Cubemap customReflection;
        public static bool fog;public static FogMode fogMode;
    }
    public static class DynamicGI{public static void UpdateEnvironment(){}}
    public enum TextureFormat { RGB24 }
    public enum TextureWrapMode { Clamp }
    public class Texture2D:Object
    {
        public int height;public TextureWrapMode wrapMode;
        public Texture2D(int w,int h,TextureFormat format,bool mip){height=h;}
        public void SetPixel(int x,int y,Color color){}
        public void Apply(bool update,bool unreadable){}
    }
    public class Material:Object,ICloneable
    {
        public Texture2D mainTexture;public int renderQueue=2000;
        public Dictionary<string,Texture2D> Textures=new();
        public Dictionary<string,float> Values = new Dictionary<string,float>();
        public Dictionary<string,Vector4> Vectors = new Dictionary<string,Vector4>();
        public Shader shader;
        public Material(object value)
        {
            if(value is Material source) {shader=source.shader;Values=new(source.Values);Vectors=new(source.Vectors);renderQueue=source.renderQueue;}
            else shader=value as Shader;
        }
        public bool HasProperty(string name)=>Values.ContainsKey(name)||Vectors.ContainsKey(name)||Textures.ContainsKey(name);
        public Texture2D GetTexture(string name)=>Textures.TryGetValue(name,out var texture)?texture:null;
        public void SetTexture(string name,Texture2D texture)=>Textures[name]=texture;
        public Vector4 GetVector(string name)=>Vectors[name];
        public void SetVector(string name,Vector4 value)=>Vectors[name]=value;
        public float GetFloat(string name)=>Values[name];
        public void SetFloat(string name,float value)=>Values[name]=value;
        public bool IsKeywordEnabled(string name)=>false;
        public string GetTag(string name,bool fallback,string defaultValue)=>"Opaque";
        public object Clone()=>new Material(shader){Values=new Dictionary<string,float>(Values),Vectors=new Dictionary<string,Vector4>(Vectors)};
    }
    public class Camera:Component
    {
        public float farClipPlane;
        public int pixelWidth=1280,pixelHeight=720;
        public static event Action<Camera> onPreCull;
        public static int Subscribers=>onPreCull?.GetInvocationList().Length??0;
        public static void Cull(Camera c)=>onPreCull?.Invoke(c);
    }
    public static class Graphics
    {
        public static List<(Camera camera,Matrix4x4 matrix)> Draws=new();
        public static void DrawMesh(Mesh mesh,Matrix4x4 matrix,Material material,int layer,Camera camera,int sub,object block,
            Rendering.ShadowCastingMode cast,bool receive,object probe,Rendering.LightProbeUsage usage)=>Draws.Add((camera,matrix));
    }
    public class Terrain:Component { public TerrainData terrainData;public Material materialTemplate;public bool drawHeightmap=true,drawTreesAndFoliage=true;public float heightmapPixelError,basemapDistance; public void Flush(){} }
    public class TerrainCollider:Collider { public TerrainData terrainData; }
    public class TerrainData:Object,ICloneable
    {
        public Vector3 size;public TreeInstance[] treeInstances=Array.Empty<TreeInstance>();
        public DetailPrototype[] detailPrototypes=Array.Empty<DetailPrototype>();
        public object Clone()=>new TerrainData{size=size,treeInstances=(TreeInstance[])treeInstances.Clone(),detailPrototypes=detailPrototypes.Select(d=>(DetailPrototype)d.MemberClone()).ToArray()};
    }
    public struct TreeInstance { public float widthScale,heightScale;public Vector3 position; }
    public class DetailPrototype { public float minWidth,maxWidth,minHeight,maxHeight;public object MemberClone()=>MemberwiseClone(); }
    public class Light:Component { public float range,shadowNearPlane; }
    public enum ParticleSystemStopBehavior {StopEmittingAndClear}
    public enum ParticleSystemScalingMode {Hierarchy,Local,Shape}
    public enum ParticleSystemCullingMode {Automatic,AlwaysSimulate}
    public class ParticleSystem:Component
    {
        public bool isPlaying,Cleared;public ParticleSystemScalingMode Mode;public ParticleSystemCullingMode Culling=ParticleSystemCullingMode.AlwaysSimulate;
        public struct MainModule {public ParticleSystem Owner;public ParticleSystemScalingMode scalingMode{get=>Owner.Mode;set=>Owner.Mode=value;}public ParticleSystemCullingMode cullingMode{get=>Owner.Culling;set=>Owner.Culling=value;}}
        public MainModule main=>new MainModule{Owner=this};
        public void Stop(bool children,ParticleSystemStopBehavior behavior){isPlaying=false;Cleared=true;}
        public void Play(bool children){isPlaying=true;}
    }
    public struct Bounds
    {
        public Vector3 center,extents;
        public void Expand(float size){extents+=Vector3.one*(size*.5f);}
        public float SqrDistance(Vector3 point)
        {
            float x=MathF.Max(0,MathF.Abs(point.x-center.x)-extents.x),y=MathF.Max(0,MathF.Abs(point.y-center.y)-extents.y),z=MathF.Max(0,MathF.Abs(point.z-center.z)-extents.z);
            return x*x+y*y+z*z;
        }
        public void Encapsulate(Bounds other)
        {
            var min=new Vector3(MathF.Min(center.x-extents.x,other.center.x-other.extents.x),MathF.Min(center.y-extents.y,other.center.y-other.extents.y),MathF.Min(center.z-extents.z,other.center.z-other.extents.z));
            var max=new Vector3(MathF.Max(center.x+extents.x,other.center.x+other.extents.x),MathF.Max(center.y+extents.y,other.center.y+other.extents.y),MathF.Max(center.z+extents.z,other.center.z+other.extents.z));
            center=(min+max)*.5f;extents=(max-min)*.5f;
        }
    }
    public class Renderer:Component
    {
        public Rendering.ShadowCastingMode shadowCastingMode=Rendering.ShadowCastingMode.On;
        public bool receiveShadows=true;
        public bool enabled=true,forceRenderingOff,isVisible;public Bounds bounds;
        private Material[] _materials={new Material(null)};
        public Material[] sharedMaterials{get=>(Material[])_materials.Clone();set=>_materials=value;}
    }
    public struct LOD { public Renderer[] renderers; }
    public class MeshRenderer:Renderer{}
    public class SkinnedMeshRenderer:Renderer{}
    public class LODGroup:Component { public bool enabled=true;public int ForcedLevel=-1,ForceCalls;public LOD[] Lods;public LOD[] GetLODs()=>Lods;public void ForceLOD(int level){ForcedLevel=level;ForceCalls++;} }
    public struct BoundingSphere { public BoundingSphere(Vector3 p,float r){} }
    public struct CullingGroupEvent { public int index,currentDistance;public bool isVisible; }
    public class CullingGroup:IDisposable
    {
        public static CullingGroup Last;
        public CullingGroup(){Last=this;}
        public Camera targetCamera;public Action<CullingGroupEvent> onStateChanged;public bool Disposed;
        public void SetDistanceReferencePoint(Transform t){}
        public void SetBoundingSpheres(BoundingSphere[] s){}
        public void SetBoundingSphereCount(int n){}
        public void SetBoundingDistances(float[] d){}
        public void Dispose(){Disposed=true;}
    }
    public struct Plane{}
    public static class GeometryUtility {public static void CalculateFrustumPlanes(Camera c,Plane[] p){}public static bool TestPlanesAABB(Plane[] p,Bounds b)=>true;}
    public class Collider:Component {public bool isTrigger,enabled=true;}
    public struct RaycastHit {public Collider collider;}
    public enum QueryTriggerInteraction {Ignore}
    public static class Physics
    {
        public static Collider NextHit;public static int Rays;
        public static Collider[] NextHits;
        public static void SyncTransforms(){}
        public static bool Raycast(Vector3 o,Vector3 d,out RaycastHit hit,float distance,int mask,QueryTriggerInteraction t){Rays++;hit=new RaycastHit{collider=NextHit};return NextHit!=null;}
        public static int RaycastNonAlloc(Vector3 o,Vector3 d,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction t)
        {
            Rays++;var targets=NextHits??(NextHit==null?Array.Empty<Collider>():new[]{NextHit});
            int count=Math.Min(hits.Length,targets.Length);for(int i=0;i<count;i++)hits[i]=new RaycastHit{collider=targets[i]};return count;
        }
    }
    public static class Time {public static float unscaledTime;}
    public static class Application {public static bool isFocused=true;public static int targetFrameRate=-1;}
    public static class QualitySettings {public static int vSyncCount=0;}
    public struct FrameTiming {public double cpuTimeFrameComplete,cpuFrameTime,gpuFrameTime;}
    public static class FrameTimingManager {public static uint GetLatestTimings(uint count,FrameTiming[] frames)=>0;public static void CaptureFrameTimings(){}}
}
namespace UnityEngine.Rendering {public enum IndexFormat{UInt16,UInt32}public enum ShadowCastingMode{Off,On}public enum LightProbeUsage{Off}public enum AmbientMode{Skybox,Trilight}public enum DefaultReflectionMode{Skybox,Custom}}
namespace UnityEngine.Profiling
{
    public sealed class Recorder
    {
        private static readonly Dictionary<string,Recorder> All=new();
        public bool enabled,isValid=true;public int sampleBlockCount=>1;public long elapsedNanoseconds=>1000000;
        public static Recorder Get(string name){if(!All.TryGetValue(name,out var value))All[name]=value=new Recorder();return value;}
        public static int EnabledCount=>All.Values.Count(r=>r.enabled);
    }
}
