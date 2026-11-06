using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private WallpaperDraft _wallpaperDraft;
        private bool _wallpaperSaving;
        private int _homeWallpaperLoad, _lockWallpaperLoad;
        private void LoadWallpaper(bool forLock)
        {
            if (_shellUi == null || !CanAct) return;
            string path = forLock ? _lockWallpaper.Value : _homeWallpaper.Value;
            int version = forLock ? ++_lockWallpaperLoad : ++_homeWallpaperLoad;
            var post = CapturePhonePoster(false); var token = _captureLifetime.Token;
            Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return string.IsNullOrWhiteSpace(path) ? null : File.ReadAllBytes(path);
            }, token).ContinueWith(task =>
            {
                string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                if (task.IsCanceled) return;
                post(() =>
                {
                    if (version != (forLock ? _lockWallpaperLoad : _homeWallpaperLoad)) return;
                    if (error != null) { _shellUi.Message("待受を読めません：" + error); return; }
                    Texture2D texture = null;
                    if (task.Result != null)
                    {
                        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (!texture.LoadImage(task.Result)) { Destroy(texture); _shellUi.Message("待受画像の形式に対応していません"); return; }
                    }
                    var old = forLock ? _lockWallpaperTexture : _homeWallpaperTexture;
                    if (forLock) _lockWallpaperTexture = texture; else _homeWallpaperTexture = texture;
                    if (old != null) Destroy(old);
                    if (!forLock && _phone.Page == PhonePage.Home) RenderHome();
                    if (forLock && _phone.Page == PhonePage.Locked)
                    {
                        _shellUi.StartPage("", "A：メニュー／トリガー：カメラ", _lockWallpaperTexture); _shellUi.Lock();
                    }
                });
            }, TaskScheduler.Default);
        }
        private void OpenWallpaperEditor(bool forLock)
        {
            if (_viewerTexture == null) { _shellUi.Message("画像の読み込みを待ってください"); return; }
            _wallpaperDraft = new WallpaperDraft { ForLock = forLock };
            SyncWallpaperNumbers();
            ShowPhonePage(PhonePage.Wallpaper);
        }
        private void RenderWallpaperEditor()
        {
            if (_viewerTexture == null || _wallpaperDraft == null) return;
            _shellUi.StartPage(_wallpaperDraft.ForLock ? "ロック画面の待受" : "メニュー画面の待受", "A：選択・確定／B：編集を破棄してメニュー");
            _shellUi.Editor(_viewerTexture, _wallpaperDraft, _controlSelection);
        }
        private void ConfirmWallpaperControl()
        {
            if (_wallpaperSaving) return;
            if (_wallpaperDraft.Edit != 0) { _wallpaperDraft.Edit = 0; _shellUi.EditorValues(_viewerTexture, _wallpaperDraft); return; }
            switch (_controlSelection)
            {
                case 0: _wallpaperDraft.Edit = 1; break;
                case 1: _wallpaperDraft.Edit = 2; break;
                case 2: SaveWallpaper(); return;
                case 3: _wallpaperDraft.Reset(); break;
            }
            SyncWallpaperNumbers();
            _shellUi.EditorValues(_viewerTexture, _wallpaperDraft);
        }
        private void SaveWallpaper()
        {
            if (_wallpaperSaving || _viewerTexture == null || _wallpaperDraft == null || !CanAct) return;
            Texture2D pixels = null;
            RenderTexture render = null, previous = RenderTexture.active;
            try
            {
                var crop = _wallpaperDraft.Crop(_viewerTexture.width, _viewerTexture.height);
                render = RenderTexture.GetTemporary(1125, 2436, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_viewerTexture, render, new Vector2(crop.Width, crop.Height), new Vector2(crop.X, crop.Y));
                RenderTexture.active = render;
                pixels = new Texture2D(1125, 2436, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1125, 2436), 0, 0); pixels.Apply();
                byte[] png = pixels.EncodeToPNG();
                bool forLock = _wallpaperDraft.ForLock;
                string folder = Path.Combine(PhoneDataDirectory, "Wallpapers");
                string path = Path.Combine(folder, (forLock ? "Lock_" : "Menu_") + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 6) + ".png");
                var post = CapturePhonePoster(false); var token = _captureLifetime.Token;
                _wallpaperSaving = true; _shellUi.Message("待受画像を保存中…");
                Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested(); Directory.CreateDirectory(folder);
                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.Write(png, 0, png.Length);
                }, token).ContinueWith(task =>
                {
                    string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                    if (task.IsCanceled) return;
                    post(() =>
                    {
                        _wallpaperSaving = false;
                        if (error != null) { _shellUi.Message("待受の保存失敗：" + error); return; }
                        if (forLock) _lockWallpaper.Value = path; else _homeWallpaper.Value = path;
                        if (_phone.Page == PhonePage.Wallpaper) _shellUi.Message("待受を保存しました。Bでメニューへ戻れます。");
                    });
                }, TaskScheduler.Default);
            }
            catch (Exception ex) { _wallpaperSaving = false; _shellUi.Message("待受の保存失敗：" + ex.Message); }
            finally
            {
                RenderTexture.active = previous;
                if (render != null) RenderTexture.ReleaseTemporary(render);
                if (pixels != null) Destroy(pixels);
            }
        }
    }
}
