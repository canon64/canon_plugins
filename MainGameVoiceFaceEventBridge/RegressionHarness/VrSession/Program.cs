using System;
using System.Collections.Generic;
using MainGameVoiceFaceEventBridge;
using CanonGate;
namespace UnityEngine { public class MonoBehaviour {} }
public class ChaControl { public byte sex=1; }
public class HFlag { public enum EMode {aibu,sonyu} public EMode mode; }
public class HVoiceCtrl { }
public class VRHFlag:HFlag { }
public class VRHVoiceCtrl:HVoiceCtrl { }
public class HSceneProc:UnityEngine.MonoBehaviour { public HFlag flags=new HFlag(); public HVoiceCtrl voice=new HVoiceCtrl(); public List<ChaControl> lstFemale=new List<ChaControl>{new ChaControl()}; }
public class VRHScene:UnityEngine.MonoBehaviour { public VRHFlag flags=new VRHFlag(); public VRHVoiceCtrl voice=new VRHVoiceCtrl(); public List<ChaControl> lstFemale=new List<ChaControl>{new ChaControl()}; }
namespace HarmonyLib
{
 public static class AccessTools
 {
  public static int Reads;
  public static System.Reflection.FieldInfo Field(Type type,string name){Reads++;return type.GetField(name);}
 }
}
namespace CanonGate
{
 public static class GateApi
 {
  public enum SessionKind {None,H,Maker}
  public enum ActivityKind {AiShoujoMode}
  public sealed class Context {public SessionKind Kind=SessionKind.H;public UnityEngine.MonoBehaviour Scene;}
  public sealed class AiContext {public object Actor;}
  public static Context Current;public static AiContext AiShoujoMode;public static bool Granted;
  public struct Permit {public bool IsValid=>Granted;}
  public struct ActivityPermit {public bool IsValid=>Granted;}
  public sealed class ActivityRegistration:IDisposable
  {
   public Action Start,Stop;public ActivityPermit Capture()=>new ActivityPermit();
   public void Dispose(){Stop();}
  }
  public static ActivityRegistration Registration;
  public static Permit Capture(string guid)=>new Permit();
  public static ActivityRegistration RegisterActivity(string guid,ActivityKind kind,Action start,Action stop)
   =>Registration=new ActivityRegistration{Start=start,Stop=stop};
 }
}
static class Program
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main()
 {
  int starts=0,stops=0;
  SessionGate.Open("voice-test",()=>starts++,()=>{stops++;Check(!SessionGate.Allowed,"stop callback retained permission");},()=>true);
  foreach(var scene in new UnityEngine.MonoBehaviour[]{new HSceneProc(),new VRHScene()})
  {
   GateApi.Current=new GateApi.Context{Scene=scene};GateApi.Granted=true;GateApi.Registration.Start();
   Check(SessionGate.Allowed&&ReferenceEquals(SessionGate.Scene,scene),"accepted scene rejected");
   Check((SessionGate.HScene!=null)==(scene is HSceneProc),"VR scene was treated as desktop HSceneProc");
   Check(SessionGate.Females.Count==1,"session participant was not captured");
   Check(SessionGate.Flags!=null&&SessionGate.Voice!=null,"native flags and voice must be captured for both scene types");
   Check(SessionGate.Flags.mode==HFlag.EMode.aibu,"VR aibu mode must remain available without HSceneProc");
   SessionGate.Flags.mode=HFlag.EMode.sonyu;
   Check(SessionGate.Flags.mode==HFlag.EMode.sonyu,"captured flags must track live mode changes without polling or recapture");
   int reads=HarmonyLib.AccessTools.Reads;int epoch=SessionGate.Epoch;
   for(int i=0;i<1000;i++)Check(SessionGate.Allowed,"cached permission changed");
   Check(HarmonyLib.AccessTools.Reads==reads,"permission reads repeated participant reflection");
   GateApi.Granted=false;GateApi.Registration.Stop();
   Check(!SessionGate.Allowed&&SessionGate.Scene==null&&SessionGate.Females.Count==0&&SessionGate.Epoch!=epoch,"stop retained scene/participants");
   Check(SessionGate.Flags==null&&SessionGate.Voice==null,"stop retained native flags or voice");
  }
  Check(starts==2&&stops==2,"desktop and VR lifecycles differ");
  GateApi.Granted=false;GateApi.Current=null;GateApi.Registration.Start();
  Check(!SessionGate.Allowed&&starts==2,"empty session started");
  SessionGate.Close();
  Console.WriteLine("DESKTOP_VR_VOICE_SESSION_OK checks="+checks+" (real SessionGate; host/gate stubs)");
 }
}
