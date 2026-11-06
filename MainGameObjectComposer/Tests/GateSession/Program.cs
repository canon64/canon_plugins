using System;
using System.Collections.Generic;
using CanonPlugins.Local;
using MainGameObjectComposer;
namespace UnityEngine { public class MonoBehaviour { public bool enabled; } }
namespace BepInEx { public class BaseUnityPlugin : UnityEngine.MonoBehaviour {} }
public class ChaControl {}
public class HSceneProc : UnityEngine.MonoBehaviour { public List<ChaControl> lstFemale = new List<ChaControl>(); }
public class VrScene : UnityEngine.MonoBehaviour { public List<ChaControl> lstFemale = new List<ChaControl>(); }
namespace CanonGate {
 public static class GateApi {
  public enum SessionKind { H, Maker }
  public sealed class Context { public SessionKind Kind; public ulong Id; public UnityEngine.MonoBehaviour Scene; }
  public static Context Current; public static bool Granted; public static Action Start, End; public static int Registrations;
  public struct Permit { internal ulong id; public bool IsValid => Granted && Current != null && id == Current.Id; }
  public static Permit Capture(string guid) => new Permit { id = Current.Id };
  public static IDisposable Register(string guid, Action start, Action end) { Registrations++; Start=start; End=end; return new Token(); }
  private sealed class Token : IDisposable { public void Dispose() { Start=End=null; } }
 }
}
class Program {
 static int count; static void Check(bool ok,string label) { if(!ok) throw new Exception(label); count++; }
 static void Main() {
  var otherPose=new PresetEntry{key="aibu/1/O",displayName="previous"};
  var currentPose=new PresetEntry{key="sonyu/4/S",displayName="current"};
  var weakPose=new PresetEntry{key="sonyu/4/W",displayName="weak"};
  var choices=new[]{otherPose,currentPose,weakPose};
  Check(PresetOpenSelection.Choose(choices,"sonyu/4/S",otherPose,otherPose.key,otherPose.displayName)==currentPose,"opening chooses current pose instead of remembered unrelated preset");
  Check(PresetOpenSelection.Choose(choices,"sonyu/4/S",weakPose,weakPose.key,weakPose.displayName)==currentPose,"exact strength wins over another active strength");
  Check(PresetOpenSelection.Choose(choices,"sonyu/9/S",otherPose,otherPose.key,otherPose.displayName)==null,"unbound pose does not open arbitrary first preset");
  Check(PresetOpenSelection.Choose(choices,null,otherPose,otherPose.key,otherPose.displayName)==null,"unavailable current pose does not load previous preset");
  currentPose.applyToS=false;weakPose.applyToS=false;
  Check(PresetOpenSelection.Choose(choices,"sonyu/4/S",currentPose,currentPose.key,currentPose.displayName)==null,"opening respects strength permission");
  currentPose.applyToS=true;currentPose.autoLoadEligible=false;
  Check(PresetOpenSelection.Choose(choices,"sonyu/4/S",null,null,null)==currentPose,"manual opening can select bound preset with automatic switching disabled");
  Check(PresetOpenSelection.Choose(choices,"sonyu/4/W",null,null,null)==weakPose,"weak pose loads weak preset");
  var samePose=new PresetEntry{key="sonyu/4/S",displayName="current-2"};
  Check(PresetOpenSelection.Choose(new[]{currentPose,samePose},"sonyu/4/S",samePose,null,null)==samePose,"multiple bindings retain matching active choice");
  Check(PresetOpenSelection.Choose(new[]{currentPose,samePose},"sonyu/4/S",null,samePose.key,samePose.displayName)==samePose,"remembered choice only wins within current pose");
  var owner=new BepInEx.BaseUnityPlugin(); int starts=0,ends=0;
  SessionGate.Bind(owner,"test",()=>starts++,()=>{ Check(!SessionGate.Allowed,"invalid before cleanup"); ends++; });
  Check(!owner.enabled && !SessionGate.Allowed,"denied before grant");
  try { SessionGate.Bind(owner,"test",()=>{},()=>{}); throw new Exception("duplicate accepted"); } catch(InvalidOperationException) { count++; }
  CanonGate.GateApi.Current=new CanonGate.GateApi.Context {Kind=CanonGate.GateApi.SessionKind.Maker,Id=1,Scene=new HSceneProc()};
  CanonGate.GateApi.Start(); Check(starts==0,"non-H rejected");
  var scene=new HSceneProc(); var girl=new ChaControl(); scene.lstFemale.Add(girl);
  CanonGate.GateApi.Current=new CanonGate.GateApi.Context {Kind=CanonGate.GateApi.SessionKind.H,Id=2,Scene=scene}; CanonGate.GateApi.Granted=true;
  CanonGate.GateApi.Start(); var permit=SessionGate.Capture(); var generation=SessionGate.Generation;
  Check(SessionGate.Allowed && starts==1 && owner.enabled,"authorized native start");
  Check(SessionGate.Actors.Length==1 && SessionGate.Actors[0]==girl && SessionGate.HScene==scene,"cached native actors");
  CanonGate.GateApi.Start(); Check(starts==1,"duplicate start ignored");
  CanonGate.GateApi.Granted=false; Check(!SessionGate.Allowed && !permit.IsValid,"permit revocation rejects operations immediately");
  CanonGate.GateApi.End(); CanonGate.GateApi.End();
  Check(ends==1 && !owner.enabled && SessionGate.Scene==null && SessionGate.Actors.Length==0,"idempotent end clears references");
  Check(SessionGate.Generation!=generation,"old delayed generation invalidated");
  var vr=new VrScene(); vr.lstFemale.Add(girl); CanonGate.GateApi.Current=new CanonGate.GateApi.Context {Kind=CanonGate.GateApi.SessionKind.H,Id=3,Scene=vr}; CanonGate.GateApi.Granted=true;
  CanonGate.GateApi.Start(); Check(SessionGate.Allowed && SessionGate.HScene==null && SessionGate.Scene==vr && SessionGate.Actors[0]==girl,"VR scene distinct and authorized");
  Check(!permit.IsValid,"old permit cannot cross sessions"); SessionGate.Dispose(); Check(ends==2 && CanonGate.GateApi.Start==null,"dispose unregisters");
  SessionGate.Bind(owner,"test",()=>throw new Exception("start failed"),()=>ends++);
  try { CanonGate.GateApi.Start(); } catch(Exception) {}
  Check(!SessionGate.Allowed && !owner.enabled && SessionGate.Scene==null && ends==3,"partial start cleanup"); SessionGate.Dispose();
  int cleanups=0; try { SessionGate.Cleanup(()=>throw new Exception(),()=>cleanups++); } catch(Exception) {}
  Check(cleanups==1,"cleanup failure does not skip remaining cleanup");
  var view=new PresetEditorState();view.Open();Check(view.Blend(.6f)==0,"L1 edit endpoint");
  view.Select(2);view.Select(0);Check(view.Blend(.6f)==1,"both edit retains L2 endpoint");
  var authoredView=view.Capture();
  var serializedView=System.Text.Json.JsonSerializer.Serialize(authoredView,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
  var restoredView=System.Text.Json.JsonSerializer.Deserialize<PresetEditorSnapshot>(serializedView,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
  var otherView=new PresetEditorState();otherView.Restore(restoredView);
  Check(otherView.EditSlot==0 && otherView.FixedSlot==2,"saved edit destination survives switching");
  otherView.Restore(null);Check(otherView.EditSlot==1 && otherView.FixedSlot==1,"old presets keep compatible edit destination");
  var original=new object();var candidate=new object();var list=new List<object>{original};
  var standing=new PresetEntry{key="aibu/0/O",displayName="test2",animationName="立ち愛撫"};
  var rebound=new PresetEntry{key=standing.key,displayName=standing.displayName};
  rebound.loop1Json.Add("L1 pose");rebound.loop2Json.Add("L2 pose");rebound.ikLinks.Add("body\tobject-1");
  PresetConditionBinding.Bind(rebound,"sonyu/99/S",(p,key)=>{p.animationName="押し付けバック";p.modeDisplayName="sonyu";p.animationId=99;});
  Check(rebound.key=="sonyu/99/S"&&rebound.animationName=="押し付けバック","saving a reused preset binds key and condition metadata to the current pose");
  Check(standing.key=="aibu/0/O"&&standing.animationName=="立ち愛撫","candidate preparation leaves the old source unchanged");
  Check(rebound.loop1Json[0]=="L1 pose"&&rebound.loop2Json[0]=="L2 pose"&&rebound.ikLinks[0]=="body\tobject-1","rebinding preserves both endpoints and IK links");
  var bindingList=new List<PresetEntry>{standing};
  Check(!PresetWriteTransaction.Commit(bindingList,standing,rebound,false,()=>false)&&bindingList[0].key=="aibu/0/O","failed save preserves the old binding");
  Check(PresetWriteTransaction.Commit(bindingList,standing,rebound,false,()=>true)&&bindingList[0].key=="sonyu/99/S","successful overwrite adopts the current pose binding");
  bool badKeyRejected=false;try{PresetConditionBinding.Bind(new PresetEntry(),"",(p,key)=>{});}catch(InvalidOperationException){badKeyRejected=true;}
  Check(badKeyRejected,"unavailable current pose cannot silently retain an old binding");
  Check(!PresetWriteTransaction.Commit(list,original,candidate,false,()=>false) && list[0]==original,"failed overwrite preserves original");
  try { PresetWriteTransaction.Commit(list,original,candidate,true,()=>throw new Exception()); } catch(Exception) {}
  Check(list.Count==1 && list[0]==original,"failed save-as removes candidate");
  Check(PresetWriteTransaction.Commit(list,original,candidate,true,()=>true) && list.Count==2 && list[0]==original,"save-as preserves source");
  Console.WriteLine("PASS "+count+" gate and editor lifecycle checks (stubbed engine/gate boundary)");
 }
}
