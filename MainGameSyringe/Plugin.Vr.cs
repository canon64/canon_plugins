using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  SteamVR_Action_Boolean _grip,_trigger;Controller _heldController;Controller.Lock _focus;
  SteamVR_Events.Action _inputFocusEvent,_deviceEvent;bool _vrSubscribed,_vrFocused=true;
  GripLifetime _gripLifetime;
  Vector3 _heldLocalPosition;Quaternion _heldLocalRotation;
  readonly Dictionary<Controller,Controller.Lock> _pendingGripLocks=new Dictionary<Controller,Controller.Lock>();
  static Plugin _vrGripOwner;
  // Queried by the head-grab input only on a grip edge, before either listener wins.
  public static bool HasGripPriority(Controller controller)
  {
   var owner=_vrGripOwner;
   if(owner==null||!owner._vrSubscribed||!owner.Allowed||!owner.InteractionFocused||!owner._vrFocused||owner._model==null||!IsVrActive()||controller==null||controller.Input==null)return false;
   if(controller!=VR.Mode.Left&&controller!=VR.Mode.Right)return false;
   if(owner._policy.HeldBy!=Hand.None)return owner._heldController==controller;
   return owner.WithinGripRange(controller.transform.position);
  }
  static bool IsVrActive()=>VR.Active&&VR.Mode!=null;
  bool InteractionFocused=>IsVrActive()?_vrFocused:_focused;
  static Hand ToHand(SteamVR_Input_Sources source)=>source==SteamVR_Input_Sources.LeftHand?Hand.Left:source==SteamVR_Input_Sources.RightHand?Hand.Right:Hand.None;
  void SubscribeVr()
  {
   if(_vrSubscribed)return;
   _grip=SteamVR_Actions.legacy_emulate.Grip_Press;_trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
   if(_grip==null||_trigger==null)throw new InvalidOperationException("VRのグリップ・トリガー入力イベントを取得できません。");
   _vrSubscribed=true;
   _vrGripOwner=this;
   foreach(var source in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
   {
    _grip.AddOnStateDownListener(GripDown,source);_grip.AddOnStateUpListener(GripUp,source);
    _trigger.AddOnStateDownListener(TriggerDown,source);_trigger.AddOnStateUpListener(TriggerUp,source);
    _grip.AddOnActiveChangeListener(ActiveChanged,source);_trigger.AddOnActiveChangeListener(ActiveChanged,source);
   }
   _vrFocused=true;
   _inputFocusEvent=SteamVR_Events.InputFocusAction(focused=>{_vrFocused=focused;if(!focused)ReleaseGrab();});_inputFocusEvent.enabled=true;
   _deviceEvent=SteamVR_Events.DeviceConnectedAction((index,connected)=>{if(!connected)ReleaseGrab();});_deviceEvent.enabled=true;
   if(_diagnostics.Value)Trace("VRグリップ・トリガーの入力イベントを接続しました。");
  }
  void UnsubscribeVr()
  {
   if(ReferenceEquals(_vrGripOwner,this))_vrGripOwner=null;
   if(_inputFocusEvent!=null)_inputFocusEvent.enabled=false;_inputFocusEvent=null;
   if(_deviceEvent!=null)_deviceEvent.enabled=false;_deviceEvent=null;
   if(_vrSubscribed)
   {
    _vrSubscribed=false;
    foreach(var source in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
    {
     _grip?.RemoveOnStateDownListener(GripDown,source);_grip?.RemoveOnStateUpListener(GripUp,source);
     _trigger?.RemoveOnStateDownListener(TriggerDown,source);_trigger?.RemoveOnStateUpListener(TriggerUp,source);
     _grip?.RemoveOnActiveChangeListener(ActiveChanged,source);_trigger?.RemoveOnActiveChangeListener(ActiveChanged,source);
    }
   }
   _grip=_trigger=null;
   foreach(var pending in _pendingGripLocks)
    if(pending.Key!=null&&pending.Value!=null&&pending.Value.IsValid&&pending.Value.IsInvalidating)pending.Value.Release();
   _pendingGripLocks.Clear();
  }
  void GripDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
  {
   if(!Allowed||!InteractionFocused||!_vrFocused||_model==null||!IsVrActive())
   {if(_diagnostics.Value)Trace("握り入力を受信 / 操作条件外: allowed="+Allowed+" focus="+_focused+" vrFocus="+_vrFocused+" held="+_policy.HeldBy);return;}
   var hand=ToHand(source);if(hand==Hand.None)return;
   if(_policy.HeldBy!=Hand.None){if(_policy.HeldBy!=hand)return;ReleaseGrab();}
   var controller=hand==Hand.Left?VR.Mode.Left:VR.Mode.Right;
   if(controller==null||controller.Input==null){if(_diagnostics.Value)Trace("握り入力を受信 / コントローラー入力なし: "+hand);return;}
   if(!WithinGripRange(controller.transform.position)){if(_diagnostics.Value)Trace("握り入力を受信 / 注射器から距離外: "+hand+" hand="+controller.transform.position+" origin="+_model.transform.position+" radius="+_gripRadius.Value);return;}
   Controller.Lock focus;
   if(!CanonPlugins.Shared.VrItemFocus.TryAcquire(controller,out focus)){if(_diagnostics.Value)Trace("握り入力を受信 / 他の操作が手を使用中: "+hand);return;}
   _pendingGripLocks.Remove(controller);
   if(!_policy.Grab(hand)){focus.SafeRelease();return;}
   _focus=focus;_heldController=controller;
   try
   {
    _gripLifetime=controller.gameObject.AddComponent<GripLifetime>();_gripLifetime.Released=ReleaseGrab;
    _heldLocalPosition=controller.transform.InverseTransformPoint(_model.transform.position);
    _heldLocalRotation=Quaternion.Inverse(controller.transform.rotation)*_model.transform.rotation;
    if(_insertedBone!=null)ArmReturnBoundary(true);
    else{_model.transform.SetParent(controller.transform,true);}
    if(_diagnostics.Value)Trace("握る: "+hand);
   }
   catch(Exception ex)
   {
    EndGrab(true);_status="注射器を握れませんでした。コントローラーの操作権を返しました。";
    if(_diagnostics.Value)Trace("握り開始失敗: "+ex);
   }
  }
  bool WithinGripRange(Vector3 hand)
  {
   // Imported Studio meshes need not be centered on the GameObject origin.
   // Evaluate the visible item only when grip is pressed, never in a polling loop.
   foreach(var renderer in _model.GetComponentsInChildren<Renderer>())
    if(renderer.enabled&&Vector3.Distance(hand,renderer.bounds.ClosestPoint(hand))<=_gripRadius.Value)return true;
   return Vector3.Distance(hand,_model.transform.position)<=_gripRadius.Value;
  }
  void UpdateHeldPose()
  {
   if(_heldController==null||_model==null||_insertedBone!=null)return;
   _model.transform.localRotation=Quaternion.Euler(_gripAngles.Value);
   ApplyModelScale();
   _model.transform.localPosition=_gripPosition.Value-_model.transform.localRotation*Vector3.Scale(_gripCenter,_model.transform.localScale);
   _heldLocalPosition=_model.transform.localPosition;_heldLocalRotation=_model.transform.localRotation;
  }
  void RestoreHeldPose()
  {
   if(_model==null||_heldController==null)return;
   _model.transform.SetParent(_heldController.transform,false);
   ApplyModelScale();_model.transform.localPosition=_heldLocalPosition;_model.transform.localRotation=_heldLocalRotation;
  }
  void GripUp(SteamVR_Action_Boolean action,SteamVR_Input_Sources source){if(_policy.HeldBy==ToHand(source))ReleaseGrab();}
  void TriggerDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
  {
   if(!_policy.TriggerDown(ToHand(source)))return;
   if(!_vrFocused||_heldController==null||_heldController.Input==null||_focus==null||!_focus.IsValid){ReleaseGrab();return;}
   try
   {
    if(!Allowed||!InteractionFocused)return;
    if(InjectionBusy){_status=Refilling?"薬液を補充中です。":"投与動作中です。終了後にもう一度トリガーを押してください。";return;}
    if(_insertedBone==null&&_spent){Refill();return;}
    if(_insertedBone==null){TryInsert(true);return;}
    if(ReturnIfOutside())return;
    Inject();
   }
   catch(Exception ex){EndGrab(true);if(_diagnostics.Value)Trace("トリガー操作失敗: "+ex);}
  }
  void TriggerUp(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)=>_policy.TriggerUp(ToHand(source));
  void ActiveChanged(SteamVR_Action_Boolean action,SteamVR_Input_Sources source,bool active){if(!active&&_policy.HeldBy==ToHand(source))ReleaseGrab();}
  void ReleaseGrab()
  {
   EndGrab(false);
  }
  void EndGrab(bool immediate)
  {
   var lifetime=_gripLifetime;_gripLifetime=null;
   var focus=_focus;_focus=null;
   var held=_heldController;
   _heldController=null;_policy.ResetInput();
   try
   {
    ClearReturnBoundary();
    if(lifetime!=null){lifetime.Released=null;Destroy(lifetime);}
    if(_model!=null&&_model.transform.parent!=null&&_insertedBone==null)_model.transform.SetParent(null,true);
   }
   finally
   {
    // A failed detach must never retain the controller lock.
    if(focus!=null&&focus.IsValid)
    {
     if(immediate||!_vrSubscribed)focus.Release();
     else{focus.SafeRelease();if(held!=null)_pendingGripLocks[held]=focus;}
    }
   }
  }
 }
 public sealed class GripLifetime:MonoBehaviour
 {
  internal Action Released;
  void OnDisable(){var callback=Released;Released=null;callback?.Invoke();}
  void OnDestroy(){var callback=Released;Released=null;callback?.Invoke();}
 }
}
