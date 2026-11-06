using System;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  float _injectionBusyUntil;
  bool InjectionBusy=>_model!=null&&(Refilling||Time.realtimeSinceStartup<_injectionBusyUntil);
  public string GetPrimaryActionHint()
  {
   if(_vr)return _model==null?"クリック：呼び出す / 再クリック：削除":"クリック：削除 / グリップ：握る / トリガー：刺入→投与、抜いた空の注射器は補充";
   switch(PrimaryActionPolicy.Decide(_model!=null,_insertedBone!=null,_spent,InjectionBusy))
   {
    case SyringeAction.Spawn:return "左クリック：呼び出す / 右クリック：削除";
    case SyringeAction.Wait:return (Refilling?"薬液を補充中です":"投与動作中です")+" / 右クリック：削除";
    case SyringeAction.Withdraw:return "左クリック：抜く / 右クリック：削除";
    case SyringeAction.Refill:return "左クリック：補充する / 右クリック：削除";
    case SyringeAction.Inject:return "左クリック：投与する / 右クリック：削除";
    default:return "左クリック：今の位置で刺す（投与は次のクリック） / 右クリック：削除";
   }
  }
  public void PrimaryAction()
  {
   try
   {
    if(!Allowed||!InteractionFocused){_status="現在は注射器を操作できません（H許可・有効設定・フォーカスを確認）。";return;}
    if(_vr){if(_model==null)Activate();else DeleteSyringe();return;}
    switch(PrimaryActionPolicy.Decide(_model!=null,_insertedBone!=null,_spent,InjectionBusy))
    {
     case SyringeAction.Wait:_status=(Refilling?"薬液を補充中です。":"投与動作中です。")+"終了後にもう一度押してください。";return;
     case SyringeAction.Spawn:Activate();return;
     case SyringeAction.Withdraw:
      WithdrawNeedle();_status="抜きました。次の「注射器」で補充します。";return;
     case SyringeAction.Refill:
      Refill();return;
     case SyringeAction.Insert:
      TryInsert(true);
      if(_insertedBone==null){_status="刺入できません："+_probeReport;return;}
      _status="刺入しました。次の「注射器」で投与します。";return;
     case SyringeAction.Inject:Inject();return;
    }
   }
   catch(Exception ex){_status="注射器の操作に失敗しました："+ex.Message;Logger.LogWarning(_status);}
   finally{Trace("基本ボタン / "+_status);}
  }
  public void DeleteSyringe()
  {
   Despawn();_status="注射器を削除しました。投与済みの瞳効果は継続します。";Trace("右クリック削除 / "+_status);
  }
 }
}
