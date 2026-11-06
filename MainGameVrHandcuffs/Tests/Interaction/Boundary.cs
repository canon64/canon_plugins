using UnityEngine;
using Valve.VR;
using VRGIN.Controls;

public class ChaControl { }
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero=>new();
        public static Vector3 one=>new(1,1,1);
        public static Vector3 up=>new(0,1,0);
        public static Vector3 right=>new(1,0,0);
        public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;
        public float magnitude=>MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized=>this/magnitude;
        public void Normalize() { this=normalized; }
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n)=>a-n*Dot(a,n)/n.sqrMagnitude;
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static float Distance(Vector3 a,Vector3 b) { var v=a-b;return MathF.Sqrt(v.x*v.x+v.y*v.y+v.z*v.z); }
    }
    public struct Quaternion
    {
        System.Numerics.Quaternion q;
        public float x {get=>q.X;set=>q.X=value;}
        public float y {get=>q.Y;set=>q.Y=value;}
        public float z {get=>q.Z;set=>q.Z=value;}
        public float w {get=>q.W;set=>q.W=value;}
        public Quaternion(float x,float y,float z,float w) { q=new(x,y,z,w); }
        System.Numerics.Quaternion Value=>q.LengthSquared()==0?System.Numerics.Quaternion.Identity:q;
        public static Quaternion identity=>new(){q=System.Numerics.Quaternion.Identity};
        public Quaternion normalized=>new(){q=System.Numerics.Quaternion.Normalize(Value)};
        public static float Dot(Quaternion a,Quaternion b)=>System.Numerics.Quaternion.Dot(a.Value,b.Value);
        public static float Angle(Quaternion a,Quaternion b)=>2*MathF.Acos(Math.Clamp(MathF.Abs(Dot(a.normalized,b.normalized)),0,1))*180/MathF.PI;
        public static Quaternion Inverse(Quaternion a)=>new(){q=System.Numerics.Quaternion.Inverse(a.Value)};
        public static Quaternion LookRotation(Vector3 f,Vector3 u)
        {
            f.Normalize();var r=Vector3.Cross(u,f).normalized;u=Vector3.Cross(f,r);
            var m=new System.Numerics.Matrix4x4(r.x,r.y,r.z,0,u.x,u.y,u.z,0,f.x,f.y,f.z,0,0,0,0,1);
            return new(){q=System.Numerics.Quaternion.CreateFromRotationMatrix(m)};
        }
        public static Quaternion Euler(float a,float b,float c)=>new(){q=System.Numerics.Quaternion.CreateFromYawPitchRoll(b*MathF.PI/180,a*MathF.PI/180,c*MathF.PI/180)};
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(){q=a.Value*b.Value};
        public static Vector3 operator *(Quaternion a,Vector3 b)
        { var p=System.Numerics.Vector3.Transform(new(b.x,b.y,b.z),a.Value);return new(p.X,p.Y,p.Z); }
    }
    public static class Mathf
    {
        public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
        public static float Sqrt(float v)=>MathF.Sqrt(v);
        public static float Max(float a,float b)=>MathF.Max(a,b);
    }
    public class Transform
    {
        public string name="";
        public Transform[] AnatomyBones=Array.Empty<Transform>();
        Vector3 _localPosition;Quaternion _localRotation;
        public Transform parent;public bool FailDetach;public GameObject gameObject;
        public Vector3 localScale=Vector3.one;
        public Vector3 lossyScale=>parent==null?localScale:new Vector3(localScale.x*parent.lossyScale.x,localScale.y*parent.lossyScale.y,localScale.z*parent.lossyScale.z);
        public Transform transform=>this;
        public Vector3 localPosition {get=>_localPosition;set=>_localPosition=value;}
        public Vector3 position { get=>parent==null?_localPosition:parent.TransformPoint(_localPosition);set=>_localPosition=parent==null?value:parent.InverseTransformPoint(value); }
        public Quaternion rotation { get=>parent==null?_localRotation:parent.rotation*_localRotation;set=>_localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value; }
        public Vector3 right=>rotation*Vector3.right;
        public Vector3 up=>rotation*Vector3.up;
        public Vector3 forward=>rotation*Vector3.forward;
        public void SetPositionAndRotation(Vector3 p,Quaternion q) { position=p;rotation=q; }
        public void SetParent(Transform p,bool world) { if (FailDetach) throw new Exception("detach fault"); var pos=position;var rot=rotation;parent=p;if(world)SetPositionAndRotation(pos,rot); }
        public Vector3 InverseTransformPoint(Vector3 p)=>Quaternion.Inverse(rotation)*(p-position);
        public Vector3 TransformPoint(Vector3 p)=>position+rotation*p;
        public T[] GetComponentsInChildren<T>()=>gameObject.Renderers as T[];
        public T[] GetComponentsInChildren<T>(bool inactive)=>AnatomyBones as T[];
    }
    public class Component { public Transform transform; }
    public class Collider { }
    public class GameObject
    {
        public Transform transform;public List<object> Components=new();public Renderer[] Renderers=Array.Empty<Renderer>();
        public bool Active;
        public void SetActive(bool value) { Active=value; }
        public GameObject() { transform=new(){gameObject=this}; }
        public T AddComponent<T>() where T:new() { var v=new T();Components.Add(v);return v; }
    }
    public class Bounds
    {
        public Vector3 Center;
        public float SqrDistance(Vector3 v) { float d=Vector3.Distance(Center,v);return d*d; }
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1f){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color yellow=>new(1,1,0);
    }
    public class Material { public Color color; }
    public class Renderer { public Bounds bounds=new();public Material sharedMaterial=new();public GameObject gameObject=new(); }
    public class Rigidbody
    {
        public GameObject gameObject=new();public Transform transform=>gameObject.transform;
        public Vector3 position=>transform.position;public Vector3 velocity,angularVelocity;public bool isKinematic=true;
        public Renderer[] Renderers=Array.Empty<Renderer>();
        public T[] GetComponentsInChildren<T>()=>Renderers as T[];
    }
    public enum ConfigurableJointMotion { Locked,Limited,Free }
    public enum JointProjectionMode { PositionAndRotation }
    public struct SoftJointLimit { public float limit,contactDistance; }
    public struct SoftJointLimitSpring { public float spring,damper; }
    public class ConfigurableJoint
    {
        public Rigidbody connectedBody;public bool autoConfigureConnectedAnchor,enableCollision,enablePreprocessing;
        public Vector3 anchor,connectedAnchor;
        public ConfigurableJointMotion xMotion,yMotion,zMotion,angularXMotion,angularYMotion,angularZMotion;
        public JointProjectionMode projectionMode;public float projectionDistance,projectionAngle;
        public SoftJointLimit linearLimit;
        public SoftJointLimitSpring linearLimitSpring;
    }
    public static class Application { public static bool isFocused=true; }
}
namespace Valve.VR
{
    public enum SteamVR_Input_Sources { LeftHand,RightHand }
    public enum ETrackingResult { Running_OK,Uninitialized }
    public class SteamVR_Behaviour_Pose
    {
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
    }
    public class SteamVR_Action_Boolean
    {
        public delegate void Edge(SteamVR_Action_Boolean a,SteamVR_Input_Sources s);
        public delegate void Active(SteamVR_Action_Boolean a,SteamVR_Input_Sources s,bool active);
        readonly Dictionary<SteamVR_Input_Sources,Edge> _down=new(),_up=new();
        readonly Dictionary<SteamVR_Input_Sources,Active> _active=new();
        public void AddOnStateDownListener(Edge e,SteamVR_Input_Sources s) { _down.TryGetValue(s,out var old);_down[s]=old+e; }
        public void AddOnStateUpListener(Edge e,SteamVR_Input_Sources s) { _up.TryGetValue(s,out var old);_up[s]=old+e; }
        public void AddOnActiveChangeListener(Active e,SteamVR_Input_Sources s) { _active.TryGetValue(s,out var old);_active[s]=old+e; }
        public void RemoveOnStateDownListener(Edge e,SteamVR_Input_Sources s) { if (_down.ContainsKey(s)) _down[s]-=e; }
        public void RemoveOnStateUpListener(Edge e,SteamVR_Input_Sources s) { if (_up.ContainsKey(s)) _up[s]-=e; }
        public void RemoveOnActiveChangeListener(Active e,SteamVR_Input_Sources s) { if (_active.ContainsKey(s)) _active[s]-=e; }
        public void Down(SteamVR_Input_Sources s) { if (_down.TryGetValue(s,out var e)) e?.Invoke(this,s); }
        public void Up(SteamVR_Input_Sources s) { if (_up.TryGetValue(s,out var e)) e?.Invoke(this,s); }
        public void Inactive(SteamVR_Input_Sources s) { if (_active.TryGetValue(s,out var e)) e?.Invoke(this,s,false); }
    }
    public static class SteamVR_Actions
    {
        public static Actions legacy_emulate=new();
        public class Actions { public SteamVR_Action_Boolean Grip_Press=new(),Axis1_Press=new(),ApplicationMenu_Press=new(),Axis0_Press=new(); }
    }
    public static class SteamVR_Events
    {
        public class Action { public bool enabled;public System.Action<bool> Handler; }
        public static Action InputFocusAction(System.Action<bool> handler)=>new(){Handler=handler};
    }
}
namespace VRGIN.Controls
{
    public class Controller : Component
    {
        public object Input=new WristInput();public bool IsTracking=true;public SteamVR_Behaviour_Pose Tracking=new();
        public Controller() { transform=new(); }
        private Lock _Lock;
        public Lock Current { get=>_Lock;set=>_Lock=value; }
        public object GetComponent(Type t)=>null;
        public bool TryAcquireFocus(out Lock focus)
        {
            focus=null;if (_Lock!=null && _Lock.IsValid) return false;
            focus=_Lock=new Lock();return true;
        }
        public class Lock
        {
            public bool IsValid=true,IsInvalidating;
            public void SafeRelease() { IsInvalidating=true; }
            public void Release() { IsValid=false; }
            public void NativeButtonsReleased() { if (IsInvalidating) IsValid=false; }
        }
    }
}
namespace VRGIN.Core
{
    public static class VR { public static bool Active=true;public static ModeState Mode=new(); }
    public class ModeState { public Controller Left=new(),Right=new(); }
}
namespace RootMotion.FinalIK
{
    public class IKConstraintBend { public Transform bone2=new(),bendGoal;public float weight; }
    public class IKEffector
    {
        public Transform target,bone=new();public Vector3 position;public Quaternion rotation;public float positionWeight,rotationWeight;
    }
}
namespace MainGirlHipHijack.Runtime
{
    public class Gizmo { public bool Visible;public int GrabState;public void SetVisible(bool v) { Visible=v; }public void SetVRGrabState(int s) { GrabState=s; } }
    internal class BodyIkOperationPointState
    {
        public bool Running,ExternalOwner,HasOriginalEffectorTarget,GizmoDragging,SuppressConfiguredFollow,VrAutoEnabled,HasPostDragHold,VrElbowFromWrist;
        public int PostDragHoldFrames;
        public GameObject ProxyGo,FallbackEffectorTargetGo,BoneMarkerGo,VRMarkerGo;
        public Renderer FollowLine,VRMarkerRend;
        public Transform CandidateBone;
        public Transform Proxy,OriginalEffectorTarget,FollowBone;public Gizmo Gizmo;
    }
}
namespace CanonPlugins.Local { public static class SessionGate { public static bool Allowed=true; } }
namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        public static Plugin Instance;
        internal const int BIK_LH=0,BIK_RH=1,BIK_LE=8,BIK_RE=9,BIK_BODY=12,BIK_TOTAL=13;
        internal readonly Runtime.BodyIkOperationPointState[] _bikEff=Enumerable.Range(0,13).Select(_=>new Runtime.BodyIkOperationPointState()).ToArray();
        internal readonly bool[] _bikWant=new bool[13];internal readonly float[] _bikWeight=Enumerable.Range(0,13).Select(i=>i==0?.3f:i==1?.6f:1f).ToArray();
        public class References { public Transform leftForearm=new(){position=new Vector3(-.24f,0,0)},rightForearm=new(){position=new Vector3(-.24f,0,0)}; }
        public class Ik { public bool enabled;public References references=new(); }
        public class Female { public ChaControl Character=new();public Ik FullBodyIk=new(); }
        internal Female _femaleSession=new();internal bool _abandonedByPostureChange,_pendingAbandonByPostureChange,Enabled=true;
        internal object _poseTransitionCoroutine;
        internal readonly RootMotion.FinalIK.IKEffector[] Effectors=Enumerable.Range(0,13).Select(_=>new RootMotion.FinalIK.IKEffector()).ToArray();
        public Plugin()
        {
            for(int i=0;i<2;i++)
            {
                var wrist=Effectors[i].bone;var side=i==0?"L":"R";float sign=i==0?1:-1;
                wrist.AnatomyBones=new[]{
                    new Transform{name="cf_j_index01_"+side,parent=wrist,localPosition=new Vector3(.07f,0,.018f*sign)},
                    new Transform{name="cf_j_little01_"+side,parent=wrist,localPosition=new Vector3(.07f,0,-.018f*sign)}};
            }
        }
        bool IsPluginEnabled()=>Enabled;
        static int IndexOfEffectorKey(string k)=>k=="left_hand"?0:k=="right_hand"?1:-1;
        RootMotion.FinalIK.IKEffector BIK_GetEffector(int i)=>Effectors[i];
        void NotifyArmIkRunningChanged(int i,bool running) { }
        bool IsGizmoVisible(int i)=>_bikEff[i].Running;
        public void End()=>ReleaseRuntimeWrists();
        public static void EvaluateItems()=>Instance?._runtimeItemConstraintsEvaluating?.Invoke();
    }
}
namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        public const string Guid="test.handcuffs";
        readonly HandcuffState _state=new();readonly Transform[] _parts=Enumerable.Range(0,5).Select(_=>new GameObject().transform).ToArray();
        readonly Transform[] _chainEyes={new(),new()};
        readonly Transform[] _wristTargets={new(),new()};GameObject _rig=new();
        readonly MainGirlHipHijack.Plugin.RuntimeWristBinding[] _bindings=new MainGirlHipHijack.Plugin.RuntimeWristBinding[2];
        internal bool CanAct=true;public string LastResult;
        public Plugin()
        {
            for(int i=0;i<2;i++){_chainEyes[i].SetParent(_parts[i],false);_wristTargets[i].SetParent(_parts[i],false);}
        }
        void DetachGizmo() { }
        void BindingInvalidated() { Delete(); }
        public void Start()=>ConnectInput();public void Stop()=>DisconnectInput();
        public void Delete()
        {
            ReleaseGrab();FinishPendingLocks();
            for (int i=0;i<2;i++) { var b=_bindings[i];_bindings[i]=null;if(b!=null){b.Invalidated-=BindingInvalidated;b.Dispose();} }
            _state.Reset();
        }
        public int Held=>_state.Held;public int Wrist(int i)=>_state.Wrist(i);public Transform Part(int i)=>_parts[i];
        public void CuffAt(int i,Vector3 p) { _parts[i].position=p;_parts[i].gameObject.Renderers=new[]{new Renderer{bounds=new Bounds{Center=p}}}; }
        public void FocusLost()=>_focusEvent.Handler(false);
        public void ThrowOnDetach(int i,bool value) { _parts[i].FailDetach=value; }
    }
}
