using System.Collections.Generic;
namespace MainGameSyringe
{
 public struct OpacityStep
 {
  public float Delay;public int Level;
  public OpacityStep(float delay,int level){Delay=delay;Level=level;}
 }
 public static class OpacitySteps
 {
  // Finite effect sequence: 100 divisions between the exact transparent/opaque endpoints.
  public static IEnumerable<OpacityStep> Build(float fadeIn,float fadeOut)
  {
   for(int level=1;level<=100;level++)yield return new OpacityStep(fadeIn/100f,level);
   for(int level=99;level>=0;level--)yield return new OpacityStep(fadeOut/100f,level);
  }
 }
}
