using System;
using System.IO;
using UnityEngine;
using MainGameTransformGizmo;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  Animation _injectionAnimation;AnimationClip _injectionClip;Transform _pusher,_liquidPivot,_assembly;
  Vector3 _pusherRest,_pusherFull;bool _spent;
  Vector3 _gripCenter;AssetBundle _modelBundle;
  void ApplyModelScale()
  {
   if(_model==null)return;
   var parent=_model.transform.parent;var inherited=parent!=null?parent.lossyScale:Vector3.one;float scale=_scale.Value;
   _model.transform.localScale=new Vector3(Mathf.Abs(inherited.x)>.000001f?scale/inherited.x:scale,Mathf.Abs(inherited.y)>.000001f?scale/inherited.y:scale,Mathf.Abs(inherited.z)>.000001f?scale/inherited.z:scale);
  }
  GameObject ModelPrefab(string name)
  {
   if(_modelBundle==null)
   {
    using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream("MainGameSyringe.Models.bundle"))
    {
     if(stream==null)throw new InvalidDataException("注射器・ボトルの内蔵モデルがありません。");
     using(var memory=new MemoryStream()){stream.CopyTo(memory);_modelBundle=AssetBundle.LoadFromMemory(memory.ToArray());}
    }
    if(_modelBundle==null)throw new InvalidDataException("注射器のモデル素材を開けません。");
   }
   var prefab=_modelBundle.LoadAsset<GameObject>("assets/generated/"+name+".prefab");
   if(prefab==null)throw new InvalidDataException("内蔵モデルがありません: "+name);return prefab;
  }
  void CreateModel()
  {
   _model=Instantiate(ModelPrefab("Syringe"));_model.name="canon Syringe";_model.transform.localScale=Vector3.one*_scale.Value;
   _assembly=_model.transform.Find("Assembly");_pusher=_assembly.Find("Pusher");_liquidPivot=_assembly.Find("LiquidPivot");_tip=_assembly.Find("NeedleTip");
   if(_pusher==null||_liquidPivot==null||_tip==null)throw new InvalidDataException("注射器の可動部または針先がありません。");
   _pusherRest=Vector3.zero;_pusherFull=Vector3.forward*.0405f;_gripCenter=Vector3.zero;
   _injectionAnimation=_model.AddComponent<Animation>();_spent=false;RebuildInjectionAnimation();
  }
  void RebuildInjectionAnimation()
  {
   if(_injectionAnimation==null||_pusher==null)return;
   CancelRefill();_injectionAnimation.Stop();_injectionAnimation.RemoveClip("Inject");if(_injectionClip!=null)Destroy(_injectionClip);
   _injectionClip=new AnimationClip{legacy=true,wrapMode=WrapMode.ClampForever};
   AddCurve("Assembly/Pusher","localPosition.x",_pusherRest.x,_pusherFull.x);AddCurve("Assembly/Pusher","localPosition.y",_pusherRest.y,_pusherFull.y);AddCurve("Assembly/Pusher","localPosition.z",_pusherRest.z,_pusherFull.z);
   AddCurve("Assembly/LiquidPivot","localScale.z",1,_remainingLiquid.Value);_injectionAnimation.AddClip(_injectionClip,"Inject");
   _pusher.localPosition=_spent?_pusherFull:_pusherRest;_liquidPivot.localScale=new Vector3(1,1,_spent?_remainingLiquid.Value:1);
  }
  void AddCurve(string path,string property,float from,float to)
  {_injectionClip.SetCurve(path,typeof(Transform),property,AnimationCurve.EaseInOut(0,from,SyringeEffectAudio.MotionSeconds,to));}
  void PlayInjection(){_spent=true;_injectionBusyUntil=Time.realtimeSinceStartup+SyringeEffectAudio.MotionSeconds;_injectionAnimation["Inject"].speed=1;_injectionAnimation.Play("Inject");_injectionAnimation["Inject"].time=0;PlayInjectionSound();}
  void DestroyModel()
  {
   CancelRefill();StopInjectionSound();_injectionBusyUntil=0;
   if(_gizmo!=null)_gizmo.DragStateChanged-=GizmoDragChanged;
   if(_gizmo!=null){TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);TransformGizmoApi.Detach(_model);}_gizmo=null;
   if(_model!=null){_model.SetActive(false);Destroy(_model);}_model=null;_tip=null;_pusher=null;_liquidPivot=null;_assembly=null;_injectionAnimation=null;
   if(_injectionClip!=null)Destroy(_injectionClip);_injectionClip=null;
   if(_modelBundle!=null)_modelBundle.Unload(true);_modelBundle=null;
  }
 }
}
