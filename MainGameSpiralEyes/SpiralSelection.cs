using System;
namespace MainGameSpiralEyes
{
 public static class SpiralSelection
 {
  // Sideloader remaps numeric IDs; match this zipmod's actual asset identity instead.
  public static bool Matches(string bundle,string texture)
  {
   return string.Equals(bundle?.Replace('\\','/'),"sideloader/canon_spiral_eyes.unity3d",StringComparison.OrdinalIgnoreCase)
    &&string.Equals(texture,"canon_spiral",StringComparison.OrdinalIgnoreCase);
  }
 }
}
