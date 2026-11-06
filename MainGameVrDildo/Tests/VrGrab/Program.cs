using System;
using System.Collections.Generic;
using MainGameVrDildo;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;

namespace UnityEngine
{
 public struct Vector2 { public float x,y; }
 public struct Vector3
 {
  public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
 }
 public struct Quaternion { public static Quaternion Euler(float a,float b,float c)=>new();public static Quaternion operator *(Quaternion a,Quaternion b)=>a; }
 public class Transform
 {
  public Transform parent;public Vector3 position;public Quaternion rotation;public bool FailDetach;
  public Vector3 TransformPoint(Vector3 p)=>new(position.x+p.x,position.y+p.y,position.z+p.z);
  public void SetParent(Transform p,bool world){if(FailDetach&&p==null)throw new Exception("detach");parent=p;}
 }
 public class GameObject {public Transform transform=new();public Renderer[] Renderers=Array.Empty<Renderer>();public T[] GetComponentsInChildren<T>()=>Renderers as T[];}
 public class Renderer {public bool enabled=true;public Bounds bounds=new();}
 public class Bounds {public Vector3 Closest;public Vector3 ClosestPoint(Vector3 p)=>Closest;}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);}
}
namespace Valve.VR
{
 public enum SteamVR_Input_Sources {LeftHand,RightHand}
 public enum ETrackingResult {Running_OK,Uninitialized}
 public class SteamVR_Behaviour_Pose
 {
  public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
  public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
 }
 public class SteamVR_Action_Boolean
 {
  public delegate void Edge(SteamVR_Action_Boolean a,SteamVR_Input_Sources s);
  public delegate void Active(SteamVR_Action_Boolean a,SteamVR_Input_Sources s,bool active);
  readonly Dictionary<SteamVR_Input_Sources,Edge> down=new(),up=new();
  public void AddOnStateDownListener(Edge h,SteamVR_Input_Sources s){down.TryGetValue(s,out var old);down[s]=old+h;}
  public void AddOnStateUpListener(Edge h,SteamVR_Input_Sources s){up.TryGetValue(s,out var old);up[s]=old+h;}
  public void RemoveOnStateDownListener(Edge h,SteamVR_Input_Sources s){down[s]-=h;}
  public void RemoveOnStateUpListener(Edge h,SteamVR_Input_Sources s){up[s]-=h;}
  public void AddOnActiveChangeListener(Active h,SteamVR_Input_Sources s){}
  public void RemoveOnActiveChangeListener(Active h,SteamVR_Input_Sources s){}
  public void Down(SteamVR_Input_Sources s){if(down.TryGetValue(s,out var h))h?.Invoke(this,s);}
  public void Up(SteamVR_Input_Sources s){if(up.TryGetValue(s,out var h))h?.Invoke(this,s);}
 }
 public class SteamVR_Action_Vector2
 {
  public delegate void Change(SteamVR_Action_Vector2 a,SteamVR_Input_Sources s,Vector2 v,Vector2 d);
  readonly Dictionary<SteamVR_Input_Sources,Change> changes=new(),updates=new();
  readonly Dictionary<SteamVR_Input_Sources,Vector2> axes=new();
  public void AddOnChangeListener(Change h,SteamVR_Input_Sources s){changes.TryGetValue(s,out var old);changes[s]=old+h;}
  public void RemoveOnChangeListener(Change h,SteamVR_Input_Sources s){changes[s]-=h;}
  public void AddOnUpdateListener(Change h,SteamVR_Input_Sources s){updates.TryGetValue(s,out var old);updates[s]=old+h;}
  public void RemoveOnUpdateListener(Change h,SteamVR_Input_Sources s){updates[s]-=h;}
  public Vector2 GetAxis(SteamVR_Input_Sources s)=>axes.TryGetValue(s,out var v)?v:default;
  public void Move(SteamVR_Input_Sources s,float x,float y){var v=new Vector2{x=x,y=y};axes[s]=v;if(changes.TryGetValue(s,out var h))h?.Invoke(this,s,v,default);}
  public void Tick(SteamVR_Input_Sources s){if(updates.TryGetValue(s,out var h))h?.Invoke(this,s,GetAxis(s),default);}
  public bool Updating(SteamVR_Input_Sources s)=>updates.TryGetValue(s,out var h)&&h!=null;
 }
 public static class SteamVR_Actions {public static int Reads;public static Actions Current=new();public static Actions legacy_emulate {get {Reads++;return Current??throw new NullReferenceException("SteamVR is not initialized");}}public class Actions {public SteamVR_Action_Boolean Grip_Press=new(),Axis1_Press=new();public SteamVR_Action_Vector2 Axis0_2D=new();}}
 public static class SteamVR_Events {public class Action {public bool enabled;}public static Action InputFocusAction(System.Action<bool> h)=>new();}
}
namespace VRGIN.Controls
{
 public class Controller
 {
  public object GetComponent(Type type)=>null;
  public Transform transform=new();public object Input=new();public bool IsTracking=true;public SteamVR_Behaviour_Pose Tracking=new();private Lock _Lock;public Lock Current {get=>_Lock;set=>_Lock=value;}
  public bool TryAcquireFocus(out Lock focus){if(Current!=null&&Current.IsValid){focus=null;return false;}focus=Current=new Lock();return true;}
  public class Lock
  {
   public static Lock Invalid=new(){IsValid=false};public bool IsValid=true;public bool Pending;public bool IsInvalidating=>Pending;
   public void SafeRelease(){Pending=true;}public void Release(){IsValid=false;}public void NativeButtonsReleased(){if(Pending)IsValid=false;}
  }
 }
}
namespace VRGIN.Core {public static class VR {public static bool Active=true;public static ModeState Mode=new();public class ModeState {public VRGIN.Controls.Controller Left=new(),Right=new();}}}
namespace CanonPlugins.Shared {public static class PluginSessionGate {public static bool Allowed=true;}}
namespace MainGameVrDildo
{
 public sealed partial class Plugin
 {
  class Settings {public bool Enabled=true,EnableVrGrab=true;public float GripOffsetX,GripOffsetY,GripOffsetZ,GripRotationX,GripRotationY,GripRotationZ,GripRadius=.08f;}
  Settings _settings=new();GameObject _model=new();Transform _pistonMotion;VRGIN.Controls.Controller _grabController;VRGIN.Controls.Controller.Lock _grabLock;
  Transform[] _bones;bool _barGizmoRequested;public bool GizmoVisible=true;
  void DetachDesktopGizmo(){GizmoVisible=false;}
  public bool isActiveAndEnabled=true;public bool Held=>_grabController!=null;public Transform Root=>_model.transform;
  public void Start()=>ConnectGrabInput();public void Stop()=>DisconnectGrabInput();
  public void SetGeometry(Vector3 root,Vector3 visible){_model.transform.position=root;_bones=new[]{new Transform{position=visible},new Transform{position=new Vector3(visible.x,visible.y+.1f,visible.z)}};_model.Renderers=new[]{new Renderer{bounds=new Bounds{Closest=new Vector3(0,0,0)}}};}
  public int AxisSteps;public Vector2 LastAxis;
  void ChangePistonFromStick(Vector2 axis){AxisSteps++;LastAxis=axis;}
  public bool Piston;public int TriggerStarts;
  void BeginHeldPiston(){Piston=true;TriggerStarts++;}void EndHeldPiston(){Piston=false;}
  void ToggleHeldPiston(){if(Piston)EndHeldPiston();else BeginHeldPiston();}
  bool MotionTraceEnabled=>false;
  void TraceVibeEvent(string state,string detail){}
 }
}
static class Program
{
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Main()
 {
  VR.Active=false;SteamVR_Actions.Current=null;int reads=SteamVR_Actions.Reads;
  var desktop=new Plugin();desktop.Start();
  Check(SteamVR_Actions.Reads==reads,"desktop session starts without accessing uninitialized SteamVR actions");
  desktop.Stop();desktop.Start();desktop.Stop();
  Check(SteamVR_Actions.Reads==reads,"desktop cleanup and next H remain independent of SteamVR");
  VR.Active=true;SteamVR_Actions.Current=new SteamVR_Actions.Actions();
  var p=new Plugin();p.Start();var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var side=SteamVR_Input_Sources.RightHand;var hand=VR.Mode.Right;
  p.SetGeometry(new Vector3(.4f,0,0),new Vector3(.01f,0,0));
  grip.Down(side);Check(p.Held&&p.Root.parent==hand.transform,"visible shaft near hand can be grabbed despite distant origin");
  var trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
  Check(!p.Piston,"grip alone does not start piston");
  trigger.Down(SteamVR_Input_Sources.LeftHand);Check(!p.Piston,"other hand trigger is ignored");
  trigger.Down(side);Check(p.Piston,"holding hand trigger starts piston");
  trigger.Up(SteamVR_Input_Sources.LeftHand);Check(p.Piston,"other hand trigger release cannot stop piston");
  trigger.Up(side);Check(p.Piston&&p.Held,"trigger release keeps piston running");
  trigger.Down(SteamVR_Input_Sources.LeftHand);Check(p.Piston,"other hand cannot toggle running piston");
  trigger.Down(side);Check(!p.Piston&&p.Held,"second trigger press pauses without releasing grip");
  trigger.Up(side);Check(!p.Piston,"release after second press stays paused");
  trigger.Down(side);
  Check(!p.GizmoVisible,"grabbing closes the competing desktop gizmo");
  var stale=hand.Current;grip.Down(side);Check(p.Held&&hand.Current!=stale&&!stale.IsValid&&!hand.Current.IsInvalidating&&p.Root.parent==hand.transform,"fresh Down recovers a missed Up with a fresh native lock");var first=hand.Current;
  Check(p.Piston,"re-grip preserves running piston");
  grip.Up(SteamVR_Input_Sources.LeftHand);Check(p.Held,"other hand release ignored");
  grip.Up(side);Check(!p.Held&&p.Root.parent==null&&first.Pending,"release detaches and returns focus through native safe-release");
  Check(p.Piston,"grip release keeps piston running");
  grip.Down(side);Check(p.Held&&hand.Current!=first&&!first.IsValid&&!hand.Current.IsInvalidating,"quick re-grip replaces the pending native lock");grip.Up(side);
  first.NativeButtonsReleased();grip.Down(side);Check(p.Held&&hand.Current!=first,"second grip can reacquire after native button release");
  var second=hand.Current;p.Root.FailDetach=true;try{grip.Up(side);}catch(Exception){}
  Check(!p.Held&&second.Pending,"detach failure still clears held state and returns lock");
  p.Root.FailDetach=false;second.NativeButtonsReleased();grip.Down(side);
  trigger.Down(side);Check(!p.Piston,"trigger stops piston after re-grab");
  trigger.Down(side);Check(p.Piston,"next trigger restarts piston after re-grab");
  var axis=SteamVR_Actions.legacy_emulate.Axis0_2D;
  axis.Move(side,1,0);int steps=p.AxisSteps;
  axis.Tick(side);axis.Tick(side);axis.Tick(side);
  Check(p.AxisSteps==steps+3&&p.LastAxis.x==1,"unchanged held stick adjusts on every native input update");
  axis.Move(side,0,1);axis.Tick(side);Check(p.LastAxis.y==1,"held stick can change from speed to stroke");
  axis.Move(side,0,0);steps=p.AxisSteps;axis.Tick(side);
  Check(p.AxisSteps==steps&&!axis.Updating(side),"neutral removes active adjustment subscription");
  axis.Move(SteamVR_Input_Sources.LeftHand,1,0);axis.Tick(SteamVR_Input_Sources.LeftHand);
  Check(p.AxisSteps==steps,"other hand stick cannot adjust item");
  axis.Move(side,1,0);grip.Up(side);steps=p.AxisSteps;axis.Tick(side);
  Check(p.Piston&&p.AxisSteps==steps&&!axis.Updating(side),"release removes adjustment subscription while preserving piston");
  grip.Down(side);axis.Tick(side);Check(p.AxisSteps==steps+1,"re-grab detects already deflected stick");
  hand.Tracking.onTrackingChangedEvent?.Invoke(hand.Tracking,side,ETrackingResult.Uninitialized);
  Check(!p.Held&&!p.Piston&&hand.Current.Pending,"tracking loss pauses piston and releases without movement subscriptions");
  hand.Current.NativeButtonsReleased();CanonPlugins.Shared.PluginSessionGate.Allowed=false;grip.Down(side);Check(!p.Held,"revoked session cannot grab");
  CanonPlugins.Shared.PluginSessionGate.Allowed=true;grip.Down(side);trigger.Down(side);p.Stop();Check(!p.Held&&!p.Piston&&!hand.Current.IsValid,"session stop pauses piston and releases held and retiring item locks");
  hand.Current.NativeButtonsReleased();grip.Down(side);Check(!p.Held,"stopped callbacks are unsubscribed");
  p.Start();p.SetGeometry(new Vector3(1,0,0),new Vector3(.4f,0,0));grip.Down(side);
  Check(!p.Held,"oversized renderer bounds cannot capture a grip away from the actual shaft");
  p.SetGeometry(new Vector3(1,0,0),new Vector3(.01f,-.05f,0));grip.Down(side);
  Check(p.Held,"middle of a shaft segment is pickable without a nearby endpoint");p.Stop();
  hand.Current.NativeButtonsReleased();
  var pelvis=new Transform();p.Root.parent=pelvis;p.Start();
  grip.Down(side);Check(p.Held&&p.Root.parent==hand.transform,"inserted item can leave its target parent when grabbed");
  grip.Up(side);Check(!p.Held&&p.Root.parent==pelvis,"releasing an inserted item restores its target parent");
  var insertedLock=hand.Current;grip.Down(side);
  Check(p.Held&&p.Root.parent==hand.transform&&hand.Current!=insertedLock&&!insertedLock.IsValid&&!hand.Current.IsInvalidating,"inserted item can be regrabbed immediately with a fresh native lock");
  p.Stop();
  int starts=p.TriggerStarts;trigger.Down(side);Check(!p.Piston&&p.TriggerStarts==starts,"trigger listeners removed at session stop");
  foreach(var s in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
  {
   var h=s==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
   h.Current?.NativeButtonsReleased();axis.Move(s,0,0);p.Start();
   foreach(bool inserted in new[]{false,true})
   {
    p.Root.parent=inserted?pelvis:null;
    grip.Down(s);Check(p.Held&&!p.Piston,"stopped item can be grabbed with either hand");
    grip.Up(s);Check(!p.Held&&!p.Piston,"manual release never starts a stopped item");
    grip.Down(s);trigger.Down(s);axis.Move(s,1,0);axis.Tick(s);
    grip.Up(s);Check(p.Piston&&!axis.Updating(s),"both hands release running item and clear held adjustment");
    grip.Down(s);Check(p.Piston&&p.Held,"both hands can regrab a running item");
    trigger.Down(s);Check(!p.Piston,"both hands can stop after regrab");
    grip.Up(s);axis.Move(s,0,0);
   }
   p.Stop();Check(!axis.Updating(s),"session stop removes active stick updates for either hand");
  }
  Console.WriteLine("PASS "+checks+" product VR grab checks; native host/geometry boundaries are doubles.");
 }
}
