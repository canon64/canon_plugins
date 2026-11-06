using Valve.VR;
using Hip = MainGirlHipHijack.Plugin;

namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        private SteamVR_Action_Boolean _elbowAimAction;
        private Hip.RuntimeWristBinding _elbowAimBinding;

        private void ConnectElbowAimInput()
        {
            if (_elbowAimAction != null) return;
            // Physical A (left Touch: X) uses ApplicationMenu in the installed bindings.
            _elbowAimAction = SteamVR_Actions.legacy_emulate.ApplicationMenu_Press;
            foreach (var hand in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _elbowAimAction.AddOnStateDownListener(ElbowAimDown, hand);
                _elbowAimAction.AddOnStateUpListener(ElbowAimUp, hand);
                _elbowAimAction.AddOnActiveChangeListener(ElbowAimActiveChanged, hand);
            }
        }

        private void DisconnectElbowAimInput()
        {
            try { ReleaseCuffElbowAim(); }
            finally
            {
                foreach (var hand in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
                {
                    _elbowAimAction?.RemoveOnStateDownListener(ElbowAimDown, hand);
                    _elbowAimAction?.RemoveOnStateUpListener(ElbowAimUp, hand);
                    _elbowAimAction?.RemoveOnActiveChangeListener(ElbowAimActiveChanged, hand);
                }
                _elbowAimAction = null;
            }
        }

        private void ElbowAimDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        {
            if (!CanAct || !_vrFocused || _controller == null || source != _source || _state.Held < 0) return;
            if (_focus == null || !_focus.IsValid || !_controller.IsTracking) { ReleaseGrab(); return; }
            var binding = _bindings[_state.Held];
            if (binding == null || !binding.IsValid) return;
            if (binding.TryBeginElbowAim(source == SteamVR_Input_Sources.RightHand)) _elbowAimBinding = binding;
        }

        private void ElbowAimUp(SteamVR_Action_Boolean action, SteamVR_Input_Sources source)
        { if (_controller != null && source == _source) ReleaseCuffElbowAim(); }

        private void ElbowAimActiveChanged(SteamVR_Action_Boolean action, SteamVR_Input_Sources source, bool active)
        { if (!active && _controller != null && source == _source) ReleaseCuffElbowAim(); }

        private void ReleaseCuffElbowAim()
        {
            var binding = _elbowAimBinding;
            _elbowAimBinding = null;
            binding?.EndElbowAim();
        }
    }
}
