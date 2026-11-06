using System;
using UnityEngine;
namespace MainGameHScenePoseInjector {
 public sealed partial class Plugin {
  [Serializable] private sealed class HotelEffectState {
   public bool[] manual, hasDesired; public int[] desired;
  }
  public static string CaptureHotelCharacterEffects(ChaControl cha) {
   if(!CanonPlugins.Shared.PluginSessionGate.Allowed || Instance==null || cha==null) throw new InvalidOperationException("肌効果の保存は許可されたH内で行ってください。");
   Instance.RefreshEffectScene();
   var target=Instance.StateFor(cha);
   var state=new HotelEffectState {manual=(bool[])target.ManualOverride.Clone(),hasDesired=new bool[EffectChannelCount],desired=new int[EffectChannelCount]};
   for(int i=default(int);i<EffectChannelCount;++i) { state.hasDesired[i]=target.Desired[i].HasValue; state.desired[i]=target.Desired[i].GetValueOrDefault(); }
   return JsonUtility.ToJson(state);
  }
  public static void RestoreHotelCharacterEffects(ChaControl cha,string json) {
   if(!CanonPlugins.Shared.PluginSessionGate.Allowed || Instance==null || cha==null) throw new InvalidOperationException("肌効果の復元は許可されたH内で行ってください。");
   var saved=JsonUtility.FromJson<HotelEffectState>(json);
   if(saved?.manual?.Length!=EffectChannelCount || saved.hasDesired?.Length!=EffectChannelCount || saved.desired?.Length!=EffectChannelCount) throw new ArgumentException("肌効果の制御配列が不正です。");
   Instance.RefreshEffectScene();
   var target=Instance.StateFor(cha);
   for(int i=default(int);i<EffectChannelCount;++i) {
    target.ManualOverride[i]=saved.manual[i];target.Desired[i]=saved.hasDesired[i]?(int?)saved.desired[i]:null;
    if(target.Desired[i].HasValue) Instance.WriteEffect(cha,i,target.Desired[i].Value);
   }
  }
  private sealed class EffectSuspension {
   internal System.Collections.Generic.Dictionary<int,EffectTarget> Targets;
   internal int? Tears; internal int? Hoho;
   internal AutoCycleSettings Auto;
   internal CanonGate.GateApi.Permit Permit;
   internal int Selected; internal bool[] Enabled=new bool[EffectChannelCount]; internal int[] Manual=new int[EffectChannelCount],Index=new int[EffectChannelCount]; internal float[] Remaining=new float[EffectChannelCount];
  }
  private EffectSuspension _sessionEffectSuspension;
  public static object SuspendEffectsForAstral() {
   if(!CanonPlugins.Shared.PluginSessionGate.Allowed) return null;
   var p=Instance; if(p==null) return null;
   if(p._sessionEffectSuspension!=null) return p._sessionEffectSuspension;
   var s=new EffectSuspension {Permit=CanonPlugins.Shared.PluginSessionGate.Capture(),Tears=p._desiredTears,Hoho=p._desiredHohoAka,Auto=p.Auto.SuspensionCopy(),Targets=new System.Collections.Generic.Dictionary<int,EffectTarget>(p._effectTargets),Selected=p._selectedEffectCharacter};
   p._settings.AutoCycle=p.Auto.SuspensionCopy();
   p._sessionEffectSuspension=s;
   for(int i=0;i<EffectChannelCount;i++) {
    var ch=p.ChannelSettings(p.Auto,i); s.Enabled[i]=ch.Enabled; s.Manual[i]=ch.ManualValue;
    s.Index[i]=p._effectClocks[i].Index; s.Remaining[i]=Mathf.Max(0,p._effectClocks[i].NextAt-Time.unscaledTime);
    ch.Enabled=false;
   }
   p._desiredHohoAka=null; p._desiredTears=null;
   p._effectTargets.Clear(); p._selectedEffectCharacter=0; return s;
  }
  public static void RestoreEffectsFromAstral(object snapshot) {
   if(!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
   var p=Instance; var s=snapshot as EffectSuspension; if(p==null || s==null || !s.Permit.IsValid || !ReferenceEquals(p._sessionEffectSuspension,s)) return;
   p._sessionEffectSuspension=null;
   p._settings.AutoCycle=s.Auto; p._desiredTears=s.Tears; p._desiredHohoAka=s.Hoho;
   p._effectTargets.Clear();
   foreach(var pair in s.Targets) if(pair.Value.Cha!=null) p._effectTargets[pair.Key]=pair.Value;
   p._selectedEffectCharacter=s.Selected;
   for(int i=0;i<EffectChannelCount;i++) {
    var ch=p.ChannelSettings(p.Auto,i); ch.Enabled=s.Enabled[i]; ch.ManualValue=s.Manual[i];
    p._effectClocks[i].Index=s.Index[i]; p._effectClocks[i].NextAt=Time.unscaledTime+s.Remaining[i];
   }
  }
  public static bool SetCharacterEffect(ChaControl cha, int channel, int value) {
   if(!CanonPlugins.Shared.PluginSessionGate.Allowed) return false;
   var p=Instance; if(p==null || cha==null || channel<0 || channel>=EffectChannelCount) return false;
   p.RefreshEffectScene();
   value=Mathf.Clamp(value,0,p.ChannelMax(channel));
   p.StateFor(cha).Desired[channel]=value; p.StateFor(cha).ManualOverride[channel]=true;
   if(p.Auto!=null && p._selectedEffectCharacter==cha.GetInstanceID()) { p.ChannelSettings(p.Auto,channel).Enabled=false; p.ChannelSettings(p.Auto,channel).ManualValue=value; p.SaveSettings(); }
   p.WriteEffect(cha,channel,value); return true;
  }
  public static int GetCharacterEffect(ChaControl cha,int channel) => Instance!=null && cha!=null && channel>=0 && channel<EffectChannelCount ? Instance.ReadEffect(cha,channel) : -1;
 }
}
