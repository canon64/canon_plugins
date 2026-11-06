using System;
using System.Collections.Generic;
using UnityEngine;

// Only the character, gate, controller and voice boundaries are stubs. The probe and Unity BakeMesh are real.
public sealed class ChaControl:MonoBehaviour {public GameObject objBody;}
namespace MainGameSyringe
{
 public enum VoiceCueEvent {Prepared,Inserted,Injected}
 public sealed class GripLifetime:MonoBehaviour {public Action Released;}
 public sealed partial class Plugin:MonoBehaviour
 {
  sealed class Number {public float Value;public Number(float value){Value=value;}}
  sealed class Flag {public bool Value;}
  readonly Flag _nearSurface=new Flag();
  sealed class Focus {public bool IsValid=true;}
  sealed class Controller {public Transform transform;}
  sealed class TestLog {public void LogWarning(string text){}}
  sealed class BodySegment {public ChaControl Actor;public Transform A,B;public float Radius;public bool Thigh,Neck;public string Label=>Neck?"首":Thigh?"太もも":"腕";}
  readonly Number _pullDistance=new Number(.12f),_stickDistance=new Number(.025f),_insertionDepth=new Number(.008f),_needleWidth=new Number(.002f),_probeMove=new Number(.005f),_probeAngle=new Number(2),_armRadius=new Number(.035f),_thighRadius=new Number(.065f),_neckRadius=new Number(.06f);
  readonly List<BodySegment> _segments=new List<BodySegment>();readonly List<ChaControl> _actors=new List<ChaControl>();
  bool Allowed=true,_focused=true;Controller _heldController;Focus _focus;GameObject _model;Transform _tip;string _status;TestLog Logger=new TestLog();
  bool IsInjectionTarget(ChaControl actor)=>actor!=null&&_actors.Contains(actor);
  void SyncGizmoVisibility(){}void UpdateHeldPose(){}void Trace(string text){}void PlayVoiceCue(VoiceCueEvent cue,ChaControl actor){}
  public void Setup(ChaControl actor,Transform first,Transform last)
  {
   _actors.Add(actor);_segments.Add(new BodySegment{Actor=actor,A=first,B=last,Radius=.035f});CacheSkin(actor);
   if(_model==null){_model=new GameObject("probe product test");_tip=new GameObject("tip").transform;_tip.SetParent(_model.transform,false);}
  }
  public void Place(Vector3 point,Quaternion angle){DetachInsertion(false);_model.transform.SetPositionAndRotation(point,angle);}
  public void Probe(bool commit)=>TryInsert(true,commit);
  public bool Inserted=>_insertedBone!=null;public string Result=>_probeReport;
  public Transform Parent=>_model.transform.parent;public Vector3 Tip=>_tip.position;
  public void Width(float width){_needleWidth.Value=width;}
  public void Cleanup(){DetachInsertion(false);if(_model!=null)DestroyImmediate(_model);}
 }
}
public static class SyringeContactProductTests
{
 static void Check(bool value,string reason){if(!value)throw new Exception("Desktop contact product: "+reason);}
 static ChaControl Actor(List<UnityEngine.Object> owned,out Transform first,out Transform last)
 {
  var obj=new GameObject("female fixture");owned.Add(obj);var actor=obj.AddComponent<ChaControl>();actor.objBody=obj;
  first=new GameObject("cf_j_forearm01_L").transform;first.SetParent(obj.transform);first.localPosition=new Vector3(0,-.1f,0);
  last=new GameObject("cf_j_hand_L").transform;last.SetParent(obj.transform);last.localPosition=new Vector3(0,.1f,0);
  var bone=new GameObject("skin").transform;bone.SetParent(obj.transform);
  var mesh=new Mesh();owned.Add(mesh);mesh.vertices=new[]{new Vector3(-.02f,-.02f,0),new Vector3(.02f,-.02f,0),new Vector3(0,.02f,0)};
  mesh.triangles=new[]{0,1,2};mesh.bindposes=new[]{bone.worldToLocalMatrix*obj.transform.localToWorldMatrix};
  mesh.boneWeights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1}};mesh.RecalculateBounds();
  var renderer=obj.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=new[]{bone};renderer.rootBone=bone;return actor;
 }
 public static void Run()
 {
  var owned=new List<UnityEngine.Object>();var host=new GameObject("contact test host");owned.Add(host);var plugin=host.AddComponent<MainGameSyringe.Plugin>();
  try
  {
   var actor=Actor(owned,out var first,out var last);plugin.Setup(actor,first,last);
   var position=new Vector3(0,0,-.015f);plugin.Place(position,Quaternion.identity);plugin.Probe(false);
   Check(!plugin.Inserted&&plugin.Tip==position&&plugin.Result.Contains("刺入可能"),"dry probe changed pose or missed near skin");
   plugin.Probe(true);Check(plugin.Inserted&&plugin.Parent==first,"desktop explicit action failed or chose distal hand bone");
   plugin.Place(position,Quaternion.identity);plugin.Probe(true);Check(plugin.Inserted,"desktop reinsert blocked after detachment");
   plugin.Place(new Vector3(0,0,-.05f),Quaternion.identity);plugin.Probe(true);Check(!plugin.Inserted&&plugin.Result.Contains("届きません"),"distance rejection not diagnosed");
   plugin.Place(position,Quaternion.Euler(0,90,0));plugin.Probe(true);Check(!plugin.Inserted&&plugin.Result.Contains("方向"),"direction rejection not diagnosed");
   plugin.Place(new Vector3(.0205f,-.02f,-.015f),Quaternion.identity);plugin.Width(0);plugin.Probe(false);Check(!plugin.Inserted&&!plugin.Result.Contains("刺入可能"),"zero width accepted an edge miss");
   plugin.Width(.002f);plugin.Probe(false);Check(plugin.Result.Contains("刺入可能"),"configured needle width failed at mesh edge");plugin.Probe(true);Check(plugin.Inserted,"confirmation and insertion disagree");
   plugin.Place(position,Quaternion.identity);var other=Actor(owned,out first,out last);plugin.Setup(other,first,last);plugin.Probe(true);
   Check(!plugin.Inserted&&plugin.Result.Contains("複数女性"),"overlapping women were not refused");
   Debug.Log("SYRINGE_DESKTOP_PROBE_PRODUCT_TESTS_OK");
  }
  finally{plugin.Cleanup();foreach(var obj in owned)if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
 }
}
