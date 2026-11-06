using System;
using System.Collections.Generic;
using CanonGate;

namespace MainGameSpiralEyes
{
 public static class PublicApi
 {
  public static bool PreparePupilOrbit(string owner,ChaControl actor)=>Plugin.Instance!=null&&Plugin.Instance.PrepareOrbit(owner,actor);
  public static bool SetPupilOrbit(string owner,ChaControl actor,float radius,float speed)=>Plugin.Instance!=null&&Plugin.Instance.SetOrbit(owner,actor,radius,speed);
  public static void EndPupilOrbit(string owner,ChaControl actor){if(Plugin.Instance!=null)Plugin.Instance.EndOrbit(owner,actor);}
  public static event Action<ChaControl> OverlayReleased;
  public static event Action<string,ChaControl> OwnerOverlayReleased;
  public static bool BeginOverlay(string owner, ChaControl actor)
   => Plugin.Instance != null && Plugin.Instance.BeginForcedOverlay(owner, actor);
  public static bool BeginPositionedOverlay(string owner,ChaControl actor,float x,float y)
   => Plugin.Instance!=null&&Plugin.Instance.BeginForcedOverlay(owner,actor,1,x,y);
  public static bool BeginFadingOverlay(string owner,ChaControl actor)
   => Plugin.Instance!=null&&Plugin.Instance.BeginForcedOverlay(owner,actor,0);
  public static bool SetOverlayOpacity(string owner,ChaControl actor,int level)
   => Plugin.Instance!=null&&Plugin.Instance.SetForcedOpacity(owner,actor,level);
  // Cleanup remains available after either gate permit has been revoked.
  public static void EndOverlay(string owner, ChaControl actor)
  { if(Plugin.Instance != null)Plugin.Instance.EndForcedOverlay(owner, actor); }
  internal static void NotifyReleased(ChaControl actor)
  {
   if(OverlayReleased==null)return;
   foreach(Action<ChaControl> callback in OverlayReleased.GetInvocationList())
    try{callback(actor);}catch(Exception ex){UnityEngine.Debug.LogWarning("SpiralEyes release callback: "+ex.Message);}
  }
  internal static void NotifyOwnerReleased(string owner,ChaControl actor)
  {
   var callbacks=OwnerOverlayReleased;if(callbacks==null)return;
   foreach(Action<string,ChaControl> callback in callbacks.GetInvocationList())
    try{callback(owner,actor);}catch(Exception ex){UnityEngine.Debug.LogWarning("SpiralEyes owner release callback: "+ex.Message);}
  }
 }

 public sealed partial class Plugin
 {
  const string DildoOwner="com.kks.maingame.vrdildo";
  const string SyringeOwner="com.kks.maingame.syringe";
  static bool KnownOwner(string owner)=>owner==DildoOwner||owner==SyringeOwner||owner=="canon.maingame.dollmode";
  struct OverlayRequest {public GateApi.Permit Permit;public float Opacity;public UnityEngine.Vector2 Position;}
  readonly OverlayOwners<ChaControl,OverlayRequest> _forcedActors=new OverlayOwners<ChaControl,OverlayRequest>();
  internal bool BeginForcedOverlay(string owner,ChaControl actor,float opacity=1,float x=0,float y=0)
  {
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_mainThread)
    throw new InvalidOperationException("Use the Unity main thread");
   if(!KnownOwner(owner)||!Allowed||!_enabled.Value||actor==null||!_actors.Contains(actor))return false;
   var permit=GateApi.Capture(owner);
   if(!permit.IsValid)return false;
   if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y))return false;
   bool added=_forcedActors.Add(actor,owner,new OverlayRequest{Permit=permit,Opacity=opacity,Position=new UnityEngine.Vector2(UnityEngine.Mathf.Clamp(x,-.5f,.5f),UnityEngine.Mathf.Clamp(y,-.5f,.5f))});
   try
   {
    BindActor(actor);
    bool bound=actor.rendEye!=null&&actor.rendEye.Length>=2;
    if(actor.rendEye!=null)foreach(var renderer in actor.rendEye)bound&=renderer!=null&&_followBindings!=null&&_followBindings.HasBinding(renderer);
    if(!bound){EndForcedOverlay(owner,actor);return false;}
    return true;
   }
   catch(Exception ex)
   {
    EndForcedOverlay(owner,actor);
    Logger.LogWarning("SpiralEyes temporary overlay failed: "+ex.Message);
    return false;
   }
  }
  bool HasForcedOverlay(ChaControl actor)
   => _forcedActors.Has(actor,request=>request.Permit.IsValid);
  bool HasDollLayer(ChaControl actor)=>_forcedActors.TryGet(actor,"canon.maingame.dollmode",out var r)&&r.Permit.IsValid;
  UnityEngine.Vector2 DollLayerPosition(ChaControl actor)=>_forcedActors.TryGet(actor,"canon.maingame.dollmode",out var r)?r.Position:UnityEngine.Vector2.zero;
  float EffectiveOpacity(ChaControl actor)=>IsSpiralSelected(actor)?1:_forcedActors.Maximum(actor,r=>r.Permit.IsValid,r=>r.Opacity);
  void ApplyActorOpacity(ChaControl actor)
  {
   if(actor==null||actor.rendEye==null)return;
   float opacity=EffectiveOpacity(actor);
   foreach(var renderer in actor.rendEye)_followBindings?.SetOpacity(renderer,opacity);
  }
  internal bool SetForcedOpacity(string owner,ChaControl actor,int level)
  {
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_mainThread)throw new InvalidOperationException("Use the Unity main thread");
   if(!KnownOwner(owner)||!Allowed||!_enabled.Value||actor==null||!_forcedActors.TryGet(actor,owner,out var request)||!request.Permit.IsValid)return false;
   request.Opacity=Math.Max(0,Math.Min(100,level))/100f;
   // A scheduled opacity change also binds the current eye material. Expression changes
   // may have replaced the renderer/material since the previous step; retain this request.
   _forcedActors.Add(actor,owner,request);BindActor(actor);return true;
  }
  internal void EndForcedOverlay(string owner,ChaControl actor)
  {
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_mainThread)
    throw new InvalidOperationException("Use the Unity main thread");
   if(!KnownOwner(owner)||ReferenceEquals(actor,null)||!_forcedActors.Remove(actor,owner))return;
   EndOrbit(owner,actor);
   PublicApi.NotifyOwnerReleased(owner,actor);
   if(HasForcedOverlay(actor)){BindActor(actor);return;}
   UnbindOrbit(actor);
   if(actor!=null&&actor.rendEye!=null)
    foreach(var renderer in actor.rendEye)_followBindings?.Restore(renderer);
   _selectedActors.Remove(actor);
   PublicApi.NotifyReleased(actor);
   if(Allowed&&_enabled.Value&&actor!=null)BindActor(actor);
   if(_selectedActors.Count==0)ReleaseEffects();
  }
  void ClearForcedOverlays()
  {
   ClearOrbits();
   var removed=_forcedActors.Clear();var actors=new HashSet<ChaControl>();
   foreach(var pair in removed){actors.Add(pair.Key);PublicApi.NotifyOwnerReleased(pair.Value,pair.Key);}
   foreach(var actor in actors)PublicApi.NotifyReleased(actor);
  }
 }
}
