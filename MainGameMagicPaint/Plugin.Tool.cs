using System;
using MainGameTransformGizmo;
using CanonPlugins.Shared;
using UnityEngine;
using VRGIN.Core;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        readonly GadgetBarCycle _bar = new GadgetBarCycle();
        TransformGizmo _gizmo;
        Transform View() => VR.Active && VR.Camera != null ? VR.Camera.Head : Camera.main != null ? Camera.main.transform : null;
        public void PlaybackBarPrimaryAction()
        {
            if (!CanAct || !Application.isFocused) return;
            _bar.Press(_model != null, () => { SpawnTool(); return _model != null; }, ShowGizmo, HideGizmo, _gizmo != null && _gizmo.IsVisible);
        }
        public void PlaybackBarDelete() => DeleteTool();
        public string GetPlaybackBarActionHint() => "左クリック：初回だけ目の前へ呼び出し。その後はギズモ表示／非表示を切り替え。\n右クリック：アイテム削除（落書きは残ります）。\nVR：握ってトリガー／押し込みで道具選択／Aで設定表示／上下で項目・左右で調整／設定を閉じて左右UNDO・REDO／Bで全削除。";
        void SpawnTool()
        {
            if (!CanAct) return;
            try
            {
                ReleaseHand(); HideGizmo(); SetDesktop(false);
                if (_model == null) { _model = new ToolModel(_scene.transform, _tool); _hud = new ToolHud(_scene.transform); _model.SetColor(InkColor); }
                _model.Visual.localPosition = Vector3.zero;
                var view = View(); if (view == null) throw new InvalidOperationException("視点がありません");
                _model.Root.transform.SetPositionAndRotation(view.position + view.forward * .35f, view.rotation);
                _status = "呼び出しました。VRは近くでグリップ、通常版は設定の「マウスで描く」。";
            }
            catch (Exception ex) { DeleteTool(); Fail("生成失敗", ex); }
        }
        bool ShowGizmo()
        {
            if (_model == null) return false; ReleaseHand(); SetDesktop(false);
            if (!TransformGizmoApi.IsAvailable) { _status = "共通ギズモが利用できません"; return false; }
            _gizmo = TransformGizmoApi.Attach(_model.Root);
            if (_gizmo == null) return false;
            _gizmo.enabled = true; TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo); TransformGizmoApi.SetSizeMultiplier(_gizmo, .3f); _gizmo.SetMode(GizmoMode.Move); _gizmo.SetVisible(true); return _gizmo.IsVisible;
        }
        void HideGizmo()
        {
            if (_gizmo == null) return; _gizmo.SetVisible(false); TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo); _gizmo.enabled = false;
        }
        void DeleteTool()
        {
            try { ReleaseHand(); SetDesktop(false); HideGizmo(); }
            finally { _gizmo = null; _hud?.Dispose(); _hud = null; _model?.Dispose(); _model = null; _bar.Reset(); }
        }
        void ChangeTool(PaintTool tool)
        {
            if (!CanAct || tool == _tool) return; StopPaintPress(); EndStroke(); CancelInspection(); ResetContacts();
            _tool = tool; if (tool == PaintTool.Marker && _colorIndex == 10) _colorIndex = 0;
            _preferredTool.Value = tool; _preferredColor.Value = _colorIndex;
            if (tool == PaintTool.Marker && Array.IndexOf(MarkerSettings, _setting) < 0) _setting = PaintSetting.Width;
            if (_model != null)
            {
                var old = _model; var anchor = old.Root.transform; var parent = anchor.parent; var pos = anchor.position; var rot = anchor.rotation;
                if (_loop != null) { _loop.Owner = null; _loop.enabled = false; Destroy(_loop); _loop = null; }
                HideGizmo(); _gizmo = null;
                _model = new ToolModel(parent, tool); _model.Root.transform.SetPositionAndRotation(pos, rot); _model.SetColor(InkColor); old.Dispose();
                if (_hand != null) { _loop = _model.Root.AddComponent<HeldPaintLoop>(); _loop.Owner = this; }
            }
            _contact = false; _trigger = false; _gaugeUntil = Time.unscaledTime + 2;
            _hud?.Hide();
        }
        static string SettingName(PaintSetting s)
        {
            switch (s) { case PaintSetting.Color:return "色"; case PaintSetting.Width:return "太さ"; case PaintSetting.Grain:return "粗さ"; case PaintSetting.Transparency:return "透明度"; case PaintSetting.Flow:return "噴射量"; case PaintSetting.Feather:return "縁のぼかし"; default:return "ばらつき"; }
        }
        void UpdateHud()
        {
            if (_model == null || _hud == null) return;
            float normalized = 1; string label = "色：" + ColorNames[_colorIndex]; var entry = SettingValue();
            if (entry != null)
            {
                var range = (BepInEx.Configuration.AcceptableValueRange<float>)entry.Description.AcceptableValues;
                normalized = Mathf.InverseLerp(range.MinValue, range.MaxValue, entry.Value);
                string unit = _setting == PaintSetting.Width || _setting == PaintSetting.Grain ? " mm" : _setting == PaintSetting.Flow ? " 粒/秒" : " %";
                label = SettingName(_setting) + "：" + entry.Value.ToString("0.#") + unit;
            }
            var view = View(); Vector3 offset = view != null ? view.right * .16f : Vector3.up * .1f;
            var settings = _tool == PaintTool.Marker ? MarkerSettings : SpraySettings;
            var rows = new System.Text.StringBuilder();
            foreach (var setting in settings)
            {
                rows.Append(setting == _setting ? "▶ " : "   ");
                if (setting == PaintSetting.Color) rows.Append("色：").Append(ColorNames[_colorIndex]);
                else
                {
                    var value = setting == PaintSetting.Width ? (_tool == PaintTool.Marker ? _markerWidth : _sprayWidth) : setting == PaintSetting.Grain ? _grain : setting == PaintSetting.Transparency ? _transparency : setting == PaintSetting.Flow ? _flow : setting == PaintSetting.Feather ? _feather : _variation;
                    rows.Append(SettingName(setting)).Append("：").Append(value.Value.ToString("0.#")).Append(setting == PaintSetting.Width || setting == PaintSetting.Grain ? " mm" : setting == PaintSetting.Flow ? " 粒/秒" : " %");
                }
                rows.AppendLine();
            }
            rows.Append("上下：項目　左右：調整　A：閉じる");
            label = rows.ToString();
            var actor = Selected;
            int pixels = (actor?.FaceBuffer?.InkPixels ?? 0) + (actor?.BodyBuffer?.InkPixels ?? 0);
            string drawingStatus = _status + "\nインク：" + pixels + " px";
            _hud.Show(_model.Root.transform.position + offset, view, drawingStatus, label, normalized, _setting == PaintSetting.Color ? InkColor : new Color(.25f,.9f,.75f), _settingsVisible, _stick.Menu, _stick.MenuSelection);
        }
    }
}
