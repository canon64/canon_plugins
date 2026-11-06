using Bindings = CanonPlugins.Shared.PluginInput;
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace MainGameVMDPlay {
 public sealed partial class Plugin {
  private ConfigEntry<int> _clothingTarget;
  private Harmony _clothingHarmony;
  private float _clothingRetryAt;
  private static readonly string[] ClothesKeys={"Top","Bottom","Bra","Pants","Gloves","Pantyhose","Socks","Shoes","Wear","Naked","Shift","Coordinate","CoordinateB"};
  private readonly ConfigEntry<KeyboardShortcut>[] _clothesEntries=new ConfigEntry<KeyboardShortcut>[13];
  private readonly string[] _clothesFallbacks=new string[13];
  private readonly EventHandler[] _clothesChanged=new EventHandler[13];
  private static readonly string[] ClothesActionIds=Array.ConvertAll(ClothesKeys,k=>"MainGameVMDPlay.clothes."+k);
  private static readonly FieldInfo HFemales=AccessTools.Field(typeof(HSceneProc),"lstFemale");
  private void InitClothingShortcuts() {
   _clothingTarget=Config.Bind("Clothing shortcuts","Target",1,new ConfigDescription("0=呼び出しキャラ 1=メインキャラ 2=両方",new AcceptableValueRange<int>(0,2)));
   EnsureClothingShortcuts();
  }
  private void EnsureClothingShortcuts() {
   if(_clothingHarmony!=null || UnityEngine.Time.unscaledTime < _clothingRetryAt) return;
   _clothingRetryAt=UnityEngine.Time.unscaledTime+3;
   if(!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("KKS_GirlsClothesChange",out var info) || info.Instance==null) return;
   var t=info.Instance.GetType();
   var update=t==null?null:AccessTools.Method(t,"Update");
   if(update==null) return;
   for(int i=0;i<13;i++) {
    var field=AccessTools.Field(t,ClothesKeys[i]);
    _clothesEntries[i]=field?.GetValue(info.Instance) as ConfigEntry<KeyboardShortcut>;
    if(_clothesEntries[i]==null) { LogWarn("Clothing shortcut contract mismatch"); return; }
   }
   for(int i=0;i<13;i++) {
    int slot=i;
    _clothesFallbacks[i]=_clothesEntries[i].Value.ToString();
    _clothesChanged[i]=(sender,args)=>_clothesFallbacks[slot]=_clothesEntries[slot].Value.ToString();
    _clothesEntries[i].SettingChanged+=_clothesChanged[i];
   }
   _clothingHarmony=new Harmony(Guid+".clothing");
   _clothingHarmony.Patch(update,prefix:new HarmonyMethod(typeof(Plugin),nameof(ClothesUpdatePrefix)));
   LogInfo("[Clothing] GirlsClothesChange target routing installed");
  }
  private void DisposeClothingShortcuts() {
   for(int i=0;i<13;i++) {
    if(_clothesEntries[i]!=null && _clothesChanged[i]!=null) _clothesEntries[i].SettingChanged-=_clothesChanged[i];
    _clothesEntries[i]=null; _clothesChanged[i]=null; _clothesFallbacks[i]=null;
   }
   _clothingHarmony?.UnpatchSelf(); _clothingHarmony=null;
  }
  public static int GetClothingTarget() => Instance?._clothingTarget?.Value ?? 1;
  public static void SetClothingTarget(int value) { if(RoomAllowed && Instance?._clothingTarget!=null) Instance._clothingTarget.Value=Mathf.Clamp(value,0,2); }
  private static bool ClothesUpdatePrefix(object __instance) {
   if(!RoomAllowed) return true;
   var p=Instance;
   if(p==null || !p._insideHScene || p._hSceneProc==null || !p._cfgEnabled.Value) return true;
   if(GUIUtility.keyboardControl!=0) return false;
   try {
    for(int i=0;i<13;i++) {
     if(p._clothesEntries[i]==null || !Bindings.Down(ClothesActionIds[i], p._clothesFallbacks[i])) continue;
     var actor=p.ReadyIndependentActor();
     var targets=ClothingTargets.Capture(p._clothingTarget.Value,p._hSceneProc.lstFemale,p.AstralBlocked?null:actor);
     foreach(var cha in targets) {
      if(cha==null) continue;
      if(i<7) cha.SetClothesStateNext(i);
      else if(i==7) cha.SetClothesState(8,(byte)(cha.fileStatus.clothesState[8]==0?1:0));
      else if(i==8) cha.SetClothesStateAll(0);
      else if(i==9) cha.SetClothesStateAll(3);
      else if(i==10) { cha.SetClothesStateAll(1); cha.SetClothesState(6,0); }
      else {
       int count=cha.chaFile.coordinate.Length; if(count==0) continue;
       int slot=(cha.fileStatus.coordinateType+(i==11?-1:1)+count)%count;
       if(ReferenceEquals(cha,actor)) p.ApplyActorCoordinate(null,slot,cha);
       else cha.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)slot);
      }
     }
    }
   } catch(Exception ex) { p.LogError("衣装ショートカット: "+ex.Message); }
   return false;
  }
 }
}

