using System;
using CanonGate;
using CanonPlugins.Local;
internal static class Program {
 static int checks;
 static void Check(bool value,string name){++checks;if(!value)throw new Exception(name);}
 static void Main(){
  var owner=new BepInEx.BaseUnityPlugin(); int starts=0,stops=0,state=0;
  SessionGate.Bind(owner,"client",()=>{++starts;++state;Check(SessionGate.Allowed,"allowed during start");},()=>{++stops;--state;Check(!SessionGate.Allowed,"revoked before cleanup");});
  Check(!owner.enabled && !SessionGate.Allowed,"inactive before H");
  GateApi.SetRunning(true); var policy=new ExceptionPolicy();
  var scene=new HSceneProc();scene.lstFemale.Add(new ChaControl());
  for(int mask=0;mask<8;mask++){
   policy.Load("",""+((mask&1)!=0?"client,":"")+((mask&2)!=0?"peer1,":"")+((mask&4)!=0?"peer2,":""));
   int p1=0,p2=0;
   using(var r1=GateApi.Register("peer1",()=>++p1,()=>--p1))
   using(var r2=GateApi.Register("peer2",()=>++p2,()=>--p2)){
    int beforeStart=starts,beforeStop=stops;
    GateApi.BeginSession(GateApi.SessionKind.H,false,policy,scene);
    GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
    bool expected=(mask&1)!=0;
    Check(SessionGate.Allowed==expected,"independent GUID decision");
    Check(p1==((mask&2)!=0?1:0) && p2==((mask&4)!=0?1:0),"peer decisions");
    var permit=SessionGate.Capture();
    for(int i=0;i<5000;i++){
     bool previous=false; Check(SessionGate.Tick(ref previous,()=>throw new Exception("poll cleanup"))==expected,"cached tick");
     Check(SessionGate.Allowed==expected,"cached query");
     Check(permit.IsValid==expected,"cached permit");
    }
    if(expected)Check(ReferenceEquals(SessionGate.HScene,scene) && SessionGate.Actors.Length==1,"entry refs");
    GateApi.EndSession(GateApi.SessionKind.H);GateApi.EndSession(GateApi.SessionKind.H);
    Check(starts-beforeStart==(expected?1:0) && stops-beforeStop==(expected?1:0),"start stop once");
    Check(!permit.IsValid && !SessionGate.Allowed && state==0,"old session invalid and restored");
    Check(SessionGate.Scene==null && SessionGate.Actors.Length==0,"refs released");
   }
  }
  policy.Load("client",""); GateApi.BeginSession(GateApi.SessionKind.Maker,false,policy);
  Check(!SessionGate.Allowed,"H runtime denied in Maker");GateApi.EndSession(GateApi.SessionKind.Maker);
  policy.Load("","");GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
  var old=SessionGate.Capture();GateApi.SetRunning(false);Check(!old.IsValid && state==0,"gate disable cleanup");
  SessionGate.Dispose();SessionGate.Dispose();Check(state==0,"dispose idempotent");
  GateApi.SetRunning(true); int partial=0,partialStops=0;
  SessionGate.Bind(owner,"failed",()=>{++partial;throw new Exception("expected startup failure");},()=>{--partial;++partialStops;});
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
  Check(partial==0 && partialStops==1 && !SessionGate.Allowed,"partial startup rolled back");
  GateApi.EndSession(GateApi.SessionKind.H);SessionGate.Dispose();Check(partialStops==1,"failed start stops once");
  Console.WriteLine("PASS "+checks+" checks: real SessionGate/GateApi, 8 GUID combinations, 5000 query loops each, duplicate/stop/Maker/disable/references");
 }
}
