using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  GameObject _refillRig;AnimationClip _refillClip;Quaternion _refillRestRotation;
  readonly List<Material> _refillMaterials=new List<Material>();bool _refillDrewLiquid;
  static Vector4[] FluidProfile()
  {
   var result=new Vector4[24];
   using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream("MainGameSyringe.FluidProfile.txt"))using(var reader=new StreamReader(stream))
   {int i=0;string line;while((line=reader.ReadLine())!=null){var p=line.Split(',');result[i++]=new Vector4(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),0,0);}for(int j=i;j<result.Length;j++)result[j]=result[i-1];}
   return result;
  }
  void BeginRefillVisuals()
  {
   _refillDrewLiquid=false;_refillRestRotation=_assembly.localRotation;_refillRig=new GameObject("RefillRig");_refillRig.transform.SetParent(_assembly,false);
   var bottle=Instantiate(ModelPrefab("Bottle"));bottle.name="Bottle";bottle.transform.SetParent(_refillRig.transform,false);
   // Compensate the barrel XY scale while preserving the approved bottle size.
   bottle.transform.localScale=new Vector3(.8f,.8f,8f/15);
   // Turn the label toward the viewer around the bottle's longitudinal (local Z) axis.
   bottle.transform.localRotation=Quaternion.Euler(180,0,0)*Quaternion.AngleAxis(180,Vector3.forward);var profile=FluidProfile();
   foreach(var renderer in bottle.GetComponentsInChildren<MeshRenderer>())
   {if(renderer.name!="PurpleLiquid"&&renderer.name!="FreeSurface")continue;var material=renderer.material;material.SetVectorArray("_Profile",profile);material.SetFloat("_Fill",SyringeRefillMotion.InitialFill);_refillMaterials.Add(material);}
   var inside=new Vector3(0,0,_tip.localPosition.z+(float)(.0832305-.016)*(8f/15));var above=inside+Vector3.forward*.12f;
   _refillRig.transform.localPosition=above;
   var upright=Quaternion.FromToRotation(Vector3.forward,_model.transform.InverseTransformDirection(Vector3.up));
   _refillClip=SyringeRefillMotion.Create(_pusherFull,_pusherRest,_remainingLiquid.Value,_refillRestRotation,upright,above,inside);
   _injectionAnimation.AddClip(_refillClip,"Refill");_injectionAnimation.Play("Refill");_injectionAnimation["Refill"].time=0;KickRefillWaves();
  }
  void KickRefillWaves(){foreach(var material in _refillMaterials)if(material!=null)material.SetFloat("_WaveStart",Time.time);}
  void CompleteRefillDrawing(){_refillDrewLiquid=true;_spent=false;foreach(var material in _refillMaterials)if(material!=null)material.SetFloat("_Fill",0);}
  void EndRefillVisuals()
  {
   if(_refillRig==null&&_refillClip==null)return;
   if(_injectionAnimation!=null){_injectionAnimation.Stop("Refill");_injectionAnimation.RemoveClip("Refill");}
   if(_assembly!=null)_assembly.localRotation=_refillRestRotation;
   if(_pusher!=null)_pusher.localPosition=_refillDrewLiquid?_pusherRest:_pusherFull;
   if(_liquidPivot!=null)_liquidPivot.localScale=new Vector3(1,1,_refillDrewLiquid?1:_remainingLiquid.Value);_spent=!_refillDrewLiquid;
   if(_refillRig!=null){_refillRig.SetActive(false);Destroy(_refillRig);}_refillRig=null;
   foreach(var material in _refillMaterials)if(material!=null)Destroy(material);_refillMaterials.Clear();if(_refillClip!=null)Destroy(_refillClip);_refillClip=null;
  }
 }
}
