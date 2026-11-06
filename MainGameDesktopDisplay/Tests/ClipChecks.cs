using System;
using UnityEngine;
using VRGIN.Core;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static void CheckClip()
        {
            var bounds=new DesktopCapture.Bounds{Left=-1920,Top=-1080,Right=0,Bottom=0};
            var clip=new DisplayClip(bounds,10,20,25,0);
            Check(clip.Bounds.Left==-1728&&clip.Bounds.Right==-384&&clip.Bounds.Top==-810&&clip.Bounds.Bottom==0,"four-edge crop produces exact physical source bounds including negative origins");
            Check(Math.Abs(clip.Width-.7f)<.00001f&&Math.Abs(clip.Height-.75f)<.00001f&&Math.Abs(clip.CenterX+.05f)<.00001f&&Math.Abs(clip.CenterY+.125f)<.00001f,"cropping preserves scale and shifts retained geometry to its original location");
            clip=new DisplayClip(bounds,100,100,100,100);Check(clip.Bounds.Width==1&&clip.Bounds.Height==1,"extreme clipping always leaves one source pixel");
            clip=new DisplayClip(bounds,float.NaN,float.PositiveInfinity,-10,0);Check(clip.Bounds.Width==1920&&clip.Bounds.Height==1080,"invalid/nonfinite crop values cannot create empty or inverted geometry");
            var p=VrSession();var entry=p._clipEntry;int saved=entry.Writes,created=DesktopCapture.Created;
            p._clipDraft[0]=10;p._clipDraft[1]=20;p._clipDraft[2]=25;p._clipDraft[3]=0;p._clipDirty=true;
            Event.current.type=EventType.Repaint;Event.current.rawType=EventType.Repaint;
            p.DrawClipControls();p.DrawClipControls();
            Check(entry.Writes==saved&&DesktopCapture.Created==created,"drag previews do not save settings or recreate capture resources");
            Event.current.type=EventType.MouseUp;Event.current.rawType=EventType.MouseUp;p.DrawClipControls();
            Check(entry.Writes==saved+1&&DesktopCapture.Created==created+1,"mouse release commits all four edges with one save and one capture recreation");
            p.DrawClipControls();Check(entry.Writes==saved+1&&DesktopCapture.Created==created+1,"repeated MouseUp cannot duplicate the crop commit");
            Check(p._capture.Source.Left==192&&p._capture.Source.Right==1536&&p._capture.Source.Top==270,"actual product capture receives only the retained rectangle");
            float baseHeight=p._width.Value/(1920f/1080);
            Check(Math.Abs(p._panelTransform.localScale.x-p._width.Value*.7f)<1e-5f&&Math.Abs(p._panelTransform.localScale.y-baseHeight*.75f)<1e-5f,"mesh shrinks by cropped fractions without magnifying content");
            Check(Math.Abs(p._panelTransform.localPosition.x+p._width.Value*.05f)<1e-5f&&Math.Abs(p._bodyTransform.localPosition.x-p._panelTransform.localPosition.x)<1e-5f,"frame and screen share the clipped centre offset");
            MapDesktopPoint(p._mouseBounds,0,0,out int x,out int y);Check(x==192&&y==270,"cropped top-left click points to retained desktop top-left");
            var hand=VR.Mode.Right;hand.transform.position=p._panelTransform.position+new Vector3(0,0,-.3f);hand.transform.forward=new Vector3(0,0,1);
            MapDesktopPoint(p._mouseBounds,.5f,.5f,out int centreX,out int centreY);
            p.TickDesktopPointer();Check(p.AimDesktop(out x,out y)&&x==centreX&&y==centreY,"cropped centre ray agrees with cropped physical desktop centre");
            p._monitor=1;p.LoadMonitorClip();Check(p._clipDraft[0]==0&&p._clipDraft[1]==0,"each monitor starts with its independent crop profile");
            p._clipDraft[0]=5;p._clipDirty=true;p.CommitClipDraft();p._monitor=0;p.LoadMonitorClip();
            Check(p._clipDraft[0]==10&&p._clipDraft[1]==20&&p._clipDraft[2]==25,"switching back restores that monitor's saved four edges");
            entry=p._clipEntry;entry.Value="NaN,Infinity,100,100";saved=entry.Writes;p.LoadMonitorClip();
            Check(entry.Writes==saved&&p._clip.Bounds.Width>=1&&p._clip.Bounds.Height>=1,"loading damaged saved crop validates runtime data without rewriting the setting");
            p._monitors[1].Rect=new(){Left=1920,Top=-100,Right=3000,Bottom=1820};p._monitor=1;p.LoadMonitorClip();p.StartCapture();
            Check(Math.Abs(p._screenAspect-1080f/1920)<1e-5f,"portrait monitor geometry uses the original full-monitor aspect");
            p._clipDraft[2]=10;p._clipDirty=true;Event.current.type=EventType.KeyDown;Event.current.keyCode=KeyCode.Return;Event.current.rawType=EventType.KeyDown;GUI.Focus="DesktopClipTop";p.DrawClipControls();
            Check(!p._clipDirty&&Event.current.type==EventType.Used,"Enter commits typed crop without waiting for mouse input");
            p._paused=true;p._clipDraft[3]=5;p._clipDirty=true;p.CommitClipDraft();Check(p._paused&&!p._driver.enabled&&!p._pointerDriver.enabled,"crop commit preserves paused state and disabled pointer");
            GUI.Focus="";p.StopSession();p.OnDestroy();VR.Active=false;
        }
    }
}
