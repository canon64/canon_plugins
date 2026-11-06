using System;
using System.Collections.Generic;
namespace MainGameSyringe
{
 public sealed class DoseLedger
 {
  sealed class Dose {public double At,Rise,Fall;}
  readonly List<Dose> _doses=new List<Dose>();
  public int Count=>_doses.Count;
  public void RemoveLast(){if(_doses.Count>0)_doses.RemoveAt(_doses.Count-1);}
  public void Add(double at,double rise,double fall){if(rise<=0||fall<=0)throw new ArgumentOutOfRangeException();_doses.Add(new Dose{At=at,Rise=rise,Fall=fall});}
  static int Level(Dose d,double now)
  {
   double age=Math.Max(0,now-d.At);
   if(age>=d.Rise+d.Fall-1e-7)return 0;
   return age<d.Rise?Math.Min(100,(int)Math.Floor(age/d.Rise*100+1e-7)):Math.Max(0,100-(int)Math.Floor((age-d.Rise)/d.Fall*100+1e-7));
  }
  public void Evaluate(double now,int maximum,float radius,out int opacity,out float orbit,out double next)
  {
   opacity=0;double sum=0;next=double.PositiveInfinity;
   _doses.RemoveAll(d=>now>=d.At+d.Rise+d.Fall-1e-7);
   foreach(var d in _doses)
   {
    int level=Level(d,now);opacity=Math.Max(opacity,level);sum+=level/100d;
    double age=Math.Max(0,now-d.At);bool rising=age<d.Rise-1e-7;double start=rising?d.At:d.At+d.Rise;double step=(rising?d.Rise:d.Fall)/100;
    double change=start+(Math.Floor((now-start)/step+1e-7)+1)*step;
    next=Math.Min(next,Math.Min(change,d.At+d.Rise+d.Fall));
   }
   orbit=radius*(float)Math.Min(1,sum/Math.Max(1,maximum));
  }
 }
}
