using System;
using MainGameTransformGizmo;
using UnityEngine;
using Valve.VR;

namespace MainGameTransformGizmo
{
    public enum GizmoMode { Move }
    public sealed class TransformGizmo
    {
        public bool enabled, IsVisible;
        public GameObject Target;
        public void SetMode(GizmoMode mode) { }
        public void SetVisible(bool visible) { IsVisible = visible; }
    }
    public static class TransformGizmoApi
    {
        public static bool IsAvailable = true, Allowed = true, Capture = true;
        public static TransformGizmo Attached;
        public static TransformGizmo Attach(GameObject target) => Attached = Allowed ? new TransformGizmo { Target = target } : null;
        public static bool EnableCameraInputCaptureOnDrag(TransformGizmo gizmo) => Capture;
        public static bool DisableCameraInputCaptureOnDrag(TransformGizmo gizmo) => true;
        public static bool SetSizeMultiplier(TransformGizmo gizmo, float size) => true;
    }
}

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private static void CheckPlaybackBar()
        {
            var p = new Plugin();
            p.DestroyPhone();
            p.CanAct = false;
            p.PlaybackBarPrimaryAction();
            Check(p._root == null && p.Recalls == 0, "denied bar call cannot spawn");
            p.CanAct = true;
            Application.isFocused = false;
            p.PlaybackBarPrimaryAction();
            Check(p.Recalls == 0, "unfocused bar call ignored");
            Application.isFocused = true;
            p.FailSpawn = true;
            p.PlaybackBarPrimaryAction();
            Check(!p._barCycle.NextShowsGizmo, "failed spawn does not advance cycle");
            p.FailSpawn = false;
            p.PlaybackBarPrimaryAction();
            var phone = p._root;
            int recalls = p.Recalls;
            Check(phone != null && p._barCycle.NextShowsGizmo, "first successful click recalls");
            TransformGizmoApi.Allowed = false;
            p.PlaybackBarPrimaryAction();
            Check(p._phoneGizmo == null && p._barCycle.NextShowsGizmo && p.Recalls == recalls, "denied gizmo stays on gizmo phase");
            TransformGizmoApi.Allowed = true;
            TransformGizmoApi.Capture = false;
            p.PlaybackBarPrimaryAction();
            Check(!p._phoneGizmo.enabled && !p._phoneGizmo.IsVisible && p._barCycle.NextShowsGizmo, "input protection failure hides gizmo and retains phase");
            TransformGizmoApi.Capture = true;
            p.PlaybackBarPrimaryAction();
            Check(p._phoneGizmo.Target == phone && p._phoneGizmo.IsVisible && !p._barCycle.NextShowsGizmo && p.Recalls == recalls, "second successful click attaches visible gizmo directly to phone without recall");
            p.PlaybackBarPrimaryAction();
            Check(p._root == phone && p.Recalls == recalls && !p._phoneGizmo.IsVisible && !p._phoneGizmo.enabled && p._barCycle.NextShowsGizmo, "third click hides gizmo without recalling");
            p.PlaybackBarPrimaryAction();
            Check(p._phoneGizmo.IsVisible && !p._barCycle.NextShowsGizmo, "fourth click shows gizmo again");
            p.ConnectVrInput();
            SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.LeftHand);
            Check(p.IsHeld && !p._phoneGizmo.IsVisible && !p._phoneGizmo.enabled, "VR grab hides gizmo");
            p.PlaybackBarPrimaryAction();
            Check(p._phoneGizmo.IsVisible && p.Recalls == recalls, "after grab hides gizmo next click shows without recall");
            SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.LeftHand);
            var focus = p._handFocus;
            p.CanAct = false;
            p.PlaybackBarDelete();
            Check(p._root == null && p._phoneGizmo == null && !p._barCycle.NextShowsGizmo && !p.IsHeld && focus.Releases == 1, "delete while held cleans gizmo and focus after permit revocation");
            p.PlaybackBarDelete();
            p.CanAct = true;
            p.PlaybackBarPrimaryAction();
            Check(p._root != null && p._barCycle.NextShowsGizmo, "after delete next click recalls");
            p.DisconnectVrInput();
            p.DestroyPhone();
        }
    }
}
