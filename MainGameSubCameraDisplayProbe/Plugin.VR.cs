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
                UpdateHandPreview(null);
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
                UpdateHandPreview(null);
                StopGrip();
                return;
            }

            if (!_heldController.Input.GetPress(EVRButtonId.k_EButton_Grip))
            {
                UpdateHandPreview(null);
                StopGrip();
                return;
            }

            Transform hand = ((Component)_heldController).transform;
            _heldObject.transform.position = hand.position + hand.rotation * _holdLocalPosition;
            _heldObject.transform.rotation = hand.rotation * _holdLocalRotation;
            UpdateHandPreview(hand);
            UpdateHeldCameraFovFromStick(_heldController);
            bool photoTriggerDown = _heldObject == _cameraAnchorObject && IsPhotoTriggerDown(_heldController);
            if (photoTriggerDown)
            {
                LogInfo("photo trigger down enabled=" + _settings.PhotoCaptureOnTrigger + " controller=" + _heldController.name);
                if (_settings.PhotoCaptureOnTrigger)
                    CaptureSubCameraPhoto("vr-trigger");
                else
                    SetStatus("写真保存OFF");
            }
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
            UpdateHandPreview(hand);
            LogInfo("vr grip start target=" + target.name + " controller=" + controller.name);
            return true;
        }

        private void StopGrip()
        {
            UpdateHandPreview(null);
            if (_heldObject != null)
                LogInfo("vr grip end target=" + _heldObject.name);
            ReleaseHeldControllerFocus();
            _heldController = null;
            _heldObject = null;
        }

        private void UpdateHandPreview(Transform hand)
        {
            if (_handPreviewObject == null)
                return;

            bool shouldShow =
                hand != null &&
                _renderTexture != null &&
                _settings.ShowHandPreviewWhileGrabbingCamera &&
                _heldObject == _cameraAnchorObject;

            if (!shouldShow)
            {
                if (_handPreviewObject.activeSelf)
                {
                    _handPreviewObject.SetActive(false);
                    LogInfo("hand preview hide");
                }
                return;
            }

            if (!_handPreviewObject.activeSelf)
            {
                _handPreviewObject.SetActive(true);
                LogInfo("hand preview show hand=" + hand.name);
            }

            Quaternion previewRotation = hand.rotation * Quaternion.Euler(90f, 0f, 0f);
            _handPreviewObject.transform.SetPositionAndRotation(hand.position + hand.forward * 0.22f, previewRotation);
            _handPreviewObject.transform.localScale = new Vector3(0.23f, 0.23f, 1f);
        }

        private void UpdateHeldCameraFovFromStick(Controller controller)
        {
            if (!_settings.VrFovStickEnabled || _heldObject != _cameraAnchorObject || controller == null || controller.Input == null)
                return;

            Vector2 axis = controller.Input.GetAxis();
            float deadzone = Mathf.Clamp(_settings.VrFovStickDeadzone, 0f, 0.95f);
            float y = axis.y;
            if (Mathf.Abs(y) <= deadzone)
                return;

            float normalized = Mathf.Sign(y) * ((Mathf.Abs(y) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float speed = Mathf.Clamp(_settings.VrFovStickSpeed, 1f, 120f);
            float nextFov = Round2(Mathf.Clamp(_settings.CameraFieldOfView + normalized * speed * deltaTime, 10f, 170f));
            if (Mathf.Abs(nextFov - _settings.CameraFieldOfView) <= 0.001f)
                return;

            _settings.CameraFieldOfView = nextFov;
            if (_subCamera != null)
                _subCamera.fieldOfView = nextFov;
            SyncActiveFovFromUi();
            SetStatus("FOV: " + nextFov.ToString("0.##"));
        }

        private static bool IsPhotoTriggerDown(Controller controller)
        {
            if (controller == null || controller.Input == null)
                return false;

            return controller.Input.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger)
                || controller.Input.GetPressDown(EVRButtonId.k_EButton_Axis1);
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
