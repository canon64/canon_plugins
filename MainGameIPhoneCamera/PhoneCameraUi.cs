using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameIPhoneCamera
{
    // Native legacy clips animate the offscreen UI. No Update/timer/coroutine.
    internal sealed class PhoneCameraUi : IDisposable
    {
        internal Canvas Canvas { get; private set; }
        private GameObject _root;
        private Transform _orientationPivot;
        private RectTransform _contentRoot;
        private PhoneOrientationMotion _orientationMotion;
        private int _turns;
        private Font _font;
        private readonly Font _clockFont;
        private Texture2D _circleTexture, _thumbnail;
        private Sprite _circle;
        private Text _photo, _video, _timer, _zoom, _message, _systemClock;
        private Image _shutter, _stop;
        private RawImage _thumb, _cameraPreview;
        private CanvasGroup _recording, _notice, _flash, _surface;
        private RectTransform _marker;
        private Animation _animation;
        private Animation _pulse;
        private AnimationClip _transition, _flashClip, _pulseClip;
        private string _transitionName;
        private int _clipSerial, _lastSecond = -1;
        internal void CameraPreview(Texture texture) => _cameraPreview.texture = texture;
        internal void Orientation(int turns)
        {
            _turns = turns & 3;
            _orientationMotion.Transition(_turns, LayoutOrientation);
        }
        private void LayoutOrientation()
        {
            bool wide = (_turns & 1) != 0;
            float width = wide ? 1166 : 560, height = wide ? 560 : 1166;
            _contentRoot.sizeDelta = new Vector2(width, height);
            Place("Bottom", wide ? new Vector2(450, 0) : new Vector2(0, -440), wide ? new Vector2(260, 552) : new Vector2(552, 280));
            Place("ClockShade", new Vector2(-width / 2 + 101, height / 2 - 57));
            Place("SystemClock", new Vector2(-width / 2 + 101, height / 2 - 57));
            Place("Photo", wide ? new Vector2(378, 110) : new Vector2(-72, -337));
            Place("Video", wide ? new Vector2(522, 110) : new Vector2(72, -337));
            Place("SelectionRail", wide ? new Vector2(450, 75) : new Vector2(0, -374));
            foreach (string name in new[] { "ShutterRing", "RingInside", "Shutter", "Stop" })
                Place(name, wide ? new Vector2(450, 0) : new Vector2(0, -455));
            Place("Thumbnail", wide ? new Vector2(450, -145) : new Vector2(-183, -455));
            Place("Zoom", wide ? new Vector2(0, -220) : new Vector2(0, -260));
            Place("Recording", new Vector2(0, wide ? -225 : 0));
            Place("Notice", new Vector2(0, wide ? -335 : 0));
            var flash = _flash.transform.Find("White") as RectTransform;
            if (flash != null) flash.sizeDelta = new Vector2(width, height);
        }
        private void Place(string name, Vector2 position, Vector2? size = null)
        {
            var rect = _contentRoot.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = position;
            if (size.HasValue) rect.sizeDelta = size.Value;
        }
        internal void Active(bool value)
        {
            if (value && !_root.activeSelf) _surface.alpha = 0;
            _root.SetActive(value);
        }

        internal PhoneCameraUi(Transform screenParent, Mesh screenMesh, CameraAppState app, Texture preview)
        {
            _root = new GameObject("CameraApp", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(screenParent, false);
            rect.sizeDelta = new Vector2(560, 1166);
            Canvas = _root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.sortingOrder = 20;
            _orientationPivot = new GameObject("CameraOrientation").transform;
            _orientationPivot.SetParent(rect, false);
            _orientationMotion = _orientationPivot.gameObject.AddComponent<PhoneOrientationMotion>();
            _contentRoot = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _contentRoot.SetParent(_orientationPivot, false);
            _contentRoot.sizeDelta = new Vector2(560, 1166);
            _animation = _contentRoot.gameObject.AddComponent<Animation>();
            _surface = _contentRoot.gameObject.AddComponent<CanvasGroup>();
            _animation.cullingType = AnimationCullingType.AlwaysAnimate;
            _root.AddComponent<PhoneScreenMask>().Initialize(screenMesh);
            _root.AddComponent<Mask>().showMaskGraphic = false;
            _cameraPreview = Node("CameraPreview", Vector2.zero, new Vector2(560, 1166)).gameObject.AddComponent<RawImage>();
            _cameraPreview.transform.SetParent(rect, false);
            _cameraPreview.transform.SetSiblingIndex(0);
            _cameraPreview.raycastTarget = false;
            CameraPreview(preview);
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 32);
            _clockFont = PhoneClockStyle.CreateFont();
            Panel("ClockShade", new Vector2(-179, 526), new Vector2(174, 64), new Color(0, 0, 0, .65f));
            _systemClock = Label("SystemClock", "", new Vector2(-179, 526), new Vector2(168, 62), 44);
            PhoneClockStyle.Apply(_systemClock, _clockFont);
            SystemClock(DateTime.Now);
            _circleTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var colors = new Color32[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(64, 64));
                colors[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(63 - distance));
            }
            _circleTexture.SetPixels32(colors); _circleTexture.Apply();
            _circle = Sprite.Create(_circleTexture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
            Panel("Bottom", new Vector2(0, -440), new Vector2(552, 280), new Color(0, 0, 0, .82f));
            _photo = Label("Photo", "写真", new Vector2(-72, -337), new Vector2(120, 50), 27);
            _video = Label("Video", "ビデオ", new Vector2(72, -337), new Vector2(130, 50), 27);
            var selectionRail = Node("SelectionRail", new Vector2(0, -374), new Vector2(560, 8));
            _marker = Panel("Selection", new Vector2(-72, 0), new Vector2(8, 8), Color.yellow, selectionRail).rectTransform;
            _marker.GetComponent<Image>().sprite = _circle;
            var ring = Panel("ShutterRing", new Vector2(0, -455), new Vector2(108, 108), Color.white);
            ring.sprite = _circle;
            Panel("RingInside", new Vector2(0, -455), new Vector2(98, 98), Color.black).sprite = _circle;
            _shutter = Panel("Shutter", new Vector2(0, -455), new Vector2(86, 86), Color.white);
            _shutter.sprite = _circle;
            _stop = Panel("Stop", new Vector2(0, -455), new Vector2(86, 86), Color.red);
            _stop.transform.localScale = Vector3.zero;
            _thumb = Node("Thumbnail", new Vector2(-183, -455), new Vector2(70, 100)).gameObject.AddComponent<RawImage>();
            _thumb.raycastTarget = false; _thumb.enabled = false;
            _zoom = Label("Zoom", "1.00×", new Vector2(0, -260), new Vector2(170, 48), 28);
            _recording = Group("Recording");
            Panel("Pill", new Vector2(0, 450), new Vector2(226, 58), new Color(.6f, 0, 0, .9f), _recording.transform);
            var dot = Panel("Dot", new Vector2(-83, 450), new Vector2(17, 17), Color.red, _recording.transform);
            dot.sprite = _circle;
            _pulse = dot.gameObject.AddComponent<Animation>(); _pulse.cullingType = AnimationCullingType.AlwaysAnimate;
            _pulseClip = new AnimationClip { legacy = true, wrapMode = WrapMode.Loop };
            _pulseClip.SetCurve("", typeof(Transform), "localScale.x", new AnimationCurve(new Keyframe(0, .8f), new Keyframe(.5f, 1.15f), new Keyframe(1, .8f)));
            _pulseClip.SetCurve("", typeof(Transform), "localScale.y", new AnimationCurve(new Keyframe(0, .8f), new Keyframe(.5f, 1.15f), new Keyframe(1, .8f)));
            _pulse.AddClip(_pulseClip, "Pulse"); _pulse.enabled = false;
            _timer = Label("Time", "00:00", new Vector2(13, 450), new Vector2(170, 54), 30, _recording.transform);
            _recording.alpha = 0;
            _notice = Group("Notice");
            Panel("NoticeBack", new Vector2(0, 335), new Vector2(380, 65), new Color(0, 0, 0, .8f), _notice.transform);
            _message = Label("Message", "", new Vector2(0, 335), new Vector2(374, 60), 27, _notice.transform);
            _notice.alpha = 0;
            _flash = Group("Flash");
            Panel("White", Vector2.zero, new Vector2(552, 1150), Color.white, _flash.transform);
            _flash.alpha = 0;
            Apply(app, false);
        }

        private RectTransform Node(string name, Vector2 position, Vector2 size, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform; rect.SetParent(parent ?? _contentRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private Image Panel(string name, Vector2 position, Vector2 size, Color color, Transform parent = null)
        {
            var image = Node(name, position, size, parent).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(string name, string value, Vector2 position, Vector2 size, int fontSize, Transform parent = null)
        {
            var text = Node(name, position, size, parent).gameObject.AddComponent<Text>();
            text.font = _font; text.fontSize = fontSize; text.text = value;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; return text;
        }
        private CanvasGroup Group(string name) => Node(name, Vector2.zero, new Vector2(560, 1166)).gameObject.AddComponent<CanvasGroup>();
        private static AnimationCurve Curve(float from, float to, float seconds) => AnimationCurve.EaseInOut(0, from, seconds, to);
        private void Play(AnimationClip clip)
        {
            // A transition supersedes the previous transition from its actual current values.
            _animation.Stop();
            if (_transition != null) { _animation.RemoveClip(_transitionName); UnityEngine.Object.Destroy(_transition); }
            clip.legacy = true; clip.wrapMode = WrapMode.ClampForever;
            string name = "Transition" + (++_clipSerial);
            _transition = clip; _transitionName = name;
            _animation.AddClip(clip, name); _animation.Play(name);
        }
        private static void Alpha(AnimationClip clip, string path, CanvasGroup group, float target, float duration = .22f)
            => clip.SetCurve(path, typeof(CanvasGroup), "m_Alpha", Curve(group.alpha, target, duration));
        private static void Scale(AnimationClip clip, string path, Transform transform, float target, float duration = .22f)
        {
            clip.SetCurve(path, typeof(Transform), "localScale.x", Curve(transform.localScale.x, target, duration));
            clip.SetCurve(path, typeof(Transform), "localScale.y", Curve(transform.localScale.y, target, duration));
        }
        internal void Apply(CameraAppState app, bool animate = true)
        {
            animate = animate && _root.activeInHierarchy;
            bool video = app.Mode == CaptureMode.Video, recording = app.Phase == CapturePhase.Recording;
            bool saving = app.Phase == CapturePhase.Saving;
            if (recording && !_pulse.enabled) { _pulse.enabled = true; _pulse.Play("Pulse"); }
            else if (!recording) { _pulse.Stop(); _pulse.enabled = false; }
            _photo.color = video ? Color.white : Color.yellow;
            _video.color = video ? Color.yellow : Color.white;
            _zoom.text = app.Zoom.ToString("0.00") + "×";
            _shutter.color = video ? Color.red : Color.white;
            _message.text = saving ? "保存中…" : app.Phase == CapturePhase.Error ? "保存に失敗しました" : "保存しました";
            float notice = saving || app.Phase == CapturePhase.Saved || app.Phase == CapturePhase.Error ? 1 : 0;
            // SetCurve rejects runtime clips until legacy is set; setting it in Play is too late.
            var clip = new AnimationClip { legacy = true };
            Alpha(clip, "", _surface, 1, .2f);
            // Keep the vertical placement on an unanimated parent. A partial
            // Transform position binding can otherwise reset unkeyed axes.
            clip.SetCurve("SelectionRail/Selection", typeof(Transform), "localPosition.x", Curve(_marker.localPosition.x, video ? 72 : -72, .24f));
            Scale(clip, "Shutter", _shutter.transform, recording || saving ? 0 : 1);
            Scale(clip, "Stop", _stop.transform, recording ? .52f : 0);
            Alpha(clip, "Recording", _recording, recording ? 1 : 0);
            Alpha(clip, "Notice", _notice, notice);
            if (app.Phase == CapturePhase.Saved)
            {
                clip.SetCurve("Notice", typeof(CanvasGroup), "m_Alpha", new AnimationCurve(new Keyframe(0, _notice.alpha), new Keyframe(.2f, 1), new Keyframe(1.6f, 1), new Keyframe(2, 0)));
                clip.SetCurve("Thumbnail", typeof(Transform), "localScale.x", new AnimationCurve(new Keyframe(0, .65f), new Keyframe(.18f, 1.1f), new Keyframe(.35f, 1)));
                clip.SetCurve("Thumbnail", typeof(Transform), "localScale.y", new AnimationCurve(new Keyframe(0, .65f), new Keyframe(.18f, 1.1f), new Keyframe(.35f, 1)));
            }
            if (!animate)
            {
                _marker.localPosition = new Vector3(video ? 72 : -72, 0, 0);
                _shutter.transform.localScale = Vector3.one * (recording || saving ? 0 : 1);
                _stop.transform.localScale = Vector3.one * (recording ? .52f : 0);
                _recording.alpha = recording ? 1 : 0; _notice.alpha = notice;
                _surface.alpha = 1;
                UnityEngine.Object.Destroy(clip);
            }
            else Play(clip);
        }
        internal void PhotoFlash()
        {
            // A separate animation on the flash layer can overlap the saved-thumbnail transition.
            var animation = _flash.GetComponent<Animation>() ?? _flash.gameObject.AddComponent<Animation>();
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", new AnimationCurve(new Keyframe(0, .8f), new Keyframe(.18f, 0)));
            animation.Stop(); animation.RemoveClip("Flash");
            if (_flashClip != null) UnityEngine.Object.Destroy(_flashClip);
            _flashClip = clip; animation.AddClip(clip, "Flash"); animation.Play("Flash");
        }
        internal void Clock(double seconds)
        {
            int whole = Math.Max(0, (int)seconds);
            if (_lastSecond == whole) return;
            _lastSecond = whole; _timer.text = (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
        internal void SystemClock(DateTime time) { _systemClock.text = time.ToString("HH:mm"); }
        internal void Zoom(float value) { _zoom.text = value.ToString("0.00") + "×"; }
        internal void Thumbnail(Texture2D texture)
        {
            if (_thumbnail != null) UnityEngine.Object.Destroy(_thumbnail);
            _thumbnail = texture; _thumb.texture = texture; _thumb.enabled = texture != null;
        }
        public void Dispose()
        {
            _orientationMotion?.Cancel();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_thumbnail != null) UnityEngine.Object.Destroy(_thumbnail);
            if (_circle != null) UnityEngine.Object.Destroy(_circle);
            if (_circleTexture != null) UnityEngine.Object.Destroy(_circleTexture);
            if (_font != null) UnityEngine.Object.Destroy(_font);
            if (_clockFont != null) UnityEngine.Object.Destroy(_clockFont);
            if (_transition != null) UnityEngine.Object.Destroy(_transition);
            if (_flashClip != null) UnityEngine.Object.Destroy(_flashClip);
            if (_pulseClip != null) UnityEngine.Object.Destroy(_pulseClip);
            _root = null; Canvas = null;
        }
    }
}
