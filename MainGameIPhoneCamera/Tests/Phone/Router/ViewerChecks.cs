using System;
using System.Threading;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private static void CheckViewer()
        {
            var p = new Plugin(); p.CreatePhoneShell(new UnityEngine.Transform()); p.IsHeld = true; p.PhoneGrabbed();
            p.ShowPhonePage(PhonePage.Viewer);
            Check(p._viewerUiVisible && p._shellUi.ControlsVisible, "viewer opens with overlay controls");
            p.PhoneAxis(0, 1);
            Check(p._shellUi.Moving && !p._shellUi.Panning && p._shellUi.ZoomAxis == 1 && p._controlSelection == 0, "vertical axis zooms without navigating buttons");
            p.SetViewerPan(true); p.PhoneAxis(1, -1);
            Check(p._viewerPanning && p._shellUi.Panning && p._shellUi.PanX == 1 && p._shellUi.PanY == -1 && p._shellUi.ZoomAxis == 0, "trigger changes held stick from zoom to two-dimensional pan");
            p.ConfirmPhone(); p.TriggerPhone();
            Check(p.ViewerActions == 0 && p.Shots == 0, "pan does not execute buttons or camera actions");
            p.PhoneAxis(0, 0); p.SetViewerPan(false); p.PhoneAxis(0, -1);
            Check(!p._viewerPanning && p._shellUi.ZoomAxis == -1, "trigger release restores zoom");
            p.PhoneAxis(0, 0);
            Check(!p._shellUi.Moving, "neutral stick stops motion");
            p.PhoneAxis(1, 0);
            Check(p._controlSelection == 1, "horizontal stick selects next control");
            p.PhoneReleased();
            Check(!p._shellUi.Moving && !p._viewerPanning && p._axisX == 0, "release cancels motion and held navigation");
            p.ShowViewerUi(); p.PhoneAxis(0, 1);
            Thread.Sleep(3150); p._mainThread.Drain();
            Check(p._viewerUiVisible && p._shellUi.ControlsVisible && p._shellUi.Moving, "holding zoom longer than three seconds keeps UI visible without axis events");
            p.SetViewerPan(true);
            Thread.Sleep(3150); p._mainThread.Drain();
            Check(p._viewerUiVisible && p._shellUi.Panning, "held trigger and stick keep pan controls visible");
            p.PhoneAxis(0, 0); p.SetViewerPan(false);
            Thread.Sleep(1600); p.NavigatePhone(1, 0);
            Thread.Sleep(1600); p._mainThread.Drain();
            Check(p._viewerUiVisible, "button navigation resets the three-second idle period");
            p.ConfirmPhone(); int actions = p.ViewerActions;
            Thread.Sleep(1600); p._mainThread.Drain();
            Check(p._viewerUiVisible, "A command resets the idle period");
            Thread.Sleep(1600); p._mainThread.Drain();
            Check(!p._viewerUiVisible && !p._shellUi.ControlsVisible, "UI hides three seconds after the final operation");
            p.PhoneAxis(0, -1); p.SetViewerPan(true);
            Check(p._shellUi.Moving && p._viewerPanning && p._shellUi.Panning, "hidden controls allow held trigger plus stick pan");
            p.PhoneAxis(-1, 1);
            Check(p._shellUi.PanX == -1 && p._shellUi.PanY == 1 && p._shellUi.ZoomAxis == 0, "hidden pan uses both axes without zoom");
            p.ConfirmPhone();
            Check(p._viewerUiVisible && p._shellUi.ControlsVisible && p.ViewerActions == actions, "first A only restores hidden controls");
            Check(p._viewerPanning, "restored controls respect trigger still held across hiding");
            p.SetViewerPan(false);
            p.PhoneAxis(0, -1);
            Check(!p._shellUi.Panning && p._shellUi.ZoomAxis == -1, "trigger release restores zoom without requiring UI");
            p.ConfirmPhone(); Check(p.ViewerActions == actions + 1, "visible A executes selected action");
            p.PhoneAxis(0, 1); p.ShowPhonePage(PhonePage.Home);
            Check(!p._viewerUiVisible && !p._shellUi.Moving, "page exit stops viewer");
            p.DestroyPhoneShell();
            Check(ViewerLayout.Zoom(-1) == 1 && ViewerLayout.Zoom(30) == 20, "viewer zoom limits");
            float fit = ViewerLayout.Fit(1920, 886);
            Check(Math.Abs(fit * 1920 - 560) < .01 && fit * 886 < 1166, "landscape media uses full screen width without cropping");
            Check(ViewerLayout.Limit(560, 560, 1) == 0 && ViewerLayout.Limit(560, 560, 2) == 280, "pan range follows actual overflow");
            Check(ViewerLayout.Clamp(500, 280) == 280 && ViewerLayout.Clamp(-500, 280) == -280, "pan cannot expose space beyond image edge");
        }
    }
}
