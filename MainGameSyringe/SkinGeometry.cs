using System;
using System.Numerics;
namespace MainGameSyringe
{
 public static class SkinGeometry
 {
  public static float DistanceToSegment(Vector3 point,Vector3 first,Vector3 last)
  {
   var delta=last-first;float length=delta.LengthSquared();
   float t=length<1e-10f?0:Math.Max(0,Math.Min(1,Vector3.Dot(point-first,delta)/length));
   return (point-first-delta*t).Length();
  }
  public static bool RayTriangle(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,float maximum,out float distance)
  {
   distance=0;var edge1=b-a;var edge2=c-a;var p=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,p);
   if(Math.Abs(determinant)<1e-8f)return false;
   float inverse=1/determinant;var t=origin-a;float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;
   var q=Vector3.Cross(t,edge1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)return false;
   distance=Vector3.Dot(edge2,q)*inverse;return distance>=0&&distance<=maximum;
  }
  public static bool ShouldRelease(float distance,float threshold)=>distance>=threshold;
  public static Vector3 ClosestTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
  {
   var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
   if(d1<=0&&d2<=0)return a;
   var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
   float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
   var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
   float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
   float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
   float sum=va+vb+vc;if(Math.Abs(sum)<1e-12f)return a;
   return a+ab*(vb/sum)+ac*(vc/sum);
  }
  public static float SegmentDistance(Vector3 a,Vector3 b,Vector3 c,Vector3 d,out Vector3 onSecond)
  {
   var u=b-a;var v=d-c;var w=a-c;float aa=Vector3.Dot(u,u),bb=Vector3.Dot(u,v),cc=Vector3.Dot(v,v),dd=Vector3.Dot(u,w),ee=Vector3.Dot(v,w);
   float s=0,t=0;
   if(aa<1e-12f)t=cc<1e-12f?0:Clamp(ee/cc);
   else if(cc<1e-12f)s=Clamp(-dd/aa);
   else{float denom=aa*cc-bb*bb;s=denom>1e-12f?Clamp((bb*ee-cc*dd)/denom):0;t=(bb*s+ee)/cc;if(t<0){t=0;s=Clamp(-dd/aa);}else if(t>1){t=1;s=Clamp((bb-dd)/aa);}}
   onSecond=c+v*t;return Vector3.Distance(a+u*s,onSecond);
  }
  static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
  public static float SegmentTriangle(Vector3 start,Vector3 end,Vector3 a,Vector3 b,Vector3 c,out Vector3 surface)
  {
   var direction=end-start;float length=direction.Length();
   if(length>1e-8f&&RayTriangle(start,direction/length,a,b,c,length,out float hit)){surface=start+direction/length*hit;return 0;}
   surface=ClosestTriangle(start,a,b,c);float nearest=Vector3.Distance(start,surface);
   var p=ClosestTriangle(end,a,b,c);float distance=Vector3.Distance(end,p);if(distance<nearest){nearest=distance;surface=p;}
   distance=SegmentDistance(start,end,a,b,out p);if(distance<nearest){nearest=distance;surface=p;}
   distance=SegmentDistance(start,end,b,c,out p);if(distance<nearest){nearest=distance;surface=p;}
   distance=SegmentDistance(start,end,c,a,out p);if(distance<nearest){nearest=distance;surface=p;}
   return nearest;
  }
  public static Vector3 InsertionDelta(Vector3 tip,Vector3 surface,Vector3 direction,float depth)=>surface+direction*depth-tip;
  public static bool ContactTriangle(Vector3 tip,Vector3 previous,Vector3 direction,float reach,float width,bool nearestSkin,Vector3 a,Vector3 b,Vector3 c,out Vector3 contact)
  {
   if(nearestSkin)
   {
    contact=ClosestTriangle(tip,a,b,c);
    if(Vector3.Distance(tip,contact)<=reach+width*.5f+1e-6f)return true;
    // Movement events retain swept contact for fast VR crossings.
    return previous!=tip&&SegmentTriangle(previous,tip,a,b,c,out contact)<=width*.5f+1e-6f;
   }
   float gap=SegmentTriangle(tip-direction*reach,tip+direction*reach,a,b,c,out contact);
   if(previous!=tip)
   {float swept=SegmentTriangle(previous,tip,a,b,c,out var sweptContact);if(swept<gap){gap=swept;contact=sweptContact;}}
   return gap<=width*.5f+1e-6f;
  }
 }
}
