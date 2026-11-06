using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksConversationView : IDisposable
    {
        internal sealed class Choice
        {
            internal readonly string Text;
            internal readonly bool Enabled;
            internal readonly Action Select;
            internal Choice(string text, Action select, bool enabled = true) { Text = text; Select = select; Enabled = enabled; }
        }
        private sealed class LogEntry
        {
            internal string Name, Text;
            internal Action Replay;
        }
        private sealed class LogRow
        {
            internal GameObject Root;
            internal Text Name, Message;
            internal Button Voice;
        }
        private const string MessagePath = "Canvas_Main/MsgWindowCanvas";
        private const string ButtonsPath = MessagePath + "/MsgWindow/Buttons/Under_BG/Under_Right";
        private readonly KksUiHierarchy _ui;
        private readonly List<Button> _choices = new List<Button>();
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly List<LogRow> _rows = new List<LogRow>();
        private readonly Text _name, _message;
        private readonly RectTransform _content;
        private readonly Button _template, _voice;
        private readonly Toggle _auto, _skip;
        private readonly GameObject _log, _restoreClick;
        private readonly Scrollbar _logScroll;
        private readonly ScrollRect _menuScroll;
        private readonly float _choiceMinimumHeight, _choiceVerticalPadding;
        private Action _replay;
        private GameObject _aiInputRoot;
        private InputField _aiInput;
        internal string AiInputText => _aiInput == null ? string.Empty : _aiInput.text;
        internal void SetAiInput(bool visible, bool enabled)
        {
            if (_disposed) return;
            if (visible && _aiInputRoot == null)
            {
                _aiInputRoot = new GameObject("AI conversation text input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
                _aiInputRoot.transform.SetParent(_content, false);
                _aiInputRoot.transform.SetSiblingIndex(default(int));
                _aiInputRoot.GetComponent<Image>().color = _template.GetComponent<Image>().color;
                var label = UnityEngine.Object.Instantiate(_template.GetComponentInChildren<Text>(true), _aiInputRoot.transform, false);
                label.text = string.Empty; label.raycastTarget = false;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(_choiceVerticalPadding, default(float));
                label.rectTransform.offsetMax = new Vector2(-_choiceVerticalPadding, default(float));
                _aiInput = _aiInputRoot.GetComponent<InputField>(); _aiInput.textComponent = label;
                var placeholder = UnityEngine.Object.Instantiate(label, _aiInputRoot.transform, false);
                placeholder.text = "ここに文字を入力（Human_2からも送れます）"; placeholder.color = Color.gray;
                _aiInput.placeholder = placeholder;
                _aiInput.lineType = InputField.LineType.MultiLineNewline;
                _aiInputRoot.GetComponent<LayoutElement>().minHeight = _choiceMinimumHeight + _choiceVerticalPadding;
            }
            if (_aiInputRoot != null)
            {
                _aiInputRoot.SetActive(visible); _aiInput.interactable = enabled;
                if (!visible) _aiInput.text = string.Empty;
            }
        }
        internal void ClearAiInput() { if (_aiInput != null) _aiInput.text = string.Empty; }
        private int _choiceGeneration, _logOffset;
        private bool _disposed, _hidden, _logVisible;
        internal Action AdvanceRequested, ReplayRequested;
        private Action<string, string> _editRequested;
        private static readonly HashSet<KksConversationView> AuthoringViews = new HashSet<KksConversationView>();
        private static bool AuthoringControlsVisible;
        internal static void SetAuthoringControlsVisible(bool value)
        {
            AuthoringControlsVisible = value;
            foreach (var view in AuthoringViews) if (!view._disposed) view._ui.Get<Button>(ButtonsPath + "/Config").gameObject.SetActive(value);
        }
        internal Action<string, string> EditRequested
        {
            get => _editRequested;
            set
            {
                _editRequested = value;
                var button = _ui.Get<Button>(ButtonsPath + "/Config");
                button.interactable = value != null;
                if (value == null) AuthoringViews.Remove(this); else AuthoringViews.Add(this);
                button.gameObject.SetActive(value != null && AuthoringControlsVisible);
                var label = button.GetComponentInChildren<Text>(true); if (label != null) label.text = "編集";
                Bind(button, () => _editRequested?.Invoke(_name.text, _message.text));
            }
        }
        internal void SetDisplayHidden(bool hidden) { if (!_disposed) SetHidden(hidden); }
        internal bool DisplayHidden => _hidden;
        internal void SetInteractionEnabled(bool value)
        {
            if (_disposed) return;
            var group = _ui.Root.GetComponent<CanvasGroup>() ?? _ui.Root.AddComponent<CanvasGroup>();
            group.interactable = value; group.blocksRaycasts = value;
        }
        internal Action<bool> AutoChanged, SkipChanged;
        internal event Action DisplayStateChanged;
        internal bool Active => !_disposed && _ui.Root != null && _ui.Root.activeSelf;
        internal Transform Transform => _ui.Root.transform;
        internal bool LogVisible => _logVisible;

        internal KksConversationView(Transform parent, string contentDirectory, Action closeRequested = null)
        {
            _ui = new KksUiHierarchy(parent, contentDirectory);
            try
            {
                _name = _ui.Get<Text>(MessagePath + "/Window/Name");
                _message = _ui.Get<Text>(MessagePath + "/Window/Message");
                _content = _ui.Get<RectTransform>("Canvas_Main/Choices");
                _template = _ui.Get<Button>("Canvas_Main/Choices/Button"); _template.gameObject.SetActive(false);
                _choiceMinimumHeight = ((RectTransform)_template.transform).sizeDelta.y;
                _choiceVerticalPadding = -_template.GetComponentInChildren<Text>(true).rectTransform.sizeDelta.y;
                var menuLayout = _content.GetComponent<VerticalLayoutGroup>();
                menuLayout.childControlHeight = true; menuLayout.childForceExpandHeight = false;
                _menuScroll = CreateMenuViewport();
                _voice = _ui.Get<Button>(ButtonsPath + "/Voice");
                Bind(_voice, () => { _replay?.Invoke(); ReplayRequested?.Invoke(); });
                _voice.interactable = false;
                _auto = _ui.Get<Toggle>(ButtonsPath + "/Auto"); _skip = _ui.Get<Toggle>(ButtonsPath + "/Skip");
                _auto.onValueChanged = new Toggle.ToggleEvent(); _skip.onValueChanged = new Toggle.ToggleEvent();
                _auto.SetIsOnWithoutNotify(false); _skip.SetIsOnWithoutNotify(false);
                _auto.onValueChanged.AddListener(value => AutoChanged?.Invoke(value));
                _skip.onValueChanged.AddListener(value => SkipChanged?.Invoke(value));
                EnablePlaybackControls(false);
                _ui.Get<Button>(ButtonsPath + "/Config").interactable = false;
                Bind(_ui.Get<Button>(ButtonsPath + "/BackLog"), () => ShowLog(true));
                Bind(_ui.Get<Button>(MessagePath + "/MsgWindow/Buttons/Close"), () => { if (closeRequested != null) closeRequested(); else SetHidden(true); });
                var next = _ui.Get(MessagePath + "/MsgWindow").AddComponent<Button>(); next.transition = Selectable.Transition.None;
                Bind(next, () => { if (!_logVisible && !_hidden) AdvanceRequested?.Invoke(); });
                _restoreClick = new GameObject("Restore conversation window", typeof(RectTransform), typeof(Image), typeof(Button));
                _restoreClick.SetActive(false); _restoreClick.transform.SetParent(_ui.Root.transform, false);
                _ui.Data.rectOverrides.First(rect => rect.path == "Canvas_Main").Apply((RectTransform)_restoreClick.transform);
                _restoreClick.GetComponent<Image>().color = Color.clear;
                _restoreClick.GetComponent<Button>().transition = Selectable.Transition.None;
                Bind(_restoreClick.GetComponent<Button>(), () => SetHidden(false));
                _log = _ui.Get("Canvas_BackLog"); _log.SetActive(false);
                _logScroll = _ui.Get<Scrollbar>("Canvas_BackLog/Scroll/Scrollbar");
                _logScroll.direction = Scrollbar.Direction.BottomToTop;
                _logScroll.onValueChanged = new Scrollbar.ScrollEvent();
                _logScroll.onValueChanged.AddListener(value =>
                {
                    _logOffset = Mathf.RoundToInt((_ui.Data.one - value) * MaxLogOffset); RefreshLog();
                });
                Bind(_ui.Get<Button>("Canvas_BackLog/Scroll/Arrow_Up"), () => MoveLog(-_rows.Count));
                Bind(_ui.Get<Button>("Canvas_BackLog/Scroll/Arrow_Down"), () => MoveLog(_rows.Count));
                Bind(_ui.Get<Button>("Canvas_BackLog/Return"), () => ShowLog(false));
                foreach (var node in _ui.Data.nodes.Where(node => node.parent == "Canvas_BackLog/Logs").OrderBy(node => node.path, StringComparer.Ordinal))
                    _rows.Add(new LogRow
                    {
                        Root = _ui.Get(node.path), Name = _ui.Get<Text>(node.path + "/TextBackGround/TextWindow/Name"),
                        Message = _ui.Get<Text>(node.path + "/TextBackGround/TextWindow/Message"), Voice = _ui.Get<Button>(node.path + "/VoiceButton")
                    });
                if (!_rows.Any()) throw new InvalidOperationException("KKS conversation log has no display rows.");
                SetCanAdvance(false);
            }
            catch { _ui.Dispose(); throw; }
        }

        private ScrollRect CreateMenuViewport()
        {
            var viewport = new GameObject("Conversation commands viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_ui.Root.transform, false);
            _ui.Data.menuViewport.Apply((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = Color.clear;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform; scroll.content = _content;
            scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = _ui.Data.scrollSensitivity;
            _content.SetParent(viewport.transform, false);
            return scroll;
        }
        private static void Bind(Button button, Action action)
        {
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => action());
        }
        internal void EnablePlaybackControls(bool enabled) { _auto.interactable = enabled; _skip.interactable = enabled; }
        internal void SetCanAdvance(bool canAdvance) => _ui.Get(MessagePath + "/MsgWindow/Next").SetActive(canAdvance);
        internal void Show(string name, string text)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksConversationView));
            _ui.Root.SetActive(true); SetHidden(false); ShowLog(false); PresentLine(name, text, addToLog: !string.IsNullOrEmpty(text));
            ResizeChoices();
            _menuScroll.verticalNormalizedPosition = _ui.Data.one;
        }
        internal void Hide()
        {
            if (_disposed) return;
            SetAiInput(false, false);
            ++_choiceGeneration;
            _auto.SetIsOnWithoutNotify(false); _skip.SetIsOnWithoutNotify(false);
            _ui.Root.SetActive(false); _hidden = false; _logVisible = false;
        }
        internal void PresentLine(string name, string text, Action replay = null, bool addToLog = true)
        {
            if (_disposed) return;
            _name.text = name ?? string.Empty; _message.text = text ?? string.Empty;
            _replay = replay; _voice.interactable = replay != null;
            if (addToLog) _entries.Add(new LogEntry { Name = _name.text, Text = _message.text, Replay = replay });
        }
        internal void SetTextColor(Color color) { if (!_disposed) { _name.color = color; _message.color = color; } }
        internal void AppendChoice(string name, string text) => _entries.Add(new LogEntry { Name = name, Text = text });
        internal void SetChoices(IEnumerable<Choice> choices)
        {
            if (_disposed) return;
            var generation = ++_choiceGeneration;
            foreach (var choice in _choices) { choice.gameObject.SetActive(false); UnityEngine.Object.Destroy(choice.gameObject); }
            _choices.Clear();
            foreach (var choice in choices)
            {
                var button = UnityEngine.Object.Instantiate(_template, _content, false);
                button.name = choice.Text; button.GetComponentInChildren<Text>(true).text = choice.Text;
                var element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                element.minHeight = _choiceMinimumHeight;
                button.interactable = choice.Enabled; Bind(button, () =>
                {
                    if (Active && !_hidden && !_logVisible && generation == _choiceGeneration && choice.Enabled) choice.Select?.Invoke();
                });
                _choices.Add(button); button.gameObject.SetActive(true);
            }
            _menuScroll.gameObject.SetActive(!_hidden && !_logVisible && _choices.Any());
            ResizeChoices();
            _menuScroll.verticalNormalizedPosition = _ui.Data.one;
        }
        private void ResizeChoices()
        {
            // Width must be resolved before measuring wrapped choice text.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            foreach (var button in _choices)
            {
                var text = button.GetComponentInChildren<Text>(true);
                button.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(_choiceMinimumHeight, text.preferredHeight + _choiceVerticalPadding);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }
        internal bool CloseOverlay()
        {
            if (_logVisible) { ShowLog(false); return true; }
            if (_hidden) { SetHidden(false); return true; }
            return false;
        }
        private void SetHidden(bool hidden)
        {
            _hidden = hidden;
            _ui.Get(MessagePath).SetActive(!hidden);
            _menuScroll.gameObject.SetActive(!hidden && !_logVisible && _choices.Any());
            _restoreClick.SetActive(hidden);
        }
        private int MaxLogOffset => Math.Max(_ui.Data.zero, _entries.Count - _rows.Count);
        private void MoveLog(int delta) { _logOffset = Mathf.Clamp(_logOffset + delta, _ui.Data.zero, MaxLogOffset); RefreshLog(); }
        private void ShowLog(bool visible)
        {
            var changed = _logVisible != visible;
            _logVisible = visible; _log.SetActive(visible);
            _ui.Get(MessagePath).SetActive(!visible && !_hidden);
            _menuScroll.gameObject.SetActive(!visible && !_hidden && _choices.Any());
            if (visible) { _logOffset = MaxLogOffset; RefreshLog(); _log.transform.SetAsLastSibling(); }
            if (changed) DisplayStateChanged?.Invoke();
        }
        private void RefreshLog()
        {
            var offset = _logOffset;
            foreach (var row in _rows)
            {
                var present = offset < _entries.Count; row.Root.SetActive(present);
                if (!present) continue;
                var entry = _entries[offset++]; row.Name.text = entry.Name; row.Message.text = entry.Text;
                row.Voice.interactable = entry.Replay != null; Bind(row.Voice, () => entry.Replay?.Invoke());
            }
            _logScroll.SetValueWithoutNotify(MaxLogOffset == _ui.Data.zero ? _ui.Data.one : _ui.Data.one - (float)_logOffset / MaxLogOffset);
            _logScroll.size = _entries.Count == _ui.Data.zero ? _ui.Data.one : Mathf.Min(_ui.Data.one, (float)_rows.Count / _entries.Count);
        }
        public void Dispose()
        {
            if (_disposed) return;
            Hide(); _disposed = true;
            AuthoringViews.Remove(this); _editRequested = null;
            AdvanceRequested = null; ReplayRequested = null; AutoChanged = null; SkipChanged = null; _replay = null;
            DisplayStateChanged = null;
            _choices.Clear(); _entries.Clear(); _rows.Clear(); _ui.Dispose();
        }
    }
}
