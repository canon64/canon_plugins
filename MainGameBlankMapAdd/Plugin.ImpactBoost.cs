using System;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public partial class Plugin
    {
        private ConfigEntry<bool> _impactBoostEnabled;
        private ConfigEntry<bool> _impactMoanModeEnabled;
        private bool _impactSessionVr;
        private bool _impactEntriesResolved;
        private bool _impactEventsBound;
        private readonly System.Collections.Generic.Dictionary<ConfigEntry<bool>, bool> _impactOriginalValues = new System.Collections.Generic.Dictionary<ConfigEntry<bool>, bool>();
        private Type _impactVrType;
        private PropertyInfo _impactVrActiveProperty;
        private ConfigEntry<bool> _impactNormalEnabledEntry;
        private ConfigEntry<bool> _impactVrEnabledEntry;
        private ConfigEntry<bool> _impactNormalMoanEntry;
        private ConfigEntry<bool> _impactVrMoanEntry;
        private Action<bool> _applyNormalImpactEnabled;
        private Action<bool> _applyVrImpactEnabled;
        private readonly bool _impactVrProcess = Process.GetCurrentProcess().ProcessName
            .IndexOf("_VR", StringComparison.OrdinalIgnoreCase) >= 0;

        private void UpdateImpactBoost()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            ResolveImpactEntriesIfNeeded(Time.unscaledTime);
            bool vr = _impactSessionVr;
            if (_impactBoostEnabled == null)
            {
                // Update runs after plugin initialization; migrate the selected mode's current setting.
                bool initial = (vr ? _impactVrEnabledEntry : _impactNormalEnabledEntry)?.Value ?? false;
                _impactBoostEnabled = Config.Bind("03.Video", "ImpactBoostEnabled", initial,
                    "再生バーのImpactBoost。ON: 通常/VRの対応側だけ有効。OFF: 両方無効。");
                _impactMoanModeEnabled = Config.Bind("03.Video", "ImpactMoanModeEnabled", false,
                    "喘ぎ声モード。ImpactBoost ON時、選択した音源の音声をセリフの有無に関係なく挿入。");
            }
            if (!_impactEventsBound) {
                _impactEventsBound = true;
                _impactBoostEnabled.SettingChanged += (_, __) => UpdateImpactBoost();
                _impactMoanModeEnabled.SettingChanged += (_, __) => UpdateImpactBoost();
            }
            SetImpactEntry(_impactNormalMoanEntry, _impactMoanModeEnabled.Value);
            SetImpactEntry(_impactVrMoanEntry, _impactMoanModeEnabled.Value);
            if (_applyNormalImpactEnabled == null)
            {
                _applyNormalImpactEnabled = enabled => SetImpactEntry(_impactNormalEnabledEntry, enabled);
                _applyVrImpactEnabled = enabled => SetImpactEntry(_impactVrEnabledEntry, enabled);
            }
            ImpactBoostRouting.Apply(_impactBoostEnabled.Value, vr,
                _applyNormalImpactEnabled,
                _applyVrImpactEnabled);
        }

        private void ResolveImpactEntriesIfNeeded(float now)
        {
            if (_impactEntriesResolved) return;
            _impactEntriesResolved = true;
            _impactSessionVr = IsImpactBoostVr();
            if (_impactNormalEnabledEntry == null)
                _impactNormalEnabledEntry = GetImpactEntry("com.kks.main.voiceimpactboost", "00.General");
            if (_impactVrEnabledEntry == null)
                _impactVrEnabledEntry = GetImpactEntry("com.kks.main.vrhipikvoiceimpactboost", "1.全般");
            if (_impactNormalMoanEntry == null)
                _impactNormalMoanEntry = GetImpactEntry("com.kks.main.voiceimpactboost", "00.General", "MoanModeEnabled");
            if (_impactVrMoanEntry == null)
                _impactVrMoanEntry = GetImpactEntry("com.kks.main.vrhipikvoiceimpactboost", "1.全般", "MoanModeEnabled");
            foreach (var entry in new[] { _impactNormalEnabledEntry, _impactVrEnabledEntry, _impactNormalMoanEntry, _impactVrMoanEntry })
                if (entry != null && !_impactOriginalValues.ContainsKey(entry)) _impactOriginalValues.Add(entry, entry.Value);
        }

        private void RestoreImpactEntries()
        {
            foreach (var pair in _impactOriginalValues) SetImpactEntry(pair.Key, pair.Value);
            _impactOriginalValues.Clear(); _impactEntriesResolved = false;
        }

        private bool IsImpactBoostVr()
        {
            if (_impactVrProcess) return true;
            if (_impactVrType == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _impactVrType = assembly.GetType("VRGIN.Core.VR", false);
                    if (_impactVrType != null) break;
                }
                if (_impactVrType != null)
                    _impactVrActiveProperty = _impactVrType.GetProperty("Active", BindingFlags.Public | BindingFlags.Static);
            }
            return _impactVrActiveProperty?.GetValue(null, null) is bool active && active;
        }

        private static ConfigEntry<bool> GetImpactEntry(string guid, string section, string key = "Enabled")
        {
            if (!Chainloader.PluginInfos.TryGetValue(guid, out var plugin) || plugin.Instance == null)
                return null;
            return plugin.Instance.Config.TryGetEntry<bool>(section, key, out var entry) ? entry : null;
        }

        private static void SetImpactEntry(ConfigEntry<bool> entry, bool value)
        {
            if (entry != null && entry.Value != value) entry.Value = value;
        }
    }
}
