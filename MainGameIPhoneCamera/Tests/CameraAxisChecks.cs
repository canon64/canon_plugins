using System;
using MainGameTransformGizmo;
using Valve.VR;
using VRGIN.Core;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private static void CheckCameraAxes()
        {
            foreach (float baseFov in new[] { 20f, 60f, 100f })
            {
                float previous = 180;
                foreach (float zoom in new[] { .1f, .15f, .25f, .5f, 1f, 5f, 10f, 20f })
                {
                    float fov = PhoneZoomPlan.Fov(baseFov, zoom);
                    Check(fov > 0 && fov < 179 && fov < previous, "expanded zoom keeps finite, monotonic native camera FOV");
                    Check(Math.Abs(PhoneZoomPlan.Zoom(baseFov, fov) - zoom) < .0001f, "expanded zoom restores its multiplier from native FOV");
                    previous = fov;
                }
            }
            var p = new Plugin();
            var axis = SteamVR_Actions.legacy_emulate.Axis0_2D;
            var grip = SteamVR_Actions.legacy_emulate.Grip_Press;
            var trigger = SteamVR_Actions.legacy_emulate.Axis1_Press;
            var left = SteamVR_Input_Sources.LeftHand; var right = SteamVR_Input_Sources.RightHand;
            p.ConnectVrInput();
            Check(axis.Count == 2, "one axis change listener per hand");
            axis.Change(left, 1, 0); Check(p._app.Mode == CaptureMode.Photo, "unheld mode input ignored");
            grip.Down(left);
            UnityEngine.Application.isFocused = false;
            axis.Change(left, 1, 0); Check(p._app.Mode == CaptureMode.Photo, "left stick ignored even when left hand holds phone");
            axis.Change(right, .4f, 0); Check(p._app.Mode == CaptureMode.Photo, "axis dead zone respected");
            axis.Change(right, 1, 0); Check(p._app.Mode == CaptureMode.Video, "right stick selects video while left hand holds phone");
            trigger.Down(left); Check(p._app.Phase == CapturePhase.Recording, "held trigger begins recording in video mode");
            axis.Change(right, 0, 0); axis.Change(right, -1, 0);
            Check(p._app.Mode == CaptureMode.Video && p._app.Phase == CapturePhase.Recording, "mode switching locked while recording");
            axis.Change(right, 0, 0); axis.Change(right, 0, 1);
            Check(p._zoomMotion.Input == 1 && p._app.Phase == CapturePhase.Recording, "up starts continuous zoom during recording");
            p._zoomMotion.Advance(1.137f); Check(p._app.Zoom == 1.137f, "timeline values do not snap to quarter steps");
            axis.Change(right, .05f, .9f); Check(p._zoomMotion.Input == .9f, "axis magnitude changes continuous speed");
            axis.Change(right, 0, 0); Check(p._zoomMotion.Input == 0 && p._app.Zoom == 1.137f, "neutral stops at current zoom");
            axis.Change(right, 0, -1); Check(p._zoomMotion.Input == -1, "down reverses zoom");
            trigger.Down(left); Check(p._app.Phase == CapturePhase.Saving, "second trigger stops video");
            axis.Change(right, 0, 0); axis.Change(right, -1, 0); Check(p._app.Mode == CaptureMode.Video, "mode switching locked while saving");
            p._app.Complete(true);
            axis.Change(right, 0, 0); axis.Change(right, -1, 0); Check(p._app.Mode == CaptureMode.Photo, "left tilt returns to photo after saved");
            p._zoomMotion.Advance(21);
            Check(p._app.Zoom == 20, "zoom upper limit is twenty times");
            p._zoomMotion.Advance(.01f);
            Check(p._app.Zoom == .1f, "zoom lower limit includes ultra wide angle");
            Check(PhoneZoomPlan.Fov(60,.5f)>60 && Math.Abs(PhoneZoomPlan.Zoom(60,PhoneZoomPlan.Fov(60,1.137f))-1.137f)<.0001f, "wide angle and continuous FOV inverse");
            p._phoneButtons.A(); p._phoneButtons.B(); Check(p.Confirms == 1 && p.Backs == 1, "right physical A/B routed while phone held");
            var navigation = p._navigationFocus;
            p.CanAct = false; axis.Change(right, 0, 0); axis.Change(right, 0, 1); Check(p._app.Zoom == .1f, "revoked permit cannot change zoom");
            p._phoneButtons.A(); p._phoneButtons.B(); Check(p.Confirms == 1 && p.Backs == 1, "revoked permit rejects A/B");
            p.DisconnectVrInput(); Check(axis.Count == 0, "axis subscriptions removed on stop");
            Check(navigation.Releases == 1, "right navigation focus returned when left grip releases");
            p.CanAct = true; p.ConnectVrInput(); Check(axis.Count == 2, "next session binds axis once");
            p.DisconnectVrInput(); p.DestroyPhone();
            UnityEngine.Application.isFocused = true;
        }
    }
}
