using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private GUIStyle _pistonHudLabel;
        private void DrawPistonHud()
        {
            if (_model == null || _settings == null || !_settings.Enabled || Event.current.type != EventType.Repaint) return;
            float width = Mathf.Min(264f, Screen.width - 16f);
            var panel = new Rect(Mathf.Max(8f, Screen.width - width - 12f), 48f, width, 108f);
            if (_pistonHudLabel == null) _pistonHudLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            GUI.Box(panel, GUIContent.none);
            float speed = PistonPolicy.Speed(_settings.PistonSpeed), stroke = PistonPolicy.Stroke(_settings.PistonStrokeCm);
            DrawPistonGauge(new Rect(panel.x + 8f, panel.y + 4f, width - 16f, 30f),
                "往復幅 " + stroke.ToString("F1") + " / 20 cm", stroke / 20f, new Color(.25f, .75f, 1f, 1f));
            DrawPistonGauge(new Rect(panel.x + 8f, panel.y + 38f, width - 16f, 30f),
                "設定速度 " + speed.ToString("F2") + " / 5 往復/秒", speed / 5f, new Color(.4f, 1f, .45f, 1f));
            string state = IsPistonDriving ? "動作中" : VRGIN.Core.VR.Active ? "停止中・掴んでトリガーで開始／停止" : "停止中";
            if (_settings.PistonRhythmSync && _pistonRhythmAvailable && IsPistonDriving && _pistonAnimation?[PistonClipName] != null)
                state += "（同期速度 " + _pistonAnimation[PistonClipName].speed.ToString("F2") + "）";
            GUI.Label(new Rect(panel.x + 8f, panel.y + 77f, width - 16f, 24f), state, _pistonHudLabel);
        }
        private void DrawPistonGauge(Rect rect, string label, float fraction, Color tint)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), label, _pistonHudLabel);
            var bar = new Rect(rect.x, rect.y + 21f, rect.width, 7f);
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(.12f, .12f, .16f, .95f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = tint;
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height), Texture2D.whiteTexture);
            }
            finally { GUI.color = previous; }
        }
    }
}
