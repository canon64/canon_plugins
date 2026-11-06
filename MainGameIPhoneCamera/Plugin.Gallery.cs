using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private GalleryItem[] _galleryItems = new GalleryItem[0];
        private int _gallerySelection, _viewerIndex;
        private Texture2D _viewerTexture;
        private readonly List<Texture2D> _galleryThumbnails = new List<Texture2D>();
        private VideoPlayer _galleryPlayer;
        private GameObject _galleryPlayerObject;
        private bool _galleryPlaying;
        private bool _galleryCompatibility;
        private bool _galleryConverting;
        private PhoneDeadline _videoPrepareDeadline;
        private void RenderGalleryLoading()
        {
            ClearGalleryThumbnails();
            _shellUi.StartPage("写真と動画", "スティック：選択／A：開く／B：メニュー");
            _shellUi.Gallery(_galleryItems, _gallerySelection);
            _shellUi.Message("写真・動画を読み込み中…");
        }

        private void ReloadGallery()
        {
            if (!CanAct || _shellUi == null || _phone.Page != PhonePage.Gallery) return;
            string selected = _galleryItems.Length > 0 ? _galleryItems[Math.Min(_gallerySelection, _galleryItems.Length - 1)].Path : null;
            BeginPhonePage(); var post = CapturePhonePoster(); var token = _pageLifetime.Token;
            string folder = PhotoDirectory, extra = _galleryFolders.Value;
            _shellUi.Message("写真・動画を読み込み中…");
            Task.Run(() => GalleryLibrary.Scan(folder, extra, token), token).ContinueWith(task =>
            {
                string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                if (task.IsCanceled) return;
                post(() =>
                {
                    if (error != null) { _shellUi.Message("一覧を読み込めません：" + error); return; }
                    _galleryItems = task.Result.Items;
                    int index = Array.FindIndex(_galleryItems, x => string.Equals(x.Path, selected, StringComparison.OrdinalIgnoreCase));
                    _gallerySelection = index < 0 ? Math.Min(_gallerySelection, Math.Max(0, _galleryItems.Length - 1)) : index;
                    RenderGallery();
                    if (!string.IsNullOrEmpty(task.Result.Error)) _shellUi.Message(task.Result.Error);
                });
            }, TaskScheduler.Default);
        }
        private void RenderGallery()
        {
            if (_shellUi == null) return;
            ClearGalleryThumbnails();
            _shellUi.StartPage("写真と動画", "スティック：選択／A：開く／B：メニュー");
            _shellUi.Gallery(_galleryItems, _gallerySelection);
            if (_galleryItems.Length == 0) return;
            int first = _gallerySelection / 9 * 9;
            var post = CapturePhonePoster(); var token = _pageLifetime.Token;
            string ffmpeg = _ffmpegPath.Value, cache = Path.Combine(PhoneDataDirectory, "Thumbnails");
            for (int i = first; i < Math.Min(first + 9, _galleryItems.Length); i++)
            {
                int slot = i; var item = _galleryItems[i];
                Task.Run(() => GalleryLibrary.Thumbnail(ffmpeg, cache, item, token), token).ContinueWith(task =>
                {
                    string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                    if (task.IsCanceled) return;
                    post(() =>
                    {
                        if (error != null) { if (slot == _gallerySelection) _shellUi.Message("サムネイルなし：" + item.Name); return; }
                        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (!texture.LoadImage(task.Result)) { Destroy(texture); return; }
                        _galleryThumbnails.Add(texture); _shellUi.Thumbnail(slot, texture);
                    });
                }, TaskScheduler.Default);
            }
        }
        private void NavigateGallery(int x, int y)
        {
            int before = _gallerySelection;
            _gallerySelection = MoveGrid(_gallerySelection, _galleryItems.Length, x, y, _shellUi.GridColumns);
            if (before / 9 != _gallerySelection / 9) { BeginPhonePage(false); RenderGallery(); }
            else { _shellUi.Select(_gallerySelection % 9); _shellUi.Message(_galleryItems.Length == 0 ? "A：再読込" : (_gallerySelection + 1) + " / " + _galleryItems.Length + "   A：開く"); }
        }
        private void ClearGalleryThumbnails()
        {
            foreach (var texture in _galleryThumbnails) if (texture != null) Destroy(texture);
            _galleryThumbnails.Clear();
        }
        private string[] ViewerControls()
        {
            bool video = _galleryItems.Length > 0 && _galleryItems[Math.Min(_viewerIndex, _galleryItems.Length - 1)].Video;
            return video ? new[] { "一覧へ", "前へ", "次へ", _galleryPlaying ? "一時停止" : "再生", "−10秒", "+10秒" }
                : new[] { "一覧へ", "前へ", "次へ", "メニュー待受", "ロック待受" };
        }
        private void RenderViewer()
        {
            if (_galleryItems.Length == 0) return;
            _shellUi.StartPage(_galleryItems[_viewerIndex].Video ? "動画" : "写真", "上下：ズーム　左右：選択\nトリガー＋スティック：移動");
            _shellUi.Viewer(ViewerControls(), _controlSelection);
            _shellUi.Message(_galleryItems[_viewerIndex].Name);
            if (_galleryPlayer != null && _galleryPlayer.isPrepared) _shellUi.Preview(_galleryPlayer.texture);
            else if (_viewerTexture != null) _shellUi.Preview(_viewerTexture);
            ShowViewerUi();
        }
        private void OpenGalleryItem(int index)
        {
            if (_galleryItems.Length == 0) return;
            StopGalleryVideo(); ClearViewerImage();
            _viewerIndex = _gallerySelection = Math.Max(0, Math.Min(_galleryItems.Length - 1, index));
            ShowPhonePage(PhonePage.Viewer);
            var item = _galleryItems[_viewerIndex];
            if (item.Video)
            {
                _galleryCompatibility = false;
                if (string.Equals(Path.GetExtension(item.Path), ".mkv", StringComparison.OrdinalIgnoreCase)) PrepareCompatibleGalleryVideo(item.Path);
                else StartGalleryVideo(item.Path);
                return;
            }
            var post = CapturePhonePoster(); var token = _pageLifetime.Token;
            _shellUi.Message("画像を読み込み中…");
            Task.Run(() => { token.ThrowIfCancellationRequested(); return File.ReadAllBytes(item.Path); }, token).ContinueWith(task =>
            {
                string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                if (task.IsCanceled) return;
                post(() =>
                {
                    if (error != null) { _shellUi.Message("画像を開けません：" + error); return; }
                    var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (!texture.LoadImage(task.Result)) { Destroy(texture); _shellUi.Message("画像の形式に対応していません"); return; }
                    _viewerTexture = texture; _shellUi.Preview(texture); _shellUi.Message(item.Name);
                });
            }, TaskScheduler.Default);
        }
        private void ConfirmViewerControl()
        {
            if (_galleryItems.Length == 0) return;
            switch (_controlSelection)
            {
                case 0: ShowPhonePage(PhonePage.Gallery); break;
                case 1: OpenGalleryItem(Math.Max(0, _viewerIndex - 1)); break;
                case 2: OpenGalleryItem(Math.Min(_galleryItems.Length - 1, _viewerIndex + 1)); break;
                case 3:
                    if (_galleryItems[_viewerIndex].Video) ToggleGalleryVideo(); else OpenWallpaperEditor(false);
                    break;
                case 4:
                    if (_galleryItems[_viewerIndex].Video) SeekGallery(-10); else OpenWallpaperEditor(true);
                    break;
                case 5: SeekGallery(10); break;
            }
        }
        private void StartGalleryVideo(string path)
        {
            _galleryConverting = false;
            try
            {
                _galleryPlayerObject = new GameObject("IPhoneVideoPlayer"); _galleryPlayerObject.transform.SetParent(_root.transform, false);
                var source = _galleryPlayerObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = .1f; source.maxDistance = 5; source.volume = _playbackVolume.Value;
                _galleryPlayer = _galleryPlayerObject.AddComponent<VideoPlayer>();
                _galleryPlayer.playOnAwake = false; _galleryPlayer.isLooping = false;
                _galleryPlayer.source = VideoSource.Url; _galleryPlayer.url = path;
                _galleryPlayer.renderMode = VideoRenderMode.APIOnly; _galleryPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                _galleryPlayer.controlledAudioTrackCount = 1; _galleryPlayer.SetTargetAudioSource(0, source);
                _galleryPlayer.prepareCompleted += GalleryVideoPrepared;
                _galleryPlayer.started += GalleryVideoStarted;
                _galleryPlayer.loopPointReached += GalleryVideoEnded;
                _galleryPlayer.errorReceived += GalleryVideoError;
                _galleryPlaying = true; UpdatePhoneMediaActivity();
                var current = _galleryPlayer;
                _videoPrepareDeadline = new PhoneDeadline(_mainThread);
                _videoPrepareDeadline.Arm(TimeSpan.FromSeconds(15), () =>
                {
                    if (!CanAct || current != _galleryPlayer || current.isPrepared) return;
                    StopGalleryVideo(); _shellUi.Message("動画の準備がタイムアウトしました");
                });
                _shellUi.Message("動画を準備中…"); _galleryPlayer.Prepare();
            }
            catch (Exception ex) { StopGalleryVideo(); _shellUi.Message("動画を開けません：" + ex.Message); }
        }
        private void GalleryVideoPrepared(VideoPlayer player)
        {
            if (player != _galleryPlayer || !CanAct || _phone.Page != PhonePage.Viewer) return;
            _videoPrepareDeadline?.Cancel();
            _shellUi.Preview(player.texture); player.Play();
        }
        private void GalleryVideoStarted(VideoPlayer player)
        {
            if (player != _galleryPlayer || !CanAct) return;
            _galleryPlaying = true; UpdatePhoneMediaActivity(); _shellUi.ViewerControls(ViewerControls(), _controlSelection);
        }
        private void GalleryVideoEnded(VideoPlayer player)
        {
            if (player != _galleryPlayer) return;
            _galleryPlaying = false; UpdatePhoneMediaActivity(); if (CanAct && _phone.Page == PhonePage.Viewer) _shellUi.ViewerControls(ViewerControls(), _controlSelection);
        }
        private void GalleryVideoError(VideoPlayer player, string message)
        {
            if (player != _galleryPlayer) return;
            StopGalleryVideo();
            if (!CanAct || _shellUi == null || _phone.Page != PhonePage.Viewer) return;
            if (!_galleryCompatibility) PrepareCompatibleGalleryVideo(_galleryItems[_viewerIndex].Path);
            else _shellUi.Message("動画を再生できません：" + message);
        }
        private void PrepareCompatibleGalleryVideo(string path)
        {
            _galleryCompatibility = true; _galleryConverting = true; _galleryPlaying = true; UpdatePhoneMediaActivity();
            _shellUi.Message("動画を端末用に変換中…（Bで一覧へ戻れます）");
            var post = CapturePhonePoster(); var token = _pageLifetime.Token;
            string ffmpeg = _ffmpegPath.Value, cache = Path.Combine(PhoneDataDirectory, "PlaybackCache");
            Task.Run(() => GalleryLibrary.CompatibleVideo(ffmpeg, cache, path, token), token).ContinueWith(task =>
            {
                string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                if (task.IsCanceled) return;
                post(() =>
                {
                    _galleryConverting = false;
                    if (error != null) { _galleryPlaying = false; UpdatePhoneMediaActivity(); _shellUi.Message("動画を変換できません：" + error); return; }
                    StartGalleryVideo(task.Result);
                });
            }, TaskScheduler.Default);
        }
        private void ToggleGalleryVideo()
        {
            if (_galleryConverting) return;
            if (_galleryPlayer == null)
            {
                if (_galleryItems.Length > 0)
                {
                    if (_galleryCompatibility) PrepareCompatibleGalleryVideo(_galleryItems[_viewerIndex].Path);
                    else StartGalleryVideo(_galleryItems[_viewerIndex].Path);
                }
                return;
            }
            if (!_galleryPlayer.isPrepared) return;
            if (_galleryPlaying) { _galleryPlayer.Pause(); _galleryPlaying = false; }
            else { _galleryPlayer.Play(); _galleryPlaying = true; }
            UpdatePhoneMediaActivity(); _shellUi.ViewerControls(ViewerControls(), _controlSelection);
        }
        private void SeekGallery(double seconds)
        {
            if (_galleryPlayer == null || !_galleryPlayer.isPrepared || !_galleryPlayer.canSetTime) return;
            _galleryPlayer.time = Math.Max(0, Math.Min(_galleryPlayer.length, _galleryPlayer.time + seconds));
        }
        private void StopGalleryVideo()
        {
            _videoPrepareDeadline?.Dispose(); _videoPrepareDeadline = null;
            _galleryConverting = false;
            var player = _galleryPlayer; _galleryPlayer = null;
            if (player != null)
            {
                player.prepareCompleted -= GalleryVideoPrepared; player.started -= GalleryVideoStarted;
                player.loopPointReached -= GalleryVideoEnded; player.errorReceived -= GalleryVideoError; player.Stop();
            }
            if (_galleryPlayerObject != null) Destroy(_galleryPlayerObject); _galleryPlayerObject = null;
            _galleryPlaying = false; UpdatePhoneMediaActivity();
        }
        private void ClearViewerImage() { if (_viewerTexture != null) Destroy(_viewerTexture); _viewerTexture = null; }
        private void PhoneCaptureCompleted()
        {
            if (_phone.Page == PhonePage.Gallery) ReloadGallery();
        }
    }
}
