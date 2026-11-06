using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using VRGIN.Controls.Handlers;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _gadgetDeleteButton;
        private CanonPlugins.Shared.DedicatedVrButton _gadgetB;
        private SteamVR_Events.Action _gadgetDeleteFocus;
        private readonly List<GadgetDeleteTarget> _gadgetDeleteTargets = new List<GadgetDeleteTarget>();
        private int _gadgetDeleteFrame = -1;
        private GadgetDeleteTarget _gadgetDeletePressed;
        private SteamVR_Input_Sources _gadgetDeleteHand;
        private bool _gadgetDeleteVrFocused;
        private sealed class GadgetDeleteTarget
        {
            internal Rect Rect;
            internal string Item;
            internal Action Delete;
        }

        private void ConnectVrGadgetDelete()
        {
            if (!VR.Active || _gadgetDeleteButton != null) return;
            _gadgetB = new CanonPlugins.Shared.DedicatedVrButton("itemdelete");
            _gadgetDeleteButton = _gadgetB.Action;
            if (_gadgetDeleteButton == null) return;
            _gadgetDeleteVrFocused = true;
            foreach (var hand in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _gadgetDeleteButton.AddOnStateDownListener(VrGadgetDeleteDown, hand);
                _gadgetDeleteButton.AddOnStateUpListener(VrGadgetDeleteUp, hand);
                _gadgetDeleteButton.AddOnActiveChangeListener(VrGadgetDeleteActiveChanged, hand);
            }
            _gadgetDeleteFocus = SteamVR_Events.InputFocusAction(focused => {
                _gadgetDeleteVrFocused = focused;
                if (!focused) ClearVrGadgetDeleteTargets();
            });
            _gadgetDeleteFocus.enabled = true;
        }

        private void DisconnectVrGadgetDelete()
        {
            ClearVrGadgetDeleteTargets();
            _gadgetDeleteVrFocused = false;
            if (_gadgetDeleteFocus != null) _gadgetDeleteFocus.enabled = false;
            _gadgetDeleteFocus = null;
            foreach (var hand in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _gadgetDeleteButton?.RemoveOnStateDownListener(VrGadgetDeleteDown, hand);
                _gadgetDeleteButton?.RemoveOnStateUpListener(VrGadgetDeleteUp, hand);
                _gadgetDeleteButton?.RemoveOnActiveChangeListener(VrGadgetDeleteActiveChanged, hand);
            }
            _gadgetDeleteButton = null;
            _gadgetB?.Dispose();
            _gadgetB = null;
        }

        private void ClearVrGadgetDeleteTargets()
        {
            _gadgetDeleteTargets.Clear();
            _gadgetDeleteFrame = -1;
            _gadgetDeletePressed = null;
        }

        private void RegisterVrGadgetDelete(Rect rect, string item, Action delete)
        {
            if (_gadgetDeleteButton == null || !GUI.enabled || Event.current.type != EventType.Repaint) return;
            if (_gadgetDeleteFrame != Time.frameCount)
            {
                _gadgetDeleteTargets.Clear();
                _gadgetDeleteFrame = Time.frameCount;
            }
            // These buttons are drawn in the root PlaybackBar GUI coordinates.
            _gadgetDeleteTargets.Add(new GadgetDeleteTarget { Rect = rect, Item = item, Delete = delete });
        }

        private bool VrGadgetPointer(SteamVR_Input_Sources source, out Vector2 mouse)
        {
            mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!_sessionCommandsOpen || !_gadgetDeleteVrFocused || !VR.Active || VR.Mode == null || Time.frameCount - _gadgetDeleteFrame > 1) return false;
            var controller = source == SteamVR_Input_Sources.LeftHand ? VR.Mode.Left : VR.Mode.Right;
            var menu = controller != null ? controller.GetComponent<MenuHandler>() : null;
            return menu != null && menu.LaserVisible;
        }

        private void VrGadgetDeleteDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (_gadgetDeletePressed != null || !VrGadgetPointer(source, out var mouse)) return;
            foreach (var target in _gadgetDeleteTargets)
                if (target.Rect.Contains(mouse))
                {
                    _gadgetDeletePressed = target;
                    _gadgetDeleteHand = source;
                    return;
                }
        }

        private void VrGadgetDeleteActiveChanged(SteamVR_Action_Boolean action, SteamVR_Input_Sources source, bool active)
        {
            if (!active && _gadgetDeleteHand == source) _gadgetDeletePressed = null;
        }

        private void VrGadgetDeleteUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (_gadgetDeletePressed == null || _gadgetDeleteHand != source) return;
            var pressed = _gadgetDeletePressed;
            _gadgetDeletePressed = null;
            if (!VrGadgetPointer(source, out var mouse) || !pressed.Rect.Contains(mouse)) return;
            // Require the same item button to remain visible at release.
            foreach (var target in _gadgetDeleteTargets)
                if (target.Item == pressed.Item && target.Rect == pressed.Rect && target.Rect.Contains(mouse))
                {
                    pressed.Delete();
                    return;
                }
        }
    }
}
