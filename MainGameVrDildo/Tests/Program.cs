using System;
using System.Collections.Generic;
using CanonGate;
using MainGameVrDildo;
using CanonPlugins.Shared;

static class Program
{
 static int checks;
 static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
 static void Main()
 {
  Console.WriteLine("START: heart/star expression, climax hold and voice-cycle integration; real gate, rendering/audio stubbed.");
  var five = new[] { 1, 4, 7, 10, 13 };
  Check(Array.Find(ExpressionNames.Eyes, entry => entry.Key == 25).Value == "瞳にハート", "eye list ID 25 name lost");
  Check(Array.Find(ExpressionNames.Eyelids, entry => entry.Key == 25).Value.Contains("左目"), "shape ID 25 mislabeled as heart");
  Check(ExpressionSlotChoices.Normalize(new[]{1000007}, null)[0] == 1000007, "MOD eye ID truncated during save normalization");
  for (int i = 0; i < 5; i++) { int selected = i; Check(ExpressionSlotChoices.Choose(five, true, -1, 53, 0, count => selected) == five[i], "slot " + i + " unreachable"); }
  Check(ExpressionSlotChoices.Choose(new[] { -1, 9, 11, -1, -1 }, false, -1, 53, 0, count => 0) == 9, "fixed mode ignores first enabled slot");
  Check(ExpressionSlotChoices.Choose(new[] { 24, 24, -1, -1, -1 }, true, 24, 53, 0, count => 0) == 24, "duplicate slots caused empty choices");
  Check(ExpressionSlotChoices.Choose(new[] { 24, 43, 44, 45, 7 }, true, 24, 53, 0, count => 3) == 7, "fifth mouth candidate lost after repeat exclusion");
  var migrated = ExpressionSlotChoices.Normalize(null, new[] { 8, 12 });
  Check(migrated.Length == 5 && migrated[0] == 8 && migrated[1] == 12 && migrated[4] == -1, "legacy two-slot migration");
  foreach(var pose in new[]{(0,"立ち愛撫"),(1,"仰向け愛撫"),(7,"座り愛撫")})
  {
   Check(CloneSpiralPolicy.IsClone(0,pose.Item1,pose.Item2+"【クローン】"),"clone rejected");
   Check(!CloneSpiralPolicy.IsClone(0,pose.Item1,pose.Item2),"ordinary pose accepted");
  }
  foreach(float gauge in new[]{0f,69.99f,100f,float.NaN,float.PositiveInfinity})
   Check(!CloneSpiralPolicy.IsReady(gauge,"A_Loop"),"invalid readiness accepted");
  Check(CloneSpiralPolicy.IsReady(70,"A_Loop")&&CloneSpiralPolicy.IsReady(99.99f,"A_Idle"),"ready boundaries");
  foreach(string state in new[]{"Orgasm_Start","Orgasm_Loop","Orgasm_A","After",null,""})
   Check(!CloneSpiralPolicy.IsReady(80,state),"climax accepted as preparation");
  GateApi.SetRunning(true);GateApi.ReloadSettings=()=>{};GateApi.AllowActivity=(g,k)=>false;
  GateApi.ReportError=e=>throw new Exception("gate start failed",e);
  var scene=new HSceneProc();var actor=new ChaControl();var other=new ChaControl();scene.lstFemale.Add(actor);
  var plugin=new Plugin();plugin.Initialize(scene,actor);
  Check(!plugin.Ready,"gate bypass before H");
  Check(Plugin.AcquireSyringeVoicePriority()==null,"voice priority bypassed gate before H");
  var policy=new ExceptionPolicy();GateApi.BeginSession(GateApi.SessionKind.H,false,policy,scene);
  Check(!plugin.Ready,"gate bypass in denied H");Check(Plugin.AcquireSyringeVoicePriority()==null,"voice priority bypassed denied H");GateApi.EndSession(GateApi.SessionKind.H);
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
  Check(plugin.Ready,"allowed H did not start");
  int requestsBefore=MainGameVoiceFaceEventBridge.PublicApi.PistonRequests;
  var priorityA=Plugin.AcquireSyringeVoicePriority();var priorityB=Plugin.AcquireSyringeVoicePriority();
  Check(priorityA!=null&&priorityB!=null&&plugin.VoiceSuppressed,"syringe priority acquisition failed");
  Check(MainGameVoiceFaceEventBridge.PublicApi.ImmediateStops==1&&plugin.OralStops==1,"first syringe did not immediately stop current dildo audio");
  plugin.RequestPiston();Check(MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==requestsBefore,"piston request played over syringe dialogue");
  priorityA.Dispose();priorityA.Dispose();Check(plugin.VoiceSuppressed&&!MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled,"first/duplicate release interrupted second syringe voice");
  priorityB.Dispose();Check(!plugin.VoiceSuppressed&&MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled,"last syringe voice did not release suppression");
  plugin.DispatchVoices();Check(MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==requestsBefore,"discarded dildo voice replayed after syringe ended");
  MainGameVoiceFaceEventBridge.PublicApi.Accepted=true;
  int hardBefore=MainGameVoiceFaceEventBridge.PublicApi.HardRequests;
  plugin.RequestBothWithNormalRetryPending();
  Check(MainGameVoiceFaceEventBridge.PublicApi.HardRequests==hardBefore+1&&MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==requestsBefore,
   "qualified hard cue was delayed by pending normal cue retry");
  plugin.ResetVoiceTest();
  plugin.SetVoiceEnabled(false);
  Check(!MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled && !plugin.Ready,"master OFF left cue owner or climax voice enabled");
  int mutedPiston=MainGameVoiceFaceEventBridge.PublicApi.PistonRequests;
  int mutedHard=MainGameVoiceFaceEventBridge.PublicApi.HardRequests;
  plugin.RequestBothWithNormalRetryPending();plugin.RequestOgazm();plugin.SampleTrigger(1);
  Check(MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==mutedPiston && MainGameVoiceFaceEventBridge.PublicApi.HardRequests==mutedHard && !plugin.Pending,"master OFF dispatched or queued special voices");
  var mutedPriority=Plugin.AcquireSyringeVoicePriority();mutedPriority.Dispose();
  Check(!MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled,"syringe completion bypassed master OFF");
  var enabledPriority=Plugin.AcquireSyringeVoicePriority();plugin.SetVoiceEnabled(true);
  Check(!MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled,"master ON bypassed active syringe priority");
  enabledPriority.Dispose();Check(MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled && plugin.Ready,"master ON did not recover after syringe completion");
  plugin.DispatchVoices();Check(MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==mutedPiston,"master ON replayed discarded request");
  int stopBefore=MainGameVoiceFaceEventBridge.PublicApi.ImmediateStops, oralBefore=plugin.OralStops;
  plugin.SetVoiceEnabled(false);
  Check(MainGameVoiceFaceEventBridge.PublicApi.ImmediateStops==stopBefore+1 && plugin.OralStops==oralBefore+1,"master OFF did not immediately stop cue and oral playback");
  plugin.SetVoiceEnabled(true);plugin.ResetVoiceTest();
  UnityEngine.Random.Next=0;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==25,"heart branch not chosen");
  Check(actor.fileStatus.mouthPtn==24,"tongue selection lost");
  UnityEngine.Random.Next=1;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==25,"eyes rerolled every frame");
  UnityEngine.Time.unscaledTime=10;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==24,"star branch not chosen at existing switch interval");
  plugin.ChangeEyes(actor,25);Check(actor.fileStatus.eyesPtn==24,"native heart override replaced the selected star");
  Check(plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(25),"eyelid B was replaced by the star's list ID");
  plugin.SetEyeSelection(false,25,false,7,12);
  Check(actor.fileStatus.eyesPtn==25&&plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(7),"manual eyelid and eye list ID are not independent");
  UnityEngine.Random.Next=1;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==25&&plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(7),"fixed eye/eyelid rerolled");
  plugin.SetEyeSelection(false,25,false,999,12);
  Check(plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(53),"invalid shape exceeded actor's morph range");
  UnityEngine.Random.Next=0;plugin.SetEyeSelection(true,25,true,24,25);
  Check(actor.fileStatus.mouthPtn>=43&&actor.fileStatus.mouthPtn<=45,"mouth randomization broken");
  plugin.FaceTick(0);
  Check(actor.fileStatus.eyesPtn==0&&actor.fileStatus.mouthPtn==0,"random eyes not restored below threshold");
  UnityEngine.Random.Next=0;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==25,"heart reentry failed");
  MainGameVoiceFaceEventBridge.PublicApi.Accepted=false;plugin.RequestOgazm();
  Check(!plugin.OgazmLatched,"failed voice accepted");
  MainGameVoiceFaceEventBridge.PublicApi.Accepted=true;plugin.RequestOgazm();
  Check(plugin.OgazmLatched,"Ogazm not accepted");
  scene.flags.gaugeFemale=0;plugin.SampleTrigger(.2f);
  Check(!plugin.OgazmLatched,"regression: latch stuck outside readiness");
  scene.flags.gaugeFemale=80;plugin.SampleTrigger(.96f);Check(plugin.Pending,"second cycle failed to arm");
  plugin.SeedRenderedEyePose();plugin.Motion("Orgasm_Start");
  Check(plugin.FaceHeld&&plugin.HeldEyes==25&&plugin.HeldMouth==24,"climax did not preserve selected heart/tongue");
  Check(plugin.HeldEye.Inward==9.96f&&plugin.HeldEye.Upward==24.9f,"last gaze lost");
  Check(!plugin.OgazmLatched&&!plugin.Pending,"climax did not reset voice requests");
  foreach(string state in new[]{"Orgasm_Loop","Orgasm_A","A_Idle","A_Loop"})
  {
   plugin.Motion(state);scene.flags.gaugeFemale=0;
   UnityEngine.Random.Next=1;UnityEngine.Time.unscaledTime+=20;plugin.FaceTick(0);
   Check(plugin.FaceHeld&&actor.fileStatus.eyesPtn==25&&actor.fileStatus.mouthPtn==24,"held face changed in "+state);
   Check(plugin.HeldEye.IrisInward==.0664f,"held iris changed");
  }
  plugin.ChangeEyes(actor,0);Check(actor.fileStatus.eyesPtn==25,"native eye command replaced held heart");
  plugin.ChangeEyes(other,0);Check(other.fileStatus.eyesPtn==0,"eye override affected another actor");
  actor.ChangeMouthPtn(0);actor.ChangeMouthFixed(false);plugin.MouthChanged(actor);
  Check(actor.fileStatus.mouthPtn==24&&actor.fileStatus.mouthFixed,"native lip sync replaced held tongue");
  Check(plugin.Mix(actor.mouthCtrl)&&actor.mouthCtrl.Weights.ContainsKey(24),"direct mouth mix replaced hold");
  Check(plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(24),"held eyelid changed to heart list ID");
  Check(!plugin.Mix(other.eyesCtrl),"mixed another actor's eyes");
  int previous=MainGameVoiceFaceEventBridge.PublicApi.PistonRequests;plugin.RequestPiston();
  Check(MainGameVoiceFaceEventBridge.PublicApi.PistonRequests==previous+1&&plugin.PistonPlayed,"post-climax sound failed");
  Check(plugin.FaceHeld,"sound released face");
  Check(!plugin.HardVoicePlayed,"unrequested hard voice marked consumed");
  plugin.Motion("Orgasm_Start");plugin.Motion("A_Idle");
  Check(!plugin.OgazmLatched&&!plugin.PistonPlayed,"second climax did not rearm sound");
  scene.flags.nowAnimationInfo.nameAnimation="仰向け愛撫";plugin.ChangePose();
  Check(!plugin.FaceHeld&&!actor.fileStatus.mouthFixed&&actor.fileStatus.eyesPtn==0,"ordinary pose failed to release");
  plugin.ResetReady();plugin.FaceTick(0);UnityEngine.Random.Next=1;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==24,"star reentry failed");
  plugin.SeedRenderedEyePose();plugin.Motion("Orgasm_Start");
  Check(plugin.HeldEyes==24,"climax did not capture selected star");
  plugin.ChangeEyes(actor,25);Check(actor.fileStatus.eyesPtn==24,"held star changed to heart");
  plugin.SetFaceFeature(false);Check(!plugin.FaceHeld&&!actor.fileStatus.mouthFixed,"OFF did not release");
  plugin.SetFaceFeature(true);Check(!plugin.FaceHeld,"ON resurrected hold");
  plugin.Motion("A_Idle");plugin.Motion("Orgasm_Start");
  scene.lstFemale[0]=other;plugin.ChangePose();Check(!plugin.FaceHeld,"target change leaked hold");
  scene.lstFemale[0]=actor;plugin.ResetReady();plugin.Motion("Orgasm_Start");
  plugin.Delete();Check(!plugin.FaceHeld,"model deletion leaked hold");
  plugin.RestoreModel();plugin.ResetReady();plugin.Motion("Orgasm_Start");
  Check(plugin.FaceHeld,"gate cleanup setup failed");
  var stalePriority=Plugin.AcquireSyringeVoicePriority();
  var oldPermit=GateApi.Capture(Plugin.Guid);GateApi.EndSession(GateApi.SessionKind.H);
  Check(!oldPermit.IsValid&&!plugin.FaceHeld&&!actor.fileStatus.mouthFixed,"gate revoke cleanup failed");
  plugin.ChangeEyes(actor,0);Check(actor.fileStatus.eyesPtn==0,"revoked hook still changed eyes");
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
  var freshPriority=Plugin.AcquireSyringeVoicePriority();stalePriority.Dispose();
  Check(plugin.VoiceSuppressed&&!MainGameVoiceFaceEventBridge.PublicApi.CuesEnabled,"old voice completion released next-session suppression");freshPriority.Dispose();
  Check(!plugin.FaceHeld,"held face leaked to next H");
  plugin.ResetReady();plugin.SampleTrigger(.96f);Check(plugin.Pending,"fresh H sound failed");
  plugin.Delete();plugin.RestoreModel();plugin.ResetReady();actor.fileStatus.eyebrowPtn=9;UnityEngine.Random.Next=4;plugin.SetFiveSlots();plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==8&&actor.fileStatus.mouthPtn==7&&plugin.Mix(actor.eyesCtrl)&&actor.eyesCtrl.Weights.ContainsKey(4),"fifth eye/eyelid/mouth slots did not reach actual application");
  plugin.Motion("Orgasm_Start");Check(plugin.HeldMouth==7,"configured fifth mouth lost during face capture");
  Check(actor.fileStatus.eyebrowPtn==5&&plugin.Mix(actor.eyebrowCtrl)&&actor.eyebrowCtrl.Weights.ContainsKey(5),"fifth eyebrow slot lost during hold");
  plugin.ChangeEyebrows(actor,0);Check(actor.fileStatus.eyebrowPtn==5,"native eyebrow command replaced hold");
  plugin.ClearBrowSlots();Check(actor.fileStatus.eyebrowPtn==9&&!plugin.Mix(actor.eyebrowCtrl),"unused eyebrow slots did not release original");
  plugin.Delete();plugin.RestoreModel();plugin.ResetReady();
  actor.fileStatus.eyesOpenMax=.77f;actor.eyesCtrl.OpenMax=.77f;actor.eyesCtrl.FixedRate=.42f;
  UnityEngine.Random.Next=0;plugin.SetOpeningSlots();plugin.FaceTick(60);
  Check(actor.fileStatus.eyesOpenMax==.2f&&actor.eyesCtrl.FixedRate==.2f,"eyelid A opening not paired with its shape");
  UnityEngine.Random.Next=1;plugin.RefreshEyes();
  Check(actor.fileStatus.eyesOpenMax==.8f&&actor.eyesCtrl.FixedRate==.8f,"same eyelid ID in slot B lost its own opening");
  plugin.Motion("Orgasm_Start");actor.ChangeEyesOpenMax(.05f);plugin.EyeOpeningChanged(actor);
  Check(actor.fileStatus.eyesOpenMax==.8f&&actor.eyesCtrl.FixedRate==.8f,"climax overwrote selected eyelid opening");
  plugin.Motion("A_Idle");plugin.Mix(actor.eyesCtrl);
  Check(actor.eyesCtrl.FixedRate==.8f,"idle lost selected opening");
  plugin.SetOpeningOverride(false);
  Check(actor.fileStatus.eyesOpenMax==.77f&&actor.eyesCtrl.FixedRate==.42f,"opening override did not restore native values");
  plugin.SetOpeningOverride(true);
  Check(actor.eyesCtrl.FixedRate==.8f,"opening override could not resume");
  GateApi.SetRunning(false);Check(!plugin.Ready&&!plugin.FaceHeld,"global gate stop failed");
  Check(actor.fileStatus.eyesOpenMax==.77f&&actor.eyesCtrl.FixedRate==.42f,"gate stop leaked eyelid opening");
  GateApi.SetRunning(true);GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
  plugin.RestoreModel();plugin.ResetReady();plugin.ConfigureTimedFace();UnityEngine.Random.Next=0;
  plugin.FaceTick(60);actor.eyesCtrl.ChangePtn(24,false);
  UnityEngine.Random.Next=1;UnityEngine.Time.unscaledTime+=11;plugin.FaceTick(60);
  Check(actor.fileStatus.eyesPtn==25&&actor.eyesCtrl.Pattern==28,"timer did not apply eyelid change while eye texture stayed the same");
  Check(actor.fileStatus.eyesOpenMax==.8f,"timer did not apply the opening paired with the new eyelid");
  GateApi.EndSession(GateApi.SessionKind.H);
  PluginSessionGate.Dispose();
  Console.WriteLine("PASS: "+checks+" assertions; both random branches, stable selection, climax/idle, native/direct face overrides, voice restart, lifecycle and real gate.");
 }
}

namespace UnityEngine
{
 public class MonoBehaviour{public bool enabled=true;public bool isActiveAndEnabled=>enabled;}
 public static class Time{public static float unscaledTime;}
 public static class Mathf{public static float Clamp01(float f)=>Math.Clamp(f,0,1);public static int Clamp(int value,int min,int max)=>Math.Clamp(value,min,max);}
 public static class Random{public static int Next;public static int Range(int min,int max)=>min+(Next%(max-min));}
}
public class FBSBase
{
 private Dictionary<int,float> dictFace=new Dictionary<int,float>{{0,1}};
 public Dictionary<int,float> Weights=>dictFace;
 public void ChangeFace(bool blend){}
}
public class FaceController:FBSBase
{
 public float FixedRate=-1,OpenMax=1;public int Pattern;
 public int GetMaxPtn()=>54;
 public void ChangePtn(int ptn,bool blend){Pattern=ptn;}
}
public class FaceStatus{public int mouthPtn,eyesPtn,eyebrowPtn;public float mouthOpenMax=1,eyesOpenMax=1;public bool mouthFixed;public byte tongueState;}
public class ChaControl:UnityEngine.MonoBehaviour
{
 public FaceStatus fileStatus=new FaceStatus();public FaceController mouthCtrl=new FaceController(),eyesCtrl=new FaceController(),eyebrowCtrl=new FaceController();
 public void ChangeMouthFixed(bool fix){fileStatus.mouthFixed=fix;mouthCtrl.FixedRate=fix?fileStatus.mouthOpenMax:-1;}
 public void ChangeEyesOpenMax(float value){fileStatus.eyesOpenMax=value;eyesCtrl.OpenMax=value;}
 public void ChangeMouthOpenMax(float value){fileStatus.mouthOpenMax=value;mouthCtrl.OpenMax=value;}
 public void ChangeMouthPtn(int ptn,bool blend=true){fileStatus.mouthPtn=ptn;mouthCtrl.ChangePtn(ptn,blend);}
 public void ChangeEyesPtn(int ptn,bool blend=true){fileStatus.eyesPtn=ptn;eyesCtrl.ChangePtn(ptn,blend);}
 public void ChangeEyebrowPtn(int ptn,bool blend=true){fileStatus.eyebrowPtn=ptn;eyebrowCtrl.ChangePtn(ptn,blend);}
 public void ChangeTongueState(byte state){fileStatus.tongueState=state;}
}
public class HFlag
{
 public enum EMode{aibu,houshi,sonyu}
 public EMode mode;public float gaugeFemale=80;public string nowAnimStateName="A_Loop";
 public HSceneProc.AnimationListInfo nowAnimationInfo=new HSceneProc.AnimationListInfo();
}
public class HSceneProc:UnityEngine.MonoBehaviour
{
 public class AnimationListInfo{public HFlag.EMode mode;public int id=1;public string nameAnimation="仰向け愛撫【クローン】";}
 public HFlag flags=new HFlag();public List<ChaControl> lstFemale=new List<ChaControl>();public void ChangeAnimator(){}
}
public class HActionBase{public HFlag flags;public bool SetPlay(string state,bool fade=true){flags.nowAnimStateName=state;return true;}}
namespace BepInEx{public class BaseUnityPlugin:UnityEngine.MonoBehaviour{}}
namespace HarmonyLib
{
 public class Harmony{public Harmony(string id){}}
 public class HarmonyMethod{public int priority;public HarmonyMethod(Type type,string name){}}
 public static class Priority{public const int Last=0;}
 public static class AccessTools
 {
  public static System.Reflection.MethodInfo Method(Type type,string name,Type[] parameters=null)=>parameters==null?type.GetMethod(name):type.GetMethod(name,parameters);
  public static System.Reflection.FieldInfo Field(Type type,string name)=>type.GetField(name,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 }
}
namespace MainGameVrDildo
{
 public class Settings
 {
  public bool Enabled=true,VoiceEnabled=true,CloneClimaxFaceHoldEnabled=true,OgazmVoiceEnabled=true,MotionGaugeEnabled=true;
  public bool EyeOpeningOverride=true;public float[] EyelidOpeningSlots={1,1,1,1,1};
  public bool MouthPattern24Enabled=true,MouthPattern43Enabled=true,MouthPattern44Enabled=true,MouthPattern45Enabled=true;
  public int HeartEyesPattern=25,StarEyesPattern=24;public float MouthChangeSeconds=10,FaceStartGaugeFraction=.5f;
  public bool EyePatternRandom=true,EyelidShapeRandom=true;public int EyelidShapePatternA=24,EyelidShapePatternB=25;
  public bool MouthPatternRandom=true;
  public int[] EyePatternSlots={25,24,-1,-1,-1},EyelidShapeSlots={24,25,-1,-1,-1},MouthPatternSlots={24,43,44,45,-1};
  public bool EyebrowPatternRandom=true;public int[] EyebrowPatternSlots={-1,-1,-1,-1,-1};
  public float MotionGaugeFullCm=100,OgazmExitGaugeFraction=.9f,OgazmEnterGaugeFraction=.95f;
  public float PistonVoiceVolume,PistonVoiceCooldownSeconds,PistonAcceptedRetrySeconds,PistonRejectedRetrySeconds;
  public float HardPistonVoiceVolume,HardPistonVoiceCooldownSeconds,HardPistonAcceptedRetrySeconds,HardPistonRejectedRetrySeconds;
  public float OgazmVoiceVolume,OgazmVoiceCooldownSeconds;
 }
 public sealed partial class Plugin:BepInEx.BaseUnityPlugin
 {
  public const string Guid="com.kks.maingame.vrdildo";public static Plugin Instance;
  enum TargetKind{None,Vaginal,Oral}TargetKind _selectedTarget=TargetKind.Vaginal;
  HSceneProc _scene;ChaControl _female;object _model=new object();Settings _settings=new Settings();
  HFlag _sceneFlags=>_scene?.flags;
  float _nextPistonVoiceAttemptAt,_nextHardPistonVoiceAttemptAt;
  bool _ogazmCueRequestedThisEntry,_pistonVoicePending,_pistonPlayedThisEntry,_hardVoicePending,_hardPlayedThisEntry,_ogazmVoicePending;
  static readonly System.Reflection.FieldInfo FemaleListField=typeof(HSceneProc).GetField("lstFemale");
  public class Log{public void LogWarning(string s)=>Console.WriteLine(s);}readonly Log Logger=new Log();
  void CaptureCloneClimaxEyeTransforms(){}void ReleaseCloneClimaxEyeTransforms(){}
  void PatchOwned(HarmonyLib.Harmony h,System.Reflection.MethodBase t,HarmonyLib.HarmonyMethod prefix=null,HarmonyLib.HarmonyMethod postfix=null){if(t==null)throw new ArgumentNullException(nameof(t));}
  void RemoveOwnedHooks(ref HarmonyLib.Harmony h){h=null;}
  void LogMotionEvent(string s,string detail){}
  public int OralStops;void StopOralAudio(){OralStops++;}
  public bool VoiceSuppressed=>SyringeVoiceSuppressed;
  internal void SetVoiceEnabled(bool enabled){_settings.VoiceEnabled=enabled;ApplyVoiceSetting();}
  internal void Initialize(HSceneProc s,ChaControl a){Instance=this;_scene=s;_female=a;PluginSessionGate.Bind(this,Guid,BeginCloneReactionSession,()=>{_syringeVoiceLeases.Clear();EndCloneReactionSession();ReleaseGaugeFace();});}
  internal bool Ready=>CanStartCloneOgazm();internal bool FaceHeld=>CloneClimaxFaceHeld;
  internal bool Pending=>_ogazmVoicePending;internal bool OgazmLatched=>_ogazmCueRequestedThisEntry;
  internal int HeldEyes=>_climaxEyesPattern;internal int HeldMouth=>_climaxMouthPattern;internal ClimaxEyePose HeldEye=>_climaxEyePose;
  internal bool PistonPlayed=>_pistonPlayedThisEntry;
  internal bool HardVoicePlayed=>_hardPlayedThisEntry;
  internal void FaceTick(float cm){_motionGaugeCm=cm;UpdateGaugeFace();}
  internal void ConfigureTimedFace(){_settings.EyePatternSlots=new[]{25,-1,-1,-1,-1};_settings.EyelidShapeSlots=new[]{24,28,-1,-1,-1};_settings.EyelidOpeningSlots=new[]{.2f,.8f,1,1,1};_settings.EyelidShapeRandom=true;_settings.EyeOpeningOverride=true;}
  void TraceVibeEvent(string stage,string detail){}
  internal void RequestOgazm(){_ogazmVoicePending=true;DispatchPendingSpecialVoices();}
  internal void SampleTrigger(float fraction){_motionGaugeCm=fraction*100;UpdateOgazmCueTrigger(true);}
  internal void SeedRenderedEyePose(){_lastCloneEyeFemale=_female;_lastCloneEyePose=new ClimaxEyePose{Weight=.83f,Inward=9.96f,Upward=24.9f,IrisInward=.0664f,IrisUp=.0996f};}
  internal void Motion(string state){var action=new HActionBase{flags=_scene.flags};CloneClimaxMotionStarting(action,state);action.SetPlay(state);}
  internal void ChangeEyes(ChaControl actor,int ptn){CloneClimaxEyesChanging(actor,ref ptn);actor.ChangeEyesPtn(ptn);}
  internal void SetEyeSelection(bool randomEyes,int eyeA,bool randomShape,int a,int b){_settings.EyePatternRandom=randomEyes;_settings.HeartEyesPattern=eyeA;_settings.EyePatternSlots=new[]{eyeA,24,-1,-1,-1};_settings.EyelidShapeRandom=randomShape;_settings.EyelidShapePatternA=a;_settings.EyelidShapePatternB=b;_settings.EyelidShapeSlots=new[]{a,b,-1,-1,-1};RefreshSelectedEyeSettings();}
  internal void SetFiveSlots(){_settings.EyePatternRandom=_settings.EyelidShapeRandom=_settings.MouthPatternRandom=true;_settings.EyePatternSlots=new[]{25,24,0,7,8};_settings.EyelidShapeSlots=new[]{0,1,2,3,4};_settings.MouthPatternSlots=new[]{24,43,44,45,7};_settings.EyebrowPatternSlots=new[]{1,2,3,4,5};}
  internal void ChangeEyebrows(ChaControl actor,int ptn){CloneClimaxEyebrowsChanging(actor,ref ptn);actor.ChangeEyebrowPtn(ptn);}
  internal void ClearBrowSlots(){_settings.EyebrowPatternSlots=new[]{-1,-1,-1,-1,-1};RefreshSelectedEyeSettings();}
  internal void SetOpeningSlots(){_settings.EyelidShapeRandom=true;_settings.EyelidShapeSlots=new[]{7,7,-1,-1,-1};_settings.EyelidOpeningSlots=new[]{.2f,.8f,1,1,1};}
  internal void RefreshEyes()=>RefreshSelectedEyeSettings();
  internal void EyeOpeningChanged(ChaControl actor)=>SelectedEyeOpeningChanged(actor);
  internal void SetOpeningOverride(bool on){_settings.EyeOpeningOverride=on;ApplySelectedEyeOpening(_climaxFemale??_gaugeFaceFemale);}
  internal void MouthChanged(ChaControl actor)=>CloneClimaxMouthChanged(actor);
  internal bool Mix(FBSBase face){face.Weights.Clear();face.Weights[0]=1;bool blend=true;CloneClimaxMouthMixChanging(face,ref blend);return !blend;}
  internal void RequestPiston(){_pistonVoicePending=true;_ogazmVoicePending=false;DispatchPendingSpecialVoices();}
  internal void DispatchVoices()=>DispatchPendingSpecialVoices();
  internal void RequestBothWithNormalRetryPending(){_pistonVoicePending=_hardVoicePending=true;_nextPistonVoiceAttemptAt=UnityEngine.Time.unscaledTime+100;DispatchPendingSpecialVoices();}
  internal void ResetVoiceTest()=>ResetCloneVoiceCycle();
  internal void ChangePose()=>CloneReactionPoseChanged(_scene);
  internal void SetFaceFeature(bool on){_settings.CloneClimaxFaceHoldEnabled=on;ApplyCloneReactionSettings();}
  internal void Delete(){ReleaseCloneClimaxFace();ReleaseGaugeFace();_model=null;}
  internal void RestoreModel(){_model=new object();}
  internal void ResetReady(){_scene.flags.gaugeFemale=80;_scene.flags.nowAnimStateName="A_Loop";_scene.flags.nowAnimationInfo.nameAnimation="仰向け愛撫【クローン】";}
 }
}
namespace MainGameVoiceFaceEventBridge
{
 public static class PublicApi
 {
  public static bool Accepted;public static int PistonRequests,HardRequests;
  public static int ImmediateStops;public static bool CuesEnabled;
  public static void SetImpactCueEnabled(string owner,bool enabled){CuesEnabled=enabled;}
  public static void SetImpactCueEnabled(string owner,bool enabled,bool immediate){CuesEnabled=enabled;if(!enabled&&immediate)ImmediateStops++;}
  public static bool TryRequestOgazmCue(string owner,float volume,float cooldown)=>Accepted;
  public static bool TryRequestContinuousPistonCue(string owner,float volume,float cooldown){PistonRequests++;return Accepted;}
  public static bool TryRequestHardPistonCue(string owner,float volume,float cooldown){HardRequests++;return Accepted;}
 }
}
