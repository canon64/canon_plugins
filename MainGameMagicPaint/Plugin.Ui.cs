using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using MainGameUiInputCapture;
using VRGIN.Core;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        readonly Dictionary<string,string> _drafts = new Dictionary<string,string>();
        string _saveName = "落書き01";
        bool _mouseDown;
        float _lastMousePaint;
        PaintTool _drawTool;
        PaintActor[] _drawActors = new PaintActor[0];
        Rect _mouseExit = new Rect(16, 16, 260, 76);
        void DrawTab()
        {
            if (!CanAct) { GUILayout.Label("対象のフリーHを開始してください。無効化後の再開は次のHからです。"); return; }
            if (Event.current.type == EventType.Layout) { _drawTool = _tool; _drawActors = _actors.ToArray(); _drawSaves = _saves; }
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("道具・対象キャラ");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("呼び出し", GUILayout.Width(90))) SpawnTool();
            if (GUILayout.Button("アイテム削除", GUILayout.Width(115))) PlaybackBarDelete();
            if (GUILayout.Button("対象を再取得", GUILayout.Width(115))) { EndStroke(); RefreshActors(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tool == PaintTool.Marker, "マジック", "Button", GUILayout.Width(100))) ChangeTool(PaintTool.Marker);
            if (GUILayout.Toggle(_tool == PaintTool.Spray, "スプレー", "Button", GUILayout.Width(100))) ChangeTool(PaintTool.Spray);
            GUILayout.EndHorizontal();
            for (int i = 0; i < _drawActors.Length; i++)
            {
                var actor = _drawActors[i]; string name = actor.Actor != null ? actor.Actor.chaFile.parameter.fullname : "キャラ消失";
                if (GUILayout.Toggle(_target == i, "女性" + (i + 1) + "：" + name, "Button", GUILayout.Width(280)) && _target != i) { EndStroke(); _target = i; }
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("色・描画設定");
            int count = _drawTool == PaintTool.Marker ? 10 : 11;
            for (int start = 0; start < count; start += 6)
            {
                GUILayout.BeginHorizontal();
                for (int i = start; i < Mathf.Min(count, start + 6); i++)
                    if (GUILayout.Toggle(_colorIndex == i, ColorNames[i], "Button", GUILayout.Width(64)) && _colorIndex != i)
                    { _colorIndex = i; _preferredColor.Value = i; _model?.SetColor(InkColor); _setting = PaintSetting.Color; _gaugeUntil = Time.unscaledTime + 2; }
                GUILayout.EndHorizontal();
            }
            if (_drawTool == PaintTool.Marker) Slider("線幅", _markerWidth, "mm"); else { Slider("噴射直径（10cm先）", _sprayWidth, "mm"); Slider("粒の粗さ", _grain, "mm"); }
            Slider("透明度", _transparency, "%");
            if (_drawTool == PaintTool.Spray) { Slider("噴射量", _flow, "粒/秒"); Slider("縁のぼかし", _feather, "%"); Slider("ばらつき", _variation, "%"); }
            else { Slider("接地開始距離", _snap, "mm"); Slider("解除・補正上限", _detach, "mm"); Slider("後端消しゴム", _eraserWidth, "mm"); }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("編集"); GUILayout.BeginHorizontal();
            if (GUILayout.Button("UNDO", GUILayout.Width(85))) Undo(false);
            if (GUILayout.Button("REDO", GUILayout.Width(85))) Undo(true);
            if (GUILayout.Button("顔・体の落書きを全削除", GUILayout.Width(210))) ClearDrawing();
            GUILayout.EndHorizontal();
            if (!VR.Active)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("マウスで描く", GUILayout.Width(125))) { if (_model == null) SpawnTool(); _desktopErase = false; SetDesktop(true); MainGameUnifiedUi.UnifiedUiApi.SetVisible(false); }
                if (GUILayout.Button("マウス消しゴム", GUILayout.Width(140))) { if (_model == null) SpawnTool(); _desktopErase = true; SetDesktop(true); MainGameUnifiedUi.UnifiedUiApi.SetVisible(false); }
                GUILayout.EndHorizontal();
                GUILayout.Label("描画中：左ドラッグ／Ctrl+Z・Y：UNDO・REDO／Delete：全削除／Esc：終了");
            }
            GUILayout.Label("VR：A＝設定表示／表示中は上下＝項目・左右＝調整／閉じて左右＝UNDO・REDO／B＝全削除／押し込み＝道具選択");
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("描き込み先の一筆検査");
            bool inspectionEnabled = GUI.enabled; GUI.enabled = inspectionEnabled && !_busy && Selected != null && !InspectionRunning;
            if (GUILayout.Button("次の一筆を検査", GUILayout.Width(160))) ArmInspection();
            GUI.enabled = inspectionEnabled && InspectionRunning;
            if (GUILayout.Button("検査をキャンセル", GUILayout.Width(160))) CancelInspection();
            GUI.enabled = inspectionEnabled;
            GUILayout.Label(_inspectionResult);
            GUILayout.Label("一筆の後、顔・体の実インク、合成後、割り当て中のPNGを保存します。未作成の画像は未作成と記録します。");
            GUILayout.EndVertical();
            DrawSaveControls();
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("描画画像の確認");
            bool diagnostics = GUILayout.Toggle(_logging.Value, "診断出力を有効にする（既定OFF）");
            if(diagnostics != _logging.Value) { _logging.Value = diagnostics; SaveConfig(); }
            bool previousEnabled = GUI.enabled; GUI.enabled = previousEnabled && _logging.Value && !_busy && Selected != null;
            if(GUILayout.Button("インク・合成・割当画像を保存", GUILayout.Width(260))) CaptureDrawingImages();
            GUI.enabled = previousEnabled;
            GUILayout.Label("本体右側の「インク：0 px」は未描画、1 px以上はインク画像に書込済み。画像保存はボタンを押した時だけ実行します。");
            GUILayout.EndVertical(); GUILayout.Label(_status);
            var e = Event.current;
            UiInputCaptureApi.Sync(Guid, "settings-drag", GUIUtility.hotControl != 0 && Input.GetMouseButton(0));
            if (e != null && e.rawType == EventType.MouseUp) SaveConfig();
        }
        void Slider(string label, BepInEx.Configuration.ConfigEntry<float> entry, string unit)
        {
            var range = (BepInEx.Configuration.AcceptableValueRange<float>)entry.Description.AcceptableValues;
            string key = entry.Definition.Section + "/" + entry.Definition.Key;
            if (!_drafts.ContainsKey(key)) _drafts[key] = entry.Value.ToString("0.###", CultureInfo.InvariantCulture);
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(150));
            float next = GUILayout.HorizontalSlider(entry.Value, range.MinValue, range.MaxValue, GUILayout.Width(170));
            if (Mathf.Abs(next - entry.Value) > .00001f) { entry.Value = next; _drafts[key] = next.ToString("0.###", CultureInfo.InvariantCulture); _gaugeUntil = Time.unscaledTime + 2; }
            GUI.SetNextControlName(key); string text = GUILayout.TextField(_drafts[key], GUILayout.Width(65));
            if (text != _drafts[key])
            {
                _drafts[key] = text;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && !float.IsNaN(number) && !float.IsInfinity(number) && number >= range.MinValue && number <= range.MaxValue) { entry.Value = number; _gaugeUntil = Time.unscaledTime + 2; }
            }
            if (GUI.GetNameOfFocusedControl() != key && GUIUtility.hotControl == 0) _drafts[key] = entry.Value.ToString("0.###", CultureInfo.InvariantCulture);
            GUILayout.Label(unit, GUILayout.Width(45)); GUILayout.EndHorizontal();
        }
        void SetDesktop(bool active)
        {
            _desktop = active && CanAct && _model != null; _mouseDown = false; StopPaintPress(); EndStroke();
            if (_desktop)
            {
                HideGizmo(); UiInputCaptureApi.SetInputBlockRects(Guid, new[] { new Rect(0, 0, Screen.width, Screen.height) }); UiInputCaptureApi.SetIdleCursorUnlock(Guid, true);
            }
            else
            {
                UiInputCaptureApi.ClearInputBlockRects(Guid); UiInputCaptureApi.SetIdleCursorUnlock(Guid, false); UiInputCaptureApi.EndOwner(Guid); _hud?.Hide();
            }
        }
        void OnGUI()
        {
            if (!_desktop || !CanAct || _model == null) return; var e = Event.current; if (e == null) return;
            GUI.Box(_mouseExit, "左ドラッグ：描画／Esc：終了\nCtrl+Z・Y：戻す・やり直し／Delete：全削除");
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { SetDesktop(false); MainGameUnifiedUi.UnifiedUiApi.ShowTab(Guid); e.Use(); return; }
                if (e.control && e.keyCode == KeyCode.Z) { Undo(false); e.Use(); }
                else if (e.control && e.keyCode == KeyCode.Y) { Undo(true); e.Use(); }
                else if (e.keyCode == KeyCode.Delete) { ClearDrawing(); e.Use(); }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && !_mouseExit.Contains(e.mousePosition))
            { if (_inspectionPending) return; _mouseDown = true; InspectionInput(true); _lastMousePaint = Time.unscaledTime - 1f/60; UiInputCaptureApi.Sync(Guid, "drawing", true); e.Use(); }
            if (e.rawType == EventType.MouseUp && e.button == 0)
            { _mouseDown = false; EndStroke(); RequestInspectionCapture("マウス解放"); UiInputCaptureApi.Sync(Guid, "drawing", false); e.Use(); }
            bool move = e.type == EventType.MouseMove || e.type == EventType.MouseDrag;
            bool sprayHold = e.type == EventType.Repaint && _mouseDown && _tool == PaintTool.Spray;
            if (!move && !sprayHold && !_mouseDown) return;
            if (e.type != EventType.Repaint && !move) return;
            if (Camera.main == null) return;
            try
            {
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y));
                if (FindRay(ray, 20, out var hit))
                {
                    _model.Root.transform.rotation = Camera.main.transform.rotation;
                    _model.Visual.localPosition = Vector3.zero;
                    if (_tool == PaintTool.Marker)
                    {
                        _model.Root.transform.position += hit.Point - (_desktopErase ? _model.Eraser.position : _model.Tip.position);
                        if (_mouseDown) Marker(hit, _desktopErase); else _previousSurface = null;
                    }
                    else
                    {
                        Vector3 origin = hit.Point - ray.direction * .1f; _model.Root.transform.position += origin - _model.Tip.position;
                        SprayFrom(new Ray(origin, ray.direction), Mathf.Min(.05f, Time.unscaledTime - _lastMousePaint), _mouseDown);
                    }
                    _lastMousePaint = Time.unscaledTime; UpdateHud();
                }
                else _previousSurface = null;
                if (Time.unscaledTime - _lastFlush >= 1f/30) { foreach (var actor in _strokeActors) actor.Flush(); _lastFlush = Time.unscaledTime; }
                if (move) e.Use();
            }
            catch (Exception ex) { SetDesktop(false); Fail("マウス描画失敗", ex); }
        }
    }
}
