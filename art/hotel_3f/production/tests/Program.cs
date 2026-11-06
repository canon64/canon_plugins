using CanonGate;
using DG.Tweening;
using MainGameAiShoujoMode;
using UnityEngine;

static class Program
{
    static int checks;
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static (Transform map,Renderer led,Animation[] curtains,ParticleSystem[] mist) Scene()
    {
        var map=new Transform("Map");var led=new Renderer{name="Line",sharedMaterials=new[]{new Material("RedLED")}};map.components.Add(led);
        var curtains=new[]{new Animation{name="Left"},new Animation{name="Right"}};map.components.AddRange(curtains);
        var mist=Enumerable.Range(0,4).Select(i=>new ParticleSystem{name="3F_Mist_"+i}).ToArray();map.components.AddRange(mist);
        var active=new Transform("3F_ReflectionProbes");map.children.Add(active);
        for(int i=0;i<3;i++)active.components.Add(new ReflectionProbe{name="Probe_"+i});
        var states=new Transform("3F_ReflectionStates");map.children.Add(states);
        foreach(var prefix in new[]{"closed_","open_"})for(int preset=0;preset<3;preset++)
        {var state=new Transform(prefix+preset);states.children.Add(state);for(int i=0;i<3;i++)state.components.Add(new ReflectionProbe{name="Probe_"+i,customBakedTexture=new Texture()});}
        return (map,led,curtains,mist);
    }
    static void Main(string[] args)
    {
        string config=Path.GetFullPath(args[0]);var scene=Scene();var original=scene.led.sharedMaterials[0];
        GateApi.allow=false;
        using(var denied=new HotelStageRuntime(scene.map,config))
        {
            denied.SetActive(true);denied.SetCurtain(true);denied.PlayMist();
            Require(!denied.Allowed&&ReferenceEquals(original,scene.led.sharedMaterials[0]),"gate denial cloned materials");
            Require(scene.mist.All(p=>p.plays==0)&&DOVirtual.pending.Count==0,"denied entry started effects");
        }
        Require(GateApi.registrations.Count==0,"denied registration leaked");
        GateApi.allow=true;scene=Scene();original=scene.led.sharedMaterials[0];
        var runtime=new HotelStageRuntime(scene.map,config);runtime.SetActive(true);runtime.SetActive(true);
        Require(runtime.Allowed&&!runtime.CurtainOpen,"entry was not closed by default");
        var clone=scene.led.sharedMaterials[0];Require(!ReferenceEquals(clone,original),"shared asset was used directly");
        Require(original.emission.r==0,"original material changed");
        runtime.SetCurtain(true);var opening=DOVirtual.pending.Last();
        Require(opening.delay==4&&scene.curtains.All(a=>a.enabled&&a.state.speed==1),"open did not start finite native playback");
        foreach(var a in scene.curtains)a.state.time=1.5f;
        runtime.SetCurtain(false);var closing=DOVirtual.pending.Last();
        Require(opening.killed&&closing.delay==1.5f&&scene.curtains.All(a=>a.state.speed==-1),"mid-animation reversal jumped or used full duration");
        closing.FireEvenIfCancelled();Require(scene.curtains.All(a=>!a.enabled&&a.sampled==0),"completed curtain playback kept running");
        runtime.SetPreset(1);Require(clone.emission.b>clone.emission.r,"violet preset did not affect instance material");
        runtime.PlayMist();var mist=DOVirtual.pending.Last();Require(scene.mist.All(p=>p.emitting)&&mist.delay==6,"mist burst did not start");
        runtime.SetActive(false);Require(clone.destroyed&&ReferenceEquals(scene.led.sharedMaterials[0],original),"exit did not restore/destroy materials");
        Require(scene.mist.All(p=>!p.emitting&&p.clears>0)&&!runtime.Allowed,"exit left particles or permit active");
        int scheduled=DOVirtual.pending.Count;opening.FireEvenIfCancelled();closing.FireEvenIfCancelled();mist.FireEvenIfCancelled();
        Require(!runtime.Allowed&&DOVirtual.pending.Count==scheduled,"stale callbacks resurrected a stopped floor");
        runtime.SetActive(true);Require(runtime.Allowed&&!runtime.CurtainOpen,"reentry did not reset default state");
        runtime.SetCurtain(true);DOVirtual.pending.Last().FireEvenIfCancelled();Require(scene.curtains.All(a=>a.sampled==4&&!a.enabled),"open endpoint was not sampled and stopped");
        runtime.Dispose();runtime.Dispose();Require(GateApi.registrations.Count==0&&scene.mist.All(p=>!p.emitting),"double disposal leaked resources");
        var broken=Scene();broken.map.Find("3F_ReflectionStates").children.RemoveAt(0);bool rejected=false;
        try{new HotelStageRuntime(broken.map,config);}catch(InvalidDataException){rejected=true;}
        Require(rejected&&GateApi.registrations.Count==0,"missing reflection state did not fail before registration");
        Console.WriteLine("HOTEL_STAGE_LIFECYCLE_PASS checks="+checks+" engineBoundaryStubs=true");
    }
}
