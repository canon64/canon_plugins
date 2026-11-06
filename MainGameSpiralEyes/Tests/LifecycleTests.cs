using System;
using CanonGate;
using MainGameSpiralEyes;
namespace UnityEngine {public class MonoBehaviour {}}
public class HSceneProc : UnityEngine.MonoBehaviour {}
static class LifecycleTests
{
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static void Run()
 {
  GateApi.SetRunning(true);int starts=0,stops=0,errors=0;bool enabled=true;
  GateApi.ReportError=e=>errors++;
  GateApi.ReloadSettings=()=>{};GateApi.AllowActivity=(g,k)=>false;
  var policy=new ExceptionPolicy();var scene=new HSceneProc();
  using(var client=new HSessionClient("spiral.test",()=>enabled,s=>{Check(s==scene,"wrong scene");starts++;},()=>stops++))
  {
   GateApi.BeginSession(GateApi.SessionKind.H,false,policy,scene);
   Check(starts==0&&!client.Allowed,"normal H not denied");GateApi.EndSession(GateApi.SessionKind.H);
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
   var old=GateApi.Capture("spiral.test");
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
   for(int i=0;i<10000;i++)Check(client.Allowed,"cached permission lost");
   Check(starts==1,"duplicate start");GateApi.EndSession(GateApi.SessionKind.H);client.Stop();
   Check(stops==1&&!old.IsValid&&!client.Allowed,"stop/revoke failed");
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);client.Stop();client.Stop();
   Check(starts==2&&stops==2&&!client.Allowed,"local disable failed");
   GateApi.EndSession(GateApi.SessionKind.H);Check(stops==2,"double stop after disable");
   enabled=false;GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
   Check(starts==2,"disabled client started");enabled=true;Check(!client.Allowed,"re-enabled in middle of H");
   GateApi.EndSession(GateApi.SessionKind.H);
   policy.Load("spiral.test","");GateApi.BeginSession(GateApi.SessionKind.Maker,false,policy);
   Check(starts==2,"H-only client started in Maker");GateApi.EndSession(GateApi.SessionKind.Maker);
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);GateApi.SetRunning(false);
   Check(starts==3&&stops==3&&!client.Allowed,"gate stop failed");
  }
  GateApi.SetRunning(true);int partialCleanup=0;
  using(var failing=new HSessionClient("spiral.failure",()=>true,s=>{throw new Exception("partial start");},()=>partialCleanup++))
  {
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
   Check(partialCleanup==1&&!failing.Allowed&&errors==1,"partial failure cleanup");
   GateApi.EndSession(GateApi.SessionKind.H);Check(partialCleanup==1,"partial failure double cleanup");
  }
  GateApi.SetRunning(false);
  Console.WriteLine("PASS: actual HSessionClient + GateApi: refusal, duplicate start/stop, disable, Maker exclusion, gate stop, stale permit, partial failure, 10000 cached reads");
 }
}
