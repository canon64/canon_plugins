using SessionGate = CanonPlugins.Local.SessionGate;
using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MainGameCameraLookControl
{
    [BepInDependency("canon.gate", "2.3.0")]
    [BepInPlugin(Guid, "MainGameCameraLookControl", "0.1.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    [BepInDependency("com.kks.maingame.uiinputcapture", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.kks.maingame.unifiedui", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.cameralookcontrol";
        private static Plugin _instance;
        private Harmony _harmony;
        private LookSettings _settings;
        private LookPolicy _policy;
        private bool _policyDirty, _savePending;
        private float _saveAt, _nextCleanup;
        private readonly HashSet<string> _warnings = new HashSet<string>();

        private void Awake()
        {
            _instance = this;
            Config.SaveOnConfigSet = false;
            _settings = new LookSettings(Config, DrawOpenWindowButton);
            _settings.WindowVisible.Value = false;
            _settings.Normalize();
            _policy = _settings.Snapshot();
            Config.SettingChanged += (_, __) =>
            {
                _savePending = true;
                _saveAt = Time.unscaledTime + 0.35f;
                if (_settings.WindowVisible.Value) UpdateWindowCapture();
            };
            foreach (FloatSetting row in _settings.Rows)
                row.Entry.SettingChanged += (_, __) => _policyDirty = true;
            _settings.Enabled.SettingChanged += (_, __) =>
            {
                if (!_settings.Enabled.Value) ClearRollLayers();
            };
            try
            {
                _harmony = new Harmony(Guid);
                PatchNativeMethods();
                Logger.LogInfo("Camera-look XYZ control ready; standard camera-look selection remains in charge. F1 > MainGameCameraLookControl.");
            }
            catch (Exception ex)
            {
                _harmony?.UnpatchSelf();
                _harmony = null;
                Logger.LogError("Camera-look hooks unavailable; no correction applied: " + ex);
            }
            Config.Save();

SessionGate.Bind(this, Guid, BeginGateSession, EndGateSession, true);
}

        private void Warn(string message)
        {
            if (_warnings.Add(message)) Logger.LogWarning(message);
        }

        private void Update()
        {
            if (_policyDirty)
            {
                _policyDirty = false;
                _policy = _settings.Snapshot();
            }
            if (!_settings.Enabled.Value) ClearRollLayers();
            if (_savePending && Time.unscaledTime >= _saveAt)
            {
                _savePending = false;
                Config.Save();
            }
            if (Time.unscaledTime >= _nextCleanup)
            {
                _nextCleanup = Time.unscaledTime + 2f;
                CleanupRuntimes();

            }
            UpdateWindowCapture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseWindowCapture();
        }

        private void OnDisable()
        {
            ClearRollLayers();
            ReleaseWindowCapture();
            UnregisterTab();
        }

        private void OnDestroy()
        {
SessionGate.Dispose();

            ClearRollLayers();
            _harmony?.UnpatchSelf();
            ReleaseWindowCapture();
            UnregisterTab();
            if (_savePending) Config.Save();
            if (_instance == this) _instance = null;
        }
    }
}
