using System;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private Controller _heldController;
        private Controller.Lock _heldControllerFocusLock = Controller.Lock.Invalid;
        private GameObject _heldObject;
        private Vector3 _holdLocalPosition;
        private Quaternion _holdLocalRotation = Quaternion.identity;

        private void UpdateVrGrab()
        {
            if (!_settings.EnableVrGrip || !VR.Active || VR.Mode == null)
            {
                if (_heldController != null || (_heldControllerFocusLock != null && _heldControllerFocusLock.IsValid))
                    StopGrip();
                return;
            }

            if (_heldController == null)
            {
                if (_settings.GripMovesCamera && TryStartGrip(VR.Mode.Left, _cameraAnchorObject))
                    return;
                if (_settings.GripMovesCamera && TryStartGrip(VR.Mode.Right, _cameraAnchorObject))
                    return;
                if (_settings.GripMovesDisplay && TryStartGrip(VR.Mode.Left, _displayAnchorObject))
                    return;
                if (_settings.GripMovesDisplay)
                    TryStartGrip(VR.Mode.Right, _displayAnchorObject);
                return;
            }

            if (_heldController.Input == null || _heldObject == null)
            {
                StopGrip();
                return;
            }

            if (!_heldController.Input.GetPress(EVRButtonId.k_EButton_Grip))
            {
                StopGrip();
                return;
            }

            Transform hand = ((Component)_heldController).transform;
            _heldObject.transform.position = hand.position + hand.rotation * _holdLocalPosition;
            _heldObject.transform.rotation = hand.rotation * _holdLocalRotation;
            PersistTransformsToSettings();
        }

        private bool TryStartGrip(Controller controller, GameObject target)
        {
            if (controller == null || controller.Input == null || target == null)
                return false;

            if (!controller.Input.GetPressDown(EVRButtonId.k_EButton_Grip))
                return false;

            Transform hand = ((Component)controller).transform;
            if (Vector3.Distance(hand.position, target.transform.position) > _settings.VrGripStartDistance)
                return false;

            if (!controller.TryAcquireFocus(out Controller.Lock focusLock) || focusLock == null || !focusLock.IsValid)
            {
                LogWarn("vr grip rejected: controller focus acquire failed controller=" + controller.name);
                return false;
            }

            _heldController = controller;
            _heldControllerFocusLock = focusLock;
            _heldObject = target;
            _holdLocalPosition = Quaternion.Inverse(hand.rotation) * (target.transform.position - hand.position);
            _holdLocalRotation = Quaternion.Inverse(hand.rotation) * target.transform.rotation;
            LogInfo("vr grip start target=" + target.name + " controller=" + controller.name);
            return true;
        }

        private void StopGrip()
        {
            if (_heldObject != null)
                LogInfo("vr grip end target=" + _heldObject.name);
            ReleaseHeldControllerFocus();
            _heldController = null;
            _heldObject = null;
        }

        private void ReleaseHeldControllerFocus()
        {
            if (_heldControllerFocusLock == null || !_heldControllerFocusLock.IsValid)
            {
                _heldControllerFocusLock = Controller.Lock.Invalid;
                return;
            }

            try
            {
                _heldControllerFocusLock.SafeRelease();
            }
            catch (Exception ex)
            {
                LogWarn("vr grip focus release failed: " + ex.Message);
            }

            _heldControllerFocusLock = Controller.Lock.Invalid;
        }
    }
}
