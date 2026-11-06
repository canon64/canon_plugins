using System;
using CanonGate;
using MainGameVrScreenEffectProbe;
namespace UnityEngine { public class MonoBehaviour { } }
public class HSceneProc:UnityEngine.MonoBehaviour { }
public class VrScene:UnityEngine.MonoBehaviour { }
internal static class Program
{
    private static void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); }
    private static void Main()
    {
        const string guid="com.kks.maingame.vrscreeneffectprobe";
        int starts=0,stops=0;
        SessionGate.Open(guid,()=>++starts,()=>++stops,()=>true);
        string result=VrStateDiagnostics.Describe(()=>throw new InvalidOperationException("VR Manager has not been created yet!"));
        Check(result.Contains("unavailable")&&GateApi.IsRegistered(guid),"uninitialized VR diagnostics do not lose gate registration");
        Check(VrStateDiagnostics.Describe(()=>"ready")=="ready","ready diagnostics preserve output");
        GateApi.SetRunning(true); var policy=new ExceptionPolicy(); policy.Load("","");
        GateApi.BeginSession(GateApi.SessionKind.H,false,policy,new HSceneProc());
        Check(!SessionGate.Allowed&&starts==0,"ordinary H cannot start render effects");
        GateApi.EndSession(GateApi.SessionKind.H);
        foreach(var scene in new UnityEngine.MonoBehaviour[]{new HSceneProc(),new VrScene()})
        {
            GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
            int count=starts;
            Check(SessionGate.Allowed&&SessionGate.Scene==scene,"normal and independent VR scene reference permitted");
            for(int i=0;i<100000;i++) { _=SessionGate.Allowed; GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene); }
            Check(starts==count,"100000 cached queries and duplicate notifications never restart");
            GateApi.EndSession(GateApi.SessionKind.H); GateApi.EndSession(GateApi.SessionKind.H);
            Check(stops==starts&&!SessionGate.Allowed&&SessionGate.Scene==null,"end stops once and clears scene");
        }
        GateApi.BeginSession(GateApi.SessionKind.Maker,true,policy);
        Check(!SessionGate.Allowed,"Maker cannot launch H render effects");
        GateApi.EndSession(GateApi.SessionKind.Maker);
        SessionGate.Close(); Check(!GateApi.IsRegistered(guid),"destroy unregisters plugin");
    }
}
