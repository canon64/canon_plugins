using System;
using System.Collections.Generic;
using CanonPlugins.Shared;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        SteamVR_Action_Boolean _grip, _trigger;
        SteamVR_Events.Action _vrFocus;
        Controller _hand;
        Controller.Lock _handLock;
        SteamVR_Behaviour_Pose _pose;
        SteamVR_Input_Sources _handSource;
        readonly List<Controller.Lock> _retiringLocks = new List<Controller.Lock>();
        static readonly SteamVR_Input_Sources[] Hands={SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand};
        void ConnectVr()
        {
            if(!VR.Active)return;
            _grip=SteamVR_Actions.legacy_emulate.Grip_Press;
            _trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
            foreach(var source in Hands)
            {
                _grip.AddOnStateDownListener(GripDown,source);_grip.AddOnStateUpListener(GripUp,source);_grip.AddOnActiveChangeListener(GripActive,source);
                _trigger.AddOnStateDownListener(IgnitionTriggerDown,source);
            }
            _vrFocus=SteamVR_Events.InputFocusAction(OnVrInputFocus);_vrFocus.enabled=true;
        }
        void DisconnectVr()
        {
            try{ReleaseHand();}
            finally
            {
                if(_vrFocus!=null)_vrFocus.enabled=false;_vrFocus=null;
                if(_grip!=null)foreach(var s in Hands)
                { _grip.RemoveOnStateDownListener(GripDown,s);_grip.RemoveOnStateUpListener(GripUp,s);_grip.RemoveOnActiveChangeListener(GripActive,s); }
                if(_trigger!=null)foreach(var s in Hands)_trigger.RemoveOnStateDownListener(IgnitionTriggerDown,s);
                _grip=null;_trigger=null;FinishRetiringLocks();
            }
        }
        void GripDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if(!CanAct||!_focused||_model==null||VR.Mode==null)return;
            if(_hand!=null){if(source!=_handSource)return;ReleaseHand();}
            var hand=source==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:source==SteamVR_Input_Sources.RightHand?VR.Mode.Right:null;
            if(hand==null||!hand.IsTracking||Vector3.Distance(hand.transform.position,_model.Root.transform.position)>.14f)return;
            if(!VrItemFocus.TryAcquire(hand,out var focus)){_status="その手は他の操作が使用中です。";return;}
            _retiringLocks.RemoveAll(l=>l==null||!l.IsValid);
            _hand=hand;_handLock=focus;_handSource=source;
            try
            {
                HideGizmo();_model.Root.transform.SetParent(hand.transform,true);
                _pose=hand.Tracking;
                if(_pose!=null){_pose.onConnectedChangedEvent+=HandConnected;_pose.onTrackingChangedEvent+=HandTracking;}
            }
            catch(Exception ex){ReleaseHand();_status="保持失敗："+ex.Message;}
        }
        void GripUp(SteamVR_Action_Boolean action,SteamVR_Input_Sources source){if(_hand!=null&&source==_handSource)ReleaseHand();}
        void IgnitionTriggerDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if(!CanAct||!_focused||_model==null||_hand==null||source!=_handSource)return;
            if(_handLock==null||!_handLock.IsValid||!_hand.IsTracking){ReleaseHand();return;}
            if(_lit.Value)return;
            if(!CandleFlow.CanIgnite(Vector3.Angle(_model.Root.transform.up,Vector3.up)))
            {_status="135度未満に戻してからトリガーを引いてください。";return;}
            _lit.Value=true;_status="再点火しました。";
        }
        void GripActive(SteamVR_Action_Boolean action,SteamVR_Input_Sources source,bool active){if(!active&&_hand!=null&&source==_handSource)ReleaseHand();}
        void HandConnected(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,bool connected){if(!connected)ReleaseHand();}
        void HandTracking(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,ETrackingResult tracking){if(tracking!=ETrackingResult.Running_OK)ReleaseHand();}
        void ReleaseHand()
        {
            var focus=_handLock;var pose=_pose;_hand=null;_handLock=null;_pose=null;_handSource=SteamVR_Input_Sources.Any;
            try
            {
                if(pose!=null){pose.onConnectedChangedEvent-=HandConnected;pose.onTrackingChangedEvent-=HandTracking;}
                if(_model!=null)_model.Root.transform.SetParent(_scene!=null?_scene.transform:null,true);
            }
            finally
            {
                if(focus!=null&&focus.IsValid)
                { try{focus.SafeRelease();}finally{_retiringLocks.Add(focus);} }
            }
        }
        void FinishRetiringLocks()
        {
            foreach(var focus in _retiringLocks)if(focus!=null&&focus.IsValid&&focus.IsInvalidating)focus.Release();
            _retiringLocks.Clear();
        }
    }
}
