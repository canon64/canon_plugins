using System;
using System.Collections.Generic;
using MainGameUiInputCapture;
using MainGameUnifiedUi;
using MapEnvironmentRuntime;
using UnityEngine;
using Vmd = MainGameVMDPlay.Plugin;

namespace MainGameAiShoujoMode
{
    // Independent host for the existing VMD transport and registered UnifiedUi tab bodies.
    // The mode supplies lifetime events; this component has no frame polling callbacks.
    public sealed class AiModePlaybackBar : MonoBehaviour
    {
        private const string Owner = Plugin.Guid + ".playback";
        private readonly Dictionary<string, Vector2> _scroll = new Dictionary<string, Vector2>();
        private Func<Parameters> _settings;
        private Action<Exception> _error;
        private SandboxFreeCamera _player;
        private Func<bool> _canDraw;
        private Parameters P => _settings();
        private float N(string key) => P.N("aiMode.playback." + key);
        private bool _visible, _expanded, _drag, _cursorOwned, _previousCursor, _inputRegistered;
        private bool? _nextVisible, _nextExpanded;
        private string _tab = "", _nextTab, _picker, _nextPicker;
        private string[] _tabs = Array.Empty<string>();
        private Vector2 _tabScroll, _pickerScroll;
        private Rect _rect;
        private int _hot;
        private int _page;
        private int? _nextPage;
        private MainGameVMDPlay.PlaybackBarSnapshot _state;

        public void Initialize(Func<Parameters> settings, SandboxFreeCamera player, Func<bool> canDraw, Action<Exception> error)
        {
            _settings = settings; _player = player; _canDraw = canDraw; _error = error;
            _visible = P.B("aiMode.playback.visible");
        }
        public void Suspend() { ReleaseInput(); enabled = false; }
        public void Resume() { enabled = true; }
        private void OnGUI()
        {
            if (_settings == null || !Plugin.ModeAllowed || !_canDraw()) { ReleaseInput(); return; }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == (KeyCode)Enum.Parse(typeof(KeyCode), P.T("aiMode.playback.key")))
            {
                _nextVisible = !_visible; e.Use();
            }
            if (e.type == EventType.Layout)
            {
                if (_nextVisible.HasValue) { _visible = _nextVisible.Value; _nextVisible = null; if (!_visible) ReleaseInput(); }
                if (_nextExpanded.HasValue) { _expanded = _nextExpanded.Value; _nextExpanded = null; }
                if (_nextTab != null) { _tab = _nextTab; _nextTab = null; }
                if (_nextPicker != null) { _picker = _nextPicker; _nextPicker = null; _page = P.I("math.zero"); }
                if (_nextPage.HasValue) { _page = _nextPage.Value; _nextPage = null; }
                if (_visible)
                {
                    _tabs = UnifiedUiApi.GetEmbeddedTabs();
                    _state = Vmd.ReadPlaybackBarSnapshot();
                    if (_tab.Length != P.I("math.zero") && Array.IndexOf(_tabs, _tab) < P.I("math.zero")) _tab = "";
                }
            }
            if (!_visible || !Application.isFocused) { ReleaseInput(); return; }
            float margin = N("margin");
            var rect = new Rect(margin, Mathf.Max(margin, Screen.height - margin - (_expanded ? N("expandedHeight") : N("height"))),
                Mathf.Max(P.N("math.one"), Screen.width - margin * P.N("math.two")),
                Mathf.Min(_expanded ? N("expandedHeight") : N("height"), Screen.height - margin * P.N("math.two")));
            if (rect != _rect || !_inputRegistered)
            {
                _rect = rect; UiInputCaptureApi.SetInputBlockRects(Owner, new[] { _rect });
                UiInputCaptureApi.SetIdleCursorUnlock(Owner, true);
                _inputRegistered = true;
                if (!_cursorOwned && _player != null)
                {
                    _previousCursor = _player.Snapshot().cursorReleaseRequested;
                    _player.SetCursorReleased(true); _cursorOwned = true;
                }
            }
            var raw = e.rawType;
            if (raw == EventType.MouseDown && _rect.Contains(e.mousePosition)) _drag = true;
            UiInputCaptureApi.Sync(Owner, "drag", _drag);
            int before = GUIUtility.hotControl;
            try
            {
                GUILayout.BeginArea(_rect, GUI.skin.box);
                try { DrawBody(); }
                finally { GUILayout.EndArea(); }
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { ReleaseInput(); _nextVisible = false; _error(ex); }
            finally
            {
                if (GUIUtility.hotControl != P.I("math.zero") && GUIUtility.hotControl != before) _hot = GUIUtility.hotControl;
                if (raw == EventType.MouseUp) { _drag = false; UiInputCaptureApi.Sync(Owner, "drag", false); }
            }
        }
        private void DrawBody()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("AI少女モード / VMD　" + (_state?.Title ?? "女の子を読み込んでください"));
            if (GUILayout.Button(_expanded ? "設定を閉じる ▲" : "設定 ▼", GUILayout.Width(N("buttonWidth")))) _nextExpanded = !_expanded;
            if (GUILayout.Button("非表示 [:]", GUILayout.Width(N("buttonWidth")))) _nextVisible = false;
            GUILayout.EndHorizontal();
            bool previous = GUI.enabled;
            GUI.enabled = previous && _state?.Ready == true;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ 前", GUILayout.Width(N("transportWidth")))) Command("previous");
            if (GUILayout.Button("▶ 再生", GUILayout.Width(N("transportWidth")))) Command("play");
            if (GUILayout.Button("Ⅱ 一時停止", GUILayout.Width(N("buttonWidth")))) Command("pause");
            if (GUILayout.Button("■ 停止", GUILayout.Width(N("transportWidth")))) Command("stop");
            if (GUILayout.Button("次 ▶", GUILayout.Width(N("transportWidth")))) Command("next");
            if (GUILayout.Button(_state?.Loop == true ? "ループ ON" : "ループ OFF", GUILayout.Width(N("buttonWidth")))) Command("loop");
            GUILayout.Label("音量", GUILayout.Width(N("labelWidth")));
            float volume = _state?.Volume ?? P.N("math.zero");
            float changed = GUILayout.HorizontalSlider(volume, P.N("math.zero"), P.N("math.one"), GUILayout.Width(N("volumeWidth")));
            if (changed != volume) Command("volume", value: changed);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            float position = _state?.Position ?? P.N("math.zero"), duration = _state?.Duration ?? P.N("math.zero");
            GUILayout.Label(position.ToString("F1") + " / " + duration.ToString("F1") + " 秒", GUILayout.Width(N("timeWidth")));
            float seek = GUILayout.HorizontalSlider(position, P.N("math.zero"), Mathf.Max(P.N("render.epsilon"), duration));
            if (seek != position && _state?.Owns == true) Command("seek", value: seek);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Selected(_state?.Groups, _state?.GroupIndex, "全プリセット") + " ▼")) OpenPicker("group");
            if (GUILayout.Button(Selected(_state?.Entries, _state?.EntryIndex, "プリセットを選択") + " ▼")) OpenPicker("select");
            GUI.enabled = previous;
            if (GUILayout.Button("一覧更新", GUILayout.Width(N("buttonWidth")))) Command("refresh");
            GUILayout.EndHorizontal();
            if (!_expanded) return;
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, GUILayout.Height(N("tabsHeight")));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tab == "", "再生一覧", GUI.skin.button)) _nextTab = "";
            for (int i = P.I("math.zero"); i < _tabs.Length; i += P.I("math.two"))
                if (GUILayout.Toggle(_tab == _tabs[i], _tabs[i + P.I("math.one")], GUI.skin.button)) _nextTab = _tabs[i];
            GUILayout.EndHorizontal(); GUILayout.EndScrollView();
            if (_tab.Length == P.I("math.zero")) DrawPicker();
            else
            {
                _scroll.TryGetValue(_tab, out var scroll);
                scroll = GUILayout.BeginScrollView(scroll);
                try { UnifiedUiApi.DrawEmbeddedTab(_tab); }
                finally { GUILayout.EndScrollView(); }
                _scroll[_tab] = scroll;
            }
        }
        private void OpenPicker(string command) { _nextPicker = command; _nextExpanded = true; _nextTab = ""; }
        private void DrawPicker()
        {
            GUILayout.Label(_state?.Status ?? "女の子を読み込むとVMDを再生できます。");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("マイリスト／グループ")) _nextPicker = "group";
            if (GUILayout.Button("プリセット")) _nextPicker = "select";
            GUILayout.EndHorizontal();
            var entries = _picker == "group" ? _state?.Groups : _state?.Entries;
            int count = entries?.Length ?? P.I("math.zero"), size = P.I("aiMode.playback.pageSize");
            int pages = Math.Max(P.I("math.one"), (count + size - P.I("math.one")) / size);
            int page = Math.Min(_page, pages - P.I("math.one"));
            GUILayout.BeginHorizontal();
            bool previous = GUI.enabled;
            GUI.enabled = previous && page > P.I("math.zero");
            if (GUILayout.Button("前のページ")) _nextPage = page - P.I("math.one");
            GUI.enabled = previous;
            GUILayout.Label((page + P.I("math.one")) + " / " + pages);
            GUI.enabled = previous && page + P.I("math.one") < pages;
            if (GUILayout.Button("次のページ")) _nextPage = page + P.I("math.one");
            GUI.enabled = previous;
            GUILayout.EndHorizontal();
            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            try
            {
                if (entries == null || entries.Length == P.I("math.zero")) GUILayout.Label("登録がありません。VMDタブでプリセットを登録してください。");
                else for (int i = page * size; i < Math.Min(entries.Length, (page + P.I("math.one")) * size); ++i)
                    if (GUILayout.Button(entries[i])) Command(_picker == "group" ? "group" : "select", i);
            }
            finally { GUILayout.EndScrollView(); }
        }
        private string Selected(string[] values, int? index, string fallback)
            => values != null && index.HasValue && index.Value >= P.I("math.zero") && index.Value < values.Length ? values[index.Value] : fallback;
        private void Command(string command, int index = default(int), float value = default(float))
        {
            try { Vmd.PlaybackBarCommand(command, index, value); }
            catch (Exception ex) { _error(ex); }
        }
        private void ReleaseInput()
        {
            if (!_inputRegistered && !_cursorOwned && !_drag && _hot == default(int)) return;
            UiInputCaptureApi.ClearInputBlockRects(Owner);
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, false);
            UiInputCaptureApi.Sync(Owner, "drag", false);
            if (_settings != null && _hot != P.I("math.zero") && GUIUtility.hotControl == _hot) GUIUtility.hotControl = P.I("math.zero");
            _hot = default(int); _drag = false; _rect = default(Rect);
            _inputRegistered = false;
            if (_cursorOwned && _player != null) _player.SetCursorReleased(_previousCursor);
            _cursorOwned = false;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseInput(); }
        private void OnDisable() => ReleaseInput();
        private void OnDestroy() => ReleaseInput();
    }
}
