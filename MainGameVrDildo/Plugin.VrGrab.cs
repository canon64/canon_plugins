using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _grabAction;
        private SteamVR_Action_Boolean _grabTrigger;
        private SteamVR_Action_Vector2 _grabAxis;
        private SteamVR_Input_Sources _grabSource;
        private SteamVR_Behaviour_Pose _grabPose;
        private SteamVR_Events.Action _grabFocusEvent;
        private bool _grabVrFocused = true;
        private bool _grabAxisAdjusting;
        private Transform _grabPreviousParent;
        private readonly Dictionary<Controller, Controller.Lock> _pendingGrabLocks = new Dictionary<Controller, Controller.Lock>();

        private void ConnectGrabInput()
        {
            // Desktop H has no SteamVR actions. Keep the item session available to its bar button.
            if (!VR.Active) return;
            if (_grabAction != null) return;
            _grabAction = SteamVR_Actions.legacy_emulate.Grip_Press;
            _grabTrigger = SteamVR_Actions.legacy_emulate.Axis1_Press;
            _grabAxis = SteamVR_Actions.legacy_emulate.Axis0_2D;
            if (_grabAction == null || _grabTrigger == null) throw new InvalidOperationException("VR grip/trigger action is unavailable");
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _grabAction.AddOnStateDownListener(GrabDown, source);
                _grabAction.AddOnStateUpListener(GrabUp, source);
                _grabAction.AddOnActiveChangeListener(GrabActiveChanged, source);
                _grabAxis?.AddOnChangeListener(GrabAxisChanged, source);
                _grabTrigger.AddOnStateDownListener(GrabTriggerDown, source);
                _grabTrigger.AddOnActiveChangeListener(GrabActiveChanged, source);
            }
            _grabVrFocused = true;
            _grabFocusEvent = SteamVR_Events.InputFocusAction(focused => { _grabVrFocused = focused; if (!focused) ReleaseGrab(true); });
            _grabFocusEvent.enabled = true;
        }

        private void DisconnectGrabInput()
        {
            ReleaseGrab(true);
            if (_grabFocusEvent != null) _grabFocusEvent.enabled = false;
            _grabFocusEvent = null;
            if (_grabAction != null)
                foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
                {
                    _grabAction.RemoveOnStateDownListener(GrabDown, source);
                    _grabAction.RemoveOnStateUpListener(GrabUp, source);
                    _grabAction.RemoveOnActiveChangeListener(GrabActiveChanged, source);
                    _grabAxis?.RemoveOnChangeListener(GrabAxisChanged, source);
                    _grabTrigger?.RemoveOnStateDownListener(GrabTriggerDown, source);
                    _grabTrigger?.RemoveOnActiveChangeListener(GrabActiveChanged, source);
                }
            _grabAction = null; _grabTrigger = null; _grabAxis = null;
            foreach (var pending in _pendingGrabLocks.Values)
                if (pending != null && pending.IsValid && pending.IsInvalidating) pending.Release();
            _pendingGrabLocks.Clear();
        }

        private void GrabDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !isActiveAndEnabled ||
                !_grabVrFocused || _settings == null || !_settings.Enabled || !_settings.EnableVrGrab ||
                _model == null || !VR.Active || VR.Mode == null)
            { if (MotionTraceEnabled) TraceVibeEvent("VR_GRAB_REJECT", "reason=inactive-or-unfocused hand=" + source); return; }
            // A new native Down is a fresh grip edge. Recover a missed Up instead of
            // rejecting every later press as a duplicate held item.
            if (_grabController != null)
            {
                if (source != _grabSource) return;
                ReleaseGrab();
            }
            Controller controller = source == SteamVR_Input_Sources.LeftHand ? VR.Mode.Left : VR.Mode.Right;
            if (controller == null || controller.Input == null || !controller.IsTracking) return;
            Transform hand = controller.transform;
            Transform gripRoot = _pistonMotion != null ? _pistonMotion : _model.transform;
            Vector3 grip = gripRoot.TransformPoint(new Vector3(_settings.GripOffsetX, _settings.GripOffsetY, _settings.GripOffsetZ));
            float distance = Vector3.Distance(hand.position, grip);
            // The skinned renderer's localBounds is deliberately oversized for rendering.
            // Pick the actual bent shaft, not that box (which could capture other items' grips).
            if (_bones != null)
                for (int i = 0; i < _bones.Length; i++)
                {
                    if (_bones[i] == null) continue;
                    Vector3 a = _bones[i].position;
                    Vector3 b = i + 1 < _bones.Length && _bones[i + 1] != null ? _bones[i + 1].position : a;
                    distance = Math.Min(distance, GrabSegmentDistance(hand.position, a, b));
                }
            if (distance > _settings.GripRadius)
            { if (MotionTraceEnabled) TraceVibeEvent("VR_GRAB_REJECT", "reason=distance hand=" + source + " distance=" + distance.ToString("F4") + " radius=" + _settings.GripRadius.ToString("F4")); return; }
            Controller.Lock focus;
            if (!CanonPlugins.Shared.VrItemFocus.TryAcquire(controller, out focus))
            { if (MotionTraceEnabled) TraceVibeEvent("VR_GRAB_REJECT", "reason=focus hand=" + source + " owner=" + CanonPlugins.Shared.VrItemFocus.DescribeCurrent(controller)); return; }
            _pendingGrabLocks.Remove(controller);
            _grabController = controller; _grabLock = focus; _grabSource = source;
            try
            {
                _barGizmoRequested = false;
                DetachDesktopGizmo();
                _grabPreviousParent = _model.transform.parent;
                _model.transform.rotation *= Quaternion.Euler(_settings.GripRotationX, _settings.GripRotationY, _settings.GripRotationZ);
                _model.transform.SetParent(hand, true);
                SyncGrabAxisAdjustment(_grabAxis != null ? _grabAxis.GetAxis(source) : default(Vector2));
                _grabPose = controller.Tracking;
                if (_grabPose != null)
                {
                    _grabPose.onConnectedChangedEvent += GrabConnected;
                    _grabPose.onTrackingChangedEvent += GrabTracking;
                }
                if (MotionTraceEnabled) TraceVibeEvent("VR_GRAB", "hand=" + source + " distance=" + distance.ToString("F4"));
            }
            catch { ReleaseGrab(true); throw; }
        }
        private static float GrabSegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            float x = b.x - a.x, y = b.y - a.y, z = b.z - a.z;
            float lengthSquared = x * x + y * y + z * z;
            float t = lengthSquared > 1e-10f ? Math.Max(0f, Math.Min(1f,
                ((point.x - a.x) * x + (point.y - a.y) * y + (point.z - a.z) * z) / lengthSquared)) : 0f;
            return Vector3.Distance(point, new Vector3(a.x + t * x, a.y + t * y, a.z + t * z));
        }
        private void GrabUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        { if (_grabController != null && source == _grabSource) ReleaseGrab(); }
        private void GrabActiveChanged(SteamVR_Action_Boolean action, SteamVR_Input_Sources source, bool active)
        { if (!active && _grabController != null && source == _grabSource) ReleaseGrab(true); }
        private void GrabConnected(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, bool connected)
        { if (!connected) ReleaseGrab(true); }
        private void GrabTracking(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, ETrackingResult tracking)
        { if (tracking != ETrackingResult.Running_OK) ReleaseGrab(true); }
        private void GrabAxisChanged(SteamVR_Action_Vector2 action, SteamVR_Input_Sources source, Vector2 axis, Vector2 delta)
        {
            if (_grabController == null || source != _grabSource || !CanonPlugins.Shared.PluginSessionGate.Allowed || !_grabVrFocused) return;
            if (_grabLock == null || !_grabLock.IsValid || !_grabController.IsTracking) { ReleaseGrab(true); return; }
            SyncGrabAxisAdjustment(axis);
        }
        private void SyncGrabAxisAdjustment(Vector2 axis)
        {
            bool adjusting = Math.Max(Math.Abs(axis.x), Math.Abs(axis.y)) > 0.25f;
            if (_grabAxis == null || adjusting == _grabAxisAdjusting) return;
            _grabAxisAdjusting = adjusting;
            // The user's held-stick adjustment is active only until neutral or grip release.
            if (adjusting) _grabAxis.AddOnUpdateListener(GrabAxisUpdated, _grabSource);
            else _grabAxis.RemoveOnUpdateListener(GrabAxisUpdated, _grabSource);
        }
        private void GrabAxisUpdated(SteamVR_Action_Vector2 action, SteamVR_Input_Sources source, Vector2 axis, Vector2 delta)
        {
            if (_grabController == null || source != _grabSource) return;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !_grabVrFocused || !isActiveAndEnabled ||
                _settings == null || !_settings.Enabled || !_settings.EnableVrGrab ||
                _grabLock == null || !_grabLock.IsValid || !_grabController.IsTracking)
            { ReleaseGrab(true); return; }
            SyncGrabAxisAdjustment(axis);
            if (!_grabAxisAdjusting) return;
            ChangePistonFromStick(axis);
        }
        private void GrabTriggerDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (!VR.Active || _grabController == null || source != _grabSource || !_grabVrFocused || !isActiveAndEnabled ||
                !CanonPlugins.Shared.PluginSessionGate.Allowed || _settings == null || !_settings.Enabled || !_settings.EnableVrGrab) return;
            if (_grabLock == null || !_grabLock.IsValid || !_grabController.IsTracking) { ReleaseGrab(true); return; }
            ToggleHeldPiston();
        }
        private void ReleaseGrab(bool stopPiston = false)
        {
            if (_grabAxisAdjusting)
            {
                _grabAxisAdjusting = false;
                _grabAxis?.RemoveOnUpdateListener(GrabAxisUpdated, _grabSource);
            }
            var focus = _grabLock; _grabLock = Controller.Lock.Invalid;
            var held = _grabController; _grabController = null;
            var parent = _grabPreviousParent; _grabPreviousParent = null;
            var pose = _grabPose; _grabPose = null;
            if (stopPiston) EndHeldPiston();
            try
            {
                if (pose != null)
                {
                    pose.onConnectedChangedEvent -= GrabConnected;
                    pose.onTrackingChangedEvent -= GrabTracking;
                }
                if (held != null && _model != null) _model.transform.SetParent(parent, true);
            }
            finally
            {
                if (focus != null && focus.IsValid)
                {
                    focus.SafeRelease();
                    if (held != null) _pendingGrabLocks[held] = focus;
                }
            }
        }
    }
}
