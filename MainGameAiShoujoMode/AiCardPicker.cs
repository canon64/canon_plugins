using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Enumerate on Refresh only, and decode only the selected page when it changes.
    internal sealed class AiCardPicker : IDisposable
    {
        private sealed class Card { public string path, error; public Texture2D image; }
        private readonly Action<Exception> _failure;
        private Parameters _settings;
        private string _directory;
        private string[] _files = Array.Empty<string>();
        private Card[] _pageCards = Array.Empty<Card>(), _drawCards = Array.Empty<Card>();
        private int _page, _drawPage, _drawPages, _columns, _drawColumns, _pageSize;
        private float _width, _height, _labelHeight, _drawWidth, _drawHeight, _drawLabelHeight;
        private bool _dirty;
        private GUIStyle _caption;
        public AiCardPicker(Parameters settings, string directory, Action<Exception> failure)
        {
            _failure = failure; Configure(settings); Refresh(directory);
        }
        public void Configure(Parameters settings)
        {
            int pageSize = settings.I("animation.cardsPerPage"), columns = settings.I("cardPicker.columns");
            float width = settings.N("cardPicker.width"), height = settings.N("cardPicker.height"), label = settings.N("cardPicker.labelHeight");
            if (pageSize <= settings.I("math.zero") || columns <= settings.I("math.zero") || width <= settings.N("math.zero") || label <= settings.N("math.zero") || height <= label)
                throw new InvalidDataException("画像カード選択のページ件数・寸法が不正です。");
            if (_pageSize != pageSize || _width != width || _height != height || _labelHeight != label) _dirty = true;
            _settings = settings; _pageSize = pageSize; _columns = columns; _width = width; _height = height; _labelHeight = label;
        }
        public void Refresh(string directory = null)
        {
            try
            {
                if (directory != null) _directory = Path.GetFullPath(directory);
                if (!Directory.Exists(_directory)) throw new DirectoryNotFoundException("カードフォルダがありません：" + _directory);
                _files = Directory.GetFiles(_directory, "*.png", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                _page = _settings.I("math.zero"); _dirty = true;
            }
            catch (Exception ex) { _failure(ex); }
        }
        private int Pages => Mathf.Max(_settings.I("math.one"), (_files.Length + _pageSize - _settings.I("math.one")) / _pageSize);
        private void ChangePage(int page)
        {
            int next = Mathf.Clamp(page, _settings.I("math.zero"), Pages - _settings.I("math.one"));
            if (next != _page) { _page = next; _dirty = true; }
        }
        private void PrepareLayout()
        {
            _page = Mathf.Clamp(_page, _settings.I("math.zero"), Pages - _settings.I("math.one"));
            if (_dirty)
            {
                _dirty = false; ReleasePage();
                _pageCards = _files.Skip(_page * _pageSize).Take(_pageSize).Select(path => new Card { path = path }).ToArray();
                foreach (var card in _pageCards)
                {
                    try { card.image = Preview(card.path); }
                    catch (Exception ex) { card.error = ex.GetBaseException().Message; }
                }
            }
            _drawCards = _pageCards; _drawPage = _page; _drawPages = Pages; _drawColumns = _columns;
            _drawWidth = _width; _drawHeight = _height; _drawLabelHeight = _labelHeight;
        }
        private Texture2D Preview(string path)
        {
            Texture2D original = null, preview = null; RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                // KKS cards append character/coordinate data after IEND. Decode the PNG segment only.
                byte[] png = PngFile.LoadPngBytes(path);
                if (png == null || !png.Any()) throw new InvalidDataException("PNG画像がありません。");
                original = new Texture2D(_settings.I("math.one"), _settings.I("math.one"), TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(original, png, true)) throw new InvalidDataException("カード画像を読み込めません。");
                float scale = Mathf.Min(_width / original.width, (_height - _labelHeight) / original.height);
                int width = Mathf.Max(_settings.I("math.one"), Mathf.RoundToInt(original.width * scale));
                int height = Mathf.Max(_settings.I("math.one"), Mathf.RoundToInt(original.height * scale));
                target = RenderTexture.GetTemporary(width, height, _settings.I("math.zero"), RenderTextureFormat.ARGB32);
                Graphics.Blit(original, target); RenderTexture.active = target;
                preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
                preview.ReadPixels(new Rect(_settings.N("math.zero"), _settings.N("math.zero"), width, height), _settings.I("math.zero"), _settings.I("math.zero"));
                preview.Apply(false, true); preview.name = "AI Card Preview " + Path.GetFileName(path);
                return preview;
            }
            catch { if (preview != null) UnityEngine.Object.Destroy(preview); throw; }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (original != null) UnityEngine.Object.Destroy(original);
            }
        }
        public void Draw(string selected, Action<string> choose)
        {
            if (Event.current.type == EventType.Layout) PrepareLayout();
            GUILayout.Label("画像をクリックして選択：" + _files.Length + "枚 / " + _directory);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("前のページ")) ChangePage(_page - _settings.I("math.one"));
            GUILayout.Label((_drawPage + _settings.I("math.one")) + " / " + _drawPages);
            if (GUILayout.Button("次のページ")) ChangePage(_page + _settings.I("math.one"));
            if (GUILayout.Button("カード一覧を更新")) Refresh();
            GUILayout.EndHorizontal();
            if (_caption == null) _caption = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            for (int index = _settings.I("math.zero"); index < _drawCards.Length; index++)
            {
                if (index % _drawColumns == _settings.I("math.zero")) GUILayout.BeginHorizontal();
                var card = _drawCards[index];
                Rect cell = GUILayoutUtility.GetRect(_drawWidth, _drawHeight, GUILayout.ExpandWidth(false));
                Color old = GUI.color;
                if (string.Equals(selected, card.path, StringComparison.OrdinalIgnoreCase)) GUI.color = GUI.skin.settings.selectionColor;
                bool clicked = GUI.Button(cell, new GUIContent("", card.path + (card.error == null ? "" : "\n" + card.error)));
                GUI.color = old;
                var imageRect = new Rect(cell.x, cell.y, cell.width, cell.height - _drawLabelHeight);
                if (card.image != null) GUI.DrawTexture(imageRect, card.image, ScaleMode.ScaleToFit);
                else GUI.Label(imageRect, "画像を表示できません", _caption);
                GUI.Label(new Rect(cell.x, cell.yMax - _drawLabelHeight, cell.width, _drawLabelHeight), Path.GetFileNameWithoutExtension(card.path), _caption);
                if (clicked) choose(card.path);
                if ((index + _settings.I("math.one")) % _drawColumns == _settings.I("math.zero") || index == _drawCards.Length - _settings.I("math.one")) GUILayout.EndHorizontal();
            }
        }
        private void ReleasePage()
        {
            foreach (var card in _pageCards) if (card.image != null) UnityEngine.Object.Destroy(card.image);
            _pageCards = Array.Empty<Card>();
        }
        public void Dispose() { ReleasePage(); _drawCards = Array.Empty<Card>(); _files = Array.Empty<string>(); }
    }
}
