using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameIPhoneCamera
{
    internal sealed class PhoneIcon
    {
        internal string Id, Title;
        internal Sprite Sprite;
    }
    internal sealed class PhoneBodyAnimation : MonoBehaviour
    {
        private AnimationClip _clip;
        internal void Enter()
        {
            var group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;
            var animation = gameObject.AddComponent<Animation>(); animation.cullingType = AnimationCullingType.AlwaysAnimate;
            _clip = new AnimationClip { legacy = true, wrapMode = WrapMode.ClampForever };
            _clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", AnimationCurve.EaseInOut(0, 0, .2f, 1));
            _clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.EaseInOut(0, 45, .2f, 0));
            animation.AddClip(_clip, "Enter"); animation.Play("Enter");
        }
        private void OnDestroy() { if (_clip != null) Destroy(_clip); }
    }
    internal sealed class PhoneShellUi : IDisposable
    {
        internal Canvas Canvas { get; private set; }
        internal RectTransform Viewport => _body;
        private readonly GameObject _root;
        private readonly Font _font;
        private readonly Font _clockFont;
        private RectTransform _body;
        private Transform _orientationPivot;
        private RectTransform _contentRoot;
        private PhoneOrientationMotion _orientationMotion;
        private string _pageLayout;
        internal int GridColumns => PhoneOrientationPlan.Columns(_viewerTurns);
        private RectTransform _chrome, _viewerLayer;
        private ViewerMotion _viewerMotion;
        private RectTransform _viewerViewport;
        private Transform _viewerContent;
        private int _viewerTurns;
        private readonly RawImage _wallpaper;
        private readonly Text _clock, _title, _hint, _message;
        private Text _largeClock, _date, _editorValues;
        private RawImage _preview;
        private readonly List<Image> _selection = new List<Image>();
        private readonly Dictionary<int, RawImage> _thumbs = new Dictionary<int, RawImage>();
        private readonly List<Sprite> _icons = new List<Sprite>();
        private readonly List<Texture2D> _iconTextures = new List<Texture2D>();
        internal PhoneShellUi(Transform parent, Mesh mesh)
        {
            _root = new GameObject("IPhoneShell", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)_root.transform; rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(560, 1166);
            Canvas = _root.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.WorldSpace; Canvas.sortingOrder = 21;
            _root.AddComponent<PhoneScreenMask>().Initialize(mesh); _root.AddComponent<Mask>().showMaskGraphic = false;
            _orientationPivot = new GameObject("ShellOrientation").transform; _orientationPivot.SetParent(rect, false);
            _contentRoot = Node(_orientationPivot, "Content", Vector2.zero, new Vector2(560, 1166));
            _orientationMotion = _orientationPivot.gameObject.AddComponent<PhoneOrientationMotion>();
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 32);
            _clockFont = PhoneClockStyle.CreateFont();
            var background = Panel(rect, "Background", Vector2.zero, new Vector2(560, 1166), new Color(.035f, .045f, .09f));
            background.transform.SetSiblingIndex(0);
            _wallpaper = Node(_contentRoot, "Wallpaper", Vector2.zero, new Vector2(560, 1166)).gameObject.AddComponent<RawImage>();
            _wallpaper.raycastTarget = false; _wallpaper.enabled = false;
            _chrome = Node(_contentRoot, "Chrome", Vector2.zero, new Vector2(560, 1166));
            rect = _chrome;
            Panel(rect, "HeaderShade", new Vector2(0, 478), new Vector2(560, 160), new Color(0, 0, 0, .3f));
            Panel(rect, "FooterShade", new Vector2(0, -510), new Vector2(560, 146), new Color(0, 0, 0, .75f));
            Panel(rect, "ClockShade", new Vector2(-179, 526), new Vector2(174, 64), new Color(0, 0, 0, .65f));
            _clock = Label(rect, "StatusTime", "", new Vector2(-179, 526), new Vector2(168, 62), 44);
            PhoneClockStyle.Apply(_clock, _clockFont);
            _title = Label(rect, "Title", "", new Vector2(0, 447), new Vector2(510, 60), 34);
            _hint = Label(rect, "Hint", "", new Vector2(0, -536), new Vector2(510, 60), 23);
            _message = Label(rect, "Message", "", new Vector2(0, -476), new Vector2(510, 55), 21);
        }
        internal void Active(bool value) => _root.SetActive(value);
        internal void StartPage(string title, string hint, Texture2D wallpaper = null)
        {
            _orientationMotion.Cancel(); _pageLayout = "other";
            StopViewerMotion();
            if (_viewerLayer != null) { _viewerLayer.name = "RetiredViewer"; _viewerLayer.gameObject.SetActive(false); UnityEngine.Object.Destroy(_viewerLayer.gameObject); }
            _viewerLayer = null; _viewerMotion = null;
            _viewerViewport = null; _viewerContent = null;
            _chrome.gameObject.SetActive(true);
            LayoutChrome(_viewerTurns);
            if (_body != null) { _body.name = "RetiredPage"; _body.gameObject.SetActive(false); UnityEngine.Object.Destroy(_body.gameObject); }
            _selection.Clear(); _thumbs.Clear(); _preview = null; _largeClock = null; _date = null; _editorValues = null;
            _body = Node(_contentRoot, "Page", Vector2.zero, new Vector2(560, 850));
            _body.gameObject.AddComponent<PhoneBodyAnimation>().Enter();
            _title.text = title; _hint.text = hint; _message.text = "";
            _wallpaper.texture = wallpaper; _wallpaper.enabled = wallpaper != null;
            if (wallpaper != null) _wallpaper.uvRect = Cover(wallpaper.width, wallpaper.height, 1125, 2436);
            Active(true); Clock(DateTime.Now);
        }
        internal void Message(string message) => _message.text = message ?? "";
        internal void Clock(DateTime now)
        {
            _clock.text = now.ToString("HH:mm");
            if (_largeClock != null) _largeClock.text = now.ToString("HH:mm");
            if (_date != null) _date.text = now.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("ja-JP"));
        }
        internal void Lock()
        {
            _pageLayout = "lock";
            Panel(_body, "LockClockShade", new Vector2(0, 270), new Vector2(530, 260), new Color(0, 0, 0, .6f));
            _largeClock = Label(_body, "LockTime", "", new Vector2(0, 300), new Vector2(510, 176), 132);
            PhoneClockStyle.Apply(_largeClock, _clockFont);
            _date = Label(_body, "LockDate", "", new Vector2(0, 184), new Vector2(510, 64), 40);
            _date.fontStyle = FontStyle.Bold;
            Label(_body, "Unlock", "A：メニューを開く\nトリガー：カメラ", new Vector2(0, -280), new Vector2(480, 140), 31);
            Clock(DateTime.Now);
            Orientation(_viewerTurns, false);
        }
        internal void Home(IReadOnlyList<PhoneIcon> apps, int selected)
        {
            _pageLayout = "home";
            int first = selected < 0 ? 0 : selected / 9 * 9;
            for (int i = first; i < Math.Min(apps.Count, first + 9); i++)
            {
                int local = i - first; Vector2 p = new Vector2((local % 3 - 1) * 174, 260 - local / 3 * 220);
                var outline = Panel(_body, "AppSelection" + i, p, new Vector2(150, 176), Color.clear); _selection.Add(outline);
                var image = Node(outline.transform, "Icon", new Vector2(0, 20), new Vector2(104, 104)).gameObject.AddComponent<Image>();
                image.raycastTarget = false; image.sprite = apps[i].Sprite;
                Label(outline.transform, "Name", apps[i].Title, new Vector2(0, -55), new Vector2(145, 48), 25);
            }
            Select(selected - first);
            _message.text = "写真・カメラ・設定" + (apps.Count > 3 ? "  " + (selected / 9 + 1) + "ページ" : "");
            Orientation(_viewerTurns, false);
        }
        internal void Gallery(IReadOnlyList<GalleryItem> items, int selected)
        {
            _pageLayout = "gallery";
            int first = selected < 0 ? 0 : selected / 9 * 9;
            for (int i = first; i < Math.Min(items.Count, first + 9); i++)
            {
                int local = i - first; Vector2 p = new Vector2((local % 3 - 1) * 174, 250 - local / 3 * 218);
                var outline = Panel(_body, "Media" + i, p, new Vector2(158, 203), Color.clear); _selection.Add(outline);
                var raw = Node(outline.transform, "Thumbnail", new Vector2(0, 20), new Vector2(139, 135)).gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false; raw.color = new Color(.2f, .2f, .25f); _thumbs[i] = raw;
                string name = items[i].Name; if (name.Length > 21) name = name.Substring(0, 18) + "…";
                Label(outline.transform, "Filename", (items[i].Video ? "▶ " : "") + name, new Vector2(0, -70), new Vector2(148, 52), 17);
            }
            Select(selected - first);
            if (items.Count == 0) Label(_body, "Empty", "写真・動画がありません\n設定アプリからフォルダーを追加できます", Vector2.zero, new Vector2(485, 220), 27);
            _message.text = items.Count == 0 ? "A：再読込" : (selected + 1) + " / " + items.Count + "   A：開く";
            Orientation(_viewerTurns, false);
        }
        internal void Thumbnail(int index, Texture texture)
        {
            if (!_thumbs.TryGetValue(index, out var image) || image == null) return;
            image.texture = texture; image.color = Color.white;
            if (texture != null) image.uvRect = Cover(texture.width, texture.height, (_viewerTurns & 1) == 0 ? 139 : 186, (_viewerTurns & 1) == 0 ? 135 : 96);
        }
        internal void Viewer(string[] controls, int selected)
        {
            _pageLayout = "viewer";
            _viewerLayer = Node(_contentRoot, "ViewerMedia", Vector2.zero, new Vector2(560, 1166));
            _viewerLayer.SetSiblingIndex(1);
            Panel(_viewerLayer, "Black", Vector2.zero, new Vector2(560, 1166), Color.black);
            _viewerViewport = Node(_viewerLayer, "OrientedViewport", Vector2.zero, new Vector2(560, 1166));
            _viewerContent = new GameObject("MediaTransform").transform;
            _viewerContent.SetParent(_viewerViewport, false);
            _preview = Node(_viewerContent, "Preview", Vector2.zero, new Vector2(560, 1166)).gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false; _preview.color = Color.clear;
            _viewerMotion = _viewerContent.gameObject.AddComponent<ViewerMotion>();
            Buttons(controls, selected);
            Orientation(_viewerTurns, false);
        }
        internal void Orientation(int turns, bool animate = true)
        {
            _viewerTurns = turns & 3;
            _orientationMotion.Transition(_viewerTurns, LayoutOrientation, animate);
        }
        private void LayoutOrientation()
        {
            bool landscape = (_viewerTurns & 1) != 0;
            float width = landscape ? 1166 : 560, height = landscape ? 560 : 1166;
            _contentRoot.sizeDelta = new Vector2(width, height);
            _wallpaper.rectTransform.sizeDelta = new Vector2(width, height);
            if (_wallpaper.texture != null) _wallpaper.uvRect = Cover(_wallpaper.texture.width, _wallpaper.texture.height, (int)width, (int)height);
            LayoutChrome(_viewerTurns);
            if (_body != null) _body.sizeDelta = new Vector2(width, landscape ? 360 : 850);
            if (_pageLayout == "home" || _pageLayout == "gallery")
            {
                for (int i = 0; i < _selection.Count; i++)
                {
                    var rect = _selection[i].rectTransform;
                    rect.anchoredPosition = landscape ? new Vector2((i % 5 - 2) * 218, 80 - i / 5 * 160)
                        : new Vector2((i % 3 - 1) * 174, (_pageLayout == "home" ? 260 : 250) - i / 3 * (_pageLayout == "home" ? 220 : 218));
                    rect.sizeDelta = landscape ? new Vector2(202, 146) : _pageLayout == "home" ? new Vector2(150, 176) : new Vector2(158, 203);
                    var raw = rect.GetComponentInChildren<RawImage>(true);
                    if (raw != null) { raw.rectTransform.sizeDelta = landscape ? new Vector2(186, 96) : new Vector2(139, 135); if (raw.texture != null) raw.uvRect = Cover(raw.texture.width, raw.texture.height, landscape ? 186 : 139, landscape ? 96 : 135); }
                    var image = rect.Find("Icon") as RectTransform; if (image != null) image.sizeDelta = landscape ? new Vector2(82, 82) : new Vector2(104, 104);
                    var text = rect.GetComponentInChildren<Text>(true); if (text != null) { text.rectTransform.anchoredPosition = new Vector2(0, landscape ? -51 : _pageLayout == "home" ? -55 : -70); text.rectTransform.sizeDelta = new Vector2(landscape ? 194 : 148, landscape ? 40 : 52); }
                }
            }
            if (_pageLayout == "lock" && _largeClock != null)
            {
                _largeClock.rectTransform.anchoredPosition = new Vector2(0, landscape ? 65 : 300);
                _date.rectTransform.anchoredPosition = new Vector2(0, landscape ? -48 : 184);
                var unlock = _body.Find("Unlock") as RectTransform; if (unlock != null) unlock.anchoredPosition = new Vector2(0, landscape ? -158 : -280);
                var shade = _body.Find("LockClockShade") as RectTransform; if (shade != null) shade.anchoredPosition = new Vector2(0, landscape ? 20 : 270);
            }
            if (_pageLayout == "editor" && _preview != null)
            {
                _preview.rectTransform.sizeDelta = landscape ? new Vector2(180, 390) : new Vector2(320, 645);
                _preview.rectTransform.anchoredPosition = landscape ? new Vector2(-300, 0) : new Vector2(0, 70);
                _editorValues.rectTransform.anchoredPosition = landscape ? new Vector2(220, 60) : new Vector2(0, -285);
            }
            if (_pageLayout == "viewer" || _pageLayout == "editor")
            {
                _hint.rectTransform.anchoredPosition = new Vector2(0, PhoneControlLayout.HintY(landscape));
                _hint.rectTransform.sizeDelta = new Vector2(width - 50, PhoneControlLayout.HintHeight);
                _hint.fontSize = 21;
                _message.rectTransform.anchoredPosition = new Vector2(0, PhoneControlLayout.MessageY(landscape, _selection.Count));
                _message.rectTransform.sizeDelta = new Vector2(width - 50, PhoneControlLayout.MessageHeight);
                PlaceChrome("FooterShade", new Vector2(0, -height / 2 + (landscape ? 110 : 145)), new Vector2(width, landscape ? 220 : 290));
                for (int i = 0; i < _selection.Count; i++)
                    _selection[i].rectTransform.anchoredPosition = new Vector2(PhoneControlLayout.ButtonX(landscape, _selection.Count, i), PhoneControlLayout.ButtonY(landscape, _selection.Count, i));
            }
            if (_viewerViewport == null) return;
            _viewerLayer.sizeDelta = new Vector2(width, height);
            var black = _viewerLayer.Find("Black") as RectTransform; if (black != null) black.sizeDelta = new Vector2(width, height);
            _viewerViewport.sizeDelta = new Vector2(width, height);
            _viewerMotion?.Resize(width, height);
            // Controls keep their font/button sizes and move with the readable viewport.
            for (int i = 0; i < _selection.Count; i++)
            {
                var rect = _selection[i].rectTransform; rect.SetParent(_viewerViewport, false);
                rect.anchoredPosition = new Vector2(PhoneControlLayout.ButtonX(landscape, _selection.Count, i), PhoneControlLayout.ButtonY(landscape, _selection.Count, i));
            }
        }
        private void LayoutChrome(int turns)
        {
            bool landscape = (turns & 1) != 0;
            float width = landscape ? 1166 : 560, height = landscape ? 560 : 1166;
            _chrome.sizeDelta = new Vector2(width, height);
            _chrome.localRotation = Quaternion.identity;
            PlaceChrome("HeaderShade", new Vector2(0, height * .5f - 105), new Vector2(width, 160));
            PlaceChrome("FooterShade", new Vector2(0, -height * .5f + 73), new Vector2(width, 146));
            PlaceChrome("ClockShade", new Vector2(-width * .5f + 101, height * .5f - 57), new Vector2(174, 64));
            _clock.rectTransform.anchoredPosition = new Vector2(-width * .5f + 101, height * .5f - 57);
            _title.rectTransform.anchoredPosition = new Vector2(0, landscape ? 223 : 447);
            _hint.rectTransform.anchoredPosition = new Vector2(0, landscape ? -250 : -536);
            _message.rectTransform.anchoredPosition = new Vector2(0, landscape ? -206 : -476);
            _hint.rectTransform.sizeDelta = new Vector2(width - 50, 60);
            _hint.fontSize = 23;
            _message.rectTransform.sizeDelta = new Vector2(510, 55);
        }
        private void PlaceChrome(string name, Vector2 position, Vector2 size)
        {
            var rect = _chrome.Find(name) as RectTransform;
            if (rect != null) { rect.anchoredPosition = position; rect.sizeDelta = size; }
        }
        internal void ViewerUi(bool visible)
        {
            _chrome.gameObject.SetActive(visible); _body.gameObject.SetActive(visible);
            if (_viewerViewport != null) foreach (var control in _selection) control.gameObject.SetActive(visible);
        }
        internal void ViewerAxis(float x, float y, bool pan) { _viewerMotion?.Drive(x, y, pan); }
        internal void StopViewerMotion() { _viewerMotion?.StopMotion(); }
        internal void ViewerControls(string[] controls, int selected)
        {
            for (int i = 0; i < Math.Min(controls.Length, _selection.Count); i++)
                _selection[i].GetComponentInChildren<Text>(true).text = controls[i];
            Select(selected);
        }
        internal void Preview(Texture texture)
        {
            if (_preview == null) return;
            bool changed = _preview.texture != texture;
            _preview.texture = texture; _preview.color = texture == null ? Color.clear : Color.white;
            if (texture == null) return;
            if (changed && _viewerMotion != null) _viewerMotion.Initialize(_viewerContent, _preview.rectTransform, texture.width, texture.height, _viewerViewport.sizeDelta.x, _viewerViewport.sizeDelta.y);
            _preview.uvRect = new Rect(0, 0, 1, 1);
        }
        internal void Editor(Texture source, WallpaperDraft draft, int selected)
        {
            _pageLayout = "editor";
            _preview = Node(_body, "CropPreview", new Vector2(0, 50), new Vector2(320, 693)).gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false; _preview.texture = source;
            _editorValues = Label(_body, "CropValues", "", new Vector2(0, -305), new Vector2(500, 45), 22);
            Buttons(new[] { "位置", "倍率", "保存", "リセット" }, selected);
            EditorValues(source, draft);
            Orientation(_viewerTurns, false);
        }
        internal void EditorValues(Texture source, WallpaperDraft draft)
        {
            if (_preview == null || source == null) return;
            var crop = draft.Crop(source.width, source.height);
            _preview.uvRect = new Rect(crop.X, crop.Y, crop.Width, crop.Height);
            _editorValues.text = "位置 " + draft.X.ToString("0.00") + ", " + draft.Y.ToString("0.00") + "   " + draft.Zoom.ToString("0.00") + "倍";
            _message.text = draft.Edit == 1 ? "スティック：位置調整／A：調整終了" : draft.Edit == 2 ? "上下：倍率調整／A：調整終了" : "保存で待受画像を作成／Bで編集を破棄";
        }
        internal void Settings(string description)
        {
            Label(_body, "SettingsInfo", description, Vector2.zero, new Vector2(490, 400), 29);
            Orientation(_viewerTurns, false);
        }
        private void Buttons(string[] controls, int selected)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                var image = Panel(_body, "Control" + i, new Vector2((i % 3 - 1) * 174, -355 - i / 3 * 69), new Vector2(160, 60), Color.clear);
                _selection.Add(image); Label(image.transform, "Text", controls[i], Vector2.zero, new Vector2(152, 55), 21);
            }
            Select(selected);
        }
        internal void Select(int selected)
        {
            for (int i = 0; i < _selection.Count; i++) _selection[i].color = i == selected ? new Color(.15f, .48f, .95f, .8f) : new Color(0, 0, 0, .35f);
        }
        internal Sprite BuiltinIcon(int kind)
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float dx = x - 64, dy = y - 64, radius = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = new Color(.85f, .87f, .9f);
                float edgeX = Math.Max(0, Math.Abs(dx) - 43), edgeY = Math.Max(0, Math.Abs(dy) - 43);
                if (edgeX * edgeX + edgeY * edgeY > 20 * 20) c = Color.clear;
                else if (kind == 0)
                {
                    c = Color.white;
                    for (int petal = 0; petal < 8; petal++)
                    {
                        float angle = petal * Mathf.PI / 4, px = dx - Mathf.Cos(angle) * 26, py = dy - Mathf.Sin(angle) * 26;
                        if (px * px + py * py < 23 * 23) c = Color.HSVToRGB(petal / 8f, .75f, .95f);
                    }
                }
                else if (kind == 1)
                {
                    if (Math.Abs(dx) < 49 && Math.Abs(dy) < 34 || Math.Abs(dx + 22) < 20 && dy > 20 && dy < 44) c = new Color(.12f, .14f, .17f);
                    if (radius < 31) c = new Color(.35f, .4f, .45f);
                    if (radius < 24) c = new Color(.015f, .05f, .13f);
                    if ((dx + 8) * (dx + 8) + (dy - 8) * (dy - 8) < 7 * 7) c = new Color(.35f, .65f, .9f);
                }
                else
                {
                    float teeth = 39 + 8 * Mathf.Max(0, Mathf.Cos(Mathf.Atan2(dy, dx) * 12));
                    if (radius < teeth && radius > 20) c = new Color(.2f, .23f, .28f);
                    if (radius < 13) c = new Color(.2f, .23f, .28f);
                }
                pixels[y * 128 + x] = c;
            }
            texture.SetPixels32(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
            _iconTextures.Add(texture); _icons.Add(sprite); return sprite;
        }
        private static Rect Cover(int w, int h, int areaW, int areaH)
        {
            float source = (float)w / Math.Max(1, h), target = (float)areaW / areaH;
            float width = Math.Min(1, target / source), height = Math.Min(1, source / target);
            return new Rect((1 - width) * .5f, (1 - height) * .5f, width, height);
        }
        private static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = Node(parent, name, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Node(parent, name, position, size).gameObject.AddComponent<Text>();
            text.font = _font; text.fontSize = fontSize; text.text = value; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        public void Dispose()
        {
            _orientationMotion.Cancel();
            StopViewerMotion();
            UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_font);
            UnityEngine.Object.Destroy(_clockFont);
            foreach (var icon in _icons) UnityEngine.Object.Destroy(icon);
            foreach (var texture in _iconTextures) UnityEngine.Object.Destroy(texture);
        }
    }
}
