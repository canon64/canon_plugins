using System;
using System.Collections.Generic;
using CanonPlugins.Shared;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
using Hip=MainGirlHipHijack.Plugin;

namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _grip,_trigger,_detach;
        private DedicatedVrButton _detachButton;
        private SteamVR_Events.Action _focusEvent;
        private Controller _controller;
        private Controller.Lock _focus;
        private SteamVR_Input_Sources _source;
        private SteamVR_Behaviour_Pose _pose;
        private bool _vrFocused=true;
        private readonly Dictionary<Controller,Controller.Lock> _pendingLocks=new Dictionary<Controller,Controller.Lock>();
        private void ConnectInput()
        {
            if (!VR.Active) return;
            if (_grip!=null) return;
            _grip=SteamVR_Actions.legacy_emulate.Grip_Press;
            _trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
            _detachButton=new DedicatedVrButton("handcuffs");
            _detach=_detachButton.Action;
            ConnectElbowAimInput();
            if (_grip==null || _trigger==null || _detach==null) throw new InvalidOperationException("VR grip/trigger/B action unavailable");
            foreach (var s in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            {
                _grip.AddOnStateDownListener(GripDown,s); _grip.AddOnStateUpListener(GripUp,s);
                _grip.AddOnActiveChangeListener(GripActiveChanged,s); _trigger.AddOnStateDownListener(TriggerDown,s);
                _detach.AddOnStateDownListener(DetachDown,s);
            }
            _vrFocused=true;
            _focusEvent=SteamVR_Events.InputFocusAction(f=> { _vrFocused=f; if (!f) ReleaseGrab(); });
            _focusEvent.enabled=true;
        }
        private void DisconnectInput()
        {
            Exception elbowFailure=null;
            try { ReleaseGrab(); }
            finally
            {
                if (_focusEvent!=null) _focusEvent.enabled=false;
                try { DisconnectElbowAimInput(); } catch(Exception ex) { elbowFailure=ex; }
                _focusEvent=null;
                foreach (var s in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
                {
                    _grip?.RemoveOnStateDownListener(GripDown,s); _grip?.RemoveOnStateUpListener(GripUp,s);
                    _grip?.RemoveOnActiveChangeListener(GripActiveChanged,s); _trigger?.RemoveOnStateDownListener(TriggerDown,s);
                    _detach?.RemoveOnStateDownListener(DetachDown,s);
                }
                _grip=null; _trigger=null; _detach=null;
                try { _detachButton?.Dispose(); } finally { _detachButton=null;FinishPendingLocks(); }
                if (elbowFailure!=null) throw elbowFailure;
            }
        }
        private void FinishPendingLocks()
        {
            foreach (var focus in _pendingLocks.Values)
                if (focus!=null && focus.IsValid && focus.IsInvalidating) focus.Release();
            _pendingLocks.Clear();
        }
        private void GripDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if (!CanAct || !_vrFocused || _rig==null || !VR.Active || VR.Mode==null) return;
            if (_controller!=null)
            {
                if (_source!=source) return;
                ReleaseGrab();
            }
            var controller=source==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
            if (controller==null || controller.Input==null || !controller.IsTracking) return;
            int nearest=-1; float distance=.05f*.05f;
            for (int i=0;i<2;i++)
            {
                foreach (var renderer in _parts[i].GetComponentsInChildren<Renderer>())
                {
                    float d=renderer.bounds.SqrDistance(controller.transform.position);
                    if (d<distance) { distance=d; nearest=i; }
                }
            }
            if (nearest<0) { LastResult="手錠から5cm以内でグリップを押してください。"; return; }
            if (!VrItemFocus.TryAcquire(controller,out var focus))
            { LastResult="この手の操作権を取得できません："+VrItemFocus.DescribeCurrent(controller); return; }
            _pendingLocks.Remove(controller);
            _controller=controller; _focus=focus; _source=source;
            try
            {
                if (!_state.TryGrab(nearest)) throw new InvalidOperationException("Handcuff already held");
                DetachGizmo();
                _parts[nearest].SetParent(controller.transform,true);
                ConnectHeldConstraint();
                _pose=controller.Tracking;
                if (_pose!=null) { _pose.onConnectedChangedEvent+=TrackingConnected; _pose.onTrackingChangedEvent+=TrackingChanged; }
                LastResult="手錠"+(nearest==0?"A":"B")+"を保持中";
            }
            catch { ReleaseGrab(); throw; }
        }
        private void GripUp(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        { if (_controller!=null && source==_source) ReleaseGrab(); }
        private void GripActiveChanged(SteamVR_Action_Boolean action,SteamVR_Input_Sources source,bool active)
        { if (!active && _controller!=null && source==_source) ReleaseGrab(); }
        private void TrackingConnected(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,bool connected) { if (!connected) ReleaseGrab(); }
        private void TrackingChanged(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,ETrackingResult tracking)
        { if (tracking!=ETrackingResult.Running_OK) ReleaseGrab(); }
        private void ReleaseGrab()
        {
            Exception evaluationFailure=null;
            try { EvaluateHeldConstraint(); } catch (Exception ex) { evaluationFailure=ex; }
            try { ReleaseCuffElbowAim(); } catch (Exception ex) { if (evaluationFailure==null) evaluationFailure=ex; }
            DisconnectHeldConstraint();
            var controller=_controller; var focus=_focus; var pose=_pose;
            _controller=null; _focus=null; _pose=null; _state.Release();
            try
            {
                if (pose!=null) { pose.onConnectedChangedEvent-=TrackingConnected; pose.onTrackingChangedEvent-=TrackingChanged; }
            }
            finally
            {
                try { FreezeParts(); }
                finally
                {
                    if (focus!=null && focus.IsValid)
                    {
                        focus.SafeRelease();
                        if (controller!=null) _pendingLocks[controller]=focus;
                    }
                }
            }
            if (evaluationFailure!=null) throw evaluationFailure;
        }
        private void DetachDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if (!CanAct || !_vrFocused || _controller==null || source!=_source || _state.Held<0) return;
            if (_focus==null || !_focus.IsValid || !_controller.IsTracking) { ReleaseGrab(); return; }
            int cuff=_state.Held;
            if (_state.Wrist(cuff)<0) return;
            var binding=_bindings[cuff];
            ReleaseCuffElbowAim();
            _bindings[cuff]=null;
            try
            {
                if (binding!=null) { binding.Invalidated-=BindingInvalidated; binding.Dispose(); }
            }
            finally { _state.ClearBinding(cuff); }
            LastResult="手錠"+(cuff==0?"A":"B")+"を手首から解除（保持を継続）";
            PlayAttachmentSound(cuff);
        }

        private void TriggerDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if (!CanAct || !_vrFocused || _controller==null || source!=_source || _state.Held<0) return;
            if (_focus==null || !_focus.IsValid || !_controller.IsTracking) { ReleaseGrab(); return; }
            int cuff=_state.Held;
            if (_state.Wrist(cuff)>=0) return;
            int nearest=-1; float distance=.05f; ChaControl female=null; Transform wrist=null;
            for (int i=0;i<2;i++)
            {
                if (!_state.CanBind(cuff,i) || !Hip.TryGetRuntimeWrist(i==0?"left_hand":"right_hand",out var f,out var w)) continue;
                float d=Vector3.Distance(_parts[cuff].position,w.position);
                if (d<=distance) { distance=d; nearest=i; female=f; wrist=w; }
            }
            if (nearest<0) { LastResult="装着できる手首が5cm以内にありません。"; return; }
            HandcuffMountPose.Calculate(cuff,nearest,wrist.position,wrist.rotation,out var mountPosition,out var mountRotation);
            var body=_parts[cuff]; var beforePosition=body.position; var beforeRotation=body.rotation;
            Hip.RuntimeWristBinding binding=null;
            try
            {
                body.SetPositionAndRotation(mountPosition,mountRotation);
                _wristTargets[cuff].SetPositionAndRotation(wrist.position,wrist.rotation);
                if (!Hip.TryBindRuntimeWrist(Guid+"/"+cuff,female,nearest==0?"left_hand":"right_hand",_wristTargets[cuff],out binding))
                {
                    body.SetPositionAndRotation(beforePosition,beforeRotation);
                    LastResult="手首IKを取得できませんでした。"; return;
                }
                _state.CommitBinding(cuff,nearest); _bindings[cuff]=binding;
                binding.Invalidated+=BindingInvalidated;
                EvaluateHeldConstraint();
                LastResult="手錠"+(cuff==0?"A":"B")+"を"+(nearest==0?"左":"右")+"手首に装着（IK100％）";
            }
            catch
            {
                binding?.Dispose();
                if (body!=null) body.SetPositionAndRotation(beforePosition,beforeRotation);
                throw;
            }
            PlayAttachmentSound(cuff);
        }
    }
}
