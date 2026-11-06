using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MainGameUiInputCapture;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class HotelRoomControlSettings
    {
        public float x, y, width, height, buttonHeight, fieldWidth, labelWidth, sliderWidth, angleRange;
        public float[] ranges;
        public float lightDefault, emissionDefault, bloomDefault, brightnessMax, neonMargin, neonDrop, neonWidth, neonEmission;
        public Color neonColor;
        public float colorMax, lightListHeight, ceilingLampDrop, ceilingLampDiameter, ceilingLampHalfHeight, ceilingLightOffset, ceilingLightRange, ceilingLightIntensity;
        public Color ceilingLightColor;
        public string[] preferences;
        public Vector3 receptionistPosition;
        public string receptionistControllerBundle, receptionistControllerAsset, receptionistIdle;
        public string[] receptionistPoses;
        public float receptionistYaw, receptionistReach, receptionistViewDot, receptionistHeadHeight;
        public int doorCardBlurRadius;
        public int doorCardBlurMax;
        public float doorCardPaleBlend;
        public float doorCardBrightness, doorCardBrightnessMax;
        public void Validate()
        {
            if (doorCardBlurMax <= default(int) || doorCardBlurRadius < default(int) || doorCardBlurRadius > doorCardBlurMax || float.IsNaN(doorCardPaleBlend) || float.IsInfinity(doorCardPaleBlend)
                || doorCardPaleBlend < default(float) || doorCardPaleBlend > Color.white.a
                || float.IsNaN(doorCardBrightnessMax) || float.IsInfinity(doorCardBrightnessMax) || doorCardBrightnessMax <= default(float)
                || float.IsNaN(doorCardBrightness) || float.IsInfinity(doorCardBrightness) || doorCardBrightness < default(float) || doorCardBrightness > doorCardBrightnessMax)
                throw new InvalidOperationException("ドア画像のぼかし・淡色設定が不正です。");
            if (ranges == null || !ranges.Any() || ranges.Concat(new[] { width, height, buttonHeight, fieldWidth, labelWidth, sliderWidth, angleRange })
                .Any(value => float.IsNaN(value) || float.IsInfinity(value) || value <= default(float)))
                throw new InvalidOperationException("客室座標UIの寸法・調整幅が不正です。");
            if (new[] { lightDefault, emissionDefault, bloomDefault }.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < default(float) || v > brightnessMax)
                || new[] { brightnessMax, neonMargin, neonDrop, neonWidth, neonEmission, receptionistReach, receptionistHeadHeight,
                    colorMax, lightListHeight, ceilingLampDrop, ceilingLampDiameter, ceilingLampHalfHeight, ceilingLightOffset, ceilingLightRange, ceilingLightIntensity }.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v <= default(float))
                || string.IsNullOrWhiteSpace(receptionistControllerBundle) || string.IsNullOrWhiteSpace(receptionistControllerAsset) || string.IsNullOrWhiteSpace(receptionistIdle)
                || receptionistPoses == null || !receptionistPoses.Contains(receptionistIdle) || receptionistPoses.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("客室の照明・ネオン・受付嬢設定が不正です。");
        }
    }
    internal sealed class HotelRoomPlacementPanel : IDisposable
    {
        private const string Owner = "MainGameAiShoujoMode.HotelPlacement";
        private readonly HotelRoomControlSettings _cfg;
        private readonly Parameters _p;
        private readonly Func<HotelRoomPlacement> _read;
        private readonly Action<HotelRoomPlacement> _apply, _save;
        private readonly Dictionary<string, string> _inputs = new Dictionary<string, string>();
        private readonly Dictionary<string, float> _centers = new Dictionary<string, float>();
        private HotelRoomPlacement _draft, _before;
        private bool _shown, _drawShown, _placementEdited;
        private float _range;
        private int _drag;
        private string _status = "位置・向きは客室内の座標（m）です。体位は変更時に自動保存します。";
        internal Action DrawGadgets, DrawLighting, DrawObjects, DrawAudioDiagnostics, Opened, Closed, OpenRegistration;
        internal Func<bool> SaveEverything { get; set; }
        internal Func<Func<HotelRoomPlacement>, Action<HotelRoomPlacement>, bool> SaveEverythingWithPlacement;
        internal Func<bool> CanEditPlacement;
        private Vector2 _scroll;
        internal bool Visible => _shown;
        internal void Toggle() { if (_shown) Close(true); else Open(); }
        internal HotelRoomPlacementPanel(HotelRoomControlSettings cfg, Parameters p, Func<HotelRoomPlacement> read,
            Action<HotelRoomPlacement> apply, Action<HotelRoomPlacement> save)
        { cfg.Validate(); _cfg = cfg; _p = p; _read = read; _apply = apply; _save = save; _range = cfg.ranges.First(); }
        internal void Status(string text) { _status = text; }
        private float Get(string axis) => axis == "X" ? _draft.x : axis == "Y" ? _draft.y : axis == "Z" ? _draft.z : _draft.yaw;
        private void Set(string axis, float value)
        {
            if (axis == "X") _draft.x = value; else if (axis == "Y") _draft.y = value; else if (axis == "Z") _draft.z = value; else _draft.yaw = value;
        }
        private void Recenter()
        {
            foreach (string axis in new[] { "X", "Y", "Z", "向き" }) _centers[axis] = Get(axis);
        }
        private void Open()
        {
            try
            {
                _draft = _read(); _before = _draft.Copy(); Recenter();
                _placementEdited = false;
                foreach (string axis in _centers.Keys) _inputs[axis] = Get(axis).ToString("0.######", CultureInfo.InvariantCulture);
                _shown = true;
                Opened?.Invoke();
            }
            catch (Exception ex) { _status = ex.Message; }
        }
        internal void RefreshPlacementBaseline()
        {
            if (!_shown) return;
            try
            {
                ReleaseInput();
                _draft = _read(); _before = _draft.Copy(); Recenter();
                _placementEdited = false;
                foreach (string axis in _centers.Keys) _inputs[axis] = Get(axis).ToString("0.######", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { _status = ex.Message; }
        }
        private void Apply()
        {
            if (CanEditPlacement?.Invoke() == false) return;
            try { _apply(_draft.Copy()); _placementEdited = true; }
            catch (Exception ex) { _status = ex.Message; }
        }
        private void CommitPlacement()
        { PersistPlacement(ReadPlacementForSave()); }
        private HotelRoomPlacement ReadPlacementForSave()
        {
            if (CanEditPlacement?.Invoke() == false) throw new InvalidOperationException("体位のギズモ編集を終了してから、客室の配置を操作してください。");
            if (_inputs.Values.Any(text => !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException("入力途中の数値があります。すべて有効な数値にしてから保存してください。");
            return _draft.Copy();
        }
        private void PersistPlacement(HotelRoomPlacement value)
        { _save(value); AcceptSavedPlacement(value); }
        private void AcceptSavedPlacement(HotelRoomPlacement value)
        { _before = value.Copy(); _placementEdited = false; }
        internal void Close(bool restore)
        {
            if (_shown && restore && _placementEdited && _before != null && CanEditPlacement?.Invoke() != false)
            {
                try
                {
                    var current = _read();
                    // An external category/gizmo move invalidates this panel's old rollback.
                    if (Mathf.Approximately(current.x, _draft.x) && Mathf.Approximately(current.y, _draft.y)
                        && Mathf.Approximately(current.z, _draft.z) && Mathf.Approximately(Mathf.DeltaAngle(current.yaw, _draft.yaw), _p.N("math.zero")))
                        _apply(_before.Copy());
                }
                catch (Exception ex) { _status = ex.Message; }
            }
            _shown = false; ReleaseInput();
            Closed?.Invoke();
            if ((GUI.GetNameOfFocusedControl() ?? "").StartsWith(Owner, StringComparison.Ordinal)) GUIUtility.keyboardControl = _p.I("math.zero");
        }
        internal void ReleaseInput()
        {
            if (_drag != _p.I("math.zero") && GUIUtility.hotControl == _drag) GUIUtility.hotControl = _p.I("math.zero");
            _drag = _p.I("math.zero");
            UiInputCaptureApi.Sync(Owner, "placement-edit", false);
            UiInputCaptureApi.ClearInputBlockRects(Owner); UiInputCaptureApi.SetIdleCursorUnlock(Owner, false);
        }
        private void Axis(string axis)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(axis == "向き" ? "向き Y（度）" : axis + "（m）", GUILayout.Width(_cfg.labelWidth));
            float old = Get(axis), span = axis == "向き" ? _cfg.angleRange : _range;
            int hot = GUIUtility.hotControl;
            float changed = GUILayout.HorizontalSlider(old, _centers[axis] - span, _centers[axis] + span, GUILayout.Width(_cfg.sliderWidth));
            if (Event.current.rawType == EventType.MouseDown && GUIUtility.hotControl != hot) _drag = GUIUtility.hotControl;
            if (changed != old) { Set(axis, changed); _inputs[axis] = changed.ToString("0.######", CultureInfo.InvariantCulture); Apply(); }
            GUI.SetNextControlName(Owner + axis);
            string next = GUILayout.TextField(_inputs[axis], GUILayout.Width(_cfg.fieldWidth));
            if (next != _inputs[axis])
            {
                _inputs[axis] = next;
                if (float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed) && !float.IsNaN(typed) && !float.IsInfinity(typed))
                { Set(axis, typed); _centers[axis] = typed; Apply(); }
            }
            GUILayout.EndHorizontal();
        }
        internal void Draw()
        {
            int initialHot = GUIUtility.hotControl;
            if (Event.current.type == EventType.Layout) _drawShown = _shown;
            if (!_drawShown) return;
            var rect = new Rect(Mathf.Min(_cfg.x, Mathf.Max(_p.N("math.zero"), Screen.width - _cfg.width)), _cfg.y,
                Mathf.Min(_cfg.width, Screen.width), Mathf.Min(_drawShown ? _cfg.height : _cfg.buttonHeight, Screen.height - _cfg.y));
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("ホテルメニュー　TABで開閉");
            if (GUILayout.Button("閉じる", GUILayout.ExpandWidth(false)))
            { if (_shown) Close(true); else Open(); }
            if (_drawShown)
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                if (GUILayout.Button("全客室・受付嬢のカード登録", GUILayout.ExpandWidth(false))) OpenRegistration?.Invoke();
                if (GUILayout.Button("現在の客室状態をまとめて保存", GUILayout.ExpandWidth(false)))
                {
                    try
                    {
                        bool saved = SaveEverythingWithPlacement != null ? SaveEverythingWithPlacement(ReadPlacementForSave, AcceptSavedPlacement)
                            : SaveLegacy();
                        if (saved) _status = "現在の客室状態を保存しました。";
                    }
                    catch (Exception ex) { _status = ex.Message; }
                }
                DrawGadgets?.Invoke(); DrawLighting?.Invoke(); DrawObjects?.Invoke(); DrawAudioDiagnostics?.Invoke();
                GUILayout.Label("女の子の位置・向き");
                GUILayout.Label(_status);
                bool previousEnabled = GUI.enabled;
                bool canEdit = CanEditPlacement?.Invoke() != false;
                GUI.enabled = previousEnabled && canEdit;
                GUILayout.BeginHorizontal(); GUILayout.Label("位置の調整幅", GUILayout.ExpandWidth(false));
                foreach (var range in _cfg.ranges)
                    if (GUILayout.Button((_range == range ? "● " : "") + "±" + range.ToString(CultureInfo.InvariantCulture) + "m", GUILayout.ExpandWidth(false)))
                    { _range = range; Recenter(); }
                GUILayout.EndHorizontal();
                foreach (string axis in new[] { "X", "Y", "Z", "向き" }) Axis(axis);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("現在値の周辺を調整", GUILayout.ExpandWidth(false))) Recenter();
                if (GUILayout.Button("位置・向きを保存", GUILayout.ExpandWidth(false)))
                {
                    try
                    {
                        CommitPlacement(); _status = "位置・向きを保存しました。次回もこの配置を使います。";
                    }
                    catch (Exception ex) { _status = ex.Message; }
                }
                GUILayout.EndHorizontal();
                GUI.enabled = previousEnabled;
                if (!canEdit) GUILayout.Label("体位のギズモ編集中です。そちらの保存・キャンセル後に操作できます。");
                GUILayout.Label("閉じると未保存の位置・向きを元に戻します。体位は本編の一覧で選択してください。");
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            if (!Application.isFocused) return;
            if (Event.current.rawType == EventType.MouseDown && GUIUtility.hotControl != initialHot) _drag = GUIUtility.hotControl;
            UiInputCaptureApi.SetInputBlockRects(Owner, new[] { rect });
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, _shown);
            if (_drag != _p.I("math.zero") && !Input.GetMouseButton(_p.I("math.zero"))) _drag = _p.I("math.zero");
            bool typing = (GUI.GetNameOfFocusedControl() ?? "").StartsWith(Owner, StringComparison.Ordinal);
            UiInputCaptureApi.Sync(Owner, "placement-edit", _drag != _p.I("math.zero") || typing);
        }
        private bool SaveLegacy() { CommitPlacement(); return SaveEverything?.Invoke() == true; }
        public void Dispose() { Close(true); }
    }
}
