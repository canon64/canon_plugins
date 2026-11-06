using System;
using System.Collections.Generic;
using System.IO;
using CanonGate;
using UnityEngine;
namespace MainGameSpiralEyes
{
 public sealed partial class Plugin
 {
  readonly Dictionary<ChaControl,PupilOrbitBindings> _pupilOrbits=new Dictionary<ChaControl,PupilOrbitBindings>();AssetBundle _orbitBundle;Shader _orbitShader;
  bool OrbitAllowed(string owner,ChaControl actor)=>owner==SyringeOwner&&Allowed&&_enabled.Value&&actor!=null&&_actors.Contains(actor);
  internal bool PrepareOrbit(string owner,ChaControl actor)
  {
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_mainThread)throw new InvalidOperationException("Use the Unity main thread");
   if(!OrbitAllowed(owner,actor)||!GateApi.Capture(owner).IsValid)return false;if(_pupilOrbits.ContainsKey(actor))return true;
   try
   {
    if(_orbitBundle==null)_orbitBundle=AssetBundle.LoadFromFile(Path.Combine(Folder,"Resources","pupilorbit.bundle"));
    if(_orbitShader==null&&_orbitBundle!=null)_orbitShader=_orbitBundle.LoadAsset<Shader>("assets/pupilorbit.shader");EnsureTextures();
    if(actor.rendEye==null||actor.rendEye.Length<2)throw new InvalidOperationException("両目のRendererがありません。");
    var orbit=new PupilOrbitBindings(_orbitShader);
    try{for(int i=0;i<actor.rendEye.Length;i++){if(actor.rendEye[i]==null)throw new InvalidOperationException("目Rendererがありません。");orbit.Apply(actor.rendEye[i],i);if(!orbit.HasPupil(actor.rendEye[i]))throw new InvalidOperationException("瞳のMainTexがありません。");}}
    catch{orbit.Dispose();throw;}
    orbit.Unbind(false);_pupilOrbits.Add(actor,orbit);return true;
   }
   catch(Exception ex){Logger.LogWarning("瞳円運動の準備失敗: "+ex.Message);return false;}
  }
  internal bool SetOrbit(string owner,ChaControl actor,float radius,float speed)
  {
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_mainThread)throw new InvalidOperationException("Use the Unity main thread");
   if(!OrbitAllowed(owner,actor)||!_forcedActors.TryGet(actor,owner,out var request)||!request.Permit.IsValid||!_pupilOrbits.TryGetValue(actor,out var orbit)||float.IsNaN(radius)||float.IsInfinity(radius)||float.IsNaN(speed)||float.IsInfinity(speed))return false;
   orbit.Set(Mathf.Clamp(radius,0,.5f),Mathf.Clamp(speed,0,5));SyncFollowOrbit(actor,orbit.Orbit);return true;
  }
  void BindOrbit(ChaControl actor)
  {if(!_pupilOrbits.TryGetValue(actor,out var orbit)||actor.rendEye==null)return;for(int i=0;i<actor.rendEye.Length;i++)orbit.Apply(actor.rendEye[i],i);SyncFollowOrbit(actor,orbit.Orbit);}
  void SyncFollowOrbit(ChaControl actor,Vector4 value){if(actor!=null&&actor.rendEye!=null)foreach(var renderer in actor.rendEye)_followBindings?.SetOrbit(renderer,value);}
  void UnbindOrbit(ChaControl actor){if(_pupilOrbits.TryGetValue(actor,out var orbit))orbit.Unbind();SyncFollowOrbit(actor,Vector4.zero);}
  internal void EndOrbit(string owner,ChaControl actor)
  {if(owner!=SyringeOwner||ReferenceEquals(actor,null)||!_pupilOrbits.TryGetValue(actor,out var orbit))return;_pupilOrbits.Remove(actor);orbit.Dispose();SyncFollowOrbit(actor,Vector4.zero);}
  void UnbindAllOrbits(){foreach(var orbit in _pupilOrbits.Values)orbit.Unbind();}
  void ClearOrbits(){foreach(var orbit in _pupilOrbits.Values)orbit.Dispose();_pupilOrbits.Clear();if(_orbitBundle!=null)_orbitBundle.Unload(false);_orbitBundle=null;_orbitShader=null;}
 }
}
