using System;
using System.Numerics;
using MainGameSyringe;
static class AdvancedTests
{
 public static void Run(Action<bool,string> check)
 {
  var a=new Vector3(-1,-1,0);var b=new Vector3(1,-1,0);var c=new Vector3(0,1,0);
  check(SkinGeometry.SegmentTriangle(new Vector3(0,0,-1),new Vector3(0,0,1),a,b,c,out var p)==0&&p.Length()<.00001,"swept needle missed skin crossing");
  float miss=SkinGeometry.SegmentTriangle(new Vector3(1.001f,-1,-.1f),new Vector3(1.001f,-1,.1f),a,b,c,out p);
  var tip=new Vector3(0,0,-.0196f);var sideways=Vector3.UnitX;
  check(!SkinGeometry.ContactTriangle(tip,tip,sideways,.025f,.002f,false,a,b,c,out p),"strict axis should diagnose screenshot direction miss");
  check(SkinGeometry.ContactTriangle(tip,tip,sideways,.025f,.002f,true,a,b,c,out p)&&p.Length()<1e-6,"near-skin mode must accept screenshot 1.96cm within 2.5cm");
  check(!SkinGeometry.ContactTriangle(new Vector3(0,0,-.03f),new Vector3(0,0,-.03f),sideways,.025f,.002f,true,a,b,c,out p),"near mode accepted out of reach");
  check(SkinGeometry.ContactTriangle(new Vector3(0,0,-.026f),new Vector3(0,0,-.026f),sideways,.025f,.002f,true,a,b,c,out p),"near mode dropped needle-width tolerance");
  check(SkinGeometry.ContactTriangle(new Vector3(0,0,.2f),new Vector3(0,0,-.2f),sideways,.025f,.002f,true,a,b,c,out p),"near mode lost swept crossing");
  var delta=SkinGeometry.InsertionDelta(tip,Vector3.Zero,sideways,.008f);
  check((tip+delta-new Vector3(.008f,0,0)).Length()<1e-6,"insertion translation changed requested needle angle/depth");
  foreach(float mirrorX in new[]{1f,-1f})foreach(float mirrorY in new[]{1f,-1f})
  {
   MainGameSpiralEyes.PupilOrbitMotion.Mapping(0,mirrorX,mirrorY,mirrorX,mirrorY,out var rx,out var ry,out var rd,out var rp);
   MainGameSpiralEyes.PupilOrbitMotion.Mapping(1,1,1,1,1,out var lx,out var ly,out var ld,out var lp);
   check(rd==-1&&ld==1,"eye directions must remain opposite");
   for(int sample=0;sample<64;sample++)
   {
    double cycles=sample/64.0;
    double rightY=Math.Sin(2*Math.PI*(cycles*rd+rp))*ry/mirrorY;
    double leftY=Math.Sin(2*Math.PI*(cycles*ld+lp))*ly;
    check(Math.Abs(rightY+leftY)<1e-6,"one pupil must be up whenever the other is down, including mirrored eye UVs");
   }
  }
  check(miss>.0009f&&miss<.0011f,"finite needle width / edge distance");
  check(SkinGeometry.SegmentTriangle(new Vector3(0,0,.05f),new Vector3(0,0,.05f),a,b,c,out p)>.049f,"stationary needle tolerance");
  var d=new DoseLedger();d.Add(0,30,600);d.Evaluate(30,5,.1f,out int opacity,out float radius,out double next);
  check(opacity==100&&Math.Abs(radius-.02)<1e-6&&next>30,"first dose peak");
  d.Add(300,30,600);d.Evaluate(300,5,.1f,out opacity,out radius,out next);
  check(opacity==55&&Math.Abs(radius-.011)<1e-6,"new dose reset old opacity/lifetime");
  d.Evaluate(630,5,.1f,out opacity,out radius,out next);check(d.Count==1&&opacity==50&&Math.Abs(radius-.01)<1e-6,"old dose was extended by new dose");
  d.Evaluate(930,5,.1f,out opacity,out radius,out next);check(d.Count==0&&opacity==0&&radius==0&&double.IsInfinity(next),"dose expiration leak");
  for(int i=0;i<10;i++)d.Add(1000,30,600);d.Evaluate(1030,5,.1f,out opacity,out radius,out next);
  check(opacity==100&&Math.Abs(radius-.1)<1e-6,"radius cap");
  d.Evaluate(1330,5,.1f,out opacity,out radius,out next);check(opacity==50&&Math.Abs(radius-.1)<1e-6,"effective dose sum cap");
  d.Evaluate(1480,5,.1f,out opacity,out radius,out next);check(opacity==25&&Math.Abs(radius-.05)<1e-6,"radius did not decay with individual doses");
  var schedule=new DoseLedger();schedule.Add(0,30,600);schedule.Add(12,60,120);double now=0;int changes=0;
  while(true){schedule.Evaluate(now,5,.1f,out opacity,out radius,out next);if(double.IsInfinity(next))break;check(next>now,"non advancing dose event");now=next;if(++changes>400)throw new Exception("non finite dose schedule");}
  check(changes<=400&&now==630,"event union expiry");
 }
}
