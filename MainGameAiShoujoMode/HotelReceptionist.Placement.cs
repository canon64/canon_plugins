using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelReceptionist
    {
        private const string EditPrefix = "MainGameAiShoujoMode.HotelReception.";
        private HotelReceptionistPlacement _placement, _savedPlacement;
        private readonly Dictionary<string, string> _coordinates = new Dictionary<string, string>();
        private readonly Dictionary<string, float> _centers = new Dictionary<string, float>();
        private float _positionRange;
        private bool _showPlacement, _drawPlacement;
        private static readonly string[] Axes = { "X", "Y", "Z", "RX", "RY", "RZ" };
        private float ReadAxis(string axis)
        {
            switch (axis) { case "X": return _placement.x; case "Y": return _placement.y; case "Z": return _placement.z;
                case "RX": return _placement.rx; case "RY": return _placement.ry; default: return _placement.rz; }
        }
        private void SetAxis(string axis, float value)
        {
            switch (axis) { case "X": _placement.x = value; break; case "Y": _placement.y = value; break; case "Z": _placement.z = value; break;
                case "RX": _placement.rx = value; break; case "RY": _placement.ry = value; break; default: _placement.rz = value; break; }
        }
        private void RecenterPlacement() { foreach (string axis in Axes) _centers[axis] = ReadAxis(axis); }
        private void ResetPlacementInputs()
        {
            if (_positionRange == default(float)) _positionRange = _cfg.ranges.First();
            foreach (string axis in Axes) _coordinates[axis] = ReadAxis(axis).ToString("0.######", CultureInfo.InvariantCulture);
            RecenterPlacement();
        }
        private void ApplyPlacement()
        {
            _placement.Validate();
            if (_stage != null) _stage.transform.SetPositionAndRotation(new Vector3(_placement.x, _placement.y, _placement.z), Quaternion.Euler(_placement.rx, _placement.ry, _placement.rz));
        }
        internal void RestorePlacement()
        {
            if (_savedPlacement == null) return;
            _placement = _savedPlacement.Copy(); ResetPlacementInputs(); ApplyPlacement();
        }
        private void DrawPlacementAxis(string axis)
        {
            bool rotation = axis.StartsWith("R", StringComparison.Ordinal);
            float value = ReadAxis(axis), range = rotation ? _cfg.angleRange : _positionRange;
            GUILayout.BeginHorizontal();
            GUILayout.Label((rotation ? "回転 " + axis.Substring(_p.I("math.one")) + "（度）" : "位置 " + axis + "（m）"), GUILayout.Width(_cfg.labelWidth));
            float next = GUILayout.HorizontalSlider(value, _centers[axis] - range, _centers[axis] + range, GUILayout.Width(_cfg.sliderWidth));
            if (next != value) { SetAxis(axis, next); _coordinates[axis] = next.ToString("0.######", CultureInfo.InvariantCulture); ApplyPlacement(); }
            GUI.SetNextControlName(EditPrefix + axis);
            string text = GUILayout.TextField(_coordinates[axis], GUILayout.Width(_cfg.fieldWidth));
            if (text != _coordinates[axis])
            {
                _coordinates[axis] = text;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float typed) && !float.IsNaN(typed) && !float.IsInfinity(typed))
                { SetAxis(axis, typed); _centers[axis] = typed; ApplyPlacement(); }
            }
            GUILayout.EndHorizontal();
        }
        private void DrawPlacement()
        {
            if (_placement == null) return;
            if (Event.current.type == EventType.Layout) _drawPlacement = _showPlacement;
            GUILayout.BeginVertical(GUI.skin.box);
            if (GUILayout.Button((_showPlacement ? "▼ " : "▶ ") + "受付嬢の位置・角度", GUILayout.ExpandWidth(false))) _showPlacement = !_showPlacement;
            if (_drawPlacement)
            {
                GUILayout.Label("ロビー内の位置XYZ・回転XYZを調整します。TABで閉じると未保存の配置を戻します。");
                GUILayout.BeginHorizontal(); GUILayout.Label("位置の調整幅", GUILayout.ExpandWidth(false));
                foreach (float range in _cfg.ranges)
                    if (GUILayout.Button((_positionRange == range ? "● " : "") + "±" + range.ToString(CultureInfo.InvariantCulture) + "m", GUILayout.ExpandWidth(false)))
                    { _positionRange = range; RecenterPlacement(); }
                GUILayout.EndHorizontal();
                foreach (string axis in Axes) DrawPlacementAxis(axis);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("現在値の周辺を調整", GUILayout.ExpandWidth(false))) RecenterPlacement();
                if (GUILayout.Button("位置・角度を保存", GUILayout.ExpandWidth(false)))
                {
                    try
                    {
                        if (_coordinates.Values.Any(text => !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value)))
                            throw new InvalidOperationException("入力途中の数値があります。");
                        _store.SaveReceptionistPlacement(_placement); _savedPlacement = _placement.Copy(); _status = "受付嬢の位置・角度を保存しました。";
                    }
                    catch (Exception ex) { _status = ex.GetBaseException().Message; }
                }
                if (GUILayout.Button("保存済みの配置へ戻す", GUILayout.ExpandWidth(false))) RestorePlacement();
                GUILayout.EndHorizontal();
                if (!string.IsNullOrWhiteSpace(_status)) GUILayout.Label(_status);
            }
            GUILayout.EndVertical();
        }
    }
}
