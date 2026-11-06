using System;
using System.Collections.Generic;
using CanonGate;
using CanonPlugins.Local;
namespace UnityEngine { public class MonoBehaviour { public bool enabled = true; } }
namespace BepInEx { public class BaseUnityPlugin : UnityEngine.MonoBehaviour {} }
public class ChaControl { }
public class HSceneProc : UnityEngine.MonoBehaviour { public List<ChaControl> lstFemale=new List<ChaControl>(); public List<ChaControl> lstMale=new List<ChaControl>(); }
public class VrScene : UnityEngine.MonoBehaviour { public List<ChaControl> lstFemale=new List<ChaControl>(); }
internal static class Program {
 static int n; static void Check(bool value,string name) { if(!value)throw new Exception(name);n++; }
 static void Main() {
  const string guid="com.kks.maingirl.hiphijack";
  var owner=new BepInEx.BaseUnityPlugin();int starts=0,stops=0;
  SessionGate.Bind(owner,guid,()=>starts++,()=>{Check(!SessionGate.Allowed,"revoked before cleanup");stops++;});
  Check(!SessionGate.Allowed && !owner.enabled,"inactive before gate startup");
  GateApi.SetRunning(true); var policy=new ExceptionPolicy();policy.Load("","");
  var native=new HSceneProc();var girl=new ChaControl();var male=new ChaControl();native.lstFemale.Add(girl);native.lstMale.Add(male);
  GateApi.BeginSession(GateApi.SessionKind.H,false,policy,native);
  Check(starts==0 && !SessionGate.Allowed,"denied room does not start");GateApi.EndSession(GateApi.SessionKind.H);
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,native);
  var old=SessionGate.Capture();ulong generation=SessionGate.Generation;
  Check(starts==1 && SessionGate.Allowed && owner.enabled,"allowed native H starts once");
  Check(SessionGate.Scene==native && SessionGate.HScene==native && SessionGate.Actors.Length==2 && SessionGate.Actors[0]==girl && SessionGate.Actors[1]==male,"native actors cached in list order");
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,native);Check(starts==1,"duplicate start ignored");
  try {SessionGate.Bind(owner,guid,()=>{},()=>{});throw new Exception("duplicate accepted");}catch(InvalidOperationException){n++;}
  SessionGate.Stop();SessionGate.Stop();Check(stops==1 && !SessionGate.Allowed && SessionGate.Actors.Length==0 && SessionGate.Scene==null,"local stop clears references once");
  Check(SessionGate.Generation!=generation,"local stop invalidates delayed generation");
  owner.enabled=true;Check(!SessionGate.Allowed,"manual reenable cannot revive this H");GateApi.EndSession(GateApi.SessionKind.H);Check(stops==1 && !old.IsValid,"central end revokes permit without duplicate cleanup");
  var vr=new VrScene();vr.lstFemale.Add(girl);GateApi.BeginSession(GateApi.SessionKind.H,true,policy,vr);
  Check(starts==2 && SessionGate.Allowed && SessionGate.Scene==vr && SessionGate.HScene==null && SessionGate.Actors[0]==girl,"official VR uses scene and H permit");
  Check(!old.IsValid,"previous permit cannot cross next H");
  var vrPermit=SessionGate.Capture();GateApi.SetRunning(false);
  Check(stops==2 && !SessionGate.Allowed && !vrPermit.IsValid,"gate disable stops and revokes VR");
  GateApi.SetRunning(true);Check(starts==2 && !SessionGate.Allowed,"gate reenable waits for real next H");
  GateApi.BeginSession(GateApi.SessionKind.Maker,true,policy,new UnityEngine.MonoBehaviour());Check(starts==2,"Maker never starts H client");GateApi.EndSession(GateApi.SessionKind.Maker);
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,native);SessionGate.Dispose();Check(stops==3 && !SessionGate.Allowed && SessionGate.Scene==null,"dispose cleans active session");GateApi.EndSession(GateApi.SessionKind.H);
  SessionGate.Bind(owner,guid,()=>throw new Exception("partial start"),()=>stops++);
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,native);Check(stops==4 && !SessionGate.Allowed && !owner.enabled,"partial start failure cleans once");GateApi.EndSession(GateApi.SessionKind.H);Check(stops==4,"failed start cleanup not repeated");SessionGate.Dispose();
  int cleaned=0;try {SessionGate.Cleanup(()=>throw new Exception(),()=>cleaned++);}catch(Exception){}
  Check(cleaned==1,"one cleanup failure does not skip later cleanup");
  GateApi.SetRunning(false);Console.WriteLine("PASS "+n+" HipHijack session checks (real GateApi; engine boundary stubbed)");
 }
}
