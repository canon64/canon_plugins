using System;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameAiShoujoMode
{
    // A static, unlit card on the actual door pane. The original collider stays in place.
    internal sealed class HotelDoorCardDisplay : IDisposable
    {
        private readonly MeshFilter _pane;
        private readonly Renderer _renderer;
        private readonly bool _originalEnabled;
        private readonly Parameters _p;
        private readonly HotelRoomControlSettings _cfg;
        private GameObject _root;
        private Texture2D _texture;
        private Color[] _originalPixels, _blurredPixels, _scratchPixels, _displayPixels;
        private int? _appliedRadius;
        private float? _appliedPale;
        private float? _appliedBrightness;

        internal HotelDoorCardDisplay(MeshFilter pane, Parameters p, HotelRoomControlSettings cfg)
        {
            _pane = pane; _p = p; _cfg = cfg; _renderer = pane.GetComponent<Renderer>();
            if (_renderer == null || pane.sharedMesh == null)
                throw new InvalidDataException("客室扉のカード表示面がありません：" + pane.name);
            _originalEnabled = _renderer.enabled;
        }

        internal void Show(HotelRoomCard card)
        {
            Clear();
            if (card == null) return;
            try
            {
                // Character data follows IEND; only decode the card's PNG image.
                var png = PngFile.LoadPngBytes(card.card);
                if (png == null || !png.Any()) throw new InvalidDataException("カードのPNG画像がありません。");
                _texture = new Texture2D(_p.I("math.one"), _p.I("math.one"), TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(_texture, png, false)) throw new InvalidDataException("カード画像を読み込めません。");
                _originalPixels = _texture.GetPixels();
                _blurredPixels = new Color[_originalPixels.Length];
                _scratchPixels = new Color[_originalPixels.Length];
                _displayPixels = new Color[_originalPixels.Length];
                RefreshAppearance();
                _texture.name = "Hotel door card " + card.room;
                _texture.wrapMode = TextureWrapMode.Clamp;

                var mesh = _pane.sharedMesh;
                var bounds = mesh.bounds;
                var normal = mesh.normals.First().normalized;
                var size = new Vector2(bounds.size.z, bounds.size.y);
                if (size.x <= default(float) || size.y <= default(float))
                    throw new InvalidDataException("客室扉のカード表示面の寸法が不正です。");
                _root = new GameObject("Hotel door card " + card.room, typeof(RectTransform), typeof(Canvas), typeof(Image));
                _root.layer = _pane.gameObject.layer;
                var rect = (RectTransform)_root.transform;
                rect.SetParent(_pane.transform, false);
                rect.localPosition = bounds.center;
                // UI faces local -Z. Use the pane normal so both corridor sides face outwards.
                rect.localRotation = Quaternion.LookRotation(-normal, Vector3.up);
                rect.sizeDelta = size;
                _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var backing = _root.GetComponent<Image>();
                backing.color = Color.black; backing.raycastTarget = false; backing.maskable = false;

                var imageRoot = new GameObject("Card image", typeof(RectTransform), typeof(RawImage));
                imageRoot.layer = _root.layer;
                var imageRect = (RectTransform)imageRoot.transform;
                imageRect.SetParent(rect, false);
                float scale = size.y / _texture.height;
                float scaledWidth = _texture.width * scale;
                float visibleWidth = Mathf.Min(size.x, scaledWidth);
                imageRect.sizeDelta = new Vector2(visibleWidth, size.y);
                var image = imageRoot.GetComponent<RawImage>();
                image.texture = _texture; image.raycastTarget = false; image.maskable = false;
                // Fill the full pane height; crop horizontal overflow symmetrically in UV space.
                float uvWidth = visibleWidth / scaledWidth;
                image.uvRect = new Rect((_p.N("math.one") - uvWidth) * _p.N("math.half"),
                    _p.N("math.zero"), uvWidth, _p.N("math.one"));
                // Replace only the pane renderer; no glass overlay or depth fighting with the card.
                _renderer.enabled = false;
            }
            catch { Clear(); throw; }
        }

        // Called only by registration/settings changes. Always start from the cached original.
        internal void RefreshAppearance()
        {
            if (_texture == null || _originalPixels == null) return;
            int radius = _cfg.doorCardBlurRadius;
            float pale = _cfg.doorCardPaleBlend;
            float brightness = _cfg.doorCardBrightness;
            if (_appliedRadius == radius && _appliedPale == pale && _appliedBrightness == brightness) return;
            int width = _texture.width, height = _texture.height;
            int zero = _p.I("math.zero"), one = _p.I("math.one");
            if (_appliedRadius != radius)
            {
                if (radius == zero) Array.Copy(_originalPixels, _blurredPixels, _originalPixels.Length);
                else for (int axis = zero; axis < _p.I("math.two"); axis++)
                {
                    bool horizontal = axis == zero;
                    var source = horizontal ? _originalPixels : _scratchPixels;
                    var target = horizontal ? _scratchPixels : _blurredPixels;
                    int extent = horizontal ? width : height;
                    int lines = horizontal ? height : width;
                    for (int line = zero; line < lines; line++)
                    {
                        Color sum = Color.clear;
                        int start = zero, end = Mathf.Min(extent - one, radius);
                        for (int sample = start; sample <= end; sample++)
                            sum += source[horizontal ? line * width + sample : sample * width + line];
                        for (int position = zero; position < extent; position++)
                        {
                            int nextStart = Mathf.Max(zero, position - radius);
                            int nextEnd = Mathf.Min(extent - one, position + radius);
                            if (nextStart > start)
                                sum -= source[horizontal ? line * width + start : start * width + line];
                            if (nextEnd > end)
                                sum += source[horizontal ? line * width + nextEnd : nextEnd * width + line];
                            start = nextStart; end = nextEnd;
                            target[horizontal ? line * width + position : position * width + line] = sum / (end - start + one);
                        }
                    }
                }
            }
            for (int index = zero; index < _blurredPixels.Length; index++)
            {
                var color = Color.Lerp(_blurredPixels[index], Color.white, pale);
                _displayPixels[index] = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            }
            _texture.SetPixels(_displayPixels);
            _texture.Apply(false, false);
            _appliedRadius = radius; _appliedPale = pale; _appliedBrightness = brightness;
        }

        private void Clear()
        {
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); _root = null; }
            if (_texture != null) { UnityEngine.Object.Destroy(_texture); _texture = null; }
            _originalPixels = _blurredPixels = _scratchPixels = _displayPixels = null;
            _appliedRadius = null; _appliedPale = null; _appliedBrightness = null;
            if (_renderer != null) _renderer.enabled = _originalEnabled;
        }
        public void Dispose() { Clear(); }
    }
}
