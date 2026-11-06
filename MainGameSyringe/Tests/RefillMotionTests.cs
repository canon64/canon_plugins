using System;
using MainGameSyringe;
using UnityEngine;
static class RefillMotionTests
{
 public static void Run(Action<bool,string> check)
 {
  var clip=SyringeRefillMotion.Create(new Vector3(0,0,.0405f),Vector3.one,.02f,new Quaternion(0,0,0,1),new Quaternion(.7f,0,0,.7f),new Vector3(0,0,.23f),new Vector3(0,0,.11f));
  var keys=clip.Curves["Assembly/Pusher:localPosition.z"].keys;
  check(keys[0].value==keys[1].value&&keys[2].value==keys[3].value,"pusher moves before insertion or after suction");
  check(Math.Abs(keys[2].time-keys[1].time-3)<.00001&&Math.Abs(keys[3].time-5.6f)<.00001,"three-second pusher interval incorrectly used as total refill time");
  foreach(var path in new[]{SyringeRefillMotion.BottleLiquid,SyringeRefillMotion.BottleLiquid+"/FreeSurface"})
  {
   var fill=clip.Curves[path+":material._Fill"].keys;
   check(fill[0].value==SyringeRefillMotion.InitialFill&&fill[2].value==0&&fill[3].value==0,"bottle does not drain completely");
   check(fill[1].time==keys[1].time&&fill[2].time==keys[2].time,"bottle draining is not synchronized with pusher");
  }
  var liquid=clip.Curves["Assembly/LiquidPivot:localScale.z"].keys;
  check(liquid[1].value==.02f&&liquid[2].value==1&&liquid[1].time==keys[1].time&&liquid[2].time==keys[2].time,"syringe liquid does not fill with the pusher");
 }
}
