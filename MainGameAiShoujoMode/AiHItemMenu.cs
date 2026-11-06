using System;
using System.Linq;
using BepInEx.Bootstrap;
using CanonGate;
using MapEnvironmentRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode
{
    internal sealed class AiHItemMenu : IDisposable
    {
        private GameObject _button, _panel;
        private readonly HSprite _sprite;
        private readonly HItemCommands _commands;
        private readonly Parameters _settings;
        private TextMeshProUGUI _help;
        private bool _disposed;

        internal AiHItemMenu(HSprite sprite, Parameters settings, Func<bool> active)
        {
            _sprite = sprite; _settings = settings;
            _commands = new HItemCommands(() => !_disposed && active(), GateApi.CanRun,
                guid => Chainloader.PluginInfos.TryGetValue(guid, out var info) ? info.Instance : null);
            try { Build(); }
            catch { Dispose(); throw; }
        }

        private void Build()
        {
            var insertion = _sprite.menuAction.GetObject(_settings.I("hItems.insertionIndex"));
            var sourcePanel = _sprite.menuActionSub.GetObject(_settings.I("hItems.motionPanelIndex"));
            var sourceContent = _sprite.menuActionSub.GetPutParent(_settings.I("hItems.motionPanelIndex")).transform;
            // Instantiate below an inactive parent, strip native action scripts before enabling.
            var staging = new GameObject("AI H item staging");
            staging.SetActive(false);
            try
            {
                _button = Object.Instantiate(insertion, staging.transform, false);
                _button.name = "AI H Items";
                StripActions(_button);
                var buttonRect = (RectTransform)_button.transform;
                _button.transform.SetParent(insertion.transform.parent, false);
                var sourceRect = (RectTransform)insertion.transform;
                buttonRect.anchoredPosition = sourceRect.anchoredPosition + Vector2.down * (sourceRect.rect.height + _settings.N("hItems.gap"));
                _button.transform.SetSiblingIndex(insertion.transform.GetSiblingIndex() + _settings.I("math.one"));
                var layout = _button.GetComponent<LayoutElement>();
                if (layout != null) layout.ignoreLayout = true;
                // Keep the native frame; replace the insertion pictogram with an item label.
                foreach (var graphic in _button.GetComponentsInChildren<Graphic>(true))
                    if (graphic.gameObject != _button) graphic.gameObject.SetActive(false);
                var label = NewText(_button.transform, "アイテム", _sprite.objMotionListNode.GetComponentInChildren<TextMeshProUGUI>(true));
                Stretch((RectTransform)label.transform, _settings.N("hItems.labelInset"));
                label.fontSize = _settings.N("hItems.iconFontSize"); label.alignment = TextAlignmentOptions.Center;
                var button = _button.GetComponent<Button>() ?? _button.AddComponent<Button>();
                button.interactable = true;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(Toggle);
                _button.AddComponent<AiHItemInput>().Initialize("button", _settings);

                _panel = Object.Instantiate(sourcePanel, staging.transform, false);
                _panel.name = "AI H Item List";
                var contentPath = RelativePath(sourcePanel.transform, sourceContent);
                StripActions(_panel);
                var content = string.IsNullOrEmpty(contentPath) ? _panel.transform : _panel.transform.Find(contentPath);
                if (content == null) throw new InvalidOperationException("標準Hスクロールの内容枠を取得できません。");
                foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var group = content.GetComponent<ToggleGroup>();
                if (group != null) Object.DestroyImmediate(group);
                var scroll = _panel.GetComponentInChildren<ScrollRect>(true);
                if (scroll == null) throw new InvalidOperationException("標準Hスクロールが見つかりません。");
                scroll.content = (RectTransform)content;
                scroll.onValueChanged = new ScrollRect.ScrollRectEvent();
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.onValueChanged = new Scrollbar.ScrollEvent();
                // ScrollRect reconnects the cloned scrollbar on enable.
                for (int index = _settings.I("math.zero"); index < HItemCommands.Items.Length; index++)
                {
                    int selected = index;
                    var row = Object.Instantiate(_sprite.objMotionListNode, content, false);
                    StripActions(row);
                    row.name = "Item " + HItemCommands.Items[index].Label;
                    foreach (var mark in row.GetComponentsInChildren<Transform>(true))
                        if (mark.name == "New" || mark.name == "NowPos") mark.gameObject.SetActive(false);
                    foreach (var text in row.GetComponentsInChildren<TextMeshProUGUI>(true)) text.text = HItemCommands.Items[index].Label;
                    var toggle = row.GetComponent<Toggle>();
                    var target = toggle != null ? toggle.targetGraphic : row.GetComponent<Graphic>();
                    if (toggle != null) Object.DestroyImmediate(toggle);
                    var rowButton = row.GetComponent<Button>() ?? row.AddComponent<Button>();
                    rowButton.targetGraphic = target;
                    rowButton.onClick = new Button.ButtonClickedEvent();
                    rowButton.onClick.AddListener(() => Operate(selected, false));
                    row.AddComponent<AiHItemClick>().Delete = () => Operate(selected, true);
                    row.SetActive(true);
                }
                var textTemplate = _sprite.objMotionListNode.GetComponentInChildren<TextMeshProUGUI>(true);
                _help = NewText(_panel.transform, "左：呼び出し → ギズモ表示　右：削除\n注射器はフリーHと同じ専用操作", textTemplate);
                var helpRect = (RectTransform)_help.transform;
                helpRect.anchorMin = helpRect.anchorMax = Vector2.up;
                helpRect.pivot = Vector2.zero;
                helpRect.anchoredPosition = Vector2.up * _settings.N("hItems.gap");
                helpRect.sizeDelta = new Vector2(_settings.N("hItems.helpWidth"), _settings.N("hItems.helpHeight"));
                _help.fontSize = _settings.N("hItems.helpFontSize"); _help.alignment = TextAlignmentOptions.BottomLeft;
                var capture = _panel.AddComponent<AiHItemInput>();
                capture.Initialize("list", _settings);
                foreach (var selectable in _panel.GetComponentsInChildren<Selectable>(true))
                    selectable.gameObject.AddComponent<AiHItemPointer>().Owner = capture;
                scroll.gameObject.AddComponent<AiHItemDrag>().Owner = capture;
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.AddComponent<AiHItemDrag>().Owner = capture;
                _panel.SetActive(false);
                _panel.transform.SetParent(sourcePanel.transform.parent, false);
                var panelRect = (RectTransform)_panel.transform;
                panelRect.anchoredPosition = new Vector2(((RectTransform)sourcePanel.transform).anchoredPosition.x, buttonRect.anchoredPosition.y);
                _button.SetActive(true);
            }
            finally { Object.Destroy(staging); }
        }

        private void Toggle()
        {
            if (_disposed) return;
            bool show = !_panel.activeSelf;
            _sprite.CloseMotionSubMenu();
            _panel.SetActive(show);
            if (show)
            {
                Canvas.ForceUpdateCanvases();
                _panel.GetComponent<AiHItemInput>().Refresh();
            }
        }
        internal void Hide() { if (_panel != null) _panel.SetActive(false); }
        private void Operate(int index, bool delete) { if (!_disposed) _help.text = _commands.Invoke(index, delete); }

        private static void StripActions(GameObject root)
        {
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
            {
                if (script == null) continue;
                string ns = script.GetType().Namespace ?? "";
                if (ns == "UnityEngine.UI" || ns == "TMPro") continue;
                script.enabled = false;
                Object.DestroyImmediate(script);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
            { toggle.group = null; toggle.onValueChanged = new Toggle.ToggleEvent(); }
        }
        private static string RelativePath(Transform root, Transform child)
        {
            string path = "";
            while (child != root)
            {
                if (child == null) throw new InvalidOperationException("標準Hリストの階層が一致しません。");
                path = child.name + (path.Length == default(int) ? "" : "/" + path); child = child.parent;
            }
            return path;
        }
        private static TextMeshProUGUI NewText(Transform parent, string value, TextMeshProUGUI template)
        {
            var go = new GameObject("Item description", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (template != null) { text.font = template.font; text.fontSharedMaterial = template.fontSharedMaterial; }
            text.text = value; text.color = Color.white; text.raycastTarget = false;
            return text;
        }
        private static void Stretch(RectTransform rect, float inset)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
        public void Dispose()
        {
            _disposed = true;
            if (_button != null) { _button.SetActive(false); Object.Destroy(_button); }
            if (_panel != null) { _panel.SetActive(false); Object.Destroy(_panel); }
            _button = _panel = null;
        }
    }
}
