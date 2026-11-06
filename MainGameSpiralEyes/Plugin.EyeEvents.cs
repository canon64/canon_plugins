using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
namespace MainGameSpiralEyes
{
 public sealed partial class Plugin
 {
  readonly Dictionary<ChaControl,int> _eyeChangeDepth=new Dictionary<ChaControl,int>();
  void InstallEyeHooks()
  {
   var names=new HashSet<string>{"ChangeEyesPtn","ChangeSettingEye","ChangeSettingEyeL","ChangeSettingEyeR","ChangeGagEyesMaterial",
    "InitializeControlFaceAll","InitializeControlFaceObject","ChangeCustomFaceWithoutCustomTexture","SetForegroundEyesAndEyebrow",
    "ChangeSettingEyeScaleWidth","ChangeSettingEyeScaleHeight"};
   foreach(var method in typeof(ChaControl).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
   {
    if(method.Name=="OnDestroy")
    {
     _harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(ActorDestroying)){priority=Priority.First});
     continue;
    }
    if(!names.Contains(method.Name))continue;
    _harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(EyesChanging)){priority=Priority.First},
     finalizer:new HarmonyMethod(typeof(Plugin),nameof(EyesChanged)){priority=Priority.Last});
   }
  }
  void RemoveEyeHooks(){_harmony?.UnpatchSelf();_eyeChangeDepth.Clear();}
  static void ActorDestroying(ChaControl __instance)
  {
   var p=Instance;if(p==null||ReferenceEquals(__instance,null)||!p._actors.Remove(__instance))return;
   p._eyeChangeDepth.Remove(__instance);p._selectionCache.Remove(__instance);
   p.UnbindOrbit(__instance);p.EndOrbit(SyringeOwner,__instance);
   if(__instance.rendEye!=null)foreach(var renderer in __instance.rendEye)p._followBindings?.Restore(renderer);
   bool selected=p._selectedActors.Remove(__instance);
   var owners=new List<string>();
   foreach(var owner in new[]{SyringeOwner,DildoOwner,"canon.maingame.dollmode"})if(p._forcedActors.Remove(__instance,owner))owners.Add(owner);
   foreach(var owner in owners)PublicApi.NotifyOwnerReleased(owner,__instance);
   if(selected||owners.Count>0)PublicApi.NotifyReleased(__instance);
   if(p._selectedActors.Count==0)p.ReleaseEffects();
  }
  static void EyesChanging(ChaControl __instance)
  {
   var p=Instance;
   if(p==null||!p.Allowed||!p._enabled.Value||!p._actors.Contains(__instance)||__instance.rendEye==null)return;
   if(p._eyeChangeDepth.TryGetValue(__instance,out var depth)){p._eyeChangeDepth[__instance]=depth+1;return;}
   p._eyeChangeDepth[__instance]=1;
   p.UnbindOrbit(__instance);
   // Let the game change its base eye without our layer; recapture once the outermost change finishes.
   foreach(var renderer in __instance.rendEye)p._followBindings?.Restore(renderer);
  }
  static Exception EyesChanged(ChaControl __instance,Exception __exception)
  {
   var p=Instance;
   if(p==null||!p._eyeChangeDepth.TryGetValue(__instance,out var depth))return __exception;
   if(depth>1){p._eyeChangeDepth[__instance]=depth-1;return __exception;}
   p._eyeChangeDepth.Remove(__instance);
   if(p.Allowed&&p._enabled.Value&&__instance!=null&&p._actors.Contains(__instance))
    try{p.BindActor(__instance);}catch(Exception ex){p.Logger.LogWarning("SpiralEyes eye-change rebind failed: "+ex.Message);}
   return __exception;
  }
 }
}
