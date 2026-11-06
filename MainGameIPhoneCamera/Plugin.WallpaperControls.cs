using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private string _wallpaperXText = ".50", _wallpaperYText = ".50", _wallpaperZoomText = "1.00";
        private void DrawWallpaperControls()
        {
            if (_phone.Page != PhonePage.Wallpaper || _wallpaperDraft == null) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("待受の切り抜き位置・倍率（端末のスティック操作と共通）");
            bool enabled = GUI.enabled; GUI.enabled = enabled && !_wallpaperSaving;
            bool changed = WallpaperNumber("横位置", ref _wallpaperDraft.X, 0, 1, ref _wallpaperXText);
            changed |= WallpaperNumber("縦位置", ref _wallpaperDraft.Y, 0, 1, ref _wallpaperYText);
            changed |= WallpaperNumber("拡大率", ref _wallpaperDraft.Zoom, 1, 8, ref _wallpaperZoomText);
            if (changed) _shellUi.EditorValues(_viewerTexture, _wallpaperDraft);
            if (GUILayout.Button("待受画像を保存", GUILayout.Width(170))) SaveWallpaper();
            GUI.enabled = enabled; GUILayout.EndVertical();
        }
        private static bool WallpaperNumber(string label, ref float value, float min, float max, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            float before = value, slider = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180));
            if (Math.Abs(slider - value) > .0001f) { value = slider; text = value.ToString("0.000"); }
            string input = GUILayout.TextField(text, GUILayout.Width(85));
            if (input != text)
            {
                text = input;
                if (float.TryParse(input, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max) value = parsed;
            }
            GUILayout.Label(min + "～" + max, GUILayout.Width(55));
            GUILayout.EndHorizontal(); return Math.Abs(value - before) > .0001f;
        }
        private void SyncWallpaperNumbers()
        {
            _wallpaperXText = _wallpaperDraft.X.ToString("0.000");
            _wallpaperYText = _wallpaperDraft.Y.ToString("0.000");
            _wallpaperZoomText = _wallpaperDraft.Zoom.ToString("0.000");
        }
    }
}
