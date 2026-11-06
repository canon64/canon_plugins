using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class HotelDoorCardSettings
    {
        public int blurRadius;
        public float paleBlend;
        public float? brightness;
    }

    internal sealed partial class HotelModeSession
    {
        private readonly Dictionary<string, HotelDoorCardDisplay> _doorCards = new Dictionary<string, HotelDoorCardDisplay>();
        private HotelDoorCardSettings _savedDoorCardSettings;
        private string _doorBlurText, _doorPaleText, _doorBrightnessText, _doorSettingsStatus;
        private bool _doorSettingsReadonly;
        private string DoorCardSettingsPath => Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "HotelMode", "door-card-display.json");

        private void InitializeDoorCards()
        {
            _savedDoorCardSettings = new HotelDoorCardSettings { blurRadius = _roomControls.doorCardBlurRadius, paleBlend = _roomControls.doorCardPaleBlend, brightness = _roomControls.doorCardBrightness };
            try
            {
                if (File.Exists(DoorCardSettingsPath))
                {
                    var saved = JsonCodec.FromJson<HotelDoorCardSettings>(File.ReadAllText(DoorCardSettingsPath));
                    // Older settings have no brightness field; preserve the original image brightness.
                    if (saved != null && !saved.brightness.HasValue) saved.brightness = _p.N("math.one");
                    if (saved == null || saved.blurRadius < default(int) || saved.blurRadius > _roomControls.doorCardBlurMax
                        || float.IsNaN(saved.paleBlend) || float.IsInfinity(saved.paleBlend) || saved.paleBlend < default(float) || saved.paleBlend > _p.N("math.one")
                        || float.IsNaN(saved.brightness.Value) || float.IsInfinity(saved.brightness.Value)
                        || saved.brightness.Value < default(float) || saved.brightness.Value > _roomControls.doorCardBrightnessMax)
                        throw new InvalidDataException("ドア画像の保存設定が不正です。元ファイルは変更していません。");
                    _savedDoorCardSettings = saved;
                }
            }
            catch (Exception ex) { _doorSettingsReadonly = true; _doorSettingsStatus = ex.Message; }
            RestoreDoorCardSettings();
            var panes = _floors.Where(f => f.Roots != null).SelectMany(f => f.Roots)
                .SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            foreach (string room in HotelRoomCards.Rooms.Where(room => room != "reception"))
            {
                try
                {
                    var pane = panes.Single(p => p.name == "2F_Room_" + room + "_o_h2_mi_lobby10kg_00");
                    _doorCards.Add(room, new HotelDoorCardDisplay(pane, _p, _roomControls));
                    _doorCards[room].Show(_roomCards.Registered(room));
                }
                catch (Exception ex) { _roomCards.ShowError(new InvalidOperationException(room + "号室のドア画像：" + ex.Message, ex)); }
            }
        }

        private void RoomCardSaved(string room)
        {
            if (room == "reception") _receptionist.Load(_roomCards.Registered(room));
            else if (_doorCards.TryGetValue(room, out var display)) display.Show(_roomCards.Registered(room));
        }

        private void DisposeDoorCards()
        {
            foreach (var display in _doorCards.Values) display.Dispose();
            _doorCards.Clear();
        }

        private void ApplyDoorCardSettings()
        {
            foreach (var display in _doorCards.Values) display.RefreshAppearance();
        }

        private void RestoreDoorCardSettings()
        {
            if (_savedDoorCardSettings == null) return;
            _roomControls.doorCardBlurRadius = _savedDoorCardSettings.blurRadius;
            _roomControls.doorCardPaleBlend = _savedDoorCardSettings.paleBlend;
            _roomControls.doorCardBrightness = _savedDoorCardSettings.brightness ?? _p.N("math.one");
            _doorBlurText = _roomControls.doorCardBlurRadius.ToString(CultureInfo.InvariantCulture);
            _doorPaleText = _roomControls.doorCardPaleBlend.ToString("0.###", CultureInfo.InvariantCulture);
            _doorBrightnessText = _roomControls.doorCardBrightness.ToString("0.###", CultureInfo.InvariantCulture);
            ApplyDoorCardSettings();
        }

        private float DoorCardSlider(string label, string control, float value, float max, bool integer, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(_roomControls.labelWidth));
            float changed = GUILayout.HorizontalSlider(value, _p.N("math.zero"), max, GUILayout.Width(_roomControls.sliderWidth));
            if (integer) changed = Mathf.Round(changed);
            if (changed != value)
            {
                text = changed.ToString(integer ? "0" : "0.###", CultureInfo.InvariantCulture);
                value = float.Parse(text, CultureInfo.InvariantCulture);
            }
            GUI.SetNextControlName("MainGameAiShoujoMode.Hotel.DoorCard." + control);
            string edited = GUILayout.TextField(text, GUILayout.Width(_roomControls.fieldWidth));
            if (edited != text)
            {
                text = edited;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= default(float) && parsed <= max
                    && (!integer || parsed == Mathf.Round(parsed))) value = parsed;
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private void DrawDoorCardSettings()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ドアのカード画像（全客室共通・その場で反映）");
            int blur = Mathf.RoundToInt(DoorCardSlider("ぼかし(px)", "Blur", _roomControls.doorCardBlurRadius,
                _roomControls.doorCardBlurMax, true, ref _doorBlurText));
            float pale = DoorCardSlider("淡さ", "Pale", _roomControls.doorCardPaleBlend, _p.N("math.one"), false, ref _doorPaleText);
            float brightness = DoorCardSlider("明暗(倍率)", "Brightness", _roomControls.doorCardBrightness,
                _roomControls.doorCardBrightnessMax, false, ref _doorBrightnessText);
            if (blur != _roomControls.doorCardBlurRadius || pale != _roomControls.doorCardPaleBlend || brightness != _roomControls.doorCardBrightness)
            {
                _roomControls.doorCardBlurRadius = blur; _roomControls.doorCardPaleBlend = pale;
                _roomControls.doorCardBrightness = brightness;
                ApplyDoorCardSettings();
                if (!_doorSettingsReadonly) _doorSettingsStatus = "調整中です。保存せず閉じると元に戻ります。";
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("画像設定を保存", GUILayout.ExpandWidth(false))) SaveDoorCardSettings();
            if (GUILayout.Button("保存した値に戻す", GUILayout.ExpandWidth(false))) RestoreDoorCardSettings();
            GUILayout.EndHorizontal();
            GUILayout.Label(_doorSettingsStatus ?? "保存せずメニューを閉じると、保存した値に戻ります。");
            GUILayout.EndVertical();
        }

        private void SaveDoorCardSettings()
        {
            string temporary = DoorCardSettingsPath + ".tmp";
            try
            {
                if (_doorSettingsReadonly) throw new InvalidOperationException("保存設定の読込エラーがあるため上書きできません。");
                if (!int.TryParse(_doorBlurText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var blur) || blur != _roomControls.doorCardBlurRadius
                    || !float.TryParse(_doorPaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pale) || pale != _roomControls.doorCardPaleBlend
                    || !float.TryParse(_doorBrightnessText, NumberStyles.Float, CultureInfo.InvariantCulture, out var brightness) || brightness != _roomControls.doorCardBrightness)
                    throw new InvalidOperationException("入力途中・範囲外の数値があります。");
                var saved = new HotelDoorCardSettings { blurRadius = blur, paleBlend = pale, brightness = brightness };
                Directory.CreateDirectory(Path.GetDirectoryName(DoorCardSettingsPath));
                File.WriteAllText(temporary, JsonCodec.ToJson(saved, true));
                if (File.Exists(DoorCardSettingsPath)) File.Replace(temporary, DoorCardSettingsPath, DoorCardSettingsPath + ".bak");
                else File.Move(temporary, DoorCardSettingsPath);
                _savedDoorCardSettings = saved; _doorSettingsStatus = "保存しました。次回起動後も使います。";
            }
            catch (Exception ex) { _doorSettingsStatus = ex.Message; }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
