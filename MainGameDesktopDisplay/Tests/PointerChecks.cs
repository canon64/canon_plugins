using System;
using UnityEngine;
using VRGIN.Core;
using CanonGate;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        sealed class CountingMouse : IDesktopMouse
        {
            internal int Moves, X, Y;internal bool Left,Right;
            public void RefreshBounds(){}
            public void Move(int x,int y){Moves++;X=x;Y=y;}
            public void Button(bool right,bool down){if(right)Right=down;else Left=down;}
            public void Scroll(int x,int y){}
            public void ReleaseAll(){Left=Right=false;}
        }
        static void CheckPointerFrames()
        {
            var p=VrSession();var mouse=new CountingMouse();p._desktopMouse=mouse;
            var hand=VR.Mode.Right;var screen=p._panelTransform;var keys=p._mouseButtons;
            hand.transform.position=screen.position+new Vector3(0,0,-.5001f);hand.transform.forward=new Vector3(0,0,1);
            p.TickDesktopPointer();Check(!p._mouseRay.Active&&!keys.Enabled&&mouse.Moves==0,"50.01cm is outside the operation range");
            hand.transform.position=screen.position+new Vector3(0,0,-.5f);p.TickDesktopPointer();
            Check(p._mouseRay.Active&&keys.Enabled,"50cm boundary is included");
            Check((p._mouseLine.Positions[1]-screen.position).sqrMagnitude<1e-8f,"line ends at actual screen intersection, never behind it");
            hand.transform.position=screen.position+new Vector3(0,0,-.49f);hand.transform.forward=new Vector3(.6f,0,.8f);p.TickDesktopPointer();
            Check(!p._mouseRay.Active,"oblique ray longer than 50cm is rejected despite short perpendicular distance");
            hand.transform.position=screen.position+new Vector3(0,0,.2f);hand.transform.forward=new Vector3(0,0,-1);p.TickDesktopPointer();
            Check(!p._mouseRay.Active,"backside cannot operate the display");
            hand.transform.position=screen.position+new Vector3(2,0,-.3f);hand.transform.forward=new Vector3(0,0,1);p.TickDesktopPointer();
            Check(!p._mouseRay.Active,"hit outside the clipped rectangle is rejected");
            hand.transform.position=screen.position+new Vector3(0,0,-.3f);p.TickDesktopPointer();keys.Button(false,true);
            hand.transform.position=screen.position+new Vector3(.1f,.1f,-.3f);p.TickDesktopPointer();
            Check(mouse.Left&&mouse.X>960&&mouse.Y<540,"cursor and held A follow both axes continuously");
            hand.transform.position=screen.position+new Vector3(.1f,.1f,-.6f);p.TickDesktopPointer();
            Check(!mouse.Left&&!p._mouseRay.Active&&p._mouseLock==null,"leaving 50cm ends drag and releases every owned input");
            hand.transform.position=screen.position+new Vector3(0,0,-.3f);p.TickDesktopPointer();
            int captures=DesktopCapture.Captures,created=DesktopCapture.Created,lineWrites=p._mouseLine.PositionWrites;
            for(int i=0;i<100;i++)p.TickDesktopPointer();
            Check(p._mouseLine.PositionWrites==lineWrites,"unchanged line endpoints do not repeat native renderer writes");
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)p.TickDesktopPointer();
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Check(allocated==0,"10000 successful pointer frames allocate zero managed bytes at engine/OS boundaries");
            Check(DesktopCapture.Captures==captures&&DesktopCapture.Created==created,"pointer frames neither capture pixels nor recreate resources");
            p._mouseEnabled.Value=false;int moves=mouse.Moves;
            for(int i=0;i<100;i++)p.TickDesktopPointer();
            Check(mouse.Moves==moves&&!p._pointerDriver.enabled,"OFF disables the frame driver and all pointer sends");
            var driver=new DesktopRenderDriver();int paints=0;driver.Paint=()=>paints++;
            var render=typeof(DesktopRenderDriver).GetMethod("OnWillRenderObject",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Time.frameCount=100;render.Invoke(driver,null);render.Invoke(driver,null);Time.frameCount++;render.Invoke(driver,null);
            Check(paints==2,"render callback paints once per frame across multiple cameras/eyes");
            p.StopSession();p.OnDestroy();VR.Active=false;
        }
    }
}
