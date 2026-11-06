using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Reflection;
using BepInEx.Bootstrap;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HipComparisonProbe : IDevelopmentCommand
{
 public string Name => "hip-comparison";
 private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 private object _result, _solver;
 private FieldInfo _preField;
 private Delegate _preDelegate;
 private Vector3? _before;
 private Transform _bone, _actor;
 private ChaFileControl _femaleCard, _maleCard;
 [DataContract] private sealed class SavedRoom { [DataMember] public SavedAppearance appearance; }
 [DataContract] private sealed class SavedAppearance { [DataMember] public string female; [DataMember] public string male; }
 private static string NotReady(HSceneProc proc)
 {
  if(proc==null)return "HSceneProc missing";
  if(!proc.isActiveAndEnabled)return "HSceneProc.Start has not enabled gameplay";
  if(Manager.Scene.IsFadeNow||Manager.Scene.IsOverlap)return "Scene fade/overlap active";
  if(Manager.Scene.NowSceneNames.Count==0||Manager.Scene.NowSceneNames[0]!="HProc")return "HProc is not the current native scene";
  if(proc.flags==null||proc.flags.nowAnimationInfo==null)return "Initial animation missing";
  if(proc.flags.isHSceneEnd)return "H scene ending";
  if(proc.flags.voiceWait)return "Native voiceWait blocks posture changes";
  var sprite=Get(proc,"sprite");
  if(sprite==null||Equals(Get(sprite,"isFade"),true))return "H sprite fading/not ready";
  return null;
 }
 private static object Selection(HSceneProc proc)
 {
  var p=proc?.flags?.nowAnimationInfo;
  return p==null?null:new{mode=p.mode.ToString(),id=p.id,name=p.nameAnimation,bundle=p.paramFemale.path.assetpath,asset=p.paramFemale.path.file};
 }
 private static MemberInfo Member(Type t,string key) { for(;t!=null;t=t.BaseType){var m=(MemberInfo)t.GetField(key,Flags|BindingFlags.DeclaredOnly)??t.GetProperty(key,Flags|BindingFlags.DeclaredOnly);if(m!=null)return m;}throw new MissingMemberException(key); }
 private static object Get(object obj,string key){if(obj==null)return null;var m=Member(obj.GetType(),key);return m is FieldInfo f?f.GetValue(obj):((PropertyInfo)m).GetValue(obj,null);}
 private static object Call(object obj,string key,params object[] args)=>obj.GetType().GetMethods(Flags).Single(m=>m.Name==key&&m.GetParameters().Length==args.Length).Invoke(obj,args);
 private static object Provider(string guid)=>Chainloader.PluginInfos[guid].Instance;
 private static object Gate()=>ReflectionCommands.ResolveType("CanonGate.GateApi").GetProperty("Current",Flags).GetValue(null,null);
 private static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
 private void Detach(){if(_preField!=null&&_solver!=null&&_preDelegate!=null)_preField.SetValue(_solver,Delegate.Remove((Delegate)_preField.GetValue(_solver),_preDelegate));_preField=null;_preDelegate=null;}
 private void BeforeSolve(){if(_bone!=null&&_actor!=null)_before=_actor.InverseTransformPoint(_bone.position);Detach();}
 public object Execute(Dictionary<string,object> args,DevelopmentContext context)
 {
  string action=ReflectionCommands.Text(args,"action","sample");
  if(action=="result")return _result;
  if(action=="remember-snapshot")
  {
   SavedRoom room;
   using(var file=File.OpenRead(ReflectionCommands.Text(args,"path")))
    room=(SavedRoom)new DataContractJsonSerializer(typeof(SavedRoom),new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=int.MaxValue}).ReadObject(file);
   if(room?.appearance==null)throw new InvalidDataException("Appearance snapshot missing");
   var decoder=ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelRoomAppearanceRuntime").GetMethod("Decode",Flags);
   _femaleCard=(ChaFileControl)decoder.Invoke(null,new object[]{room.appearance.female});
   _maleCard=(ChaFileControl)decoder.Invoke(null,new object[]{room.appearance.male});
   return new{female=_femaleCard.parameter.fullname,male=_maleCard.parameter.fullname};
  }
  if(action=="menu-state")
  {
   var menu=UnityEngine.Object.FindObjectsOfType<FreeHScene>().SingleOrDefault();
   return new{ready=menu!=null&&Equals(Get(menu,"isInit"),true)&&!Manager.Scene.IsFadeNow&&!Manager.Scene.IsOverlap,exists=menu!=null};
  }
  if(action=="hotel-title"){Call(Get(Provider("com.kks.maingame.aishoujomode"),"_hotel"),"Cancel");return "Hotel return requested once";}
  if(action=="free-menu"){UnityEngine.Object.FindObjectsOfType<TitleScene>().Single().OnOtherFreeH();return "Native FreeH menu requested";}
  if(action=="free-start")
  {
   var menu=UnityEngine.Object.FindObjectsOfType<FreeHScene>().Single();
   if(!Equals(Get(menu,"isInit"),true))throw new InvalidOperationException("FreeH menu is not ready");
   if(_femaleCard==null||_maleCard==null)throw new InvalidOperationException("Remember hotel cards first");
   if(Manager.Scene.commonSpace.GetComponent<ActionGame.H.OpenHData>()!=null)throw new InvalidOperationException("Previous OpenHData still exists");
   var member=Get(menu,"member");
   var data=new ActionGame.H.OpenHData.Data { lstFemale=new List<SaveData.Heroine>{new SaveData.Heroine(_femaleCard,false)},player=new SaveData.Player(_maleCard,false),isFreeH=true,
    mapNoFreeH=Convert.ToInt32(Get(Get(Provider("com.kks.maingameblankmapadd"),"_settings"),"AddedMapNo")),peepCategory=new List<int>{0},
    timezoneFreeH=Convert.ToInt32(Get(Get(member,"resultTimeZone"),"Value")),statusFreeH=Convert.ToInt32(Get(Get(member,"resultStatus"),"Value")),
    stageFreeH1=Convert.ToInt32(Get(Get(member,"resultStage1"),"Value")),stageFreeH2=0,isFound=Convert.ToBoolean(Get(Get(member,"resultDiscovery"),"Value"))};
   var open=Manager.Scene.commonSpace.AddComponent<ActionGame.H.OpenHData>();open.data=data;open.isLoad=true;open.isAsync=false;open.isFade=false;open.fadeType=FadeCanvas.Fade.In;
   return new{map=data.mapNoFreeH,female=data.lstFemale[0].charFile.parameter.fullname};
  }
  var gate=Gate(); var proc=Get(gate,"HScene") as HSceneProc;
  if(action=="state")return new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,gate=gate!=null,hotel=Get(gate,"IsHotelMode"),room=Get(gate,"HotelRoomId"),hproc=proc!=null,ready=NotReady(proc)==null,waiting=NotReady(proc),selection=Selection(proc)};
  if(proc==null)throw new InvalidOperationException("Native HSceneProc is not ready");
  if(action=="select-clone")
  {
   var waiting=NotReady(proc);if(waiting!=null)throw new InvalidOperationException(waiting);
   var lists=(IEnumerable)Get(proc,"lstUseAnimInfo");
   var matches=lists.Cast<IEnumerable>().Where(l=>l!=null).SelectMany(l=>l.Cast<HSceneProc.AnimationListInfo>()).Where(p=>p!=null&&p.mode==HFlag.EMode.aibu&&p.id==1&&p.nameAnimation=="仰向け愛撫【クローン】").Distinct().ToArray();
   if(matches.Length!=1)throw new InvalidOperationException("Clone count="+matches.Length);
   Call(proc,"ChangeAnimator",matches[0],false);return new{applied=ReferenceEquals(proc.flags.nowAnimationInfo,matches[0]),selection=Selection(proc)};
  }
  if(action=="leave"){proc.flags.numEnd=0;proc.flags.isHSceneEnd=true;return "Native H exit requested";}
  var hip=Provider("com.kks.main.girlbodyikgizmo");
  var female=(ChaControl)Get(Get(hip,"_femaleSession"),"Character");
  if(action=="remember")
  {
   _femaleCard=female.chaFile;
   var male=Get(proc,"male") as ChaControl??throw new InvalidOperationException("Male missing");_maleCard=male.chaFile;
   return new{female=_femaleCard.parameter.fullname,male=_maleCard.parameter.fullname};
  }
  if(action!="sample")throw new ArgumentException("Unknown action");
  Detach();_result=null;_before=null;_actor=female.transform;
  var ik=Get(Get(hip,"_femaleSession"),"FullBodyIk");_solver=Get(ik,"solver");
  _bone=(Transform)Get(Get(_solver,"bodyEffector"),"bone");
  _preField=(FieldInfo)Member(_solver.GetType(),"OnPreUpdate");
  _preDelegate=Delegate.CreateDelegate(_preField.FieldType,this,GetType().GetMethod(nameof(BeforeSolve),Flags));
  _preField.SetValue(_solver,Delegate.Combine((Delegate)_preField.GetValue(_solver),_preDelegate));
  ((MonoBehaviour)hip).StartCoroutine(Sample(proc,hip,female,gate));return new{pending=true};
 }
 private IEnumerator Sample(HSceneProc proc,object hip,ChaControl female,object gate)
 {
  yield return null;
  yield return new WaitForEndOfFrame();
  try
  {
   var composer=Provider("com.kks.main.gameobjectcomposer");
   var body=((IEnumerable)Get(hip,"_bikEff")).Cast<object>().ElementAt(12);
   var proxy=Get(body,"Proxy") as Transform;var follow=Get(body,"FollowBone") as Transform;
   var active=Get(composer,"_activePreset");
   var anim=female.animBody;var state=anim.GetCurrentAnimatorStateInfo(0);
   var dict=(IDictionary)Get(composer,"_runtimeObjects");
   var objects=dict.Keys.Cast<object>().Select(k=>{
    var obj=(GameObject)Get(dict[k],"GameObject");return new{id=k,name=obj.name,actorRelative=V(female.transform.InverseTransformPoint(obj.transform.position)),local=V(obj.transform.localPosition),parent=obj.transform.parent?.name};}).ToArray();
   var points=((IEnumerable)Get(hip,"_bikEff")).Cast<object>().Select((s,i)=>new{index=i,running=Get(s,"Running"),weight=Call(hip,"GetBodyIKWeight",i),follow=(Get(s,"FollowBone") as Transform)?.name}).ToArray();
   _result=new{frame=Time.frameCount,hotel=Get(gate,"IsHotelMode"),room=Get(gate,"HotelRoomId"),scene=proc.gameObject.scene.name,
    female=female.chaFile.parameter.fullname,bodyShape=female.chaFile.custom.body.shapeValueBody,
    coordinate=female.fileStatus.coordinateType,root=V(female.transform.position),rootScale=V(female.transform.lossyScale),
    selection=new{mode=proc.flags.nowAnimationInfo.mode.ToString(),id=proc.flags.nowAnimationInfo.id,name=proc.flags.nowAnimationInfo.nameAnimation,
     bundle=proc.flags.nowAnimationInfo.paramFemale.path.assetpath,asset=proc.flags.nowAnimationInfo.paramFemale.path.file},
    nativeAnimator=state.fullPathHash,nativeTime=state.normalizedTime,nativeState=proc.flags.nowAnimStateName,motion=proc.flags.motion,
    rawHip=_before.HasValue?V(_before.Value):null,solvedHip=V(female.transform.InverseTransformPoint(_bone.position)),
    body=new{running=Get(body,"Running"),weight=Call(hip,"GetBodyIKWeight",12),target=proxy==null?null:V(female.transform.InverseTransformPoint(proxy.position)),follow=follow?.name,
     vibeOwned=Get(hip,"_vibeOwnsBodyIk"),autoPose=Get(hip,"_autoPoseLastAppliedPresetId")},
    composer=new{preset=Get(active,"displayName"),key=Get(active,"key"),loop1=Get(active,"loop1Json"),loop2=Get(active,"loop2Json"),objects},points};
  }catch(Exception ex){_result=new{error=ex.ToString()};}
  finally{Detach();}
 }
 public void Dispose(){Detach();_result=null;}
}
