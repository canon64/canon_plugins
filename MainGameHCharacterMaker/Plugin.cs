using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using MainGameBlankMapAdd;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameHCharacterMaker
{
    [BepInPlugin(Guid, "MainGameHCharacterMaker", "0.3.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInDependency(GateApi.PluginGuid, "2.3.0")]
    [BepInDependency("com.kks.maingameblankmapadd")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("marco.kkapi")]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "canon.kks.hcharactermaker";
        private const string InputOwner = "MainGameHCharacterMaker.Window";
        private const int WindowId = 0x48434D4B;
        private IDisposable _gateRegistration, _barRegistration;
        private ConfigEntry<float> _windowX, _windowY;
        private ConfigEntry<int> _lastPage;
        private GateApi.Permit _permit;
        private HSceneProc _scene;
        private readonly List<ChaControl> _actors = new List<ChaControl>();
        private readonly Dictionary<int, ShapeEditSession> _edits = new Dictionary<int, ShapeEditSession>();
        private readonly Dictionary<int, string[]> _bodyText = new Dictionary<int, string[]>();
        private readonly Dictionary<int, string[]> _faceText = new Dictionary<int, string[]>();
        private readonly Dictionary<int, float[]> _bodyRanges = new Dictionary<int, float[]>();
        private readonly Dictionary<int, float[]> _faceRanges = new Dictionary<int, float[]>();
        private string[] _names = Array.Empty<string>();
        private bool _active, _visible, _openRequested, _focused = true, _dragging, _body = true;
        private ulong _lastSession;
        private int _selected, _ownedHotControl;
        private Vector2 _scroll;
        private Rect _window = new Rect(24, 42, 640, 650);
        private Action _pendingUi;
        private string _status = "";
        private float _range = 100f;
        private static readonly float[] RangeWidths = { 10f, 100f, 500f };
        private readonly Rect[] _inputRects = new Rect[1];
        private readonly string[] _bodyNames = BuildBodyNames();
        public string LastResult => _status;
        private bool CanEdit => _active && isActiveAndEnabled && _permit.IsValid;

        private void Awake()
        {
            _windowX = Config.Bind("Window", "X", 24f, "キャラメイク編集窓の横位置");
            _windowY = Config.Bind("Window", "Y", 42f, "キャラメイク編集窓の縦位置");
            _lastPage = Config.Bind("Window", "Page", 0, "最後に開いた編集タブ（0:体型 1:顔 2:髪 3:瞳・眉 4:化粧 5:肌・体）");
            _page = Mathf.Clamp(_lastPage.Value, 0, PageNames.Length - 1);
            _window.x = ShapeEditSession.Finite(_windowX.Value) ? _windowX.Value : 24f;
            _window.y = ShapeEditSession.Finite(_windowY.Value) ? _windowY.Value : 42f;
            _barRegistration = CharacterMakerBarApi.Register(() => CanEdit && _actors.Count > 0, OpenEditor);
            _gateRegistration = GateApi.Register(Guid, StartSession, StopSession);
        }

        private void StartSession()
        {
            var context = GateApi.Current;
            if (!isActiveAndEnabled || context == null || context.Kind != GateApi.SessionKind.H
                || context.HScene == null || context.Id == _lastSession) return;
            _lastSession = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = context.HScene;
            var females = ReadScene<List<ChaControl>>(_scene, "lstFemale");
            if (females != null)
                foreach (var actor in females)
                    if (actor != null && actor.sex == 1 && actor.chaFile?.custom?.body != null
                        && actor.chaFile.custom.face != null) _actors.Add(actor);
            _names = new string[_actors.Count];
            for (int i = 0; i < _actors.Count; i++)
                _names[i] = "女性" + (i + 1) + "  " + _actors[i].chaFile.parameter.fullname;
            CacheMotionActors();
            _active = true;
        }

        public bool TryOpenEditor()
        {
            if (!CanEdit || _actors.Count == 0 || !UiInputCaptureApi.IsAvailable) return false;
            if (_visible) return true;
            // The bar may request opening from MouseUp. Build GUILayout content only on Layout.
            _openRequested = true;
            return true;
        }

        private void ActivateEditor()
        {
            _openRequested = false;
            _selected = 0;
            _status = "調整は即時反映。決定はこのH中の確定、閉じるは未確定の編集を取消します。";
            try { EnsureEdit(_selected); SelectPage(_page); }
            catch (Exception ex) { _status = ex.Message; return; }
            _focused = UnityEngine.Application.isFocused;
            _visible = true;
            FitWindow();
            RefreshInput();
        }

        private void OpenEditor() => TryOpenEditor();

        private ShapeEditSession EnsureEdit(int index)
        {
            EnsureAppearanceEdit(index);
            if (_edits.TryGetValue(index, out var edit)) return edit;
            edit = new ShapeEditSession(new NativeTarget(this, _actors[index]), () => CanEdit);
            _edits.Add(index, edit);
            _bodyText.Add(index, FormatValues(edit.Body));
            _faceText.Add(index, FormatValues(edit.Face));
            _bodyRanges.Add(index, (float[])edit.Body.Clone());
            _faceRanges.Add(index, (float[])edit.Face.Clone());
            return edit;
        }

        private void StopSession()
        {
            _active = false;
            _permit = default;
            _openRequested = false;
            _pendingUi = null;
            CloseEditor(false);
            _actors.Clear();
            _names = Array.Empty<string>();
            ClearMotionActors();
            _scene = null;
        }

        private void CloseEditor(bool commit)
        {
            if (_visible && CanEdit) SaveWindowPosition();
            try
            {
                if (commit)
                {
                    if (!CanEdit) throw new InvalidOperationException("Hの編集許可が終了しました。");
                    foreach (var edit in _edits.Values)
                        if (!edit.CanCommit) throw new InvalidOperationException("編集対象が変更・破棄されました。");
                    foreach (var edit in _appearanceEdits.Values)
                        if (!edit.CanCommit) throw new InvalidOperationException("パーツの編集対象が変更・破棄されました。");
                    foreach (var edit in _edits.Values)
                        if (!edit.Commit()) throw new InvalidOperationException("編集対象が変更・破棄されました。");
                    foreach (var edit in _appearanceEdits.Values) edit.Commit();
                }
                else
                {
                    Exception error = null;
                    foreach (var edit in _edits.Values)
                    {
                        try { edit.Cancel(); }
                        catch (Exception ex) { error = error ?? ex; }
                    }
                    foreach (var edit in _appearanceEdits.Values)
                    {
                        try { edit.Cancel(); }
                        catch (Exception ex) { error = error ?? ex; }
                    }
                    if (error != null)
                    {
                        _status = error.Message;
                        // Keep the failure visible for an explicit user close; shutdown still cleans up.
                        if (_active) return;
                    }
                }
                _edits.Clear();
                ClearAppearance();
                _bodyText.Clear(); _faceText.Clear(); _bodyRanges.Clear(); _faceRanges.Clear();
                _visible = false;
            }
            catch (Exception ex) { _status = ex.Message; }
            finally { ReleaseInput(); }
        }

        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            try { _barRegistration?.Dispose(); _gateRegistration?.Dispose(); }
            finally { _barRegistration = null; _gateRegistration = null; StopSession(); }
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (focused) RefreshInput(); else ReleaseInput();
        }

        private void RefreshInput()
        {
            if (!_visible || !_focused || !CanEdit) return;
            _inputRects[0] = _window;
            UiInputCaptureApi.SetInputBlockRects(InputOwner, _inputRects);
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, true);
        }

        private void ReleaseInput()
        {
            UiInputCaptureApi.Sync(InputOwner, "editor-drag", false);
            UiInputCaptureApi.Sync(InputOwner, "editor-text", false);
            UiInputCaptureApi.ClearInputBlockRects(InputOwner);
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, false);
            if (_ownedHotControl != 0 && GUIUtility.hotControl == _ownedHotControl) GUIUtility.hotControl = 0;
            _ownedHotControl = 0;
            _dragging = false;
        }

        private void OnGUI()
        {
            if (_openRequested && CanEdit && Event.current.type == EventType.Layout) ActivateEditor();
            if (!_visible || !CanEdit) return;
            if (Event.current.type == EventType.Layout && _pendingUi != null)
            {
                var action = _pendingUi; _pendingUi = null;
                try { action(); } catch (Exception ex) { _status = ex.Message; }
                if (!_visible) return;
            }
            FitWindow();
            var raw = Event.current.rawType;
            if (_focused && raw == EventType.MouseDown && _window.Contains(Event.current.mousePosition))
            {
                _dragging = true;
                UiInputCaptureApi.Sync(InputOwner, "editor-drag", true);
            }
            int previousHot = GUIUtility.hotControl;
            int oldDepth = GUI.depth;
            bool oldEnabled = GUI.enabled;
            GUI.depth = -1000;
            GUI.enabled = oldEnabled && _focused;
            try { _window = GUI.Window(WindowId, _window, DrawWindow, "キャラメイク — H中の顔・体型・パーツ編集"); }
            finally { GUI.depth = oldDepth; GUI.enabled = oldEnabled; }
            if (_dragging && GUIUtility.hotControl != 0 && GUIUtility.hotControl != previousHot)
                _ownedHotControl = GUIUtility.hotControl;
            if (raw == EventType.MouseUp || raw == EventType.Ignore)
            {
                if (_dragging && raw == EventType.MouseUp) SaveWindowPosition();
                _dragging = false;
                _ownedHotControl = 0;
                UiInputCaptureApi.Sync(InputOwner, "editor-drag", false);
                BreakAppearanceMerge();
            }
            UiInputCaptureApi.Sync(InputOwner, "editor-text", _focused && GUI.GetNameOfFocusedControl().StartsWith("hcm-", StringComparison.Ordinal));
            RefreshInput();
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _names.Length; i++)
            {
                int index = i;
                if (GUILayout.Toggle(_selected == i, _names[i], GUI.skin.button, GUILayout.Height(28)) && _selected != i)
                    _pendingUi = () => { BreakAppearanceMerge(); EnsureEdit(index); _selected = index; _colorText.Clear(); SelectPage(_page); };
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PageNames.Length; i++)
            {
                if (i > 0 && i % Math.Max(1, (int)(_window.width - 24) / 94) == 0)
                { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                int page = i;
                if (GUILayout.Toggle(_page == i, PageNames[i], GUI.skin.button, GUILayout.Width(90), GUILayout.Height(28)) && _page != i)
                    _pendingUi = () => SelectPage(page);
            }
            GUILayout.EndHorizontal();
            if (_page < 2) DrawShapeEditor(); else DrawAppearance();
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _status : GUI.tooltip,
                new GUIStyle(GUI.skin.label) { wordWrap = true }, GUILayout.Height(42));
            DrawCardSave();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("決定して閉じる", GUILayout.Width(150), GUILayout.Height(30)))
                _pendingUi = () => CloseEditor(true);
            if (GUILayout.Button("取消して閉じる", GUILayout.Width(150), GUILayout.Height(30)))
                _pendingUi = () => CloseEditor(false);
            GUILayout.FlexibleSpace();
            GUILayout.Label("決定はH中へ反映／保存は上のボタン", GUILayout.Width(260));
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, _window.width, 20));
        }

        private void DrawShapeEditor()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("スライダー調整幅", GUILayout.Width(120));
            foreach (float width in RangeWidths)
            {
                float selectedWidth = width;
                if (GUILayout.Toggle(_range == width, "±" + width.ToString("0") + "%", GUI.skin.button, GUILayout.Width(76)))
                {
                    if (_range != width) _pendingUi = () => { _range = selectedWidth; RecenterRanges(); };
                }
            }
            if (GUILayout.Button("現在値の周辺へ", GUILayout.Width(130))) _pendingUi = RecenterRanges;
            GUILayout.EndHorizontal();
            var edit = _edits[_selected];
            float[] values = _body ? edit.Body : edit.Face;
            string[] text = _body ? _bodyText[_selected] : _faceText[_selected];
            float[] centers = _body ? _bodyRanges[_selected] : _faceRanges[_selected];
            string[] labels = _body ? _bodyNames : ChaFileDefine.cf_headshapename;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < values.Length; i++)
                DrawValue(edit, i, i < labels.Length ? labels[i] : "項目 " + i, values, text, centers);
            GUILayout.EndScrollView();
        }

        private void DrawValue(ShapeEditSession edit, int index, string label, float[] values, string[] text, float[] centers)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Label(label, GUILayout.Width(154));
            float half = _range / 100f;
            // Existing expanded values remain untouched while merely opening/rendering the editor.
            float low = Math.Min(centers[index] - half, values[index]);
            float high = Math.Max(centers[index] + half, values[index]);
            float next = GUILayout.HorizontalSlider(values[index], low, high, GUILayout.MinWidth(100));
            if (next != values[index] && _focused) ApplyValue(edit, index, next, text, centers, false);
            string fieldName = "hcm-" + (_body ? "body-" : "face-") + index;
            GUI.SetNextControlName(fieldName);
            string typed = GUILayout.TextField(text[index], GUILayout.Width(83));
            if (typed != text[index])
            {
                text[index] = typed;
                if (TryPercent(typed, out float value)) ApplyValue(edit, index, value, text, centers, true);
            }
            GUILayout.Label("%", GUILayout.Width(18));
            GUILayout.EndHorizontal();
        }

        private void ApplyValue(ShapeEditSession edit, int index, float value, string[] text, float[] centers, bool typed)
        {
            try
            {
                if (!edit.Preview(_body, index, value)) { _status = "変更が拒否されました。対象・Hの状態を確認してください。"; return; }
                float actual = (_body ? edit.Body : edit.Face)[index];
                if (typed) centers[index] = actual;
                else text[index] = Format(actual);
            }
            catch (Exception ex) { _status = "反映失敗：" + ex.Message + "（取消で編集前へ復元します）"; }
        }

        private void RecenterRanges()
        {
            if (!_edits.TryGetValue(_selected, out var edit)) return;
            Array.Copy(edit.Body, _bodyRanges[_selected], edit.Body.Length);
            Array.Copy(edit.Face, _faceRanges[_selected], edit.Face.Length);
        }

        private void FitWindow()
        {
            _window.width = Mathf.Min(680, Mathf.Max(1, Screen.width - 12));
            _window.height = Mathf.Min(780, Mathf.Max(1, Screen.height - 12));
            _window.x = Mathf.Clamp(_window.x, 0, Mathf.Max(0, Screen.width - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0, Mathf.Max(0, Screen.height - _window.height));
        }

        private void SaveWindowPosition()
        {
            if (_windowX != null && _windowX.Value != _window.x) _windowX.Value = _window.x;
            if (_windowY != null && _windowY.Value != _window.y) _windowY.Value = _window.y;
        }

        private static string Format(float value) => ((double)value * 100).ToString("0.###", CultureInfo.InvariantCulture);
        private static string[] FormatValues(float[] values) => Array.ConvertAll(values, Format);
        private static bool TryPercent(string text, out float value)
        {
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent);
            value = (float)(percent / 100.0);
            return parsed && ShapeEditSession.Finite(value);
        }
        private static string[] BuildBodyNames()
        {
            var labels = new List<string>(ChaFileDefine.cf_bodyshapename);
            labels.AddRange(new[] { "胸の柔らかさ", "胸の重さ", "乳輪の大きさ" });
            return labels.ToArray();
        }
        private static T ReadScene<T>(HSceneProc scene, string name) where T : class
            => scene == null ? null : typeof(HSceneProc).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(scene) as T;
    }
}
