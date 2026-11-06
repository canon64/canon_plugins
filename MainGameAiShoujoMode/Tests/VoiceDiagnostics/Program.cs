using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MainGameAiShoujoMode;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok,string message) { _checks++; if(!ok)throw new Exception(message); }
    private static void Main()
    {
        bool enabled=false; var logs=new List<string>(); var mouth=new Transform{name="mouth",position=new Vector3(TestValues.N("sourceX"),TestValues.N("one"),TestValues.N("sourceZ"))};
        var camera=new Camera{transform=new Transform{name="HCamera"}};
        Camera.main=camera;
        var session=new AiModeHSession{ActiveCamera=camera,RoomFlags=new RoomFlags{transVoiceMouth=new[]{mouth}}};
        using(var off=new HotelVoiceDiagnostics(()=>enabled,()=>session,logs.Add))
        {
            off.Snapshot("off");
            Check(Harmony.Hooks==default(int)&&UnityEngine.Object.Searches==default(int)&&!logs.Any(),"OFF allocates no hook and performs no sampling/logging");
        }
        enabled=true;
        var source=new AudioSource{transform=new Transform{name="voice",position=mouth.position},clip=new AudioClip{name="h_test",channels=TestValues.I("channels")},panStereo=TestValues.N("pan"),spatialBlend=TestValues.N("one"),isPlaying=true,Attached=new Voice_Component{voiceTrans=mouth}};
        var listener=new AudioListener{transform=new Transform{name="listener"}};
        Manager.Sound.Listener=camera.transform;
        UnityEngine.Object.Objects.Add(source);UnityEngine.Object.Objects.Add(listener);
        using(var on=new HotelVoiceDiagnostics(()=>enabled,()=>session,logs.Add))
        {
            Check(Harmony.Hooks==TestValues.I("one"),"ON installs native voice event hook once");
            Harmony.Play(source,new Manager.Voice.Loader{voiceTrans=new Transform()});
            Check(!logs.Any()&&UnityEngine.Object.Searches==default(int),"unrelated voices cannot sample hotel diagnostics");
            Harmony.Play(source,new Manager.Voice.Loader{voiceTrans=mouth});
            Check(logs.Any(x=>x.Contains("channels=2")&&x.Contains("panStereo=-0.8")&&x.Contains("spatialBlend=1")),"native event records stereo clip and source routing");
            Check(logs.Any(x=>x.Contains("right_m=-3")&&x.Contains("activeListeners=1")),"records signed source side and active listener count");
            Check(logs.Any(x=>x.Contains("equalsNativeCamera=False")&&x.Contains("equalsSoundListener=False")),"separate actual listener remains identifiable");
            camera.transform.position=new Vector3(TestValues.N("cameraX"),default(float),default(float));
            on.Snapshot("camera-moved");
            Check(logs.Any(x=>x.Contains("trigger=camera-moved")&&x.Contains("sourceFromNativeCamera={right_m=-8")),"manual snapshot follows changed display camera");
            Check(logs.Any(x=>x.Contains("trigger=camera-moved")&&x.Contains("listener={path=listener pos=(0,0,0)")&&x.Contains("sourceRelative={right_m=-3")),"manual snapshot exposes unmoved listener alongside moved camera");
            Check(source.panStereo==TestValues.N("pan")&&source.spatialBlend==TestValues.N("one")&&source.transform.position.x==TestValues.N("sourceX"),"diagnostics never alter source audio or position");
            int count=logs.Count,searches=UnityEngine.Object.Searches;
            enabled=false;Harmony.Play(source,new Manager.Voice.Loader{voiceTrans=mouth});on.Snapshot("disabled");
            Check(logs.Count==count&&UnityEngine.Object.Searches==searches,"OFF immediately blocks outstanding callbacks and manual collection");
        }
        Check(Harmony.Hooks==default(int)&&Harmony.Callback==null,"OFF disposal releases diagnostic event hook");
        enabled=true;
        using(var again=new HotelVoiceDiagnostics(()=>enabled,()=>session,logs.Add))
        {
            int count=logs.Count,searches=UnityEngine.Object.Searches;
            session.Busy=false;Harmony.Play(source,new Manager.Voice.Loader{voiceTrans=mouth});again.Snapshot("ended");
            Check(logs.Count==count&&UnityEngine.Object.Searches==searches,"session end rejects late native/manual callbacks");
        }
        Check(Harmony.Hooks==default(int),"next diagnostic lifetime leaves no hook");
        Console.WriteLine("PASS HotelVoiceDiagnostics: "+_checks+" checks; OFF/ON/OFF, ownership, camera/listener divergence, stereo source, cleanup");
    }
}
