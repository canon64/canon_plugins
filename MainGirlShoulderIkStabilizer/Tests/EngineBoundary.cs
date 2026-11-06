// A rigid transform hierarchy, not a Unity renderer or a replacement FinalIK solver.
// Production pose code runs against these small engine boundaries in the tests.
using N = System.Numerics;
namespace UnityEngine
{
    public class Object { public string name="test-bone"; public static void Destroy(Object value) { } }
    public class Component : Object { public GameObject gameObject; }
    public class GameObject : Object
    {
        public readonly List<Object> Components=new();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Components.OfType<T>().ToArray();
        public readonly Transform transform;
        public HideFlags hideFlags;
        public GameObject(string name = "test") { transform = new Transform { gameObject = this }; }
    }
    public enum HideFlags { HideAndDontSave }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition;
        public Vector3 localScale=new Vector3(1,1,1);
        public Vector3 InverseTransformPoint(Vector3 point) => Quaternion.Inverse(rotation)*(point-position);
        private Quaternion local = Quaternion.identity;
        public Quaternion localRotation { get => local; set => local = new Quaternion(N.Quaternion.Normalize(value.n)); }
        public Vector3 forward => rotation * new Vector3(0,0,1);
        public Vector3 localEulerAngles
        {
            get
            {
                var q=local.n; float deg=180f/MathF.PI;
                return new Vector3(MathF.Atan2(2*(q.W*q.X+q.Y*q.Z),1-2*(q.X*q.X+q.Y*q.Y))*deg,
                    MathF.Asin(Math.Clamp(2*(q.W*q.Y-q.Z*q.X),-1,1))*deg,
                    MathF.Atan2(2*(q.W*q.Z+q.X*q.Y),1-2*(q.Y*q.Y+q.Z*q.Z))*deg);
            }
        }
        public Vector3 position
        {
            get => parent == null ? localPosition : parent.position + parent.rotation * localPosition;
            set => localPosition = parent == null ? value : Quaternion.Inverse(parent.rotation) * (value - parent.position);
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }
    public struct Vector3
    {
        internal N.Vector3 n;
        public float x => n.X; public float y => n.Y; public float z => n.Z;
        public Vector3(float x, float y, float z) { n = new N.Vector3(x,y,z); }
        internal Vector3(N.Vector3 value) { n = value; }
        public static Vector3 zero => new Vector3(N.Vector3.Zero);
        public float sqrMagnitude => n.LengthSquared();
        public Vector3 normalized => new Vector3(N.Vector3.Normalize(n));
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.n+b.n);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.n-b.n);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.n*b);
        public static float Distance(Vector3 a, Vector3 b) => N.Vector3.Distance(a.n,b.n);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(N.Vector3.Lerp(a.n,b.n,Math.Clamp(t,0,1)));
        public static Vector3 ClampMagnitude(Vector3 a, float max) => a.n.Length() <= max ? a : a.normalized*max;
    }
    public struct Quaternion
    {
        internal N.Quaternion n;
        public float x => n.X; public float y => n.Y; public float z => n.Z; public float w => n.W;
        public Quaternion(float x,float y,float z,float w) { n = new N.Quaternion(x,y,z,w); }
        internal Quaternion(N.Quaternion q) { n=q; }
        public static Quaternion identity => new Quaternion(N.Quaternion.Identity);
        public static Quaternion operator *(Quaternion a,Quaternion b) => new Quaternion(a.n*b.n);
        public static Vector3 operator *(Quaternion a,Vector3 b) => new Vector3(N.Vector3.Transform(b.n,a.n));
        public static Quaternion Inverse(Quaternion q) => new Quaternion(N.Quaternion.Inverse(q.n));
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t) => new Quaternion(N.Quaternion.Slerp(a.n,b.n,t));
        public static float Dot(Quaternion a,Quaternion b) => N.Quaternion.Dot(a.n,b.n);
    }
    public static class Mathf
    {
        public static float Clamp(float f,float a,float b) => Math.Clamp(f,a,b);
        public static float Clamp01(float f) => Math.Clamp(f,0,1);
        public static float Abs(float f) => Math.Abs(f);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
    }
    public static class Time { public static int frameCount; public static float unscaledTime, unscaledDeltaTime = 1f/60; }
}
namespace RootMotion.FinalIK
{
    using UnityEngine;
    public enum FullBodyBipedChain { LeftArm,RightArm,LeftLeg,RightLeg }
    public enum FullBodyBipedEffector { LeftShoulder,RightShoulder }
    public class IKEffector
    {
        public Transform bone,target;
        public Vector3 position;
        public Quaternion rotation;
        public float positionWeight=1,rotationWeight;
    }
    public class IKMappingLimb
    {
        public enum BoneMapType { Parent }
        public class BoneMap { public Quaternion defaultLocalRotation=Quaternion.identity; public Vector3 localSwingAxis; }
        public Transform parentBone,bone1,bone2,bone3;
        public float weight=1;
        public readonly BoneMap Map = new BoneMap();
        public BoneMap GetBoneMap(BoneMapType _) => Map;
    }
    public class IKSolver
    {
        public event Action OnPreRead,OnPostInitiate,OnStoreDefaultLocalState;
        public bool initiated=true;
        public float IKPositionWeight=1;
        public void Read() => OnPreRead?.Invoke();
        public void Reinitialize() => OnPostInitiate?.Invoke();
        public void StoreDefaults() => OnStoreDefaultLocalState?.Invoke();
        public int SubscriptionCount => (OnPreRead?.GetInvocationList().Length??0) + (OnPostInitiate?.GetInvocationList().Length??0) + (OnStoreDefaultLocalState?.GetInvocationList().Length??0);
    }
    public class SpineMapping { public Transform[] spineBones; }
    public class IKConstraintBend { public float weight; public Transform bendGoal; }
    public class FBIKChain { public readonly IKConstraintBend bendConstraint=new IKConstraintBend(); }
    public class IKSolverFullBody : IKSolver { }
    public class IKSolverFullBodyBiped : IKSolverFullBody
    {
        public SpineMapping spineMapping = new SpineMapping();
        public readonly IKMappingLimb[] limbs = {new IKMappingLimb(),new IKMappingLimb()};
        public readonly IKEffector[] hands = {new IKEffector(),new IKEffector()};
        public readonly FBIKChain[] chains = {new FBIKChain(),new FBIKChain()};
        public readonly IKEffector leftShoulderEffector = new IKEffector(),rightShoulderEffector = new IKEffector();
        public IKMappingLimb GetLimbMapping(FullBodyBipedChain side) => limbs[(int)side];
        public IKEffector GetEndEffector(FullBodyBipedChain side) => hands[(int)side];
        public FBIKChain GetChain(FullBodyBipedChain side) => chains[(int)side];
        public IKEffector GetEffector(FullBodyBipedEffector side) => side==FullBodyBipedEffector.LeftShoulder ? leftShoulderEffector : rightShoulderEffector;
    }
    public class BipedReferences { public Transform root; }
    public class FullBodyBipedIK : Component { public bool enabled=true; public readonly IKSolverFullBodyBiped solver=new IKSolverFullBodyBiped(); public BipedReferences references=new BipedReferences(); }
}
namespace HarmonyLib
{
    public class HarmonyPrefix : Attribute { }
    public class HarmonyFinalizer : Attribute { }
    public class HarmonyPriority : Attribute { public HarmonyPriority(int i) { } }
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t,string m) { } }
    public class HarmonyMethod { public HarmonyMethod(Type type,string method) { } }
    public static class AccessTools { public static System.Reflection.MethodInfo Method(Type type,string name) => type.GetMethod(name); }
    public class Harmony
    {
        public static int ActivePatches;
        private int count;
        public Harmony(string id) { }
        public void Patch(System.Reflection.MethodBase method,HarmonyMethod prefix=null,HarmonyMethod finalizer=null)
        { if(method==null)throw new ArgumentNullException(nameof(method));count++;ActivePatches++; }
        public void UnpatchSelf() {ActivePatches-=count;count=0;}
    }
    public static class Priority { public const int First=800,Last=0; }
}
public class ChaControl : UnityEngine.Component
{
    public ExpressionBone expression;
    public UnityEngine.GameObject objBodyBone=new();
    public ExpressionBone ChildExpression;
    public T GetComponentInChildren<T>(bool inactive) where T:class => ChildExpression as T;
    public void LateUpdateForce() { }
    public void InitializeExpression(bool value) { }
    public int GetInstanceID()=>1;
}
public class HSceneProc { public bool isActiveAndEnabled=true; }
namespace CanonPlugins.Local { public static class SessionGate { public static bool Allowed=true; } }
