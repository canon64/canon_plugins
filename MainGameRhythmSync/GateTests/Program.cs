using System;
using CanonGate;
using Host=MainGameRhythmSync.Plugin;
using Beat=MainGameBeatSyncSpeed.Plugin;
using Speed=MainGameSpeedLimitBreak.Plugin;
using UnityEngine;
internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string name) { if(!ok) throw new Exception(name); ++checks; }
    private static void Main()
    {
        GateApi.SetRunning(true);
        var host=new Host();
        Beat.Host=Speed.Host=host;
        var beat=Beat.Instance=new Beat();
        var speed=Speed.Instance=new Speed();
        host.Register();
        int beatStarts=0,speedStarts=0;
        using var b=GateApi.Register(Beat.Guid,()=>{++beatStarts; host.BeginBeatSession();},host.EndBeatSession);
        using var s=GateApi.Register(Speed.Guid,()=>{++speedStarts; host.BeginSpeedSession();},host.EndSpeedSession);
        foreach(bool vr in new[]{false,true})
        for(int mask=0;mask<8;mask++)
        {
            var policy=new ExceptionPolicy();
            string guids=((mask&1)!=0?Host.Guid+",":"")+((mask&2)!=0?Beat.Guid+",":"")+((mask&4)!=0?Speed.Guid:"");
            policy.Load("",guids);
            MonoBehaviour scene=vr?new VrScene():new HSceneProc();
            var flags=vr?((VrScene)scene).flags:((HSceneProc)scene).flags;
            GateApi.BeginSession(GateApi.SessionKind.H,false,policy,scene);
            Check(host.SessionActive==((mask&1)!=0),"host independent permission");
            Check(beat.SessionActive==((mask&2)!=0),"beat independent permission");
            Check(speed.SessionActive==((mask&4)!=0),"speed independent permission");
            int beforeEpoch=beat.Epoch;
            int beforeLoads=MainGameSpeedLimitBreak.SettingsStore.Loads;
            host.BeginBeatSession(); host.BeginSpeedSession();
            Check(beat.Epoch==beforeEpoch && MainGameSpeedLimitBreak.SettingsStore.Loads==beforeLoads,"public facet begin cannot restart same H or start denied facet");
            int starts=beatStarts+speedStarts,loads=MainGameSpeedLimitBreak.SettingsStore.Loads;
            var token=GateApi.Capture(Beat.Guid);
            for(int i=0;i<10000;i++)
            {
                _=host.SessionActive; _=beat.SessionActive; _=speed.SessionActive; _=token.IsValid;
                GateApi.BeginSession(GateApi.SessionKind.H,true,policy,scene);
            }
            Check(beatStarts+speedStarts==starts && MainGameSpeedLimitBreak.SettingsStore.Loads==loads,"duplicate start and 10000 queries do not restart or reload settings");
            int epoch=beat.Epoch;
            if(beat.SessionActive) { beat.SeedPending(); flags.voice.speedMotion=true; }
            if(speed.SessionActive) { speed.OpenUi(); flags.speed=9; flags.speedCalc=9; flags.speedItem=9; flags.timeNoClick=9; flags.speedUpClac=new Vector2(9,9); }
            GateApi.EndSession(GateApi.SessionKind.H);
            Check(!host.SessionActive&&!beat.SessionActive&&!speed.SessionActive&&!token.IsValid,"end revokes all runtimes");
            Check(MainGameUiInputCapture.UiInputCaptureApi.Held==0,"end releases input");
            if((mask&4)!=0) Check(flags.speed==1.3f&&flags.speedCalc==.4f&&flags.speedItem==.7f&&flags.timeNoClick==2&&flags.speedUpClac.x==.2f&&flags.speedUpClac.y==.6f,"runtime flags restored");
            if((mask&2)!=0) Check(beat.Epoch>epoch&&beat.PendingCleared&&!flags.voice.speedMotion,"worker epoch invalidated and voice restored");
            GateApi.EndSession(GateApi.SessionKind.H);
        }
        var all=new ExceptionPolicy(); all.Load(Host.Guid+","+Beat.Guid+","+Speed.Guid,"");
        int count=beatStarts+speedStarts;
        GateApi.BeginSession(GateApi.SessionKind.Maker,false,all);
        Check(!host.SessionActive&&!beat.SessionActive&&!speed.SessionActive,"maker exceptions cannot launch H runtimes");
        GateApi.EndSession(GateApi.SessionKind.Maker);
        all.Load("","");
        GateApi.BeginSession(GateApi.SessionKind.H,true,all,new VrScene());
        Check(host.SessionActive&&beat.SessionActive&&speed.SessionActive,"AllPoseRoom allows all three GUIDs in VR context");
        GateApi.SetRunning(false);
        Check(!host.SessionActive&&!beat.SessionActive&&!speed.SessionActive,"gate disable stops all actual session handlers");
        GateApi.SetRunning(true);
        host.BeginBeatSession(); host.BeginSpeedSession();
        Check(!beat.SessionActive&&!speed.SessionActive,"reenable cannot restart the same ended H");
        Check(UnityEngine.Object.Searches==0,"no Unity scene searches");
        host.Unregister();
        Console.WriteLine("PASS "+checks+" actual session-handler checks; 16 permission/process combinations, 160000 queries");
    }
}
