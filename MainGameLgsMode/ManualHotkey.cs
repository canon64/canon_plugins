using System;
using UnityEngine;

namespace MainGameLgsMode
{
    public static class ManualHotkey
    {
        public static void Validate(string text) => Parse(text, out _, out _);
        private static void Parse(string text, out KeyCode key, out EventModifiers modifiers)
        {
            modifiers = 0; key = KeyCode.None;
            foreach (string part in (text ?? "").Split('+'))
            {
                string value = part.Trim();
                if (value.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || value.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= EventModifiers.Control;
                else if (value.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= EventModifiers.Alt;
                else if (value.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= EventModifiers.Shift;
                else if (key == KeyCode.None && Enum.TryParse(value, true, out KeyCode parsed) && parsed != KeyCode.None && Enum.IsDefined(typeof(KeyCode), parsed)) key = parsed;
                else throw new ArgumentException("ManualCycleKey must be a key name, optionally with Ctrl/Alt/Shift, e.g. M or Ctrl+F8.");
            }
            if (key == KeyCode.None) throw new ArgumentException("ManualCycleKey needs a key name.");
        }
        public static bool Matches(string text, KeyCode pressed, EventModifiers actual)
        {
            Parse(text, out KeyCode key, out EventModifiers modifiers);
            return key == pressed && (actual & (EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift | EventModifiers.Command)) == modifiers;
        }
    }
}
