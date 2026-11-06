using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGameLgsMode
{
    public sealed class ManualKeyInput
    {
        private readonly Func<ModeSettings> _settings;
        private readonly ModeController _controller;
        private KeyCode _held;
        public ManualKeyInput(Func<ModeSettings> settings, ModeController controller)
        { _settings = settings; _controller = controller; }
        public void Reset() { _held = KeyCode.None; }
        public void Handle(Event input)
        {
            if (input == null) return;
            if (input.type == EventType.KeyUp) { if (_held == input.keyCode) Reset(); return; }
            if (input.type != EventType.KeyDown || !Application.isFocused || input.keyCode == _held) return;
            var settings = _settings();
            if (!settings.Enabled || !settings.ManualCycleEnabled || !ManualHotkey.Matches(settings.ManualCycleKey, input.keyCode, input.modifiers)) return;
            _held = input.keyCode;
            // Typing an M in an editor/name field must not change the G13 bank.
            if (GUIUtility.keyboardControl != 0) return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && (selected.GetComponentInParent<InputField>() != null || selected.GetComponentInParent<TMP_InputField>() != null)) return;
            _controller.CycleManual();
        }
    }
}
