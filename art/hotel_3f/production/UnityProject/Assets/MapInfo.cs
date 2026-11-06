using System;
using System.Collections.Generic;
using UnityEngine;
// Serialization-only mirror of the game's Assembly-CSharp MapInfo.
public class MapInfo : ScriptableObject {
 [Serializable] public class Param {
  public string MapName,DisplayName; public int No,Sort; public string AssetBundleName,AssetName;
  public bool isGate,is2D,isWarning; public int State,LookFor; public bool isOutdoors,isFreeH,isSpH,isSky,isH;
  public int ThumbnailMorningID=-1,ThumbnailDayTimeID=-1,ThumbnailEveningID=-1,ThumbnailNightID=-1;
 }
 public List<Param> param=new List<Param>();
}
