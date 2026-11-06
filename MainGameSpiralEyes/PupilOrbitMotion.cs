using System;
namespace MainGameSpiralEyes
{
 public static class PupilOrbitMotion
 {
  // Match the eye's signed UV transform before sampling the moving texture.
  // Both start on the same horizontal side and rotate in opposite directions:
  // whenever one is above its center, the other is below its center.
  public static void Mapping(int side,float textureX,float textureY,float pupilX,float pupilY,out float x,out float y,out float direction,out float phase)
  {
   x=Math.Abs(pupilX)>1e-6f?textureX/Math.Abs(pupilX):1;
   y=Math.Abs(pupilY)>1e-6f?textureY/Math.Abs(pupilY):1;
   direction=side==0?-1:1;phase=0;
  }
 }
}
