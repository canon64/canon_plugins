using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using UnityEngine;
using Numerics = System.Numerics;

// Managed boundary adapter: no Unity process, native calls, game launch or desktop input.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        internal Numerics.Vector3 N => new Numerics.Vector3(x,y,z);
        internal static Vector3 From(Numerics.Vector3 n) => new Vector3(n.X,n.Y,n.Z);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3(0,1,0);
        public float sqrMagnitude => N.LengthSquared();
        public float magnitude => N.Length();
        public Vector3 normalized => magnitude<1e-9f?zero:this/magnitude;
        public static Vector3 operator +(Vector3 a,Vector3 b) => From(a.N+b.N);
        public static Vector3 operator -(Vector3 a,Vector3 b) => From(a.N-b.N);
        public static Vector3 operator -(Vector3 a) => From(-a.N);
        public static Vector3 operator *(Vector3 a,float b) => From(a.N*b);
        public static Vector3 operator /(Vector3 a,float b) => From(a.N/b);
        public static float Dot(Vector3 a,Vector3 b) => System.Numerics.Vector3.Dot(a.N,b.N);
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n) => a-n*Dot(a,n)/n.sqrMagnitude;
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) => a+(b-a)*t;
        public static Vector3 Slerp(Vector3 a,Vector3 b,float t) {
            float angle=(float)Math.Acos(Mathf.Clamp(Dot(a.normalized,b.normalized),-1,1));
            if(angle<1e-5f)return Lerp(a,b,t);
            return (a*(float)Math.Sin((1-t)*angle)+b*(float)Math.Sin(t*angle))/(float)Math.Sin(angle);
        }
    }
    public struct Quaternion
    {
        internal Numerics.Quaternion N;
        public float x=>N.X;public float y=>N.Y;public float z=>N.Z;public float w=>N.W;
        public Quaternion(float x,float y,float z,float w) { N=new Numerics.Quaternion(x,y,z,w); }
        internal Quaternion(Numerics.Quaternion n) { N=n; }
        public static Quaternion identity=>new Quaternion(Numerics.Quaternion.Identity);
        public static Quaternion Inverse(Quaternion q)=>new Quaternion(Numerics.Quaternion.Inverse(q.N));
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion(a.N*b.N);
        public static Vector3 operator *(Quaternion q,Vector3 v)=>Vector3.From(System.Numerics.Vector3.Transform(v.N,q.N));
        public static Quaternion Euler(Vector3 e)=>Euler(e.x,e.y,e.z);
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion(Numerics.Quaternion.CreateFromYawPitchRoll(y*(float)Math.PI/180,x*(float)Math.PI/180,z*(float)Math.PI/180));
        public Vector3 eulerAngles { get {
            var m=Numerics.Matrix4x4.CreateFromQuaternion(N);
            return new Vector3((float)Math.Asin(Mathf.Clamp(-m.M32,-1,1)),(float)Math.Atan2(m.M31,m.M33),(float)Math.Atan2(m.M12,m.M22))*(180/(float)Math.PI);
        }}
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>new Quaternion(Numerics.Quaternion.Slerp(a.N,b.N,t));
        public static Quaternion LookRotation(Vector3 forward,Vector3 up) {
            var f=forward.normalized.N;var r=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(up.N,f));var u=System.Numerics.Vector3.Cross(f,r);
            return new Quaternion(Numerics.Quaternion.CreateFromRotationMatrix(new Numerics.Matrix4x4(r.X,r.Y,r.Z,0,u.X,u.Y,u.Z,0,f.X,f.Y,f.Z,0,0,0,0,1)));
        }
        public static Quaternion LookRotation(Vector3 forward)=>LookRotation(forward,Vector3.up);
    }
    public class Transform
    {
        public static int Searches;
        public string name; public Transform parent; public List<Transform> children=new List<Transform>();
        public Vector3 localScale=new Vector3(1,1,1);
        public Vector3 localPosition;public Quaternion localRotation=Quaternion.identity;
        public Transform(string name,Transform parent=null) { this.name=name;this.parent=parent;parent?.children.Add(this); }
        public Vector3 position { get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent==null?value:parent.InverseTransformPoint(value); }
        public Quaternion rotation { get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value; }
        public Vector3 eulerAngles=>rotation.eulerAngles;
        public Vector3 up=>rotation*Vector3.up;public Vector3 forward=>rotation*new Vector3(0,0,1);
        public Vector3 TransformPoint(Vector3 p) { var local=localPosition+localRotation*Vector3.Scale(localScale,p);return parent==null?local:parent.TransformPoint(local); }
        public Vector3 InverseTransformPoint(Vector3 p) { var v=Quaternion.Inverse(localRotation)*((parent==null?p:parent.InverseTransformPoint(p))-localPosition);return new Vector3(v.x/localScale.x,v.y/localScale.y,v.z/localScale.z); }
        public void SetPositionAndRotation(Vector3 p,Quaternion r) { position=p;rotation=r; }
        public Transform Find(string path) { Searches++;var t=this;foreach(var s in path.Split('/')) { t=t.children.FirstOrDefault(c=>c.name==s);if(t==null)return null; }return t; }
        public T[] GetComponentsInChildren<T>(bool inactive) { Searches++;return new[]{this}.Concat(children.SelectMany(c=>c.GetComponentsInChildren<Transform>(inactive))).Cast<T>().ToArray(); }
    }
    public class GameObject { public Transform transform=new Transform("object");public T[] GetComponentsInChildren<T>(bool inactive)=>transform.GetComponentsInChildren<T>(inactive); }
    public class Camera { public Transform transform=new Transform("camera");public float fieldOfView=60; }
    public enum KeyCode { F8 }
    public static class Time { public static float unscaledTime; }
    public static class Mathf {
        public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Clamp01(float x)=>Clamp(x,0,1);public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Abs(float x)=>Math.Abs(x);public static float Sqrt(float x)=>(float)Math.Sqrt(x);
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        public static float InverseLerp(float a,float b,float x)=>Clamp01((x-a)/(b-a));
    }
}
public class ChaControl { public Transform transform=new Transform("actor"); public GameObject animBody,objBodyBone; }
public class HSceneProc { public IList lstFemale=new ArrayList(); }
public static class SessionGate { public static bool Allowed=true;public static HSceneProc HScene=new HSceneProc(); }
namespace HarmonyLib { public static class AccessTools { public static Type TypeByName(string n)=>null;public static MethodInfo Method(Type t,string n)=>null; } }
namespace BepInEx.Bootstrap {
    public class Info { public object Instance; }
    public static class Chainloader { public static Dictionary<string,Info> PluginInfos=new Dictionary<string,Info>(); }
}
namespace MainGameCameraControl {
    internal partial class Plugin {
        internal class Controller { public Camera thisCmaera=new Camera(); }
        internal Controller _ctrlCamera=new Controller();internal ChaControl owner;internal Transform bone,frame;
        internal bool _boneLinkActive=true,_activeLookAtWorldY,_activeBoneLinkUseRelativeDir,_activeBoneLinkRelFromLookAt,_activeBoneLinkRollWithBone,_relYawUpAxisFallback,_relFloorGuard;
        internal int _activeBoneLinkTarget=1;internal Vector3 _activeBoneLinkLookAtOffsetLocal,_activeBoneLinkRelDirLocal;
        internal float _activeBoneLinkRelDistance;internal string _activeBoneLinkFrameBoneName="hips";
        private void RefreshCameraControllerCache() {}private ChaControl ResolveMainFemale()=>owner;
        private Transform ResolveActiveBoneTarget(ChaControl c)=>bone;private Transform[] GetFemaleBoneCache(ChaControl c)=>new[]{bone,frame};
        private Transform ResolveFrameBone(Transform[] b,string n)=>frame;
    }
}
namespace MainGameSubCameraDisplayProbe {
    public sealed partial class Plugin {
        private const int SaveModeNormal=0,SaveModeBoneLink=1;
        private static readonly string[] BoneTargetLabels={"head","chest","waist"};
        private static readonly string[][] BoneTargetNameCandidates={new[]{"head"},new[]{"chest"},new[]{"waist"}};
        private static readonly FieldInfo HSceneFemaleListField=typeof(HSceneProc).GetField("lstFemale");
        private ProbeSettings _settings=new ProbeSettings();private GameObject _cameraAnchorObject=new GameObject();private Camera _subCamera=new Camera();
        private bool _boneFollowActive,_activeSaveCameraPosition;private string _activeBonePresetName,_activeBoneName,_activeCameraPresetName,_presetNameBuffer,_cachedActiveBoneName;
        private int _activeBoneTarget,_cachedActiveBoneTarget;private ChaControl _cachedActiveBoneFemale;
        private Transform _cachedActiveBone,_cachedActiveBoneRoot;private float _nextActiveBoneResolveTime,_activePresetFov;
        private Vector3 _activeLookAtOffsetLocal,_activeCameraOffsetLocal;private long _actorLease;
        private float _externalPresetDuration,_autoCycleAwayUntil,_autoCycleNextAt;private string poseKey="riding",status;
        private SubCameraPreset[] persisted;
        private string _lastPoseKey,_lastPoseDisplayName,testPath;private bool _poseTrackingInitialized,_poseTrackingPausedByDisable,failWrite;
        private GameObject _displayAnchorObject;
        private GameObject _rootObject=new GameObject(),_displayObject;
        private sealed class TestGizmo { public bool IsDragging; }
        private TestGizmo _cameraGizmo=new TestGizmo(),_displayGizmo;
        private bool _suspendedForAstralMode,_externalPose,_holdingCamera;
        private Vector3 _externalOffset;
        private int _testFollowCount,_testGripCaptures,_testGripAim,_testFixedSettingApplications;
        private static float Round2(float v)=>(float)Math.Round(v,2);
        private ChaControl _externalActor;private Camera _externalCamera;private SubCameraPreset _externalPreset;
        private Vector3 _externalLookOffset,_externalPresetStartPosition;private Quaternion _externalPresetStartRotation;
        private float _externalPresetStartFov,_externalPresetStarted;private Transform _externalBone,_externalBoneRoot;private int _externalBoneChoice;
        private bool ActorLeaseValid(long id)=>id!=0&&id==_actorLease&&_externalActor!=null;
        private void SyncExternalCameraPositionDisplay() {}
        private bool TryGetCurrentPoseInfo(out string key,out string label) { key=poseKey;label=poseKey;return true; }
        private SubCameraPreset FindPresetByName(string n)=>_settings.Presets.FirstOrDefault(p=>p.Name==n);
        private void SetStatus(string s) { status=s; }private void LogInfo(string s) {}private void EnsureCameraProbe() {}
        private void SyncGizmoState() { _testFixedSettingApplications++; }private void NotifyAutoCycleLoadPreset(SubCameraPreset p,string s) {}
        private void LoadPreset(SubCameraPreset p)=>LoadPreset(p,"test");
        private static bool HasArray(float[] a)=>a!=null&&a.Length>=3;
        private void BeginPlaybackCopyTrace(object[] data) {}
        private void TracePlaybackCopyApplied() {}
        private void TracePlaybackCopySaved(SubCameraPreset p) {}
        private void TraceNextPlaybackBoneFollow() { _testFollowCount++; }
        // Unrelated runtime services are inert; production Update/LateUpdate and pose math run unchanged.
        private bool IsHoldingCameraAnchor()=>_holdingCamera;
        private void TickVisibilityDiagnostic() {} private void HandleToggleKey() {}
        private void DisableRuntimeState(string reason) {} private void ReleaseActor() { _actorLease=0; }
        private void CaptureActiveBoneCameraOffset() { _testGripCaptures++; }
        private void ApplyBoneLookAtRotationOnly() { _testGripAim++; }
        private void ApplyCameraSettings() {} private void UpdateBeatFovRuntime() {}
        private void UpdateAutoCycle() {} private void UpdatePosePresetTracking() {}
        private void ApplyDisplaySettings() { _testFixedSettingApplications++; } private void UpdateVrGrab() {}
        private void UpdateVideoRecording() {} private void SyncDisplayOverlayCamera() { _testFixedSettingApplications++; }
        private void SyncCharactersOnlyHook() { _testFixedSettingApplications++; } private void UpdateInputCapture() {}
        private static Vector3 ToVector3(float[] a,Vector3 fallback)=>HasArray(a)?new Vector3(a[0],a[1],a[2]):fallback;
        private static string FormatArray(float[] a)=>"";
        private bool SaveSettings() { if(failWrite)return false;bool ok=SettingsStore.Save(testPath,_settings,s=>{throw new Exception(s);});if(ok)persisted=SettingsStore.LoadOrCreate(testPath,s=>{},s=>{throw new Exception(s);}).Presets;return ok; }
        private static T RoundTrip<T>(T value) { using(var stream=new MemoryStream()) { var s=new DataContractJsonSerializer(typeof(T));s.WriteObject(stream,value);stream.Position=0;return (T)s.ReadObject(stream); } }
        private static void Check(bool ok,string label) { if(!ok)throw new Exception(label); }
        private static void Near(Vector3 a,Vector3 b,string label) { Check((a-b).magnitude<.00005f,label+" position error "+(a-b).magnitude); }
        private static void Near(Quaternion a,Quaternion b,string label) { Check(Math.Abs(Numerics.Quaternion.Dot(Numerics.Quaternion.Normalize(a.N),Numerics.Quaternion.Normalize(b.N)))>.999999f,label+" rotation error"); }
        private static void AssertView(Plugin sub,Vector3 position,Vector3 look,string name) {
            Near(sub._subCamera.transform.position,position,name);
            Near(sub._subCamera.transform.forward,(look-position).normalized,name+" aim");
            Check(sub._boneFollowActive||sub._actorLease!=0,name+" lost bone mode");
        }
        private static void RunFrameTimingChecks(Plugin source,Transform bone,Vector3 position,Vector3 localLook) {
            var sub=new Plugin();sub._settings=RoundTrip(source._settings);sub._settings.Enabled=true;
            sub._settings.TransitionSeconds=0;
            sub._subCamera.transform.parent=sub._cameraAnchorObject.transform;
            var beforePosition=bone.position;var beforeRotation=bone.rotation;float beforeTime=Time.unscaledTime;
            var preAnimation=new Vector3(2.046623f,1.263602f,-64.101340f);
            var renderedPosition=new Vector3(1.889297f,.070629f,-64.199390f);
            sub.LoadPreset(sub._settings.Presets[0]);
            for(int frame=0;frame<3;frame++) {
                var priorRotation=sub._subCamera.transform.rotation;int priorCount=sub._testFollowCount;
                bone.position=preAnimation;Time.unscaledTime+=.02f;
                sub.Update();
                Near(sub._subCamera.transform.rotation,priorRotation,"Update changed aim before animation");
                Check(sub._testFollowCount==priorCount,"Update ran early follow");
                Check(sub._testFixedSettingApplications==0,"Idle Update reapplied fixed settings or camera hooks");
                bone.SetPositionAndRotation(renderedPosition+new Vector3(.01f*frame,.02f*frame,0),Quaternion.Euler(10*frame,20,-15));
                sub.LateUpdate();
                AssertView(sub,position,bone.TransformPoint(localLook),"post-animation frame "+frame);
                Check(sub._testFollowCount==priorCount+1,"follow must run once after animation");
            }
            var heldRotation=sub._subCamera.transform.rotation;
            for(int guard=0;guard<4;guard++) {
                int priorCount=sub._testFollowCount;bone.position=preAnimation;
                SessionGate.Allowed=guard!=0;sub._settings.Enabled=guard!=1;
                sub._suspendedForAstralMode=guard==2;sub._holdingCamera=guard==3;
                sub.Update();sub.LateUpdate();
                Near(sub._subCamera.transform.rotation,heldRotation,"late guard "+guard);
                Check(sub._testFollowCount==priorCount,"late guard followed "+guard);
            }
            SessionGate.Allowed=true;sub._settings.Enabled=true;sub._suspendedForAstralMode=false;sub._holdingCamera=false;
            sub._cameraGizmo.IsDragging=true;sub.LateUpdate();
            Check(sub._testGripCaptures==1&&sub._testGripAim==1,"gizmo drag lost offset/aim handling");
            sub._cameraGizmo.IsDragging=false;
            sub._settings.TransitionSeconds=0;sub.LoadPreset(sub._settings.Presets[1]);
            sub._settings.TransitionSeconds=.5f;sub.LoadPreset(sub._settings.Presets[0]);
            var transitionStartPosition=sub._subCamera.transform.position;var transitionStartRotation=sub._subCamera.transform.rotation;
            bone.position=preAnimation;Time.unscaledTime+=.25f;sub.Update();
            Near(sub._subCamera.transform.position,transitionStartPosition,"Update advanced transition position");
            Near(sub._subCamera.transform.rotation,transitionStartRotation,"Update advanced transition aim");
            bone.position=renderedPosition;sub.LateUpdate();
            ResolveSubBoneView(sub._transitionPreset.BoneCamera,bone,localLook,position,Quaternion.identity,out Vector3 targetPosition,out Quaternion targetRotation);
            Near(sub._subCamera.transform.position,Vector3.Lerp(transitionStartPosition,targetPosition,.5f),"late transition position");
            Near(sub._subCamera.transform.rotation,Quaternion.Slerp(transitionStartRotation,targetRotation,.5f),"late transition uses animated bone");
            bone.position=preAnimation;Time.unscaledTime+=.5f;sub.Update();
            Check(sub._transitionActive,"Update finished transition before animation");
            bone.position=renderedPosition+new Vector3(.03f,-.02f,.01f);sub.LateUpdate();
            AssertView(sub,position,bone.TransformPoint(localLook),"late transition final");
            sub._settings.TransitionSeconds=.5f;sub.LoadPreset(sub._settings.Presets[0]);
            sub._holdingCamera=true;sub.LateUpdate();Check(!sub._transitionActive,"grab did not cancel transition");
            bone.SetPositionAndRotation(beforePosition,beforeRotation);Time.unscaledTime=beforeTime;
        }
        public static string RunTransferChecks(string evidenceRoot) {
            var sourcePosition=new Vector3(2.09123f,.59123f,-63.91123f);
            var localLook=new Vector3(.123456f,.133333f,-.041234f);
            var other=new ChaControl();var actor=new ChaControl();
            SessionGate.HScene.lstFemale=new ArrayList{other,actor};
            new Transform("chest",other.transform);
            actor.transform.SetPositionAndRotation(new Vector3(-2,.7f,-60),Quaternion.Euler(7,15,-8));
            actor.transform.localScale=new Vector3(1.1f,.9f,1.2f);
            var rig=new Transform("rig",actor.transform);rig.localScale=new Vector3(.8f,1.3f,1.1f);
            var bone=new Transform("chest",rig);
            bone.SetPositionAndRotation(new Vector3(2,.6f,-64),Quaternion.Euler(-35,70,25));
            var main=new MainGameCameraControl.Plugin{owner=actor,bone=bone,_activeBoneLinkLookAtOffsetLocal=localLook};
            main._ctrlCamera.thisCmaera.transform.position=sourcePosition;
            main._ctrlCamera.thisCmaera.fieldOfView=88.123f;
            // Source Euler/roll is deliberately unrelated: the bone-mode contract uses position and look-at.
            main._ctrlCamera.thisCmaera.transform.rotation=Quaternion.Euler(60,15,75);
            BepInEx.Bootstrap.Chainloader.PluginInfos["com.kks.maingame.cameracontrol"]=new BepInEx.Bootstrap.Info{Instance=main};
            var sub=new Plugin();sub.testPath=Path.Combine(evidenceRoot,"transfer-"+Guid.NewGuid()+".json");
            sub._settings.TransitionSeconds=0;
            var subRoot=new Transform("sub-root");subRoot.position=new Vector3(4,3,-62);subRoot.rotation=Quaternion.Euler(5,40,8);
            subRoot.localScale=new Vector3(.7f,1.2f,.9f);sub._cameraAnchorObject.transform.parent=subRoot;
            sub._subCamera.transform.parent=sub._cameraAnchorObject.transform;
            sub._subCamera.transform.localPosition=new Vector3(.5f,.2f,.3f);sub._subCamera.transform.localRotation=Quaternion.Euler(15,25,35);
            sub._settings.Presets=new[]{new SubCameraPreset{Name="chest"},new SubCameraPreset{Name="other",CameraPosition=new float[]{5,2,3},CameraRotation=new float[]{0,0,0}}};
            sub._activeCameraPresetName="chest";Vector3 look=bone.TransformPoint(localLook);
            sub.OverwritePlaybackCameraPose();Check(sub.persisted!=null,"circle failed: "+sub.status);
            AssertView(sub,sourcePosition,look,"circle");
            Check(sub._settings.SelectedSaveMode==SaveModeBoneLink,"bone save mode not selected");
            Check(sub._lastPoseKey=="riding","pose-auto tracker not synchronized");
            Check(Math.Abs(sub._subCamera.fieldOfView-88.123f)<.00001f,"FOV lost");
            var saved=sub.persisted[0].PoseOverrides.Single();
            Near(ToVector3(saved.LookAtOffsetLocal,Vector3.zero),localLook,"real store rounded offset");
            Near(ToVector3(saved.CameraPosition,Vector3.zero),sourcePosition,"real store rounded position");
            Check(saved.BoneCamera.FormatVersion==2&&saved.BoneCamera.OwnerFemaleSlot==1,"binding/version lost");
            sub.UpdateBoneFollow();AssertView(sub,sourcePosition,look,"first follow");
            sub._settings.Presets=sub.persisted;sub.LoadPreset(sub._settings.Presets[1]);sub.LoadPreset(sub._settings.Presets[0]);
            AssertView(sub,sourcePosition,look,"file reload");
            RunFrameTimingChecks(sub,bone,sourcePosition,localLook);
            Check(sub.ResolveActiveBone()==bone,"wrong same-named bone");
            int searches=Transform.Searches;
            bone.SetPositionAndRotation(new Vector3(-1,1,-60),Quaternion.Euler(50,-80,-40));
            sub.UpdateBoneFollow();AssertView(sub,sourcePosition,bone.TransformPoint(localLook),"moving/rotating bone");
            Check(Transform.Searches==searches,"follow searched hierarchy");
            sub._settings.TransitionSeconds=.5f;sub.LoadPreset(sub._settings.Presets[1]);sub.LoadPreset(sub._settings.Presets[0]);
            Time.unscaledTime+=1;sub.UpdateCameraTransition();AssertView(sub,sourcePosition,bone.TransformPoint(localLook),"transition final");
            // Main's world-Y option is captured as an actual point, converted once to a bone-local point.
            main._activeLookAtWorldY=true;main._activeBoneLinkLookAtOffsetLocal=new Vector3(0,.134567f,0);
            Vector3 worldLook=bone.position+Vector3.up*.134567f;
            Vector3 converted=bone.InverseTransformPoint(worldLook);
            sub.OverwritePlaybackCameraPose();AssertView(sub,sourcePosition,worldLook,"world-Y conversion");
            bone.rotation=Quaternion.Euler(-10,20,30);sub.UpdateBoneFollow();
            AssertView(sub,sourcePosition,bone.TransformPoint(converted),"converted point follows real bone");
            // Main-only settings may vary; exported geometry and sub behavior remain the same.
            main._activeBoneLinkUseRelativeDir=true;main._activeBoneLinkRelDistance=40;main._activeBoneLinkRollWithBone=true;main._relFloorGuard=true;
            sub.OverwritePlaybackCameraPose();sub.UpdateBoneFollow();
            AssertView(sub,sourcePosition,bone.position+Vector3.up*.134567f,"main settings ignored");
            var hBefore=RoundTrip(sub._settings.Presets[0].PoseOverrides[0]);
            var independent=new ChaControl();var independentBone=new Transform("chest",independent.transform);
            independentBone.position=new Vector3(12,3,-28);independentBone.rotation=Quaternion.Euler(20,90,-15);
            _copiedIndependentActor=()=>independent;
            main.owner=independent;main.bone=independentBone;main._activeLookAtWorldY=false;main._activeBoneLinkLookAtOffsetLocal=localLook;
            sub._actorLease=1;sub._externalActor=independent;sub._externalCamera=sub._subCamera;
            sub.OverwritePlaybackCameraPose();sub.LateUpdate();
            AssertView(sub,sourcePosition,independentBone.TransformPoint(localLook),"actor circle");
            Check(sub.persisted[0].ActorCamera.BoneCamera.OwnerIndependentActor,"actor binding lost");
            Near(ToVector3(sub.persisted[0].PoseOverrides[0].CameraPosition,Vector3.zero),ToVector3(hBefore.CameraPosition,Vector3.zero),"actor changed H shot");
            var actorSaved=RoundTrip(sub.persisted[0].ActorCamera);sub._settings.Presets=sub.persisted;sub._settings.TransitionSeconds=0;
            sub.LoadPreset(sub._settings.Presets[0]);Check(sub._externalBone==independentBone,"actor recall selected H bone");
            independentBone.position+=new Vector3(2,-1,3);sub.LateUpdate();
            AssertView(sub,sourcePosition,independentBone.TransformPoint(localLook),"actor moving bone");
            sub._actorLease=0;main.owner=actor;main.bone=bone;sub.LoadPreset(sub._settings.Presets[0]);
            Check(sub.ResolveActiveBone()==bone,"H recall selected actor bone");
            sub.OverwritePlaybackCameraPose();
            Near(ToVector3(sub.persisted[0].ActorCamera.CameraPosition,Vector3.zero),ToVector3(actorSaved.CameraPosition,Vector3.zero),"H changed actor shot");
            sub.poseKey="other-pose";sub.OverwritePlaybackCameraPose();Check(sub.persisted[0].PoseOverrides.Length==2,"pose collision");
            var unchanged=sub.persisted;var priorPosition=sub._subCamera.transform.position;
            SessionGate.Allowed=false;Check(main.CaptureMainCameraForSubCamera()==null,"main bypassed gate");sub.OverwritePlaybackCameraPose();
            Check(ReferenceEquals(unchanged,sub.persisted),"rejected gate wrote settings");Near(sub._subCamera.transform.position,priorPosition,"gate rejected pose");
            SessionGate.Allowed=true;main.owner=independent;main.bone=independentBone;sub.OverwritePlaybackCameraPose();
            Check(ReferenceEquals(unchanged,sub.persisted),"actor source wrote H context");
            sub._actorLease=1;main.owner=actor;main.bone=bone;sub.OverwritePlaybackCameraPose();
            Check(ReferenceEquals(unchanged,sub.persisted),"H source wrote actor context");sub._actorLease=0;
            sub.failWrite=true;var previousOverrides=sub._settings.Presets[0].PoseOverrides;sub.OverwritePlaybackCameraPose();
            Check(ReferenceEquals(previousOverrides,sub._settings.Presets[0].PoseOverrides),"failed write changed model");
            Near(sub._subCamera.transform.position,priorPosition,"failed write changed camera");sub.failWrite=false;
            SessionGate.HScene.lstFemale=new ArrayList{other};Check(!ResolveCopiedBinding(saved.BoneCamera),"missing owner fallback");
            SessionGate.HScene.lstFemale=new ArrayList{other,actor};saved.BoneCamera.BonePath="rig/missing";
            Check(!ResolveCopiedBinding(saved.BoneCamera),"missing bone fallback");
            // Legacy copied data with exact binding/local offset can be upgraded without its erroneous follow flags.
            var legacy=RoundTrip(hBefore);legacy.BoneCamera.FormatVersion=0;
            var legacySettings=new ProbeSettings{Presets=new[]{new SubCameraPreset{Name="legacy",PoseOverrides=new[]{legacy}}}};
            string legacyPath=Path.Combine(evidenceRoot,"legacy-"+Guid.NewGuid()+".json");
            using(var stream=File.Create(legacyPath))new DataContractJsonSerializer(typeof(ProbeSettings)).WriteObject(stream,legacySettings);
            string original=File.ReadAllText(legacyPath);
            var upgraded=SettingsStore.LoadOrCreate(legacyPath,s=>{},s=>{throw new Exception(s);});
            Check(File.ReadAllText(legacyPath+".before-bone-shot-v2.bak")==original,"legacy backup differs");
            Check(upgraded.Presets[0].PoseOverrides[0].BoneCamera.FormatVersion==2,"legacy upgrade failed");
            var incompatible=RoundTrip(hBefore);incompatible.BoneCamera.LookAtWorldY=true;
            var incompatiblePreset=new SubCameraPreset{Name="incompatible",UseBoneLink=true,BoneCamera=incompatible.BoneCamera,LookAtOffsetLocal=incompatible.LookAtOffsetLocal};
            Check(!sub.PrepareCopiedBinding(incompatiblePreset),"old world-Y guessed");
            incompatible.BoneCamera.LookAtWorldY=false;incompatible.BoneCamera.HasOwnerBinding=false;
            Check(!sub.PrepareCopiedBinding(incompatiblePreset),"missing legacy owner guessed");
            var outdated=new object[25];outdated[3]=true;outdated[9]=false;Check(ReadCopiedBoneCamera(outdated)==null,"old protocol accepted");
            return "PASS: production capture, coordinate conversion, settings save/load and normalization; production Update -> animation/IK -> LateUpdate scheduling, late transition and guards/grabs; immediate/first follow/transition; fixed world position with moving and rotating bones; scaled parents; moved/world-Y look-at conversion; bone mode persistence; H/actor/pose isolation; gate rejection; failed-write rollback; legacy backup/upgrade/rejection; API version.";
        }
    }
}
