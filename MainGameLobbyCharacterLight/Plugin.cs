using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CanonPlugins.LobbyCharacterLight
{
    [BepInPlugin(Guid, "HS2 Lobby Character Light", "1.0.0")]
    [BepInDependency(GateApi.PluginGuid, "2.3.0")]
    [BepInProcess("KoikatsuSunshine")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.lobbycharacterlight";
        internal const string LobbyScene = "CanonHS2_bulk_87beb58feae5248c";
        private const int LobbyMap = 299000;
        private static readonly FieldInfo MapField = typeof(HSceneProc).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DataField = typeof(HSceneProc).GetField("lightData", BindingFlags.Instance | BindingFlags.NonPublic);
        private IDisposable _registration;
        private GateApi.Permit _permit;
        private HSceneProc _scene;
        private HSceneProc.LightData _data;
        private Light _light;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _intensity, _red, _green, _blue;
        private Color _oldColor, _oldInitialColor;
        private float _oldIntensity, _oldInitialIntensity;
        private ulong _lastSession;
        private bool _active, _insideLobby, _applied;

        private void Awake()
        {
            if (MapField == null || DataField == null)
                throw new MissingFieldException("KKS HSceneProc map/lightData contract changed.");
            _enabled = Config.Bind("Lobby", "Enabled", true, "HS2ロビー299000のキャラ用ライトを背景に合わせる。");
            _intensity = Config.Bind("Lobby", "Intensity", 0.32f,
                new ConfigDescription("キャラ用ライトの強さ。背景の暖色照明に追加する補助光。", new AcceptableValueRange<float>(0f, 2f)));
            _red = Channel("Red", 1f);
            _green = Channel("Green", 0.90f);
            _blue = Channel("Blue", 0.78f);
            _enabled.SettingChanged += SettingsChanged;
            _intensity.SettingChanged += SettingsChanged;
            _red.SettingChanged += SettingsChanged;
            _green.SettingChanged += SettingsChanged;
            _blue.SettingChanged += SettingsChanged;
            _registration = GateApi.Register(Guid, StartSession, StopSession);
        }

        private ConfigEntry<float> Channel(string key, float value) => Config.Bind("Lobby", key, value,
            new ConfigDescription("キャラ用ライトの色（0～1）。肌や衣装の素材は変更しない。", new AcceptableValueRange<float>(0f, 1f)));

        private void StartSession()
        {
            var context = GateApi.Current;
            if (!isActiveAndEnabled || context == null || context.Kind != GateApi.SessionKind.H
                || context.HScene == null || context.Id == _lastSession) return;
            _lastSession = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = context.HScene;
            _active = true;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            try
            {
                // One inspection at H start. Later transitions use explicit scene events.
                var map = MapField?.GetValue(_scene) as BaseMap;
                _insideLobby = map != null && map.no == LobbyMap && map.mapRoot != null
                    && map.mapRoot.scene.name == LobbyScene;
                Apply();
            }
            catch { StopSession(); throw; }
        }

        private bool CanAct => _active && isActiveAndEnabled && _permit.IsValid && _scene != null;

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CanAct || scene.name != LobbyScene) return;
            _insideLobby = true;
            Apply();
        }

        private void SceneUnloaded(Scene scene)
        {
            if (scene.name != LobbyScene) return;
            _insideLobby = false;
            Restore();
        }

        private void SettingsChanged(object sender, EventArgs args)
        {
            if (!CanAct) return;
            if (!_enabled.Value) Restore();
            else Apply();
        }

        private void Apply()
        {
            if (!CanAct || !_insideLobby || !_enabled.Value) return;
            if (!_applied)
            {
                var data = DataField?.GetValue(_scene) as HSceneProc.LightData;
                var light = _scene.lightCamera;
                if (light == null || data == null || data.light != light) return;
                _data = data;
                _light = light;
                _oldColor = light.color;
                _oldIntensity = light.intensity;
                _oldInitialColor = data.initColor;
                _oldInitialIntensity = data.initIntensity;
                _applied = true;
            }
            if (_light == null) return;
            var color = new Color(_red.Value, _green.Value, _blue.Value, 1f);
            _data.initColor = color;
            _data.initIntensity = _intensity.Value;
            _light.color = color;
            _light.intensity = _intensity.Value;
            // Native reset/pose changes now use these defaults. Native sliders remain usable.
            if (_scene.sprite != null) _scene.sprite.SetLightData(_data);
        }

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;
            var light = _light;
            var data = _data;
            _light = null;
            _data = null;
            try
            {
                if (data != null)
                {
                    data.initColor = _oldInitialColor;
                    data.initIntensity = _oldInitialIntensity;
                    if (_scene != null && _scene.sprite != null) _scene.sprite.SetLightData(data);
                }
            }
            // SetLightData can fire native slider callbacks; restore actual light last.
            finally { if (light != null) { light.color = _oldColor; light.intensity = _oldIntensity; } }
        }

        private void StopSession()
        {
            _active = false;
            _permit = default;
            _insideLobby = false;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            Restore();
            _scene = null;
        }

        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            _registration?.Dispose();
            _registration = null;
            StopSession();
            if (_enabled != null) _enabled.SettingChanged -= SettingsChanged;
            if (_intensity != null) _intensity.SettingChanged -= SettingsChanged;
            if (_red != null) _red.SettingChanged -= SettingsChanged;
            if (_green != null) _green.SettingChanged -= SettingsChanged;
            if (_blue != null) _blue.SettingChanged -= SettingsChanged;
        }
    }
}
