using System;
using System.Linq;
using MainGameAiShoujoH.Progression;
using MainGameAiShoujoH.Integration;
namespace UnityEngine
{
    public struct Vector2 { public float x,y; public static Vector2 zero => default(Vector2); }
    public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b); public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(v,b)); public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp(t,0,1); public static float InverseLerp(float a,float b,float v)=>(v-a)/(b-a); }
    public static class Random { public static float value=>0; }
}
public sealed class Curve { public int Calls; public float Evaluate(float v) { ++Calls; return 1+v; } }
public sealed class HFlag
{
    public float gaugeFemale,gaugeMale,rateClickGauge=2,feelMaleCheat=1,rateDecreaseGauge=1,timeDecreaseGauge=.3f,timeDecreaseGaugeCalc,speedCalc,speed,speedMaxBody=1,speedVoiceChangeSpeedRate=.5f,timeNoClick,timeSpeedUpStart=.5f,timeSpeedUpStartCalc,rateWheelSpeedUp=.05f;
    public bool lockGugeMale,lockGugeFemale,voiceWait;
    public UnityEngine.Vector2 speedUpClac;
    public readonly Curve speedHoushiCurve=new Curve();
    public readonly Voice voice=new Voice(); public readonly Wait timeWaitHoushi=new Wait(); public readonly Info nowAnimationInfo=new Info();
    public int Outside,Inside,Hand,Splash,Drink,Vomit;
    public sealed class Voice { public bool speedMotion; }
    public sealed class Info { public bool isSplash=true; }
    public sealed class Wait { public float timeIdle=10,timeIdleCalc; public void SetIdleTime(){timeIdleCalc=0;} }
    public void MaleGaugeUp(float d){if(!lockGugeMale)gaugeMale=UnityEngine.Mathf.Clamp(gaugeMale+d*feelMaleCheat,0,100);}
    public void FemaleGaugeUp(float d,bool force,bool idle){if(!lockGugeFemale)gaugeFemale=UnityEngine.Mathf.Clamp(gaugeFemale+d,0,100);}
    public void SpeedUpClick(float d,float max){speedUpClac.x=speedCalc;speedUpClac.y=UnityEngine.Mathf.Clamp(speedCalc+d,0,max);timeNoClick=1;timeSpeedUpStartCalc=0;}
    public void AddHoushiInside(){++Inside;} public void AddHoushiOutside(){++Outside;} public void AddHandFinsh(){++Hand;} public void AddSplash(){++Splash;} public void AddHoushiDrink(){++Drink;} public void AddHoushiVomit(){++Vomit;}
}
namespace MainGameAiShoujoH.Integration
{
    internal sealed class NativeKksProtocol { internal float gaugeMax=100,orgasmBoundary=70,clipEnd=1,outsideLoopEnd=2; }
    internal sealed class NativeKksSessionRuntime { internal readonly HFlag Flags=new HFlag();internal readonly NativeKksProtocol Protocol=new NativeKksProtocol(); }
}
static class Program
{
    static int count;
    static void Check(bool value,string message){if(!value)throw new Exception(message);++count;}
    static void Main()
    {
        var runtime=new NativeKksSessionRuntime();var driver=new NativeKksHoushiDriver(runtime);var sample=new DriveSample{Wheel=1};
        var first=driver.Advance(sample);Check(driver.State=="Idle"&&driver.WaitingForStartVoice&&first.Single().Name=="kks-wait-voice","native Idle waits for its exact request completion");
        sample.Wheel=0;sample.NormalizedTime=5;driver.Advance(sample);Check(driver.State=="Idle","clip events cannot substitute for voice completion");
        driver.VoiceFinished();driver.Advance(sample);Check(driver.State=="WLoop"&&runtime.Flags.speedHoushiCurve.Calls>0,"native loop starts using its curve");
        sample.NormalizedTime=0;sample.Delta=driver.SecondsToBoundary(sample);driver.Advance(sample);Check(driver.State=="OLoop"&&runtime.Flags.gaugeMale==70,"known native 70 deadline changes state without polling");
        sample.Delta=0;sample.Finish=Finish.Outside;driver.Advance(sample);Check(driver.WaitingForStartVoice&&runtime.Flags.Outside==0,"outside result waits for exact native finish voice");
        sample.Finish=Finish.None;driver.VoiceFinished();driver.Advance(sample);Check(driver.State=="M_OUT_Start"&&runtime.Flags.Outside==1&&runtime.Flags.Hand==1,"native finish counters apply once at transition");
        driver.VoiceFinished();driver.Advance(sample);Check(runtime.Flags.Outside==1,"duplicate completion does not duplicate native count");
        sample.NormalizedTime=1;driver.Advance(sample);Check(driver.State=="M_OUT_Loop","native first clip boundary");sample.NormalizedTime=2;driver.Advance(sample);Check(driver.State=="OUT_A"&&runtime.Flags.gaugeMale==0,"native two-cycle outside after state");
        driver.Stop();sample.Wheel=1;Check(driver.Advance(sample).Count==0&&driver.State=="OUT_A","late input after stop is inert");
        Console.WriteLine("KKS_HOUSHi_EVENT_BOUNDARY_PASS "+count+"; native Unity helpers/continuous parameter path are not live claims");
    }
}
