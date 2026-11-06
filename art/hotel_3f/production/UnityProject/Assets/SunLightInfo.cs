using System;
using System.Collections.Generic;
using UnityEngine;
// Serialization mirror only. KKS supplies the real SunLightInfo.Set implementation.
public class SunLightInfo:MonoBehaviour {
 [Serializable] public class Info {
  public enum Type {Morning,DayTime,Evening,Night}
  public Type type;public Texture2D backTexture;public List<GameObject> visibleList=new List<GameObject>();
  public Vector3 angle;public Color color;public float intensity;
  public Color ambientSkyColor=Color.white,ambientEquatorColor=Color.white,ambientGroundColor=Color.white;
  public bool fogUse;public Color fogColor;public float fogStart,fogEnd,fogDensity;
  public bool glFogDistanceFog=true,glFogHeightFog=true;public float glFogHeight,glFogStartDistance;
  public Transform sunShaftsTransform;public Color sunShaftsColor,sunThresholdColor;public float sunMaxRadius,sunShaftBlurRadius;public int sunRadialBlurIterations;public float sunShaftIntensity;
  public Texture aceLutTexture,aceLutBlendTexture;public float aceBlendAmount;
  public Color aoeTint;public float aoeRadius,aoePowerExponent,aoeBias;
  public float bafBloomIntensity,bafBloomThreshold,bafSepBlurSpread;
  public Transform dofFocalTransform;public float dofFocalSize,dofAperture;
  public float vignetteIntensity,vignetteBlur,vignetteBlurSpread;
 }
 public Light _targetLight;public BackGroundParam _bgParam;public Info[] _infos;
}
