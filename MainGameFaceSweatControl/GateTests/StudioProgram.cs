using System;
using CanonGate;
using CanonPlugins.Local;
internal static class StudioProgram {
 static int checks;
 static void Check(bool ok,string name){++checks;if(!ok)throw new Exception(name);}
 static void Main(){
  bool allow=false;int decisions=0,starts=0,stops=0,state=0;
  GateApi.AllowActivity=(guid,kind)=>{++decisions;return allow;};GateApi.SetRunning(true);
  var owner=new BepInEx.BaseUnityPlugin();
  SessionGate.Bind(owner,"look",()=>{++starts;++state;},()=>{++stops;--state;});
  KKAPI.Studio.StudioAPI.Set(true);allow=true;KKAPI.Studio.StudioAPI.Set(true);
  Check(decisions==1 && starts==0,"denied Studio does not retry");
  KKAPI.Studio.StudioAPI.Set(false);KKAPI.Studio.StudioAPI.Set(true);
  Check(decisions==2 && starts==1 && SessionGate.Allowed,"next Studio entry permitted");
  for(int i=0;i<5000;i++)Check(SessionGate.Allowed,"cached Studio query");
  Check(decisions==2,"no reevaluation in 5000 queries");
  KKAPI.Studio.StudioAPI.Set(false);Check(stops==1 && state==0 && !SessionGate.Allowed,"Studio exit restores");
  var p=new ExceptionPolicy();p.Load("","");var scene=new HSceneProc();
  KKAPI.Studio.StudioAPI.Set(true);GateApi.BeginSession(GateApi.SessionKind.H,false,p,scene);
  Check(!SessionGate.Allowed && state==0,"Studio grant cannot carry into denied H");
  GateApi.EndSession(GateApi.SessionKind.H);GateApi.BeginSession(GateApi.SessionKind.H,true,p,scene);
  Check(SessionGate.Allowed,"same registration receives allowed H");
  GateApi.EndSession(GateApi.SessionKind.H);Check(state==0,"H stop restores");
  SessionGate.Dispose();GateApi.SetRunning(false);Check(state==0,"disposed safely");
  Console.WriteLine("PASS "+checks+" checks: real CameraLook SessionGate, Studio decision once/denial/no carry into H/cleanup");
 }
}
