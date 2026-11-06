using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spiral=MainGameSpiralEyes.PublicApi;

namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  readonly Dictionary<ChaControl,Coroutine> _fades=new Dictionary<ChaControl,Coroutine>();
  int _fadeGeneration;
  bool StartFade(ChaControl actor)
  {
   if(!_doses.TryGetValue(actor,out var ledger))_doses.Add(actor,ledger=new DoseLedger());
   ledger.Add(Time.realtimeSinceStartup,_fadeIn.Value,_fadeOut.Value);
   double next;
   try{if(!ApplyDose(actor,ledger,out next))throw new System.InvalidOperationException("瞳の描画要求を適用できません。");}
   catch(System.Exception ex)
   {
    ledger.RemoveLast();if(ledger.Count==0)_doses.Remove(actor);
    else try{ApplyDose(actor,ledger,out var unused);}catch{}
    Logger.LogWarning("注射効果を追加できません: "+ex.Message);return false;
   }
   if(_fades.TryGetValue(actor,out var old)&&old!=null)StopCoroutine(old);
   _fades[actor]=StartCoroutine(FadeSequence(actor,ledger,_fadeGeneration,next));
   return true;
  }
  IEnumerator FadeSequence(ChaControl actor,DoseLedger ledger,int generation,double next)
  {
   // The finite union of recorded dose transitions; no state polling.
   while(!double.IsInfinity(next))
   {
    yield return new WaitForSecondsRealtime((float)System.Math.Max(.001,next-Time.realtimeSinceStartup));
    if(generation!=_fadeGeneration||!Allowed||actor==null||!ApplyDose(actor,ledger,out next))break;
   }
   FinishFade(actor);
  }
  void FinishFade(ChaControl actor)
  {
   _fades.Remove(actor);_doses.Remove(actor);_policy.Release(actor);Spiral.EndOverlay(Guid,actor);
  }
  void CancelFade(ChaControl actor)
  {
   if(_fades.TryGetValue(actor,out var coroutine)){_fades.Remove(actor);if(coroutine!=null)StopCoroutine(coroutine);}
   _doses.Remove(actor);
  }
  void CancelAllFades()
  {
   _fadeGeneration++;
   foreach(var actor in new List<ChaControl>(_fades.Keys))CancelFade(actor);
  }
 }
}
