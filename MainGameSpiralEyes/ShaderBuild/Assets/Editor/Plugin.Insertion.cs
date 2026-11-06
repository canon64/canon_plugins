using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  readonly Dictionary<ChaControl,SkinnedMeshRenderer[]> _skinMeshes=new Dictionary<ChaControl,SkinnedMeshRenderer[]>();
  readonly HashSet<SkinnedMeshRenderer> _failedSkins=new HashSet<SkinnedMeshRenderer>();
  ChaControl _insertedActor;Transform _insertedBone;GripLifetime _boneLifetime;
  Vector3 _pullAnchorLocal,_surfaceLocal;bool _probeInitialized;
  readonly ReinsertionGuard _reinsertion=new ReinsertionGuard();
  Transform _withdrawnBone;Vector3 _withdrawnSurfaceLocal;
  Vector3 _lastProbePosition;Quaternion _lastProbeRotation;
  static NVector ToNumeric(Vector3 p)=>new NVector(p.x,p.y,p.z);

  void CacheSkin(ChaControl actor)
  {
   if(actor.objBody!=null)_skinMeshes[actor]=actor.objBody.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   else _skinMeshes[actor]=new SkinnedMeshRenderer[0];
  }
  void HandMoved()
  {
   if(!Allowed||!_focused||_heldController==null||_focus==null||!_focus.IsValid)return;
   if(_insertedBone!=null)
   {
    float distance=Vector3.Distance(_heldController.transform.position,_insertedBone.TransformPoint(_pullAnchorLocal));
    if(SkinGeometry.ShouldRelease(distance,_pullDistance.Value))DetachInsertion(true);
    return;
   }
   TryInsert(false);
  }
  void TryInsert(bool force,bool commit=true)
  {
   try{EvaluateInsertion(force,commit);}
   finally{if(force)Trace((commit?"刺入":"判定のみ")+" / "+_probeReport+" / 方式="+(_nearSurface.Value?"近接肌":"針軸")+" / 針先="+(_tip!=null?_tip.position.ToString("F4"):"なし"));}
  }
  void EvaluateInsertion(bool force,bool commit)
  {
   if(!Allowed||!_focused){_probeReport="操作できません（H許可・有効設定・フォーカスを確認）";return;}
   if(_model==null||_tip==null){_probeReport="注射器を先に呼び出してください";return;}
   if(_insertedBone!=null){_probeReport="固定済み / "+_insertedBone.name+" / 投与または抜くを選んでください";return;}
   var tip=_tip.position;
   if(commit)
   {
    if(_withdrawnBone==null)_reinsertion.Reset();
    else if(!_reinsertion.CanInsert(Vector3.Distance(tip,_withdrawnBone.TransformPoint(_withdrawnSurfaceLocal)),_stickDistance.Value+_insertionDepth.Value,force))return;
   }
   if(force)_failedSkins.Clear();
   var sweepStart=!force&&_probeInitialized?_lastProbePosition:tip;
   var close=new List<ChaControl>();float nearestBone=float.PositiveInfinity;
   foreach(var segment in _segments)
   {
    if(segment.Actor==null||segment.A==null||segment.B==null)continue;
    float scale=Mathf.Max(Mathf.Abs(segment.A.lossyScale.x),Mathf.Abs(segment.A.lossyScale.z));
    float distance=SkinGeometry.SegmentDistance(ToNumeric(sweepStart),ToNumeric(tip),ToNumeric(segment.A.position),ToNumeric(segment.B.position),out var unused);
    nearestBone=Mathf.Min(nearestBone,distance);
    if(distance<=segment.Radius*scale+_stickDistance.Value+_needleWidth.Value*.5f)close.Add(segment.Actor);
   }
   if(close.Count==0){_probeInitialized=false;_probeReport="部位の範囲外 / 骨格まで "+DistanceText(nearestBone)+" / 腕範囲 "+(_armRadius.Value*100).ToString("0.##")+" cm・太もも "+(_thighRadius.Value*100).ToString("0.##")+" cm / 肌距離 未測定";return;}
   if(!force&&_probeInitialized&&Vector3.Distance(tip,_lastProbePosition)<_probeMove.Value&&Quaternion.Angle(_model.transform.rotation,_lastProbeRotation)<_probeAngle.Value)return;
   _lastProbePosition=tip;_lastProbeRotation=_model.transform.rotation;_probeInitialized=true;
   ChaControl actor=null;BodySegment chosen=null;Vector3 point=default;float nearestSkin=float.PositiveInfinity;bool meshRead=false,regionSeen=false;
   foreach(var candidate in new HashSet<ChaControl>(close))
   {
    if(!IsInjectionTarget(candidate))continue;
    if(!FindSkinSurface(candidate,tip,sweepStart,_model.transform.forward,out var hit,out var part,ref nearestSkin,ref meshRead,ref regionSeen))continue;
    if(actor!=null){_probeReport="複数女性の肌に接触しています。固定しません。";return;}
    actor=candidate;point=hit;chosen=part;
   }
   string distances=" / 肌まで "+DistanceText(nearestSkin)+" / 許容距離 "+(_stickDistance.Value*100).ToString("0.##")+" cm / 判定幅 "+(_needleWidth.Value*1000).ToString("0.##")+" mm";
   if(actor==null){_probeReport=(!meshRead?"肌メッシュを読めません":!regionSeen?"肌の接触点が腕・太ももの範囲外":nearestSkin>_stickDistance.Value+_needleWidth.Value*.5f?"肌まで届きません":_nearSurface.Value?"許容範囲内に固定できる肌接触がありません":"針の方向・判定幅内に肌の交点がありません")+distances;return;}
   _probeReport="女性"+(_actors.IndexOf(actor)+1)+" / "+(chosen.Thigh?"太もも":"腕")+" / "+chosen.A.name+distances+(commit?" / 固定しました":" / 刺入可能（確認のみ）");
   if(!commit)return;
   // Follow the region's proximal bone: upper arm, forearm, or thigh; never jump to the distal joint.
   var bone=chosen.A;
   // Translation only: keep the needle's current world angle, then preserve that local angle on the bone.
   var delta=SkinGeometry.InsertionDelta(ToNumeric(tip),ToNumeric(point),ToNumeric(_model.transform.forward),_insertionDepth.Value);
   _model.transform.position+=new Vector3(delta.X,delta.Y,delta.Z);
   _model.transform.SetParent(bone,true);_insertedBone=bone;_insertedActor=actor;_surfaceLocal=bone.InverseTransformPoint(point);
   if(_heldController!=null)_pullAnchorLocal=bone.InverseTransformPoint(_heldController.transform.position);
   _boneLifetime=bone.gameObject.AddComponent<GripLifetime>();_boneLifetime.Released=()=>DetachInsertion(false);
   SyncGizmoVisibility();_status=(chosen.Thigh?"太もも":"腕")+"へ刺さり、"+bone.name+" に固定しました。トリガーで注射。握ったまま手を離すと抜けます。";
   Trace("刺入先: actor="+actor.name+" bone="+bone.name);
   PlayVoiceCue(VoiceCueEvent.Inserted,actor);
  }
  static string DistanceText(float value)=>float.IsInfinity(value)||float.IsNaN(value)?"未測定":(value*100).ToString("0.##")+" cm";
  string _probeReport="「判定だけ確認」または「今の位置で刺す」で現在の結果を表示します。";
  BodySegment FindSurfaceRegion(ChaControl actor,Vector3 point)
  {
   BodySegment chosen=null;float nearest=float.PositiveInfinity;
   foreach(var segment in _segments)
   {
    if(!ReferenceEquals(segment.Actor,actor)||segment.A==null||segment.B==null)continue;
    float distance=SkinGeometry.DistanceToSegment(ToNumeric(point),ToNumeric(segment.A.position),ToNumeric(segment.B.position));
    float scale=Mathf.Max(Mathf.Abs(segment.A.lossyScale.x),Mathf.Abs(segment.A.lossyScale.z));
    if(distance<=segment.Radius*scale&&distance<nearest){nearest=distance;chosen=segment;}
   }
   return chosen;
  }
  bool FindSkinSurface(ChaControl actor,Vector3 tip,Vector3 previous,Vector3 direction,out Vector3 surface,out BodySegment region,ref float nearestSkin,ref bool meshRead,ref bool regionSeen)
  {
   surface=default;region=null;float nearest=float.PositiveInfinity;bool found=false;
   if(!_skinMeshes.TryGetValue(actor,out var renderers))return false;
   foreach(var renderer in renderers)
   {
    if(renderer==null||renderer.sharedMesh==null||_failedSkins.Contains(renderer))continue;
    var baked=new Mesh();
    try
    {
     renderer.BakeMesh(baked);var vertices=baked.vertices;var triangles=baked.triangles;var world=new NVector[vertices.Length];meshRead=true;
     for(int i=0;i<vertices.Length;i++)world[i]=ToNumeric(renderer.transform.TransformPoint(vertices[i]));
     for(int i=0;i+2<triangles.Length;i+=3)
      {
       var a=world[triangles[i]];var b=world[triangles[i+1]];var c=world[triangles[i+2]];
       var near=SkinGeometry.ClosestTriangle(ToNumeric(tip),a,b,c);var nearWorld=new Vector3(near.X,near.Y,near.Z);
       var nearbyRegion=FindSurfaceRegion(actor,nearWorld);
       if(nearbyRegion!=null){regionSeen=true;nearestSkin=Mathf.Min(nearestSkin,NVector.Distance(ToNumeric(tip),near));}
       if(!SkinGeometry.ContactTriangle(ToNumeric(tip),ToNumeric(previous),ToNumeric(direction),_stickDistance.Value,_needleWidth.Value,_nearSurface.Value,a,b,c,out var contact))continue;
       var p=new Vector3(contact.X,contact.Y,contact.Z);var candidate=FindSurfaceRegion(actor,p);
       if(candidate==null)continue;
       float score=Vector3.Distance(tip,p);if(score>=nearest)continue;
       nearest=score;surface=p;region=candidate;found=true;
      }
    }
    catch(Exception ex){_failedSkins.Add(renderer);_status="肌メッシュを読めません: "+ex.Message;Logger.LogWarning(_status);}
    finally{ReleaseProbeObject(baked);}
   }
   return found;
  }
  void WithdrawNeedle()
  {
   if(!Allowed||!_focused||_insertedBone==null||_model==null||_tip==null)return;
   var surface=_insertedBone.TransformPoint(_surfaceLocal);
   var outside=surface-_model.transform.forward*Mathf.Max(.001f,_needleWidth.Value*.5f);
   DetachInsertion(false);
   _model.transform.position+=outside-_tip.position;
   _probeReport="抜きました。補充後、同じ位置から再度刺入できます。";
  }
  void DetachInsertion(bool returnToHand)
  {
   if(_insertedBone==null&&ReferenceEquals(_insertedActor,null)&&_boneLifetime==null)return;
   var previousBone=_insertedBone;var previousSurface=_surfaceLocal;
   if(_boneLifetime!=null){_boneLifetime.Released=null;ReleaseProbeObject(_boneLifetime);}_boneLifetime=null;
   if(_model!=null&&_model.transform.parent!=null)_model.transform.SetParent(null,true);
   _insertedBone=null;_insertedActor=null;_probeInitialized=false;
   if(returnToHand&&_heldController!=null&&_model!=null){_model.transform.SetParent(_heldController.transform,false);UpdateHeldPose();}
   _withdrawnBone=previousBone;_withdrawnSurfaceLocal=previousSurface;
   if(previousBone!=null&&_tip!=null)
    _reinsertion.Withdraw(Vector3.Distance(_tip.position,previousBone.TransformPoint(previousSurface)),_stickDistance.Value+_insertionDepth.Value);
   else _reinsertion.Reset();
   SyncGizmoVisibility();
  }
  void RepositionInsertion()
  {
   if(_insertedBone==null||_model==null||_tip==null)return;
   _model.transform.position+=_insertedBone.TransformPoint(_surfaceLocal)+_model.transform.forward*_insertionDepth.Value-_tip.position;
  }
  static void ReleaseProbeObject(UnityEngine.Object value)
  {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
 }
}
