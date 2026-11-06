using System;
using UnityEngine;
namespace MainGameSyringe
{
 internal static class SyringeRefillMotion
 {
  internal const float Approach=.7f,Insert=.6f,DrawStart=Approach+Insert,DrawSeconds=3f,DrawEnd=DrawStart+DrawSeconds,Withdraw=.6f,Return=.7f,WithdrawEnd=DrawEnd+Withdraw,Total=WithdrawEnd+Return;
  internal const float InitialFill=.72945555f;
  internal const string BottleLiquid="Assembly/RefillRig/Bottle/MedicineBottle_ROOT/PurpleLiquid";
  static void Curve(AnimationClip clip,string path,Type type,string property,params Keyframe[] keys){clip.SetCurve(path,type,property,new AnimationCurve(keys));}
  static void Window(AnimationClip clip,string path,Type type,string property,float from,float to,float start,float end){Curve(clip,path,type,property,new Keyframe(0,from),new Keyframe(start,from),new Keyframe(end,to),new Keyframe(Total,to));}
  internal static AnimationClip Create(Vector3 full,Vector3 rest,float remaining,Quaternion pose,Quaternion upright,Vector3 above,Vector3 inside)
  {
   var clip=new AnimationClip{legacy=true,wrapMode=WrapMode.ClampForever};string[] axes={"x","y","z"};float[] a={full.x,full.y,full.z},b={rest.x,rest.y,rest.z},start={above.x,above.y,above.z},end={inside.x,inside.y,inside.z};
   for(int i=0;i<3;i++)
   {
    Window(clip,"Assembly/Pusher",typeof(Transform),"localPosition."+axes[i],a[i],b[i],DrawStart,DrawEnd);
    Curve(clip,"Assembly/RefillRig",typeof(Transform),"localPosition."+axes[i],new Keyframe(0,start[i]),new Keyframe(Approach,start[i]),new Keyframe(DrawStart,end[i]),new Keyframe(DrawEnd,end[i]),new Keyframe(WithdrawEnd,start[i]),new Keyframe(Total,start[i]));
    Window(clip,"Assembly/RefillRig",typeof(Transform),"localScale."+axes[i],1,0,Total-.25f,Total);
   }
   Window(clip,"Assembly/LiquidPivot",typeof(Transform),"localScale.z",remaining,1,DrawStart,DrawEnd);
   foreach(string path in new[]{BottleLiquid,BottleLiquid+"/FreeSurface"})Window(clip,path,typeof(MeshRenderer),"material._Fill",InitialFill,0,DrawStart,DrawEnd);
   string[] components={"x","y","z","w"};float[] qa={pose.x,pose.y,pose.z,pose.w},qb={upright.x,upright.y,upright.z,upright.w};
   for(int i=0;i<4;i++)Curve(clip,"Assembly",typeof(Transform),"localRotation."+components[i],new Keyframe(0,qa[i]),new Keyframe(Approach,qb[i]),new Keyframe(WithdrawEnd,qb[i]),new Keyframe(Total,qa[i]));
   clip.EnsureQuaternionContinuity();return clip;
  }
 }
}
