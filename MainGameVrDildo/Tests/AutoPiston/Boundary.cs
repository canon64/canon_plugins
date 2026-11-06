using System;
using System.Collections.Generic;
using N = System.Numerics;
namespace VRGIN.Core { public static class VR { public static bool Active; } }

// Native Animation/Unity/input boundary only. This harness does not run Unity or KKS.
namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x=x; this.y=y; } }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static Vector3 Divide(Vector3 a,Vector3 b)=>new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
    }
    public struct Quaternion
    {
        internal N.Quaternion Value;
        public static Quaternion identity => new Quaternion { Value=N.Quaternion.Identity };
        public static Quaternion Inverse(Quaternion q)=>new Quaternion { Value=N.Quaternion.Inverse(q.Value) };
        public static Vector3 operator *(Quaternion q,Vector3 v)
        { var r=N.Vector3.Transform(new N.Vector3(v.x,v.y,v.z),q.Value); return new Vector3(r.X,r.Y,r.Z); }
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion { Value=a.Value*b.Value };
        public static Quaternion RotateZ(float radians)=>new Quaternion { Value=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitZ,radians) };
    }
    public class Transform
    {
        public Transform parent;
        public string name;
        public readonly List<Transform> children=new List<Transform>();
        public Vector3 localPosition,localScale=Vector3.one;
        public Quaternion localRotation=Quaternion.identity;
        public Vector3 lossyScale=>parent==null?localScale:Vector3.Scale(parent.lossyScale,localScale);
        public Quaternion rotation { get=>parent==null?localRotation:parent.rotation*localRotation;
            set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value; }
        public Vector3 position { get=>parent==null?localPosition:parent.TransformPoint(localPosition);
            set=>localPosition=parent==null?value:parent.InverseTransformPoint(value); }
        public Vector3 TransformPoint(Vector3 v)=>position+rotation*Vector3.Scale(v,lossyScale);
        public Vector3 InverseTransformPoint(Vector3 v)=>Vector3.Divide(Quaternion.Inverse(rotation)*(v-position),lossyScale);
        public void SetParent(Transform p,bool world)
        {
            Vector3 pos=position; Quaternion rot=rotation; Vector3 scale=lossyScale;
            parent?.children.Remove(this);parent=p;parent?.children.Add(this);
            if(world) { position=pos;rotation=rot;localScale=p==null?scale:Vector3.Divide(scale,p.lossyScale); }
        }
        public bool IsChildOf(Transform p)=>parent!=null&&(parent==p||parent.IsChildOf(p));
    }
    public class GameObject
    {
        public readonly Transform transform;
        public GameObject(string name="object") { transform=new Transform { name=name }; }
        public T AddComponent<T>() where T:new()
        { var v=new T();if(v is Animation a)a.Root=transform;return v; }
    }
    public static class Application { public static bool isFocused=true; }
    public static class Time { public static float unscaledDeltaTime=0.05f, time; }
    public struct Rect { }
    public static class GUIUtility { public static int hotControl; }
    public enum KeyCode { LeftControl, RightControl }
    public static class Input
    {
        public static bool LeftControl, RightControl;
        public static bool GetKey(KeyCode key)=>key==KeyCode.LeftControl?LeftControl:RightControl;
        public static bool GetMouseButton(int b)=>b==0;
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Abs(float v)=>Math.Abs(v);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Sin(float a)=>(float)Math.Sin(a);
        public static float Cos(float a)=>(float)Math.Cos(a);
        public static float Repeat(float v,float length)=>v-(float)Math.Floor(v/length)*length;
        public static bool Approximately(float a,float b)=>Math.Abs(a-b)<0.000001f;
    }
    public enum AnimationCullingType { AlwaysAnimate }
    public enum WrapMode { Loop }
    public struct Keyframe
    {
        public float time,value,inTangent,outTangent;
        public Keyframe(float t,float v,float i,float o) { time=t;value=v;inTangent=i;outTangent=o; }
    }
    public class AnimationCurve
    {
        public Keyframe[] Keys;
        public AnimationCurve(Keyframe[] keys) { Keys=keys; }
        public float Evaluate(float t)
        {
            int i=0;while(i<Keys.Length-2&&t>Keys[i+1].time)i++;
            var a=Keys[i];var b=Keys[i+1];float dt=b.time-a.time;float s=(t-a.time)/dt;
            return (2*s*s*s-3*s*s+1)*a.value+(s*s*s-2*s*s+s)*dt*a.outTangent
                +(-2*s*s*s+3*s*s)*b.value+(s*s*s-s*s)*dt*b.inTangent;
        }
    }
    public class AnimationClip
    {
        public bool legacy; public string name,Path; public WrapMode wrapMode; public AnimationCurve Curve;
        public void SetCurve(string path,Type type,string property,AnimationCurve curve) { Path=path;Curve=curve; }
    }
    public class AnimationState { public float normalizedTime,speed; }
    public class Animation
    {
        public Transform Root;
        public bool enabled,playAutomatically;
        public AnimationCullingType cullingType;
        public AnimationClip Clip;
        private AnimationState State;
        public AnimationState this[string name]=>State;
        public void AddClip(AnimationClip c,string name) { Clip=c;State=new AnimationState(); }
        public void RemoveClip(string name) { Clip=null;State=null; }
        public void Stop() { }
        public void Play(string name) { }
        public void Sample()
        {
            if(Clip==null)return;
            var target=Root.children.Find(t=>t.name==Clip.Path);
            target.localPosition=new Vector3(0,Clip.Curve.Evaluate(Mathf.Repeat(State.normalizedTime,1)),0);
        }
        public void Advance(float seconds)
        { if(!enabled||State==null)return;State.normalizedTime+=State.speed*seconds;Sample(); }
    }
}
namespace CanonPlugins.Shared { public static class PluginSessionGate { public static bool Allowed=true; } }
namespace MainGameTransformGizmo
{
    public static class Plugin { public const string GUID="gizmo"; }
    public class TransformGizmo { public bool IsDragging=true;public int GetInstanceID()=>17; }
}
namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static bool Blocked,Left=true,Right;
        public static float Wheel;
        public static string LastOwner;
        public static void SetInputBlockRects(string owner,UnityEngine.Rect[] r) { }
        public static void SetIdleCursorUnlock(string owner,bool enabled) { }
        public static void Sync(string owner,string source,bool active) { }
        public static void ClearInputBlockRects(string owner) { }
        public static UnityEngine.Vector2 GetMouseScrollDeltaForOwner(string owner)
        { LastOwner=owner;return new UnityEngine.Vector2(0,Blocked?0:Wheel); }
        public static bool GetMouseButtonForOwner(string owner,int button)=>!Blocked&&(button==0?Left:Right);
    }
}
namespace MainGameVrDildo
{
    public class Settings { public bool Enabled=true,PistonRhythmSync;public float PistonSpeed=1,PistonStrokeCm=5; }
    public class Controller { public UnityEngine.Transform transform=new UnityEngine.Transform(); }
    public sealed partial class Plugin
    {
        private const string Guid="dildo";
        public bool isActiveAndEnabled=true;
        internal void Rhythm(bool active,double phase,double rate)=>ReceivePistonRhythm(active,phase,rate);
        internal void SyncRhythm(bool enabled) { _settings.PistonRhythmSync=enabled;ChangePistonRhythmSetting(); }
        private readonly HashSet<object> _syringeVoiceLeases=new HashSet<object>();
        private UnityEngine.Rect _expressionMenuRect=default,_window=default;
        private bool _showSettings=false;
        private void CloseExpressionMenu() { ClearExpressionMenuInput(); }
        private void EndCloneReactionSession() { }
        private void EndGaugePoseSource() { }
        private void ReleaseGaugeFace() { }
        private void DetachDesktopGizmo() { _desktopGizmoProxy=null;_desktopGizmo=null; }
        public static Plugin Instance;
        internal UnityEngine.GameObject _model=new UnityEngine.GameObject("base");
        internal Settings _settings=new Settings();
        internal Controller _grabController;
        internal UnityEngine.GameObject _desktopGizmoProxy;
        internal MainGameTransformGizmo.TransformGizmo _desktopGizmo=new MainGameTransformGizmo.TransformGizmo();
        private readonly Dictionary<string,string> _edits=new Dictionary<string,string>();
        private static void Destroy(object v) { }
        private void ResetAibuMotion() { }
        private void ReleaseGrab() { _grabController=null; }
        public Plugin() { CreatePistonHierarchy(new UnityEngine.GameObject("asset"));Instance=this; }
        internal void Enable(bool v) { if(v) Edit(1f,_settings.PistonStrokeCm);else StopPiston(); }
        internal bool Enabled=>_pistonEnabled;
        internal bool Driving=>IsPistonDriving;
        internal UnityEngine.Transform Motion=>_pistonMotion;
        internal UnityEngine.Animation Animation=>_pistonAnimation;
        internal UnityEngine.AnimationState State=>_pistonAnimation["DildoAutoPiston"];
        internal void Edit(float speed,float stroke)
        { float old=_settings.PistonStrokeCm;_settings.PistonSpeed=speed;_settings.PistonStrokeCm=stroke;ApplyPistonSettings(old); }
        internal void Wheel()=>ChangePistonFromWheel();
        internal void Stick(float x,float y)=>ChangePistonFromStick(new UnityEngine.Vector2(x,y));
        internal void Trigger(bool held) { if(held) BeginHeldPiston();else EndHeldPiston(); }
        internal void TriggerPress()=>ToggleHeldPiston();
        internal void Focus(bool v) { UnityEngine.Application.isFocused=v;OnApplicationFocus(v); }
        internal void Disable()=>OnDisable();
        private void DisconnectGrabInput()=>ReleaseGrab();
    }
}
