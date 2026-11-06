using System.Collections;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  Coroutine _refillFinish;int _refillGeneration;bool Refilling=>_refillFinish!=null;
  void Refill()
  {
   if(!Allowed||!InteractionFocused||_model==null)return;
   if(InjectionBusy){_status=Refilling?"薬液を補充中です。":"投与動作中は補充できません。";return;}
   if(_insertedBone!=null){_status="補充する前に抜いてください。";return;}
   if(!_spent){_status="薬液は満タンです。";return;}
   try{BeginRefillVisuals();_status="ボトルへ刺して補充します。押し子を引く時間は3秒です。";_refillFinish=StartCoroutine(FinishRefill(_model,++_refillGeneration));}
   catch{CancelRefill();throw;}
  }
  bool RefillCurrent(GameObject model,int generation)=>generation==_refillGeneration&&model==_model&&Allowed;
  IEnumerator FinishRefill(GameObject model,int generation)
  {
   yield return new WaitForSeconds(SyringeRefillMotion.DrawStart);if(!RefillCurrent(model,generation))yield break;
   PlayRefillSound();KickRefillWaves();_status="薬液を吸い上げています（押し子の動作3秒）。";
   yield return new WaitForSeconds(SyringeRefillMotion.DrawSeconds);if(!RefillCurrent(model,generation))yield break;
   StopInjectionSound();CompleteRefillDrawing();KickRefillWaves();_status="吸い上げ完了。針をボトルから抜いています。";
   yield return new WaitForSeconds(SyringeRefillMotion.Withdraw+SyringeRefillMotion.Return);if(!RefillCurrent(model,generation))yield break;
   _refillFinish=null;EndRefillVisuals();_status="薬液を補充しました。次は刺入、その次の操作で投与できます。";
  }
  void CancelRefill()
  {++_refillGeneration;if(_refillFinish!=null){StopCoroutine(_refillFinish);StopInjectionSound();_status="補充を中止しました。";}_refillFinish=null;EndRefillVisuals();}
 }
}
