using System;
using System.Collections.Generic;
using CanonGate;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        sealed class RecordingMouse : IDesktopMouse
        {
            internal readonly List<string> Events=new();internal bool Left,Right,Fail;
            public void RefreshBounds(){}
            public void Move(int x,int y){if(Fail)throw new Exception("injected mouse failure");Events.Add("move "+x+" "+y);}
            public void Button(bool right,bool down){if(right)Right=down;else Left=down;Events.Add((right?"right ":"left ")+down);}
            public void Scroll(int x,int y){Events.Add("scroll "+x+" "+y);}
            public void ReleaseAll(){Left=Right=false;Events.Add("release");}
        }
        static void CheckMouse()
        {
            var p=VrSession();var mouse=new RecordingMouse();p._desktopMouse=mouse;
            var hand=VR.Mode.Right;hand.transform.position=p._panelTransform.position+new Vector3(0,0,-.4f);
            hand.transform.forward=new Vector3(0,0,1);
            var keys=p._mouseButtons;
            Check(!keys.Enabled&&!p._mouseRay.Active&&p._pointerDriver.enabled,"display enables proximity driver, never an always-on line");
            p.TickDesktopPointer();
            Check(keys.Enabled&&p._mouseRay.Active,"valid front hit within 50cm enables pointer input");
            Check(p.AimDesktop(out int px,out int py)&&px==960&&py==540,"front ray maps screen centre to monitor pixels");
            var bounds=new DesktopCapture.Bounds{Left=-1920,Top=-1080,Right=0,Bottom=0};
            MapDesktopPoint(bounds,0,0,out px,out py);Check(px==-1920&&py==-1080,"desktop mapping supports negative monitor coordinates");
            MapDesktopPoint(bounds,1,1,out px,out py);Check(px==-1&&py==-1,"UV upper corner maps inside exclusive monitor bounds");
            Check(WindowsDesktopMouse.InputSize==(IntPtr.Size==8?40:28),"native INPUT structure uses the correct process alignment");
            Check(WindowsDesktopMouse.Normalize(-1920,-1920,3840)==0&&WindowsDesktopMouse.Normalize(1919,-1920,3840)==65535,"absolute mouse coordinates span entire virtual desktop");
            keys.Button(false,true);Check(mouse.Left&&!mouse.Right&&p._mouseLock.IsValid,"physical A path sends left Down with owned controller focus");
            hand.transform.position+=new Vector3(.2f,0,0);p.TickDesktopPointer();
            Check(mouse.Left&&mouse.Events.Exists(s=>s.StartsWith("move 1279 ")),"A drag follows the moving hit every frame before release");
            keys.Button(false,false);
            Check(!mouse.Left&&p._mouseLock!=null,"A release ends button press while hover retains its own focus");
            keys.Button(true,true);Check(mouse.Right&&!mouse.Left,"physical B path sends right Down");
            hand.transform.forward=new Vector3(0,0,-1);p.TickDesktopPointer();keys.Button(true,false);Check(!mouse.Right&&p._mouseLock==null&&!p._mouseRay.Active,"pointing away releases B, pointer and focus immediately");
            int count=mouse.Events.Count;keys.Button(false,true);Check(mouse.Events.Count==count,"pointing away never clicks desktop");
            hand.transform.forward=new Vector3(0,0,1);
            p.TickDesktopPointer();
            keys.Axis(0,1);Check(mouse.Events.Contains("scroll 0 1"),"stick up sends positive vertical wheel");
            count=mouse.Events.Count;keys.Axis(0,.8f);Check(mouse.Events.Count==count,"held or jittering deflection does not flood scroll events");
            keys.Axis(0,0);keys.Axis(0,-1);Check(mouse.Events.Contains("scroll 0 -1"),"stick down sends negative vertical wheel");
            keys.Axis(0,0);keys.Axis(1,0);Check(mouse.Events.Contains("scroll 1 0"),"stick right sends positive horizontal wheel");
            keys.Axis(0,0);keys.Axis(-1,0);Check(mouse.Events.Contains("scroll -1 0"),"stick left sends negative horizontal wheel");
            keys.Axis(0,0);Check(p._mouseLock!=null,"neutral stick preserves the active hover focus");
            keys.Button(false,true);p._mouseEnabled.Value=false;
            Check(!mouse.Left&&!keys.Enabled&&!p._mouseRay.Active&&p._mouseLock==null,"mouse toggle OFF releases button, ray and action set");
            p._mouseEnabled.Value=true;
            p.TickDesktopPointer();
            keys.Button(false,true);p._mousePose.onTrackingChangedEvent(p._mousePose,SteamVR_Input_Sources.RightHand,ETrackingResult.Uninitialized);
            Check(!mouse.Left&&!keys.Enabled&&p._mouseLock==null,"tracking loss releases Windows mouse without a pose movement subscription");
            p._mousePose.onTrackingChangedEvent(p._mousePose,SteamVR_Input_Sources.RightHand,ETrackingResult.Running_OK);
            p.TickDesktopPointer();
            keys.Button(false,true);p._paused=true;p.ReleaseDesktopMouse();p.SyncDesktopMouse();
            Check(!mouse.Left&&!keys.Enabled,"paused image cannot receive desktop input");
            p._paused=false;p.SyncDesktopMouse();
            hand.Current=new VRGIN.Controls.Controller.Lock();count=mouse.Events.Count;p.TickDesktopPointer();keys.Button(false,true);
            Check(mouse.Events.Count==count&&!mouse.Left,"another item's active right-hand focus is respected");hand.Current.Release();
            mouse.Fail=true;p.TickDesktopPointer();Check(p._mouseLock==null&&!p._mouseLeft&&!p._pointerDriver.enabled,"native input failure disables the driver instead of retrying every frame");mouse.Fail=false;
            p._mouseEnabled.Value=false;p._mouseEnabled.Value=true;
            hand.transform.position=p._panel.transform.TransformPoint(new Vector3(0,0,0));
            SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.RightHand);
            Check(p._grabHand==hand&&!keys.Enabled,"holding disables desktop input so stick keeps its size function");
            float width=p._width.Value;SteamVR_Actions.legacy_emulate.Axis0_2D.Move(SteamVR_Input_Sources.RightHand,1);
            Check(p._width.Value>width&&!mouse.Left,"held-stick sizing remains available");
            SteamVR_Actions.legacy_emulate.Grip_Press.Up(SteamVR_Input_Sources.RightHand);
            hand.transform.position=p._panelTransform.position+new Vector3(0,0,-.4f);p.TickDesktopPointer();
            keys.Button(false,true);GateApi.Allowed=false;p.PlaybackBarDelete();
            Check(!mouse.Left&&!mouse.Right&&p._mouseLock==null&&p._mouseRay==null,"delete after permit revocation releases Windows buttons and aiming ray");
            p.StopSession();Check(p._mousePose==null&&p._mouseButtons==null,"session stop removes desktop input and tracking-state subscriptions");p.OnDestroy();VR.Active=false;
            CheckPointerFrames();
        }
    }
}
