using System;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameFpsDisplay
{
    [BepInDependency("canon.gate", "2.3.0")]
    [BepInPlugin(Guid, "MainGameFpsDisplay", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.fpsdisplay";
        private ConfigEntry<bool> _visible;
        private float _sampleStartedAt;
        private int _frames;
        private string _text = "FPS: --";
        private GUIStyle _style;

        private void Awake()
        {
            _visible = Config.Bind("Display", "Visible", true,
                "画面右上のFPS表示。0.5秒間の平均フレーム数を表示します。プレイバックバーから切替可能。変更は即時反映・保存されます。");
            _visible.SettingChanged += OnVisibilityChanged;
            ResetSample();
            CanonPlugins.Shared.PluginActivityGate.Bind(this, Guid, CanonGate.GateApi.ActivityKind.Preview,
                () => { ResetSample(); }, () => { ResetSample(); });
            CanonPlugins.Shared.PluginActivityGate.Begin();
        }

        public bool IsDisplayEnabled() => CanonPlugins.Shared.PluginActivityGate.Allowed && isActiveAndEnabled && _visible != null && _visible.Value;

        public void ToggleDisplay()
        {
            if (_visible == null) return;
            _visible.Value = !_visible.Value;
            Config.Save();
        }

        private void OnVisibilityChanged(object sender, EventArgs args) => ResetSample();

        private void ResetSample()
        {
            _sampleStartedAt = Time.realtimeSinceStartup;
            _frames = 0;
            _text = "FPS: --";
        }

        private void OnEnable() => ResetSample();
        private void OnApplicationFocus(bool focused) => ResetSample();
        private void OnApplicationPause(bool paused) => ResetSample();

        private void Update()
        {
            if (!IsDisplayEnabled()) return;
            ++_frames;
            float now = Time.realtimeSinceStartup;
            float elapsed = now - _sampleStartedAt;
            if (elapsed < 0.5f) return;
            _text = "FPS: " + (_frames / elapsed).ToString("F1", CultureInfo.InvariantCulture);
            _sampleStartedAt = now;
            _frames = 0;
        }

        private void OnGUI()
        {
            if (!IsDisplayEnabled() || Event.current.type != EventType.Repaint) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                _style.normal.textColor = Color.white;
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.depth = -10000;
                var rect = new Rect(Mathf.Max(0f, Screen.width - 192f), 8f, Mathf.Min(180f, Screen.width), 32f);
                GUI.color = Color.black;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), _text, _style);
                GUI.color = Color.white;
                GUI.Label(rect, _text, _style);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.depth = previousDepth;
            }
        }

        private void OnDestroy()
        {
            CanonPlugins.Shared.PluginActivityGate.Dispose();
            if (_visible != null) _visible.SettingChanged -= OnVisibilityChanged;
        }
        private void OnDisable() => CanonPlugins.Shared.PluginActivityGate.End();
    }
}
