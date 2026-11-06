using System;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _gripAction, _triggerAction;
        private SteamVR_Action_Vector2 _axisAction;
        private bool _axisBound;
        private PhoneButtonInput _phoneButtons;
        private Controller.Lock _navigationFocus;
        private SteamVR_Behaviour_Pose _navigationPose;
        private Controller _heldBy;
        private Controller.Lock _handFocus;
        private SteamVR_Behaviour_Pose _heldPose;
        private SteamVR_Input_Sources _heldSource;
        private bool _gripDownBound, _gripUpBound, _triggerBound;
        private SteamVR_Events.Action _vrFocusEvent;
        private bool _vrInputFocused = true;
        private bool _viewerLeftTriggerHeld, _viewerRightTriggerHeld;
        private bool IsHeld => _heldBy != null;

        private Transform GetVrView() => VR.Active && VR.Camera != null ? VR.Camera.Head : null;

        private void ConnectVrInput()
        {
            if (!VR.Active) return;
            // These are the same actions used by the installed DeviceLegacyAdapter.
            // Subscribe to edges only; no onUpdate listener, Update method, or tracking loop.
            _gripAction = SteamVR_Actions.legacy_emulate.Grip_Press;
            _triggerAction = SteamVR_Actions.legacy_emulate.Axis1_Press;
            if (_gripAction == null || _triggerAction == null) throw new InvalidOperationException("VR入力アクションがありません");
            _gripDownBound = true;
            _gripAction.AddOnStateDownListener(GripDown, SteamVR_Input_Sources.LeftHand);
            _gripAction.AddOnStateDownListener(GripDown, SteamVR_Input_Sources.RightHand);
            _gripUpBound = true;
            _gripAction.AddOnStateUpListener(GripUp, SteamVR_Input_Sources.LeftHand);
            _gripAction.AddOnStateUpListener(GripUp, SteamVR_Input_Sources.RightHand);
            _triggerBound = true;
            _triggerAction.AddOnStateDownListener(TriggerDown, SteamVR_Input_Sources.LeftHand);
            _triggerAction.AddOnStateDownListener(TriggerDown, SteamVR_Input_Sources.RightHand);
            _triggerAction.AddOnStateUpListener(ViewerTriggerUp, SteamVR_Input_Sources.RightHand);
            _triggerAction.AddOnStateUpListener(ViewerTriggerUp, SteamVR_Input_Sources.LeftHand);
            _axisAction = SteamVR_Actions.legacy_emulate.Axis0_2D;
            if (_axisAction == null) throw new InvalidOperationException("VRスティック入力がありません");
            _axisBound = true;
            _axisAction.AddOnChangeListener(CameraAxisChanged, SteamVR_Input_Sources.LeftHand);
            _axisAction.AddOnChangeListener(CameraAxisChanged, SteamVR_Input_Sources.RightHand);
            _phoneButtons = new PhoneButtonInput(PhoneConfirmDown, PhoneBackDown);
            _phoneButtons.Connect();
            _vrInputFocused = true;
            _vrFocusEvent = SteamVR_Events.InputFocusAction(focused => { _vrInputFocused = focused; if (!focused) ReleasePhone(); });
            _vrFocusEvent.enabled = true;
            Logger.LogInfo("VR action edge listeners connected: Grip_Press / Axis1_Press.");
        }

        private void DisconnectVrInput()
        {
            ReleasePhone();
            if (_triggerBound) _triggerAction.RemoveOnStateUpListener(ViewerTriggerUp, SteamVR_Input_Sources.RightHand);
            if (_triggerBound) _triggerAction.RemoveOnStateUpListener(ViewerTriggerUp, SteamVR_Input_Sources.LeftHand);
            if (_vrFocusEvent != null) _vrFocusEvent.enabled = false;
            _vrFocusEvent = null;
            _phoneButtons?.Dispose(); _phoneButtons = null;
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                if (_gripDownBound) _gripAction.RemoveOnStateDownListener(GripDown, source);
                if (_gripUpBound) _gripAction.RemoveOnStateUpListener(GripUp, source);
                if (_triggerBound) _triggerAction.RemoveOnStateDownListener(TriggerDown, source);
                if (_axisBound) _axisAction.RemoveOnChangeListener(CameraAxisChanged, source);
            }
            _gripDownBound = _gripUpBound = _triggerBound = false;
            _axisBound = false; _axisAction = null;
            _gripAction = _triggerAction = null;
        }

        private static Controller GetHand(SteamVR_Input_Sources source)
        {
            if (!VR.Active || VR.Mode == null) return null;
            if (source == SteamVR_Input_Sources.LeftHand) return VR.Mode.Left;
            if (source == SteamVR_Input_Sources.RightHand) return VR.Mode.Right;
            return null;
        }

        private void GripDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (!CanAct || !_vrInputFocused || _root == null) return;
            if (IsHeld)
            {
                if (source != _heldSource) return;
                ReleasePhone();
            }
            Controller hand = GetHand(source);
            if (hand == null || !hand.IsTracking) return;
            float radius = _grabRadius.Value;
            if ((hand.transform.position - _root.transform.position).sqrMagnitude > radius * radius) return;
            Controller.Lock focus;
            if (!CanonPlugins.Shared.VrItemFocus.TryAcquire(hand, out focus)) return;
            _heldBy = hand; _handFocus = focus; _heldSource = source;
            try
            {
                if (source != SteamVR_Input_Sources.RightHand)
                {
                    var right = GetHand(SteamVR_Input_Sources.RightHand);
                    if (right == null || !right.IsTracking || !CanonPlugins.Shared.VrItemFocus.TryAcquire(right, out _navigationFocus))
                        throw new InvalidOperationException("右手の操作権を取得できません。右手の他の小物を離してから掴んでください。");
                    _navigationPose = right.Tracking;
                    if (_navigationPose != null)
                    {
                        _navigationPose.onConnectedChangedEvent += HandConnected;
                        _navigationPose.onTrackingChangedEvent += HandTracking;
                    }
                }
                HidePhoneGizmo();
                // Preserve the exact grabbed pose. Native tracked transform handles all subsequent motion.
                _root.transform.SetParent(hand.transform, true);
                _heldPose = hand.Tracking;
                if (_heldPose != null)
                {
                    _heldPose.onConnectedChangedEvent += HandConnected;
                    _heldPose.onTrackingChangedEvent += HandTracking;
                }
                _phoneButtons?.Active(true);
                PhoneGrabbed();
                _status = "右スティック：選択／A：決定／B：メニュー／トリガー：カメラ・撮影";
            }
            catch (Exception ex) { ReleasePhone(); ReportError("保持失敗", ex); }
        }

        private void GripUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        { if (IsHeld && source == _heldSource) ReleasePhone(); }

        private void TriggerDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (_phone.Page == PhonePage.Viewer)
            {
                if ((source == _heldSource || source == SteamVR_Input_Sources.RightHand) && RightPhoneInputAllowed())
                {
                    if (source == SteamVR_Input_Sources.LeftHand) _viewerLeftTriggerHeld = true;
                    else _viewerRightTriggerHeld = true;
                    RunPhoneInput(() => SetViewerPan(true));
                }
                return;
            }
            if (!CanAct || !IsHeld || source != _heldSource) return;
            if (_handFocus == null || !_handFocus.IsValid || !_heldBy.IsTracking) { ReleasePhone(); return; }
            RunPhoneInput(TriggerPhone);
        }

        private void ViewerTriggerUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (source == SteamVR_Input_Sources.LeftHand) _viewerLeftTriggerHeld = false;
            else if (source == SteamVR_Input_Sources.RightHand) _viewerRightTriggerHeld = false;
            if (RightPhoneInputAllowed()) RunPhoneInput(() => SetViewerPan(_viewerLeftTriggerHeld || _viewerRightTriggerHeld));
        }

        private void CameraAxisChanged(SteamVR_Action_Vector2 action, SteamVR_Input_Sources source, Vector2 axis, Vector2 delta)
        {
            if (source != SteamVR_Input_Sources.RightHand || !RightPhoneInputAllowed()) return;
            RunPhoneInput(() => PhoneAxis(axis.x, axis.y));
        }

        private bool RightPhoneInputAllowed()
        {
            if (!CanAct || !IsHeld || !_vrInputFocused) return false;
            var right = GetHand(SteamVR_Input_Sources.RightHand);
            if (_handFocus == null || !_handFocus.IsValid || !_heldBy.IsTracking || right == null || !right.IsTracking ||
                _heldSource != SteamVR_Input_Sources.RightHand && (_navigationFocus == null || !_navigationFocus.IsValid))
            { ReleasePhone(); return false; }
            return true;
        }
        private void PhoneConfirmDown() { if (RightPhoneInputAllowed()) RunPhoneInput(ConfirmPhone); }
        private void PhoneBackDown() { if (RightPhoneInputAllowed()) RunPhoneInput(BackPhone); }

        private void HandConnected(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, bool connected)
        { if (!connected) ReleasePhone(); }
        private void HandTracking(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, ETrackingResult result)
        { if (result != ETrackingResult.Running_OK) ReleasePhone(); }

        private void ReleasePhone()
        {
            _viewerLeftTriggerHeld = _viewerRightTriggerHeld = false;
            StopCameraZoom();
            bool held = IsHeld;
            _app.ReleaseAxis();
            if (_heldPose != null)
            {
                _heldPose.onConnectedChangedEvent -= HandConnected;
                _heldPose.onTrackingChangedEvent -= HandTracking;
            }
            _heldPose = null;
            if (_navigationPose != null)
            {
                _navigationPose.onConnectedChangedEvent -= HandConnected;
                _navigationPose.onTrackingChangedEvent -= HandTracking;
                _navigationPose = null;
            }
            var focus = _handFocus;
            var navigation = _navigationFocus; _navigationFocus = null;
            _handFocus = null; _heldBy = null; _heldSource = SteamVR_Input_Sources.Any;
            try
            {
                _phoneButtons?.Active(false);
                if (_root != null) _root.transform.SetParent(_scene != null ? _scene.transform : null, true);
                if (held) PhoneReleased();
            }
            finally
            {
                // Native Controller waits for held buttons to clear before restoring its tools.
                try { if (focus != null && focus.IsValid) focus.SafeRelease(); }
                finally { if (navigation != null && navigation.IsValid) navigation.SafeRelease(); }
            }
        }

        private void HapticPhoto()
        {
            // A haptic-device failure must not report an already saved photo as a save failure.
            try { if (_heldBy != null && _heldBy.Input != null) _heldBy.Input.TriggerHapticPulse(1000); }
            catch (Exception ex) { Logger.LogWarning("Photo saved; haptic feedback unavailable: " + ex.Message); }
        }
    }
}
