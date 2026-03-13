using BepInEx.Configuration;
using System;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private static string GetConfigHotkey(ConfigEntry<string> entry, string fallback)
        {
            if (entry == null)
            {
                return fallback ?? string.Empty;
            }

            var raw = entry.Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback ?? string.Empty;
            }

            return raw.Trim();
        }

        private static bool IsHotkeyDown(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                return false;
            }

            var tokens = hotkey.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            bool needShift = false;
            bool needCtrl = false;
            bool needAlt = false;
            KeyCode key = KeyCode.None;

            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (token.Equals("shift", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("leftshift", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("rightshift", StringComparison.OrdinalIgnoreCase))
                {
                    needShift = true;
                    continue;
                }

                if (token.Equals("ctrl", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("control", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("leftcontrol", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("rightcontrol", StringComparison.OrdinalIgnoreCase))
                {
                    needCtrl = true;
                    continue;
                }

                if (token.Equals("alt", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("leftalt", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("rightalt", StringComparison.OrdinalIgnoreCase))
                {
                    needAlt = true;
                    continue;
                }

                if (token.Equals("enter", StringComparison.OrdinalIgnoreCase))
                {
                    key = KeyCode.Return;
                    continue;
                }

                if (Enum.TryParse(token, true, out KeyCode parsed))
                {
                    key = parsed;
                }
            }

            if (key == KeyCode.None)
            {
                return false;
            }

            if (needShift && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                return false;
            }

            if (needCtrl && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                return false;
            }

            if (needAlt && !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                return false;
            }

            if (key == KeyCode.Return)
            {
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            }

            return Input.GetKeyDown(key);
        }

        private void HandlePresetHotkeys()
        {
            var s = Settings;
            if (s == null || s.BpmPresets == null || s.BpmPresets.Count == 0)
            {
                return;
            }

            if (_cfgPresetSlotHotkeys != null)
            {
                int slotCount = Mathf.Min(10, _cfgPresetSlotHotkeys.Length);
                for (int slot = 0; slot < slotCount; slot++)
                {
                    string hotkey = GetConfigHotkey(_cfgPresetSlotHotkeys[slot], string.Empty);
                    if (!IsHotkeyDown(hotkey))
                    {
                        continue;
                    }

                    ApplyPresetSlotByHotkey(slot);
                    return;
                }
            }

            if (IsHotkeyDown(GetConfigHotkey(_cfgPresetPrevHotkey, string.Empty)))
            {
                ApplyPresetStepByHotkey(-1);
                return;
            }

            if (IsHotkeyDown(GetConfigHotkey(_cfgPresetNextHotkey, string.Empty)))
            {
                ApplyPresetStepByHotkey(1);
            }
        }

        private void ApplyPresetSlotByHotkey(int slotZeroBased)
        {
            var sorted = BuildSortedPresetIndices();
            if (slotZeroBased < 0 || slotZeroBased >= sorted.Count)
            {
                ShowUiNotice($"preset slot {slotZeroBased + 1} is empty");
                return;
            }

            int presetIndex = sorted[slotZeroBased];
            ApplyPreset(presetIndex);
            _lastAppliedPresetOrderIndex = slotZeroBased;
            LogInfo($"preset hotkey slot={slotZeroBased + 1} index={presetIndex}");
        }

        private void ApplyPresetStepByHotkey(int delta)
        {
            var sorted = BuildSortedPresetIndices();
            if (sorted.Count == 0)
            {
                return;
            }

            int count = sorted.Count;
            int nextOrderIndex;
            if (_lastAppliedPresetOrderIndex < 0 || _lastAppliedPresetOrderIndex >= count)
            {
                nextOrderIndex = delta >= 0 ? 0 : count - 1;
            }
            else
            {
                int raw = _lastAppliedPresetOrderIndex + delta;
                nextOrderIndex = ((raw % count) + count) % count;
            }

            int presetIndex = sorted[nextOrderIndex];
            ApplyPreset(presetIndex);
            _lastAppliedPresetOrderIndex = nextOrderIndex;
            LogInfo($"preset hotkey step={delta} orderIndex={nextOrderIndex} presetIndex={presetIndex}");
        }
    }
}

