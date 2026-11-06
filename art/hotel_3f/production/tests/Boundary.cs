using System.Collections;
using System.Text.Json;
namespace MainGameAiShoujoMode { public class Plugin { public const string Guid="test.hotel"; } }
namespace MapEnvironmentRuntime
{
    public static class JsonCodec
    { public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,new JsonSerializerOptions{IncludeFields=true}); }
}
namespace UnityEngine
{
    public class Object { public bool destroyed;public static void Destroy(Object x){x.destroyed=true;} }
    public class Texture:Object { }
    public struct Vector3 { public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Color { public float r,g,b;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;}public static Color operator*(Color c,float m)=>new(c.r*m,c.g*m,c.b*m); }
    public static class Mathf { public static float Abs(float x)=>Math.Abs(x);public static float Sign(float x)=>Math.Sign(x); }
    public class Material:Object
    {public string name;public Color emission;public Material(string n){name=n;}public Material(Material m){name=m.name;emission=m.emission;}public void SetColor(string key,Color color){emission=color;} }
    public class Component:Object {public string name;}
    public class Renderer:Component {public Material[] sharedMaterials;}
    public class ReflectionProbe:Component {public Texture customBakedTexture;}
    public class Transform:Component,IEnumerable
    {
        public List<Transform> children=new();public List<Component> components=new();
        public Transform(string name){this.name=name;}
        public Transform Find(string n)=>children.FirstOrDefault(c=>c.name==n);
        public T[] GetComponentsInChildren<T>(bool includeInactive)=>components.OfType<T>().Concat(children.SelectMany(c=>c.GetComponentsInChildren<T>(includeInactive))).ToArray();
        public IEnumerator GetEnumerator()=>children.GetEnumerator();
    }
    public class AnimationClip:Object { }
    public class AnimationState {public float time,speed;public WrapMode wrapMode;}
    public enum WrapMode {ClampForever}
    public class Animation:Component
    {
        public bool enabled;public float sampled;public AnimationState state=new();public AnimationState this[string n]=>state;
        public AnimationClip GetClip(string n)=>new();public void Play(string n){state.time=0;}public void Stop(){state.time=0;}public void Sample(){sampled=state.time;}
    }
    public enum ParticleSystemStopBehavior {StopEmitting,StopEmittingAndClear}
    public class ParticleSystem:Component
    {public int plays,clears;public bool emitting;public void Stop(bool children,ParticleSystemStopBehavior mode){emitting=false;if(mode==ParticleSystemStopBehavior.StopEmittingAndClear)clears++;}public void Play(bool children){plays++;emitting=true;} }
    public class GUIStyle{}
    public class GUISkin {public GUIStyle box=new(),button=new();}
    public static class GUI {public static GUISkin skin=new();}
    public static class GUILayout
    {
        public static void BeginVertical(GUIStyle s){}public static void EndVertical(){}public static void BeginHorizontal(){}public static void EndHorizontal(){}public static void Label(string s){}
        public static object Height(float x)=>null;public static bool Button(string s,params object[] options)=>false;public static bool Toggle(bool value,string s,GUIStyle style,params object[] options)=>value;
    }
}
namespace DG.Tweening
{
    public class Tween
    {public float delay;public Action callback;public bool killed;public void Kill(){killed=true;}public void FireEvenIfCancelled()=>callback();}
    public static class DOVirtual
    {public static List<Tween> pending=new();public static Tween DelayedCall(float d,Action a,bool independent){var t=new Tween{delay=d,callback=a};pending.Add(t);return t;} }
}
namespace CanonGate
{
    public static class GateApi
    {
        public static bool allow=true;public static List<ActivityRegistration> registrations=new();public enum ActivityKind{Preview}
        public static ActivityRegistration RegisterActivity(string id,ActivityKind k,Action start,Action stop,bool sessions)
        {if(registrations.Any(r=>r.id==id))throw new Exception("duplicate registration");var r=new ActivityRegistration{id=id,start=start,stop=stop};registrations.Add(r);return r;}
        public class ActivityRegistration:IDisposable
        {
            public string id;public Action start,stop;public bool active;public int generation;
            public ActivityPermit Capture()=>new(this,generation);
            public void Begin(){if(!allow||active)return;active=true;generation++;try{start();}catch{End();throw;}}
            public void End(){if(!active)return;active=false;generation++;stop();}
            public void Dispose(){End();registrations.Remove(this);}
        }
        public readonly struct ActivityPermit
        {readonly ActivityRegistration owner;readonly int generation;public ActivityPermit(ActivityRegistration o,int g){owner=o;generation=g;}public bool IsValid=>owner!=null&&owner.active&&generation==owner.generation;}
    }
}
