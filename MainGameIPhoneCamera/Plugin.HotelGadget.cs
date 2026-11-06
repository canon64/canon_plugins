using System;
using System.IO;
using System.Linq;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        public bool HotelGadgetAllowed => CanAct;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!CanAct) throw new InvalidOperationException("iPhoneは無効、またはこのHでは許可されていません。");
            if (visible && _root == null) SpawnPhone(); else if (!visible && _root != null) DestroyPhone();
            if (visible && _root == null) throw new InvalidOperationException("iPhoneのモデルを読み込めません。");
        }
        [Serializable] private sealed class HotelPhone
        {
            public PhonePage page;
            public CaptureMode mode;
            public float zoom = 1f;
            public string mediaPath, extensionId;
            public bool mediaVideo, wallpaperForLock;
            public float wallpaperX = .5f, wallpaperY = .5f, wallpaperZoom = 1f;
        }
        public string CaptureHotelGadget(Transform room)
        {
            var data = GadgetRoomSnapshot.Capture(room, _root == null ? null : _root.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config);
            var state = new HotelPhone { page = _phone.Page, mode = _app.Mode, zoom = _app.Zoom,
                extensionId = _extensionApp?.Id };
            if ((_phone.Page == PhonePage.Viewer || _phone.Page == PhonePage.Wallpaper) && _galleryItems.Length > 0)
            {
                var item = _galleryItems[Math.Max(0, Math.Min(_viewerIndex, _galleryItems.Length - 1))];
                state.mediaPath = item.Path; state.mediaVideo = item.Video;
            }
            if (_phone.Page == PhonePage.Wallpaper && _wallpaperDraft != null)
            {
                state.wallpaperForLock = _wallpaperDraft.ForLock;
                state.wallpaperX = _wallpaperDraft.X; state.wallpaperY = _wallpaperDraft.Y; state.wallpaperZoom = _wallpaperDraft.Zoom;
            }
            data.runtime = JsonUtility.ToJson(state);
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("iPhoneはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("iPhoneの配置データが不正です。");
            var state = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<HotelPhone>(data.runtime);
            IPhoneApp extension = null;
            byte[] image = null;
            // Resolve dependencies before changing the existing phone. A missing saved app or
            // media file is a real restore failure, not permission to replace its page with Home.
            if (data.visible && state != null)
            {
                if (!Enum.IsDefined(typeof(PhonePage), state.page) || !Enum.IsDefined(typeof(CaptureMode), state.mode)
                    || float.IsNaN(state.zoom) || float.IsInfinity(state.zoom) || state.zoom < PhoneZoomPlan.Minimum || state.zoom > PhoneZoomPlan.Maximum)
                    throw new ArgumentException("iPhoneの画面・倍率データが不正です。");
                if (state.page == PhonePage.Extension)
                {
                    extension = IPhoneAppApi.Snapshot().FirstOrDefault(app => app.Id == state.extensionId);
                    if (extension == null) throw new InvalidOperationException("保存したiPhoneアプリが見つかりません：" + state.extensionId);
                }
                if (state.page == PhonePage.Viewer || state.page == PhonePage.Wallpaper)
                {
                    if (string.IsNullOrWhiteSpace(state.mediaPath) || !File.Exists(state.mediaPath))
                        throw new InvalidOperationException("保存したiPhoneの表示ファイルが見つかりません：" + state.mediaPath);
                    if (!state.mediaVideo) image = File.ReadAllBytes(state.mediaPath);
                    if (state.page == PhonePage.Wallpaper && (state.mediaVideo ||
                        !ValidWallpaperValue(state.wallpaperX, 0f, 1f) || !ValidWallpaperValue(state.wallpaperY, 0f, 1f) ||
                        !ValidWallpaperValue(state.wallpaperZoom, 1f, 8f)))
                        throw new ArgumentException("iPhoneの待受編集データが不正です。");
                }
            }
            GadgetConfigSnapshot.Apply(Config, data.settings); SetHotelGadgetVisible(data.visible);
            if (!data.visible) return;
            ReleasePhone(); data.poses.Single().Apply(room, _root.transform);
            if (state == null) return;
            _app.Select(state.mode); SetCameraZoom(state.zoom);
            if (state.page == PhonePage.Viewer || state.page == PhonePage.Wallpaper)
            {
                StopGalleryVideo(); ClearViewerImage();
                _galleryItems = new[] { new GalleryItem { Path = state.mediaPath, Video = state.mediaVideo } };
                _viewerIndex = _gallerySelection = 0;
                if (image != null)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (!texture.LoadImage(image)) { Destroy(texture); throw new InvalidOperationException("保存したiPhoneの画像を読み込めません。"); }
                    _viewerTexture = texture;
                }
                if (state.page == PhonePage.Wallpaper)
                    _wallpaperDraft = new WallpaperDraft { ForLock = state.wallpaperForLock,
                        X = state.wallpaperX, Y = state.wallpaperY, Zoom = state.wallpaperZoom };
                ShowPhonePage(state.page);
                if (state.mediaVideo)
                {
                    if (string.Equals(Path.GetExtension(state.mediaPath), ".mkv", StringComparison.OrdinalIgnoreCase)) PrepareCompatibleGalleryVideo(state.mediaPath);
                    else StartGalleryVideo(state.mediaPath);
                }
            }
            else
            {
                ShowPhonePage(state.page);
                if (extension != null) { _extensionApp = extension; extension.Open(_shellUi.Viewport); }
            }
        }
        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible || string.IsNullOrEmpty(data.runtime)) return true;
            var state = JsonUtility.FromJson<HotelPhone>(data.runtime);
            if (state == null || _root == null || _phone.Page != state.page || _app.Mode != state.mode || Mathf.Abs(_app.Zoom - state.zoom) > .0001f) return false;
            if (state.page == PhonePage.Extension) return _extensionApp != null && _extensionApp.Id == state.extensionId;
            if (state.page != PhonePage.Viewer && state.page != PhonePage.Wallpaper) return true;
            if (_galleryItems.Length == 0 || _viewerIndex < 0 || _viewerIndex >= _galleryItems.Length) return false;
            var item = _galleryItems[_viewerIndex];
            if (item.Path != state.mediaPath || item.Video != state.mediaVideo) return false;
            if (!state.mediaVideo && _viewerTexture == null) return false;
            if (state.mediaVideo && (_galleryConverting || _galleryPlayer == null || !_galleryPlayer.isPrepared ||
                (!_galleryCompatibility && _galleryPlayer.url != state.mediaPath))) return false;
            return state.page != PhonePage.Wallpaper || (_wallpaperDraft != null && _wallpaperDraft.ForLock == state.wallpaperForLock &&
                Mathf.Abs(_wallpaperDraft.X - state.wallpaperX) < .0001f && Mathf.Abs(_wallpaperDraft.Y - state.wallpaperY) < .0001f &&
                Mathf.Abs(_wallpaperDraft.Zoom - state.wallpaperZoom) < .0001f);
        }
        private static bool ValidWallpaperValue(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
