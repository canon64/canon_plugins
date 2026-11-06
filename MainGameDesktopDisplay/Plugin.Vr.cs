using System;
using System.Collections.Generic;
using System.Globalization;
using CanonPlugins.Shared;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _grabAction;
        private SteamVR_Action_Vector2 _sizeAxis;
        private SteamVR_Events.Action _grabFocusEvent;
        private bool _grabVrFocused = true;
        private Controller _grabHand;
        private Controller.Lock _grabLock;
        private SteamVR_Behaviour_Pose _grabPose;
        private SteamVR_Input_Sources _grabSource;
        private Transform _grabParent;
        private int _sizeDirection, _monitorDirection;
        private readonly Dictionary<Controller, Controller.Lock> _pendingGrabLocks = new Dictionary<Controller, Controller.Lock>();
        private const float GrabRadius = .18f;

        private void ConnectGrabInput()
        {
            if (!VR.Active || _grabAction != null) return;
            _grabAction = SteamVR_Actions.legacy_emulate.Grip_Press;
            _sizeAxis = SteamVR_Actions.legacy_emulate.Axis0_2D;
            if (_grabAction == null || _sizeAxis == null) throw new InvalidOperationException("VRのグリップ／スティック入力がありません。");
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _grabAction.AddOnStateDownListener(DisplayGripDown, source);
                _grabAction.AddOnStateUpListener(DisplayGripUp, source);
                _grabAction.AddOnActiveChangeListener(DisplayGripActive, source);
                _sizeAxis.AddOnChangeListener(DisplaySizeAxis, source);
            }
            _grabVrFocused = true;
            ConnectDesktopMouse();
            _grabFocusEvent = SteamVR_Events.InputFocusAction(focused =>
            { _grabVrFocused = focused; if (!focused) { ReleaseDesktopMouse(); ReleaseDisplayGrab(); CompletePendingGrabLocks(); } SyncDesktopMouse(); });
            _grabFocusEvent.enabled = true;
        }

        private void DisconnectGrabInput()
        {
            DisconnectDesktopMouse();
            ReleaseDisplayGrab();
            if (_grabFocusEvent != null) _grabFocusEvent.enabled = false;
            _grabFocusEvent = null;
            var action = _grabAction; var axis = _sizeAxis;
            _grabAction = null; _sizeAxis = null;
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                if (action != null)
                {
                    CleanupGrab(() => action.RemoveOnStateDownListener(DisplayGripDown, source));
                    CleanupGrab(() => action.RemoveOnStateUpListener(DisplayGripUp, source));
                    CleanupGrab(() => action.RemoveOnActiveChangeListener(DisplayGripActive, source));
                }
                if (axis != null) CleanupGrab(() => axis.RemoveOnChangeListener(DisplaySizeAxis, source));
            }
            CompletePendingGrabLocks();
        }

        private void DisplayGripDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (!CanAct || !_grabVrFocused || _root == null || !VR.Active || VR.Mode == null) return;
            if (_grabHand != null)
            {
                if (_grabSource != source) return;
                ReleaseDisplayGrab(); // Recover a missed Up, with a new native lock.
            }
            if (source != SteamVR_Input_Sources.LeftHand && source != SteamVR_Input_Sources.RightHand) return;
            var hand = source == SteamVR_Input_Sources.LeftHand ? VR.Mode.Left : VR.Mode.Right;
            if (hand == null || !hand.IsTracking || _panel == null) return;
            // Test the whole oriented screen at the grip edge, including its corners.
            // A large screen must not require reaching its centre or use an oversized world AABB.
            var local = _panel.transform.InverseTransformPoint(hand.transform.position);
            var nearest = _panel.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, -.5f, .5f), Mathf.Clamp(local.y, -.5f, .5f), 0));
            if ((hand.transform.position - nearest).sqrMagnitude > GrabRadius * GrabRadius) return;
            try
            {
                ReleaseDesktopMouse();
                if (!VrItemFocus.TryAcquire(hand, out var focus)) return;
                _pendingGrabLocks.Remove(hand);
                _grabHand = hand; _grabLock = focus; _grabSource = source;
                _grabParent = _root.transform.parent; _sizeDirection = _monitorDirection = 0;
                SyncDesktopMouse();
                HideGizmo();
                _root.transform.SetParent(hand.transform, true); // Preserve grabbed position and rotation.
                _grabPose = hand.Tracking;
                if (_grabPose != null)
                {
                    _grabPose.onConnectedChangedEvent += DisplayHandConnected;
                    _grabPose.onTrackingChangedEvent += DisplayHandTracking;
                }
                _status = "VR保持中：スティック上下＝サイズ／左右＝モニター切替。グリップを離すと固定。";
            }
            catch (Exception ex) { ReleaseDisplayGrab(); _status = "VR保持失敗：" + ex.Message; }
        }

        private void DisplayGripUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        { if (_grabHand != null && source == _grabSource) ReleaseDisplayGrab(); }
        private void DisplayGripActive(SteamVR_Action_Boolean action, SteamVR_Input_Sources source, bool active)
        { if (!active && _grabHand != null && source == _grabSource) ReleaseDisplayGrab(); }
        private void DisplayHandConnected(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, bool connected)
        { if (!connected && pose == _grabPose) ReleaseDisplayGrab(); }
        private void DisplayHandTracking(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, ETrackingResult tracking)
        { if (tracking != ETrackingResult.Running_OK && pose == _grabPose) ReleaseDisplayGrab(); }

        private void DisplaySizeAxis(SteamVR_Action_Vector2 action, SteamVR_Input_Sources source, Vector2 axis, Vector2 delta)
        {
            if (_grabHand == null || source != _grabSource || !CanAct || !_grabVrFocused) return;
            if (_grabLock == null || !_grabLock.IsValid || !_grabHand.IsTracking) { ReleaseDisplayGrab(); return; }
            if (Mathf.Abs(axis.x) < .25f) _monitorDirection = 0;
            if (Mathf.Abs(axis.y) < .25f) _sizeDirection = 0;
            if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y) && Mathf.Abs(axis.x) >= .55f)
            {
                int step = axis.x > 0 ? 1 : -1;
                if (step == _monitorDirection) return;
                _monitorDirection = step;
                if (_monitors.Length > 1) SelectMonitor((_monitor + _monitors.Length + step) % _monitors.Length);
                return;
            }
            int direction = axis.y >= .55f ? 1 : axis.y <= -.55f ? -1 : 0;
            if (direction == 0 || direction == _sizeDirection) return;
            _sizeDirection = direction;
            // One 10% step per stick deflection. No repeating coroutine, timer or frame polling.
            float width = Mathf.Clamp(_width.Value * (direction > 0 ? 1.1f : 1f / 1.1f), .1f, 20f);
            _width.Value = width;
            _widthText = width.ToString("0.###", CultureInfo.InvariantCulture);
            Resize();
            _status = "VR保持中：画面幅 " + _widthText + " m。上＝拡大／下＝縮小（1回10％）。";
        }

        private void ReleaseDisplayGrab()
        {
            var hand = _grabHand; var focus = _grabLock; var pose = _grabPose; var parent = _grabParent;
            _grabHand = null; _grabLock = null; _grabPose = null; _grabParent = null;
            _grabSource = SteamVR_Input_Sources.Any; _sizeDirection = _monitorDirection = 0;
            try
            {
                if (pose != null)
                {
                    CleanupGrab(() => pose.onConnectedChangedEvent -= DisplayHandConnected);
                    CleanupGrab(() => pose.onTrackingChangedEvent -= DisplayHandTracking);
                }
                if (hand != null && _root != null) CleanupGrab(() => _root.transform.SetParent(parent, true));
            }
            finally
            {
                if (focus != null && focus.IsValid)
                {
                    try { focus.SafeRelease(); }
                    catch { CleanupGrab(() => focus.Release()); }
                    finally { if (hand != null && focus.IsValid && focus.IsInvalidating) _pendingGrabLocks[hand] = focus; }
                }
                SyncDesktopMouse();
            }
        }

        private void CompletePendingGrabLocks()
        {
            foreach (var focus in _pendingGrabLocks.Values)
                if (focus != null && focus.IsValid && focus.IsInvalidating) CleanupGrab(() => focus.Release());
            _pendingGrabLocks.Clear();
        }
        private void CleanupGrab(Action action)
        { try { action(); } catch (Exception ex) { _status = "VR後始末：" + ex.Message; } }
    }
}
