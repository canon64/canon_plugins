using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;

namespace UnityEngine
{
 public class MonoBehaviour {public static void Destroy(object value){}}
 public struct Vector3
 {
  public float x,y,z;
  public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static float Distance(Vector3 a,Vector3 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
 }
 public struct Quaternion
 {
  public float Angle;
  public static Quaternion Inverse(Quaternion value)=>new(){Angle=-value.Angle};
  public static Quaternion operator *(Quaternion a,Quaternion b)=>new(){Angle=a.Angle+b.Angle};
  public static Quaternion Euler(Vector3 v)=>new(){Angle=v.x};
  public static Vector3 operator *(Quaternion a,Vector3 b){float r=a.Angle*MathF.PI/180;return new(b.x*MathF.Cos(r)-b.y*MathF.Sin(r),b.x*MathF.Sin(r)+b.y*MathF.Cos(r),b.z);}
 }
 public class Transform
 {
  public Transform parent;public Vector3 position,localPosition,localScale=new(1,1,1);public Quaternion localRotation,rotation;
  public Vector3 lossyScale=>new Vector3(1,1,1);
  public bool FailParent,PreservedOnLastParent;
  public void SetParent(Transform p,bool world){if(FailParent)throw new Exception("detach failure");parent=p;PreservedOnLastParent=world;if(world)localRotation=new Quaternion{Angle=rotation.Angle-(p!=null?p.rotation.Angle:0)};else rotation=new Quaternion{Angle=localRotation.Angle+(p!=null?p.rotation.Angle:0)};}
  public Vector3 InverseTransformPoint(Vector3 v){var p=Quaternion.Inverse(rotation)*(v-position);return new(p.x/localScale.x,p.y/localScale.y,p.z/localScale.z);}
  public Vector3 TransformPoint(Vector3 v)=>position+rotation*Vector3.Scale(v,localScale);
 }
 public class GameObject
 {
  public Transform transform=new Transform();public bool FailComponent;
  public int layer;public GameObject(){}public GameObject(string name){}
  public void SetActive(bool active){}
  public T AddComponent<T>() where T:new(){if(FailComponent)throw new Exception("component failure");return new T();}
  public T[] GetComponentsInChildren<T>()=>Array.Empty<T>();
 }
 public class Renderer {public bool enabled=true;public Bounds bounds;}
 public class Collider {public bool isTrigger;public float contactOffset;}
 public class SphereCollider:Collider {public float radius;}
 public class Rigidbody {public bool isKinematic,useGravity;}
 public static class Mathf
 {
  public static float Abs(float value)=>MathF.Abs(value);
  public static float Max(float a,float b)=>MathF.Max(a,b);
 }
 public struct Bounds {public Vector3 ClosestPoint(Vector3 v)=>v;}
}
namespace Valve.VR
{
 public enum SteamVR_Input_Sources {LeftHand,RightHand}
 public sealed class SteamVR_Action_Boolean
 {
  public delegate void StateHandler(SteamVR_Action_Boolean a,SteamVR_Input_Sources s);
  readonly Dictionary<SteamVR_Input_Sources,StateHandler> down=new(),up=new();
  public void AddOnStateDownListener(StateHandler h,SteamVR_Input_Sources s){down.TryGetValue(s,out var old);down[s]=old+h;}
  public void AddOnStateUpListener(StateHandler h,SteamVR_Input_Sources s){up.TryGetValue(s,out var old);up[s]=old+h;}
  public void RemoveOnStateDownListener(StateHandler h,SteamVR_Input_Sources s){down[s]-=h;}
  public void RemoveOnStateUpListener(StateHandler h,SteamVR_Input_Sources s){up[s]-=h;}
  public void AddOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> h,SteamVR_Input_Sources s){}
  public void RemoveOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> h,SteamVR_Input_Sources s){}
  public void Down(SteamVR_Input_Sources s){if(down.TryGetValue(s,out var h))h?.Invoke(this,s);}
  public void Up(SteamVR_Input_Sources s){if(up.TryGetValue(s,out var h))h?.Invoke(this,s);}
 }
 public static class SteamVR_Actions
 {
  public sealed class Actions {public SteamVR_Action_Boolean Grip_Press=new(),Axis1_Press=new();}
  public static Actions legacy_emulate=new();
 }
 public static class SteamVR_Events
 {
  public class Action {public bool enabled;}
  public static Action InputFocusAction(System.Action<bool> h)=>new();
  public static Action DeviceConnectedAction(System.Action<int,bool> h)=>new();
 }
 // No pose type, UnityEvent, or transform-update notifications exist in this boundary.
 // Compiling the actual grab implementation ensures it cannot depend on them.
}
namespace VRGIN.Controls
{
 public class Controller
 {
  public object GetComponent(Type type)=>null;
  public GameObject gameObject=new();public Transform transform=>gameObject.transform;public object Input=new();
  private Lock _Lock;
  public Lock Current {get=>_Lock;set=>_Lock=value;}
  public bool TryAcquireFocus(out Lock focus){if(Current!=null&&Current.IsValid){focus=null;return false;}focus=Current=new Lock();return true;}
  public class Lock
  {
   public bool IsValid=true,SafeReleased,Released;public bool IsInvalidating=>SafeReleased&&IsValid;
   public void SafeRelease(){SafeReleased=true;}
   public void Release(){Released=true;IsValid=false;}
  }
 }
}
namespace VRGIN.Core
{
 public static class VR
 {
  public class ModeState {public Controller Left=new(),Right=new();}
  public static bool Active=true;public static ModeState Mode=new();
 }
}
namespace MainGameSyringe
{
 public sealed partial class Plugin:MonoBehaviour
 {
  class Setting<T> {public T Value;public Setting(T value){Value=value;}}
  readonly Setting<bool> _diagnostics=new(false);
  readonly Setting<float> _gripRadius=new(.08f);
  readonly Setting<float> _pullDistance=new(.12f);
  readonly Setting<Vector3> _gripPosition=new(default),_gripAngles=new(default);
  Vector3 _gripCenter;Transform _insertedBone;
  GameObject _model=new();bool _focused=true;
  readonly InjectionPolicy<object> _policy=new();
  string _status;public bool Allowed=true,FailMove;public int Moves,Doses;
  bool _spent,_injecting;public bool Refilling;public bool InjectionBusy=>Refilling||_injecting;
  public int Refills;
  void Refill(){Refills++;Refilling=true;}
  public void SetEmpty(){_spent=true;}
  public void FinishRefill(){_spent=false;Refilling=false;}
  void Trace(string value){}
  void ApplyModelScale(){}
  public Vector3 InsertionSnap;
  void TryInsert(bool force){Moves++;if(FailMove)throw new Exception("contact failure");_model.transform.position+=InsertionSnap;_insertedBone=new Transform();ArmReturnBoundary(true);}
  public Vector3 ReturnAnchor=>_model.transform.TransformPoint(_returnAnchorLocal);
  public Vector3 BoundaryLocal=>_returnBoundaryObject.transform.localPosition;
  public void Rearm()=>ArmReturnBoundary();
  void DetachInsertion(bool returnToHand){ClearReturnBoundary();_insertedBone=null;if(returnToHand)RestoreHeldPose();}
  void Inject(){if(_spent)return;Doses++;_spent=true;_injecting=true;}
  public void FinishInjection(){_injecting=false;}
  public void Start()=>SubscribeVr();
  public void DesktopFocus(bool focused)=>_focused=focused;
  public void Stop(){UnsubscribeVr();ReleaseGrab();}
  public Hand Held=>_policy.HeldBy;
  public Transform Model=>_model.transform;
  public void FailDetach(bool value)=>_model.transform.FailParent=value;
  public bool Inserted=>_insertedBone!=null;
  public void PhysicsExit(bool ownHand=true)
  {
   if(_returnBoundary==null)throw new Exception("return boundary missing");
   typeof(ReturnBoundary).GetMethod("OnTriggerExit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
    .Invoke(_returnBoundary,new object[]{ownHand?_returnBoundary.Hand:new Collider()});
  }
 }
 static class SkinGeometry {public static bool ShouldRelease(float distance,float threshold)=>distance>=threshold;}
}
static partial class Program
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void CheckLifecycle(SteamVR_Input_Sources side)
 {
  var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
  var controller=side==SteamVR_Input_Sources.LeftHand?VRGIN.Core.VR.Mode.Left:VRGIN.Core.VR.Mode.Right;
  var expected=side==SteamVR_Input_Sources.LeftHand?MainGameSyringe.Hand.Left:MainGameSyringe.Hand.Right;
  controller.transform.position=default;
  var p=new MainGameSyringe.Plugin();p.Model.rotation=new Quaternion{Angle=47};controller.transform.rotation=new Quaternion{Angle=13};p.Start();
  controller.TryAcquireFocus(out var native);grip.Down(side);
  Check(p.Held==MainGameSyringe.Hand.None&&native.IsValid,"live foreign focus is preserved");
  native.SafeRelease();grip.Down(side);
  Check(p.Held==expected&&!native.IsValid&&!controller.Current.IsInvalidating,"retiring native tool focus does not block initial grab");
  Check(p.Model.PreservedOnLastParent&&p.Model.rotation.Angle==47&&p.Model.localRotation.Angle==34,"grabbing keeps the placed world angle instead of aligning to controller");
  grip.Up(side);grip.Down(side);Check(p.Held==expected&&!p.Inserted,"regrab before insertion");
  trigger.Down(side);trigger.Up(side);grip.Up(side);grip.Down(side);
  Check(p.Inserted&&p.Held==expected&&p.Doses==0,"regrab after insertion before dose");
  trigger.Down(side);trigger.Up(side);grip.Up(side);grip.Down(side);
  Check(p.Inserted&&p.Held==expected&&p.Doses==1&&p.InjectionBusy,"regrab while dosing");
  p.FinishInjection();grip.Up(side);grip.Down(side);
  Check(p.Inserted&&p.Held==expected&&p.Doses==1,"regrab after dose finishes");
  trigger.Down(side);trigger.Up(side);
  Check(p.Doses==1&&p.Refills==0,"regrabbed spent inserted syringe does not redose or refill in body");
  controller.transform.position=new Vector3(.13f,0,0);p.PhysicsExit();controller.transform.position=default;
  grip.Up(side);grip.Down(side);Check(p.Held==expected&&!p.Inserted,"regrab empty withdrawn syringe");
  trigger.Down(side);trigger.Up(side);grip.Up(side);grip.Down(side);
  Check(p.Held==expected&&p.Refilling&&p.Refills==1,"regrab during refill");
  p.FinishRefill();grip.Up(side);grip.Down(side);trigger.Down(side);trigger.Up(side);
  Check(p.Held==expected&&p.Inserted&&p.Doses==1&&p.Moves==2,"regrab after refill then reinsert without automatic dose");
  var focus=controller.Current;p.Stop();
  Check(p.Held==MainGameSyringe.Hand.None&&!focus.IsValid,"lifecycle shutdown releases the controller");
 }
 static void Main()
 {
  CheckLifecycle(SteamVR_Input_Sources.LeftHand);CheckLifecycle(SteamVR_Input_Sources.RightHand);
  CheckGripPriority();
  CheckReturnAnchor();
  var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
  var right=VRGIN.Core.VR.Mode.Right;var hand=SteamVR_Input_Sources.RightHand;
  var p=new MainGameSyringe.Plugin();p.Start();grip.Down(hand);
  p.DesktopFocus(false);
  Check(p.Held==MainGameSyringe.Hand.Right,"grab failed without pose notifications");
  Check(p.Moves==0,"grabbing performed unsolicited contact monitoring");
  trigger.Down(SteamVR_Input_Sources.LeftHand);Check(p.Moves==0&&p.Doses==0,"other hand triggered held syringe");
  var focus=right.Current;trigger.Down(hand);Check(p.Moves==1&&p.Doses==0&&p.Inserted,"first trigger must insert without injecting");
  trigger.Down(hand);Check(p.Doses==0,"held trigger injected without a second press");
  grip.Up(hand);Check(p.Inserted&&p.Held==MainGameSyringe.Hand.None&&focus.IsValid&&focus.IsInvalidating,"release before dose keeps needle inserted while native focus waits for trigger release");
  grip.Down(hand);Check(p.Inserted&&p.Held==MainGameSyringe.Hand.Right&&right.Current!=focus&&!focus.IsValid&&!right.Current.IsInvalidating&&p.Doses==0&&p.Moves==1,"re-grip retires pending focus and acquires a fresh lock without withdrawal, reinsertion or dose");
  trigger.Up(hand);right.transform.position=new Vector3(.05f,0,0);trigger.Down(hand);
  Check(p.Doses==1&&p.Inserted,"second trigger inside threshold did not inject");
  grip.Up(hand);grip.Down(hand);
  Check(p.Held==MainGameSyringe.Hand.Right&&p.Inserted&&p.Doses==1,"release and regrab during injection preserves inserted spent state");
  p.FinishInjection();grip.Up(hand);grip.Down(hand);
  Check(p.Held==MainGameSyringe.Hand.Right&&p.Inserted&&p.Doses==1,"release and regrab after dose completes");
  trigger.Up(hand);right.transform.position=new Vector3(.18f,0,0);p.PhysicsExit(false);
  Check(p.Inserted,"unrelated collider removed syringe");p.PhysicsExit();
  Check(!p.Inserted&&p.Doses==1,"threshold exit must return without a trigger or dose");
  right.transform.position=default;trigger.Down(hand);trigger.Up(hand);
  Check(p.Refilling&&p.Refills==1,"spent syringe refills after withdrawal");p.FinishRefill();
  right.transform.position=default;trigger.Down(hand);trigger.Up(hand);right.transform.position=new Vector3(.12f,0,0);trigger.Down(hand);
  Check(!p.Inserted&&p.Doses==1,"second trigger at threshold must return without injecting");
  right.transform.position=default;
  p.SetEmpty();trigger.Up(hand);trigger.Down(hand);
  Check(p.Refills==2&&p.Refilling&&!p.Inserted,"empty detached syringe trigger must refill instead of inserting");
  grip.Up(hand);grip.Down(hand);Check(p.Held==MainGameSyringe.Hand.Right&&p.Refilling,"release and regrab during refill");
  trigger.Up(hand);trigger.Down(hand);Check(p.Refills==2&&!p.Inserted,"repeat trigger during refill must not restart or insert");
  p.FinishRefill();trigger.Up(hand);trigger.Down(hand);
  Check(p.Inserted&&p.Refills==2&&p.Doses==1,"first trigger after refill inserts without dosing");
  right.transform.position=new Vector3(.13f,0,0);p.PhysicsExit();right.transform.position=default;
  grip.Up(hand);Check(p.Held==MainGameSyringe.Hand.None&&focus.SafeReleased,"grip up retained lock");
  trigger.Up(hand);grip.Down(hand);p.FailDetach(true);focus=right.Current;
  try{grip.Up(hand);}catch(Exception){}
  Check(focus.SafeReleased&&p.Held==MainGameSyringe.Hand.None,"detach exception retained controller lock");
  p.FailDetach(false);p.Stop();
  p=new MainGameSyringe.Plugin();p.Start();right.gameObject.FailComponent=true;grip.Down(hand);focus=right.Current;
  Check(focus.Released&&p.Held==MainGameSyringe.Hand.None,"partial grab failure retained lock");
  right.gameObject.FailComponent=false;grip.Down(hand);focus=right.Current;
  p.FailMove=true;trigger.Down(hand);
  Check(focus.Released&&p.Held==MainGameSyringe.Hand.None,"trigger exception retained lock");
  p.FailMove=false;grip.Down(hand);focus=right.Current;p.Stop();p.Stop();
  Check(!focus.IsValid&&p.Held==MainGameSyringe.Hand.None,"session stop retained lock");
  Console.WriteLine("VR_GRAB_BOUNDARY_OK checks="+checks+" (real Plugin.Vr.cs; Unity/VR host stubs; no pose notification API)");
 }
}
