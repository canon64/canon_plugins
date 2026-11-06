using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Integration;
using UnityEngine;
namespace UnityEngine {
 public class Object { public bool Destroyed; public static void Destroy(Object value){value.Destroyed=true;if(value is Component c)c.gameObject.Components.Remove(c);} }
 public class Component:Object { public GameObject gameObject;public Transform transform=>gameObject.transform;public T[] GetComponents<T>()=>gameObject.GetComponents<T>(); }
 public class MonoBehaviour:Component {public bool enabled=true;}
 public class GameObject:Object {public readonly Transform transform=new Transform();public readonly List<Component> Components=new List<Component>();public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}public T[] GetComponents<T>()=>Components.OfType<T>().ToArray();}
 public class Transform {public Vector3 position;public Quaternion rotation;public Vector3 eulerAngles;public Vector3 forward=>Vector3.forward;public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>default(Vector3);public static Vector3 forward=>new Vector3(0,0,1);public static Vector3 back=>new Vector3(0,0,-1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float n)=>new Vector3(a.x*n,a.y*n,a.z*n);public static Vector3 operator/(Vector3 a,float n)=>a*(1/n);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;}
 public struct Quaternion {public float x,y,z,w;}
 public struct Color { }
 public class Camera:MonoBehaviour {public float fieldOfView=65,nearClipPlane=.1f;}
 public class CapsuleCollider:MonoBehaviour { }
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
 public static class Application {public static bool isFocused=true;}
 public static class GUIUtility {public static int keyboardControl;}
}
public class ChaControl:MonoBehaviour { }
public class BaseCameraControl_Ver2:MonoBehaviour {
 public delegate bool NoCtrlFunc();public struct CameraData {public Vector3 Pos,Dir,Rot;public float Fov;}public struct ResetData {public CameraData Data;public void Copy(CameraData d,Quaternion q){Data=d;}}
 protected CameraData CamDat;protected ResetData CamReset;protected bool[] isDrags;protected bool isInit;protected CapsuleCollider viewCollider;public readonly int CONFIG_SIZE=4;public Transform transBase,targetObj;public Camera thisCmaera;public NoCtrlFunc NoCtrlCondition,ZoomCondition,KeyCondition;
 protected void Start(){ }public CameraData GetCameraData()=>CamDat;public void SetCameraData(CameraData value){CamDat=value;if(thisCmaera!=null){thisCmaera.fieldOfView=value.Fov;thisCmaera.transform.position=value.Pos+value.Dir;}}
}
namespace MainGameUiInputCapture {public static class UiInputCaptureApi {public static bool Pointer,Active;public static bool IsMouseInputBlocked()=>Pointer;public static bool IsAnyActive()=>Active;}}
namespace MainGameAiShoujoH.Contracts {
 public enum EndReason {Completed,Cancelled}
 public class Command { }
 public class Participant {public ChaControl Character;}
 public class StartRequest {public Camera Camera;public Participant[] Participants;public HSceneControls SceneControls;}
 public class SessionContext {public StartRequest Request;public Func<bool> CanInteract;public Action<string> ReportFailure;}
 public interface ISessionModule:IDisposable {string Name{get;}void Prepare(SessionContext c);void Execute(Command c);void End(EndReason r);}
 public interface IMotionSelectionTransition {void ValidateMotionSelection(string id,bool automatic);void SuspendSelection();void ResumeSelection();}
}
class Program {
 static int checks;static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 static bool Near(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)<1e-5&&Math.Abs(a.y-b.y)<1e-5&&Math.Abs(a.z-b.z)<1e-5;
 static void Main(){
  var root=new GameObject();var camera=root.AddComponent<Camera>();camera.transform.position=new Vector3(3,4,5);camera.transform.rotation=new Quaternion{w=1};
  var previous=root.AddComponent<BaseCameraControl_Ver2>();var saved=new BaseCameraControl_Ver2.CameraData{Pos=new Vector3(3,4,6),Dir=Vector3.back,Fov=65};previous.thisCmaera=camera;previous.SetCameraData(saved);
  var controls=new HSceneControls{ReadCamera=()=>new HCameraState{Position=camera.transform.position,Rotation=camera.transform.rotation,FieldOfView=camera.fieldOfView},ApplyCamera=s=>{camera.transform.SetPositionAndRotation(s.Position,s.Rotation);camera.fieldOfView=s.FieldOfView;},ResetCamera=()=>{camera.transform.position=new Vector3(3,4,5);camera.fieldOfView=65;}};
  bool ready=false;var actor=new GameObject().AddComponent<ChaControl>();var request=new StartRequest{Camera=camera,SceneControls=controls,Participants=new[]{new Participant{Character=actor}}};var context=new SessionContext{Request=request,CanInteract=()=>ready,ReportFailure=s=>throw new Exception(s)};
  var lease=new DedicatedCameraInput();lease.Prepare(context);var driver=root.GetComponents<NativeDedicatedCameraInput>().Single();
  Check(!previous.enabled&&!driver.enabled,"Prepare disables competing native owner and keeps new input dormant");
  Check(!Near(camera.transform.position,Vector3.zero)&&camera.fieldOfView==65,"Prepare does not reset pose or FOV");
  lease.ResumeSelection();Check(!driver.enabled,"Pending reveal cannot enable input");ready=true;lease.ResumeSelection();
  Check(driver.enabled&&!driver.NoCtrlCondition()&&driver.KeyCondition(),"Ready reveal enables inherited native mouse/key input");
  Check(Near(camera.transform.position,new Vector3(3,4,5)),"Native CamData synchronization preserves actual camera pose");
  MainGameUiInputCapture.UiInputCaptureApi.Pointer=true;Check(driver.NoCtrlCondition(),"Common UI pointer region blocks native mouse input");MainGameUiInputCapture.UiInputCaptureApi.Pointer=false;
  MainGameUiInputCapture.UiInputCaptureApi.Active=true;Check(!driver.KeyCondition(),"Common UI drag blocks native keyboard input");MainGameUiInputCapture.UiInputCaptureApi.Active=false;
  GUIUtility.keyboardControl=1;Check(!driver.KeyCondition(),"Focused text field blocks keyboard camera");GUIUtility.keyboardControl=0;
  Application.isFocused=false;Check(driver.NoCtrlCondition()&&!driver.KeyCondition(),"Focus loss blocks camera input");Application.isFocused=true;
  Check(!driver.ZoomCondition(),"Wheel remains H progression input, native right drag owns dolly zoom");
  request.SceneControls.ApplyCamera(new HCameraState{Position=new Vector3(8,9,10),Rotation=new Quaternion{w=1},FieldOfView=50});
  Check(Near(camera.transform.position,new Vector3(8,9,10))&&driver.GetCameraData().Fov==50,"UI camera edit synchronizes native CamData once");
  lease.SuspendSelection();Check(!driver.enabled,"Selection suspends inherited driver");ready=false;lease.ResumeSelection();Check(!driver.enabled,"Selection reveal must finish before resume");ready=true;lease.ResumeSelection();
  lease.End(EndReason.Cancelled);Check(!driver.enabled,"End stops native camera before actor restoration");lease.Dispose();lease.Dispose();
  Check(previous.enabled&&previous.GetCameraData().Pos.z==saved.Pos.z,"Existing native driver and camera data restored");
  Check(Near(camera.transform.position,new Vector3(3,4,5))&&camera.fieldOfView==65&&camera.transform.rotation.w==1,"Original position rotation FOV restored");
  Check(root.GetComponents<NativeDedicatedCameraInput>().Length==0&&root.GetComponents<CapsuleCollider>().Length==0,"Session native driver and disabled bookkeeping collider released");
  Check(ReferenceEquals(request.SceneControls,controls),"Original scene-control interface restored");
  Check(!typeof(NativeDedicatedCameraInput).GetMethods(System.Reflection.BindingFlags.DeclaredOnly|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Any(m=>m.Name=="Update"||m.Name=="LateUpdate"),"Product adds no Update/LateUpdate monitor");
  Console.WriteLine("DEDICATED_CAMERA_LEASE_SOURCE_LINK_PASS "+checks+"; native pointer behavior is not a live-game claim");
 }
}
