using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private void SaveCurrentDisplayPreset()
        {
            EnsureProbe();
            PersistTransformsToSettings();

            string name = string.IsNullOrWhiteSpace(_displayPresetNameBuffer)
                ? "Display " + ((_settings.DisplayPresets?.Length ?? 0) + 1)
                : _displayPresetNameBuffer.Trim();

            DisplayPreset preset = BuildDisplayPreset(name);
            List<DisplayPreset> presets = new List<DisplayPreset>(_settings.DisplayPresets ?? new DisplayPreset[0]);
            presets.Add(preset);
            _settings.DisplayPresets = presets.ToArray();
            _activeDisplayPresetName = preset.Name ?? string.Empty;
            SaveSettings();
            SetStatus("ディスプレイ保存: " + name);
        }

        private DisplayPreset BuildDisplayPreset(string name)
        {
            Vector3 position = new Vector3(_settings.DisplayPosX, _settings.DisplayPosY, _settings.DisplayPosZ);
            Vector3 rotation = new Vector3(_settings.DisplayRotX, _settings.DisplayRotY, _settings.DisplayRotZ);
            if (_displayAnchorObject != null)
            {
                position = _displayAnchorObject.transform.position;
                rotation = _displayAnchorObject.transform.rotation.eulerAngles;
            }

            return new DisplayPreset
            {
                Name = name,
                Position = ToArray(position),
                Rotation = ToArray(NormalizeEuler(rotation)),
                Width = _settings.DisplayWidth,
                Height = _settings.DisplayHeight
            };
        }

        private void LoadDisplayPreset(DisplayPreset preset)
        {
            if (preset == null)
                return;

            EnsureProbe();
            Vector3 pos = ToVector3(preset.Position, _displayAnchorObject != null
                ? _displayAnchorObject.transform.position
                : new Vector3(_settings.DisplayPosX, _settings.DisplayPosY, _settings.DisplayPosZ));
            Vector3 rot = ToVector3(preset.Rotation, _displayAnchorObject != null
                ? _displayAnchorObject.transform.rotation.eulerAngles
                : new Vector3(_settings.DisplayRotX, _settings.DisplayRotY, _settings.DisplayRotZ));
            _settings.DisplayWidth = Round2(Mathf.Max(0.1f, preset.Width));
            _settings.DisplayHeight = Round2(Mathf.Max(0.1f, preset.Height));
            _activeDisplayPresetName = preset.Name ?? string.Empty;
            if (_displayAnchorObject != null)
                _displayAnchorObject.transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
            PersistTransformsToSettings();
            ApplyDisplaySettings();
            SaveSettings();
            SetStatus("ディスプレイ呼び出し: " + _activeDisplayPresetName);
        }

        private void OverwriteActiveDisplayPreset()
        {
            DisplayPreset preset = FindActiveDisplayPreset();
            if (preset == null)
            {
                SetStatus("ディスプレイ上書き対象なし");
                return;
            }

            PersistTransformsToSettings();
            DisplayPreset next = BuildDisplayPreset(preset.Name ?? _activeDisplayPresetName);
            preset.Position = next.Position;
            preset.Rotation = next.Rotation;
            preset.Width = next.Width;
            preset.Height = next.Height;
            SaveSettings();
            SetStatus("ディスプレイ上書き: " + (preset.Name ?? string.Empty));
        }

        private void RemoveDisplayPresetAt(int index)
        {
            DisplayPreset[] source = _settings.DisplayPresets ?? new DisplayPreset[0];
            if (index < 0 || index >= source.Length)
                return;

            string removed = source[index]?.Name ?? string.Empty;
            List<DisplayPreset> next = new List<DisplayPreset>(source);
            next.RemoveAt(index);
            _settings.DisplayPresets = next.ToArray();
            if (string.Equals(removed, _activeDisplayPresetName, StringComparison.Ordinal))
                _activeDisplayPresetName = string.Empty;
            SaveSettings();
            SetStatus("ディスプレイ削除: " + removed);
        }

        private DisplayPreset FindActiveDisplayPreset()
        {
            if (string.IsNullOrWhiteSpace(_activeDisplayPresetName))
                return null;

            DisplayPreset[] presets = _settings.DisplayPresets ?? new DisplayPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                DisplayPreset preset = presets[i];
                if (preset != null && string.Equals(preset.Name ?? string.Empty, _activeDisplayPresetName, StringComparison.Ordinal))
                    return preset;
            }

            return null;
        }

        private bool IsActiveDisplayPreset(DisplayPreset preset)
        {
            return preset != null
                && !string.IsNullOrWhiteSpace(_activeDisplayPresetName)
                && string.Equals(preset.Name ?? string.Empty, _activeDisplayPresetName, StringComparison.Ordinal);
        }

        private static string GetDisplayPresetDisplayName(DisplayPreset preset, int index)
        {
            string name = string.IsNullOrWhiteSpace(preset?.Name) ? "Display " + (index + 1) : preset.Name;
            return "[表示] " + name;
        }
    }
}
