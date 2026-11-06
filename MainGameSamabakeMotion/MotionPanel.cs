using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using MainGameUiInputCapture;

namespace MainGameSamabakeMotion
{
    internal sealed class MotionPanel
    {
        private readonly Playback _player;
        private readonly MotionCatalog _catalog;
        private readonly string _error;
        private MotionEntry[] _filtered = new MotionEntry[0];
        private MotionEntry _entry;
        private string _kind = "aib", _search = "", _speedText = "1", _patternText = "0.5";
        private bool _female = true, _pair = true, _individual;
        private Vector2 _posesScroll, _statesScroll;
        private float _listHeight = 300f;
        internal MotionPanel(string directory, Playback player)
        {
            _player = player;
            try
            {
                _catalog = CatalogStore.Read<MotionCatalog>(directory, "SamabakeCatalog.json");
                var settings = CatalogStore.Read<PlaybackSettings>(directory, "PlaybackSettings.json");
                _player.Adjust(settings.speed, settings.pattern);
                _speedText = settings.speed.ToString("0.##", CultureInfo.InvariantCulture);
                _patternText = settings.pattern.ToString("0.##", CultureInfo.InvariantCulture);
                Filter();
            }
            catch (Exception ex) { _error = "一覧の読み込み失敗: " + ex.Message; }
        }
        private void Filter() => _filtered = _catalog.entries.Where(e => e.kind == _kind && (_search.Length == 0 || e.title.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        internal void DrawViewport(float width, float height) { _listHeight = Mathf.Max(100f, height - 260f); Draw(); }
        internal void Draw()
        {
            try { DrawContents(); }
            finally { UiInputCaptureApi.Sync(Plugin.Id, "controls", GUIUtility.hotControl != 0); }
        }
        private void DrawContents()
        {
            if (_error != null) { GUILayout.Label(_error); return; }
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("サマバケ！すくらんぶる ／ 本編モーション");
            GUILayout.Label(_catalog.bundles + "件導入・" + _catalog.skipped + "件スキップ");
            GUILayout.BeginHorizontal();
            Category("aib", "愛撫"); Category("hou", "奉仕"); Category("solo", "オナニー");
            GUILayout.Space(10);
            if (GUILayout.Button("再生解除", GUILayout.Width(100), GUILayout.Height(28))) _player.Stop();
            GUILayout.EndHorizontal();
            GUILayout.Label(_player.Status);
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("検索", GUILayout.Width(40));
            string search = GUILayout.TextField(_search, GUILayout.Width(240));
            if (search != _search) { _search = search; Filter(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(275));
            GUILayout.Label("モーション (" + _filtered.Length + ")");
            _posesScroll = GUILayout.BeginScrollView(_posesScroll, GUILayout.Height(_listHeight));
            foreach (var entry in _filtered)
                if (GUILayout.Button((_entry == entry ? "● " : "○ ") + entry.title, GUILayout.Width(250), GUILayout.Height(28)))
                {
                    _entry = entry;
                    if (_female && entry.female == null) _female = false;
                    if (!_female && entry.male == null) _female = true;
                    _statesScroll = Vector2.zero;
                    GUIUtility.ExitGUI();
                }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);
            if (_entry == null) GUILayout.Label("左のモーションを選択してください");
            else
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = _entry.female != null;
                if (GUILayout.Button((_female ? "● " : "○ ") + "女性", GUILayout.Width(75), GUILayout.Height(28))) { _female = true; GUIUtility.ExitGUI(); }
                GUI.enabled = _entry.male != null;
                if (GUILayout.Button((!_female ? "● " : "○ ") + "男性", GUILayout.Width(75), GUILayout.Height(28))) { _female = false; GUIUtility.ExitGUI(); }
                GUI.enabled = true;
                GUI.enabled = _entry.female != null && _entry.male != null;
                _pair = GUILayout.Toggle(_pair, "対応する男女を同期", GUILayout.Width(170));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Label("動きをクリックすると再生します");
                bool individual = GUILayout.Toggle(_individual, "個別カーブも表示", GUILayout.Width(180));
                if (individual != _individual) { _individual = individual; GUIUtility.ExitGUI(); }
                _statesScroll = GUILayout.BeginScrollView(_statesScroll, GUILayout.Height(Mathf.Max(60f, _listHeight - 83f)));
                var actor = _female ? _entry.female : _entry.male;
                foreach (var state in actor.states)
                {
                    if (!_individual && !state.composite) continue;
                    if (GUILayout.Button(state.label, GUILayout.Width(285), GUILayout.Height(28)))
                    { _player.Play(_entry, state, _female, _pair); GUIUtility.ExitGUI(); }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("再生調整");
            float speed = Slider("速度", _player.Speed, 0f, 3f, ref _speedText);
            float pattern = Slider("パターン", _player.Pattern, 0f, 1f, ref _patternText);
            if (speed != _player.Speed || pattern != _player.Pattern) _player.Adjust(speed, pattern);
            GUILayout.EndVertical();
        }
        private void Category(string kind, string label)
        {
            if (GUILayout.Button((_kind == kind ? "● " : "○ ") + label, GUILayout.Width(100), GUILayout.Height(28)))
            { _kind = kind; _entry = null; Filter(); GUIUtility.ExitGUI(); }
        }
        private static float Slider(string label, float value, float min, float max, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(85));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220));
            if (next != value) text = next.ToString("0.##", CultureInfo.InvariantCulture);
            string input = GUILayout.TextField(text, GUILayout.Width(65));
            if (input != text)
            {
                text = input;
                if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) next = Mathf.Clamp(parsed, min, max);
            }
            GUILayout.EndHorizontal();
            return next;
        }
    }
}
