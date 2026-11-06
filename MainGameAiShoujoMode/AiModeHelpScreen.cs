using System;
using System.Linq;
using MapEnvironmentRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MainGameAiShoujoMode
{
    // Copy native title widgets so font, sprites, colours and mouse transitions stay native.
    internal sealed class AiModeHelpScreen : IDisposable
    {
        private EventSystem _events;
        private readonly GameObject _root;
        private readonly Button _template;
        private readonly TextMeshProUGUI _font;
        private readonly Parameters _settings;
        private readonly Action _changed;
        private readonly TextMeshProUGUI _body;
        private readonly string _controls;
        internal bool Visible => _root.activeSelf;
        internal Rect InputRect => new Rect(_settings.N("math.zero"), _settings.N("math.zero"), Screen.width, Screen.height);
        internal AiModeHelpScreen(TitleScene title, Transform parent, Parameters settings, Action changed, Action openSettings, string modeLabel = null, string controls = null, string settingsLabel = null)
        {
            _settings = settings; _changed = changed; _controls = controls;
            _template = title.GetComponentsInChildren<Button>(true).First(button => button.GetComponentInChildren<TextMeshProUGUI>(true) != null);
            _font = _template.GetComponentInChildren<TextMeshProUGUI>(true);
            _root = new GameObject("AI少女モード 設定・操作説明", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = settings.I("aiMode.help.sortingOrder");
            var backdrop = new GameObject("Background", typeof(RectTransform), typeof(Image)); backdrop.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)backdrop.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(settings.N("aiMode.help.background.r"), settings.N("aiMode.help.background.g"), settings.N("aiMode.help.background.b"), settings.N("aiMode.help.background.a"));
            Label((modeLabel ?? "AI少女モード") + "　操作説明", settings.N("aiMode.help.headingY"), settings.N("aiMode.help.headingHeight"));
            _body = Label("", settings.N("aiMode.help.bodyY"), settings.N("aiMode.help.bodyHeight"));
            float x = settings.N("aiMode.help.x");
            AddButton("操作説明", x, ShowControls);
            x += settings.N("aiMode.help.buttonWidth") + settings.N("aiMode.help.gap");
            if (openSettings != null)
            {
                AddButton(settingsLabel ?? "モード設定", x, () => { SetVisible(false); openSettings(); });
                x += settings.N("aiMode.help.buttonWidth") + settings.N("aiMode.help.gap");
            }
            AddButton("閉じる", x, () => SetVisible(false));
            ShowControls(); _root.SetActive(false);
        }
        private void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private TextMeshProUGUI Label(string value, float y, float height)
        {
            var go = new GameObject("Explanation", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(_root.transform, false);
            Place((RectTransform)go.transform, _settings.N("aiMode.help.x"), y, Mathf.Min(_settings.N("aiMode.help.width"), Screen.width - _settings.N("aiMode.help.x") * _settings.N("math.two")), height);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = _font.font; label.fontSharedMaterial = _font.fontSharedMaterial;
            label.fontSize = _settings.N("aiMode.help.fontSize"); label.color = Color.white; label.raycastTarget = false; label.text = value;
            return label;
        }
        private void AddButton(string text, float x, Action action)
        {
            var button = UnityEngine.Object.Instantiate(_template, _root.transform, false); button.name = text; button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => { Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.ok_s); action(); });
            button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;
            float start = _settings.N("aiMode.help.x"), width = _settings.N("aiMode.help.buttonWidth"), gap = _settings.N("aiMode.help.gap");
            int index = Mathf.RoundToInt((x - start) / (width + gap));
            int columns = Mathf.Max(_settings.I("math.one"), Mathf.FloorToInt((Screen.width - start * _settings.N("math.two") + gap) / (width + gap)));
            Place((RectTransform)button.transform, start + index % columns * (width + gap),
                _settings.N("aiMode.help.buttonY") + index / columns * (_settings.N("aiMode.help.buttonHeight") + gap), width, _settings.N("aiMode.help.buttonHeight"));
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        }
        private void ShowControls()
        {
            if (_controls != null) { _body.text = _controls; return; }
            _body.text = "W / A / S / D　移動\nマウス移動　歩行中の視点変更（右ドラッグ不要）\nTab　カーソルの解除／中央固定・非表示\nSpace　ジャンプ　　LeftControl　しゃがみ　　Shift　走る\nF　女の子に話しかける／ドア開閉／カプセル端末／特殊通路\nV　歩行／フリーカメラの切り替え\nフリーカメラ　右ドラッグで視点、WASDで移動、Q / Eで上下\nI　メニュー（セーブ・ロード・操作説明・設定）\nF2　共有フォルダへ撮影\n:　プレイバックバーの表示／非表示\nEscape　会話を終了／通常時はタイトルへ戻る\n\n歩行中はカーソルを中央に固定して非表示にします。\n設定・説明・プレイバックバー表示中とTabで解除した間はカーソルを使えます。";
        }
        internal void SetVisible(bool visible)
        {
            if (visible && EventSystem.current == null && _events == null)
            {
                var events = new GameObject("AI Mode UI Events", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(_root.transform, false); _events = events.GetComponent<EventSystem>();
            }
            _root.SetActive(visible); _changed();
        }
        public void Dispose() { if (_root != null) UnityEngine.Object.Destroy(_root); }
    }
}
