using System;
using CanonGate;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using VRGIN.Controls;
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static Plugin VrSession()
        {
            VR.Active=true; VR.Mode=new(); GateApi.Allowed=true; GateApi.Generation++;
            GateApi.Current=new(){Kind=GateApi.SessionKind.H,Id=(ulong)GateApi.Generation};
            var plugin=new Plugin();plugin._desktopMouse=new RecordingMouse();plugin.Awake();plugin.StartSession();plugin.PlaybackBarPrimaryAction();return plugin;
        }
        static void CheckVr()
        {
            var p=VrSession();var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var axis=SteamVR_Actions.legacy_emulate.Axis0_2D;
            Check(grip.Count==6&&axis.Count==2,"VR session connects only grip edges and stick changes for both hands");
            foreach(var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            {
                var hand=side==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
                var other=side==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
                foreach(var state in new[]{"displaying","paused","capture-error"})
                {
                    p._paused=state=="paused";
                    if(state=="capture-error")p.StopCapture();else if(p._capture==null)p.StartCapture();
                    p._root.transform.rotation=new(){Yaw=37};
                    var before=p._root.transform.position;
                    hand.transform.position=p._panel.transform.TransformPoint(new Vector3(.49f,.4f,0));
                    VR.Mode.Left.Current=null;VR.Mode.Right.Current=null;
                    grip.Down(side);
                    Check(p._grabHand==hand&&p._root.transform.parent==hand.transform&&Math.Abs(p._root.transform.rotation.Yaw-37)<.001f&&(p._root.transform.position-before).sqrMagnitude<.00001f,side+" "+state+": screen edge grab preserves world pose");
                    var old=hand.Current;grip.Up(side);
                    Check(p._grabHand==null&&p._root.transform.parent==p._scene.transform&&old.IsValid&&old.IsInvalidating,side+" "+state+": release fixes position and follows native pending release");
                    grip.Down(side);
                    Check(p._grabHand==hand&&hand.Current!=old&&!old.IsValid&&!hand.Current.IsInvalidating,side+" "+state+": immediate regrab obtains fresh lock");
                    old=hand.Current;grip.Down(side);
                    Check(p._grabHand==hand&&hand.Current!=old&&!old.IsValid,side+" "+state+": missed Up recovered");
                    grip.Up(other);grip.Down(other);
                    Check(p._grabHand==hand,side+" "+state+": other hand cannot steal or release");
                    float width=p._width.Value;int captures=DesktopCapture.Captures;
                    axis.Move(other,1);Check(p._width.Value==width,"nonholding stick cannot resize");
                    axis.Move(side,1);Check(Math.Abs(p._width.Value-width*1.1f)<.0001f,"holding stick up enlarges by 10 percent");
                    Check(Math.Abs(p._panel.transform.localScale.x-p._width.Value)<.0001f&&Math.Abs(p._panel.transform.localScale.y-p._width.Value/p._screenAspect)<.0001f,"size change reaches mesh with aspect preserved in "+state);
                    axis.Move(side,.9f);Check(Math.Abs(p._width.Value-width*1.1f)<.0001f,"stick jitter does not repeat size step");
                    axis.Move(side,0);axis.Move(side,-1);Check(Math.Abs(p._width.Value-width)<.0001f,"stick down shrinks while retaining aspect and hold");
                    Check(p._grabHand==hand&&DesktopCapture.Captures==captures,"resizing does not capture/recreate desktop or release grip");
                    grip.Up(side);p.CompletePendingGrabLocks();
                    Check(!hand.Current.IsValid,"own retiring lock completed on cleanup");
                }
            }
            p.StartCapture();var right=VR.Mode.Right;var s=SteamVR_Input_Sources.RightHand;
            right.transform.position=p._panel.transform.TransformPoint(new Vector3(0,0,0));
            right.Current=new Controller.Lock();var foreign=right.Current;grip.Down(s);
            Check(p._grabHand==null&&foreign.IsValid&&!foreign.IsInvalidating,"unrelated active focus is never stolen");
            foreign.SafeRelease();grip.Down(s);
            Check(p._grabHand==right&&!foreign.IsValid,"native retiring focus can yield to a fresh display grip");
            var pose=right.Tracking;pose.onTrackingChangedEvent(pose,s,ETrackingResult.Uninitialized);
            Check(p._grabHand==null&&pose.onTrackingChangedEvent.GetInvocationList().Length==1&&pose.onConnectedChangedEvent.GetInvocationList().Length==1,"tracking loss removes grab events and retains desktop controller connection events");
            grip.Down(s);pose.onConnectedChangedEvent(pose,s,false);Check(p._grabHand==null,"disconnect releases held display");
            grip.Down(s);grip.SetActive(s,false);Check(p._grabHand==null,"inactive grip action releases display");
            grip.Down(s);var focusEvent=SteamVR_Events.Latest;focusEvent.Emit(false);
            Check(p._grabHand==null&&!right.Current.IsValid,"VR focus loss completes owned focus cleanup");
            grip.Down(s);Check(p._grabHand==null,"unfocused input cannot grab");focusEvent.Emit(true);
            grip.Down(s);p._root.transform.FailSetParent=true;grip.Up(s);
            Check(p._grabHand==null&&right.Current.IsInvalidating,"detach exception cannot retain held state or lock");
            p._root.transform.FailSetParent=false;p.ReleaseDisplayGrab();
            p._root.transform.SetParent(p._scene.transform,true);
            right.transform.position=p._panel.transform.TransformPoint(new Vector3(0,0,0));
            p._root.transform.FailSetParent=true;grip.Down(s);
            Check(p._grabHand==null&&right.Current.IsInvalidating,"failed grab attachment releases newly acquired focus");
            p._root.transform.FailSetParent=false;
            grip.Down(s);p.ShowGizmo();Check(p._grabHand==null&&p._gizmo.IsVisible,"showing gizmo releases VR holder first");
            grip.Down(s);Check(p._grabHand==right&&!p._gizmo.IsVisible,"VR grab hides competing gizmo");
            p._width.Value=20;axis.Move(s,1);Check(p._width.Value==20,"VR size upper bound");
            axis.Move(s,0);p._width.Value=.1f;axis.Move(s,-1);Check(p._width.Value==.1f,"VR size lower bound");
            GateApi.Allowed=false;p.PlaybackBarDelete();
            Check(p._root==null&&p._grabHand==null&&!right.Current.IsValid&&p._pendingGrabLocks.Count==0,"delete while held after revocation clears native focus and display");
            p.StopSession();p.StopSession();Check(grip.Count==0&&axis.Count==0&&!focusEvent.enabled,"repeated stop removes all VR subscriptions");p.OnDestroy();
            grip.FailSecondDown=true;p=VrSession();
            Check(!p._active&&grip.Count==0&&axis.Count==0,"partial VR subscription failure rolls back every handler");
            grip.FailSecondDown=false;p.OnDestroy();VR.Active=false;
        }
    }
}
