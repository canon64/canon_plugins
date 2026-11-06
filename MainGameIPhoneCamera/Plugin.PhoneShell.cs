using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private PhoneState _phone = new PhoneState();
        private PhonePage _shownPage = PhonePage.Locked;
        private PhoneShellUi _shellUi;
        private PhoneDeadline _idleDeadline, _clockDeadline, _navigationRepeat;
        private PhoneOrientationClock _orientationClock;
        private CancellationTokenSource _pageLifetime;
        private int _screenEpoch, _homeSelection, _controlSelection, _axisX, _axisY;
        private bool _phoneAxisLatched;
        private List<PhoneIcon> _homeApps;
        private IPhoneApp _extensionApp;
        private readonly Dictionary<string, Sprite> _builtinIcons = new Dictionary<string, Sprite>();
        private Texture2D _homeWallpaperTexture, _lockWallpaperTexture;

        private void CreatePhoneShell(Transform screenParent)
        {
            _phone = new PhoneState();
            _shellUi = new PhoneShellUi(screenParent, _screenMesh);
            _idleDeadline = new PhoneDeadline(_mainThread);
            _clockDeadline = new PhoneDeadline(_mainThread);
            _navigationRepeat = new PhoneDeadline(_mainThread);
            _orientationClock = new PhoneOrientationClock(_mainThread, () =>
            {
                bool keep = false;
                RunPhoneInput(() => keep = TickPhoneOrientation());
                return keep;
            });
            _builtinIcons.Clear();
            _builtinIcons["photos"] = _shellUi.BuiltinIcon(0);
            _builtinIcons["camera"] = _shellUi.BuiltinIcon(1);
            _builtinIcons["settings"] = _shellUi.BuiltinIcon(2);
            IPhoneAppApi.Changed += PhoneAppsChanged;
            BuildHomeApps();
            ShowPhonePage(PhonePage.Locked);
            LoadWallpaper(false); LoadWallpaper(true);
        }
        private void BuildHomeApps()
        {
            string selected = _homeApps != null && _homeSelection < _homeApps.Count ? _homeApps[_homeSelection].Id : null;
            _homeApps = new List<PhoneIcon> {
                new PhoneIcon { Id = "photos", Title = "写真", Sprite = _builtinIcons["photos"] },
                new PhoneIcon { Id = "camera", Title = "カメラ", Sprite = _builtinIcons["camera"] },
                new PhoneIcon { Id = "settings", Title = "設定", Sprite = _builtinIcons["settings"] }
            };
            _homeApps.AddRange(IPhoneAppApi.Snapshot().Select(x => new PhoneIcon { Id = x.Id, Title = x.Title, Sprite = x.Icon ?? _builtinIcons["settings"] }));
            int index = _homeApps.FindIndex(x => x.Id == selected);
            _homeSelection = index >= 0 ? index : Math.Min(_homeSelection, _homeApps.Count - 1);
        }
        private void PhoneAppsChanged()
        {
            _mainThread?.Post(_ =>
            {
                if (!CanAct || _shellUi == null) return;
                if (_extensionApp != null && !IPhoneAppApi.Snapshot().Contains(_extensionApp)) ShowPhonePage(PhonePage.Home);
                BuildHomeApps(); if (_phone.Page == PhonePage.Home) RenderHome();
            }, null);
        }
        private void BeginPhonePage(bool resetInput = true)
        {
            ++_screenEpoch;
            _pageLifetime?.Cancel(); _pageLifetime?.Dispose();
            _pageLifetime = new CancellationTokenSource();
            if (resetInput) { _navigationRepeat?.Cancel(); _axisX = _axisY = 0; }
        }
        private void ShowPhonePage(PhonePage page, bool setState = true)
        {
            StopCameraZoom();
            ExitViewer();
            if (_shellUi == null) return;
            bool keepViewerSelection = page == PhonePage.Viewer && _shownPage == PhonePage.Viewer;
            if (_shownPage == PhonePage.Settings && page != PhonePage.Settings) ClosePhoneSettings();
            _shownPage = page;
            if (_extensionApp != null)
            {
                var app = _extensionApp; _extensionApp = null;
                try { app.Close(); } catch (Exception ex) { _status = "アプリ終了失敗：" + ex.Message; }
            }
            if (page != PhonePage.Viewer) StopGalleryVideo();
            if (page != PhonePage.Viewer && page != PhonePage.Wallpaper) ClearViewerImage();
            BeginPhonePage();
            if (!keepViewerSelection) _controlSelection = 0;
            if (setState) _phone.Show(page, DateTime.UtcNow);
            bool camera = page == PhonePage.Camera;
            _cameraUi.Active(camera);
            _phoneCamera.enabled = camera;
            _shellUi.Active(!camera && page != PhonePage.Off);
            switch (page)
            {
                case PhonePage.Locked:
                    _shellUi.StartPage("", "A：メニュー／トリガー：カメラ", _lockWallpaperTexture); _shellUi.Lock(); break;
                case PhonePage.Home: RenderHome(); break;
                case PhonePage.Camera: _cameraUi.Apply(_app); break;
                case PhonePage.Gallery: RenderGalleryLoading(); ReloadGallery(); break;
                case PhonePage.Viewer: RenderViewer(); break;
                case PhonePage.Wallpaper: RenderWallpaperEditor(); break;
                case PhonePage.Settings:
                    _shellUi.StartPage("設定", "A：設定を再表示／B：メニュー");
                    _shellUi.Settings("F1のコンフィグマネージャーで\niPhoneの設定を開きます。\n\n待受は写真アプリから編集できます。");
                    OpenPhoneSettings(); break;
                case PhonePage.Extension: _shellUi.StartPage("アプリ", "A：決定／B：メニュー"); break;
            }
            ArmPhoneIdle(); ArmPhoneClock();
            _orientationClock.SetActive(page != PhonePage.Off && CanAct);
        }
        private void RenderHome()
        {
            _shellUi.StartPage("ホーム", "スティック：選択／A：開く", _homeWallpaperTexture);
            _shellUi.Home(_homeApps, _homeSelection);
        }
        private void ConfirmPhone()
        {
            if (!CanAct || _root == null) return;
            switch (_phone.Page)
            {
                case PhonePage.Locked: ShowPhonePage(PhonePage.Home); break;
                case PhonePage.Home:
                    var app = _homeApps[_homeSelection];
                    if (app.Id == "photos") ShowPhonePage(PhonePage.Gallery);
                    else if (app.Id == "camera") ShowPhonePage(PhonePage.Camera);
                    else if (app.Id == "settings") ShowPhonePage(PhonePage.Settings);
                    else
                    {
                        var extra = IPhoneAppApi.Snapshot().FirstOrDefault(x => x.Id == app.Id);
                        if (extra == null) { BuildHomeApps(); RenderHome(); return; }
                        ShowPhonePage(PhonePage.Extension); _extensionApp = extra;
                        try { extra.Open(_shellUi.Viewport); } catch (Exception ex) { _status = "アプリ開始失敗：" + ex.Message; ShowPhonePage(PhonePage.Home); }
                    }
                    break;
                case PhonePage.Camera: ShutterAction(); break;
                case PhonePage.Gallery: if (_galleryItems.Length == 0) ReloadGallery(); else OpenGalleryItem(_gallerySelection); break;
                case PhonePage.Viewer: ConfirmViewer(); break;
                case PhonePage.Wallpaper: ConfirmWallpaperControl(); break;
                case PhonePage.Settings: OpenPhoneSettings(); break;
                case PhonePage.Extension: _extensionApp?.Confirm(); break;
            }
        }
        private void BackPhone()
        {
            if (!CanAct || _root == null || _phone.Page == PhonePage.Off || _phone.Page == PhonePage.Locked) return;
            if (_movie != null) StopMovie();
            ShowPhonePage(_phone.Page == PhonePage.Viewer ? PhonePage.Gallery : PhonePage.Home);
        }
        private void TriggerPhone()
        {
            if (!CanAct || _root == null) return;
            if (_phone.Page == PhonePage.Locked) ShowPhonePage(PhonePage.Camera);
            else if (_phone.Page == PhonePage.Camera) ShutterAction();
            else if (_phone.Page == PhonePage.Extension) _extensionApp?.Trigger();
        }
        private void NavigatePhone(int x, int y)
        {
            switch (_phone.Page)
            {
                case PhonePage.Home:
                    int old = _homeSelection;
                    _homeSelection = MoveGrid(_homeSelection, _homeApps.Count, x, y, _shellUi.GridColumns);
                    if (old / 9 != _homeSelection / 9) RenderHome(); else _shellUi.Select(_homeSelection % 9);
                    break;
                case PhonePage.Gallery: NavigateGallery(x, y); break;
                case PhonePage.Viewer:
                    RefreshViewerUiTimeout();
                    if (_viewerUiVisible && !_viewerPanning) { _controlSelection = Math.Max(0, Math.Min(ViewerControls().Length - 1, _controlSelection + x)); _shellUi.Select(_controlSelection); } break;
                case PhonePage.Wallpaper:
                    if (_wallpaperSaving) return;
                    if (_wallpaperDraft.Edit != 0) { _wallpaperDraft.Move(x, y); SyncWallpaperNumbers(); _shellUi.EditorValues(_viewerTexture, _wallpaperDraft); }
                    else { _controlSelection = MoveGrid(_controlSelection, 4, x, y, _shellUi.GridColumns == 3 ? 3 : 4); _shellUi.Select(_controlSelection); }
                    break;
                case PhonePage.Extension: _extensionApp?.Navigate(x, y); break;
            }
        }
        internal static int MoveGrid(int current, int count, int x, int y, int columns)
            => PhoneGrid.Move(current, count, x, y, columns);
        private void PhoneAxis(float x, float y)
        {
            if (_phone.Page == PhonePage.Viewer) { ViewerAxis(x, y); return; }
            if (_phone.Page == PhonePage.Camera) { CameraZoomAxis(x, y); return; }
            float magnitude = Math.Max(Math.Abs(x), Math.Abs(y));
            if (magnitude < .25f)
            {
                _phoneAxisLatched = false; _app.ReleaseAxis(); _axisX = _axisY = 0; _navigationRepeat.Cancel(); return;
            }
            if (_phoneAxisLatched || magnitude < .65f) return;
            if (_phone.Page == PhonePage.Locked || _phone.Page == PhonePage.Off || _phone.Page == PhonePage.Settings) return;
            _phoneAxisLatched = true;
            _axisX = Math.Abs(x) >= Math.Abs(y) ? Math.Sign(x) : 0;
            _axisY = Math.Abs(y) > Math.Abs(x) ? Math.Sign(y) : 0;
            NavigatePhone(_axisX, _axisY);
            ArmPhoneRepeat(TimeSpan.FromMilliseconds(400));
        }
        private void ArmPhoneRepeat(TimeSpan delay)
        {
            int epoch = _screenEpoch;
            _navigationRepeat.Arm(delay, () =>
            {
                if (!CanAct || !IsHeld || !Application.isFocused || epoch != _screenEpoch || _axisX == 0 && _axisY == 0) return;
                RunPhoneInput(() => NavigatePhone(_axisX, _axisY));
                if (_axisX != 0 || _axisY != 0) ArmPhoneRepeat(TimeSpan.FromMilliseconds(130));
            });
        }
        private void RunPhoneInput(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                _navigationRepeat?.Cancel(); _axisX = _axisY = 0;
                _status = "iPhoneの操作失敗：" + ex.GetBaseException().Message;
                _shellUi?.Message(_status);
            }
        }
        private void PhoneGrabbed()
        {
            var before = _phone.Page;
            _phone.Grab(DateTime.UtcNow);
            _idleDeadline.Cancel();
            if (before != _phone.Page) ShowPhonePage(_phone.Page, false);
            else ArmPhoneClock();
        }
        private void PhoneReleased()
        {
            StopViewerInput();
            _navigationRepeat?.Cancel(); _phoneAxisLatched = false; _axisX = _axisY = 0;
            _phone.Release(DateTime.UtcNow); ArmPhoneIdle();
        }
        private void UpdatePhoneMediaActivity()
        {
            _phone.SetMediaActive(_movie != null || _galleryPlaying, DateTime.UtcNow); ArmPhoneIdle();
        }
        private void ArmPhoneIdle()
        {
            if (_idleDeadline == null) return;
            _idleDeadline.Cancel();
            if (_shellUi == null || _root == null || !CanAct || !_phone.Deadline.HasValue) return;
            int epoch = _captureEpoch;
            _idleDeadline.Arm(_phone.Deadline.Value - DateTime.UtcNow, () =>
            {
                if (!CanAct || epoch != _captureEpoch || _root == null) return;
                if (_phone.Expire(DateTime.UtcNow)) ShowPhonePage(_phone.Page, false);
                else ArmPhoneIdle();
            });
        }
        private void ArmPhoneClock()
        {
            _clockDeadline?.Cancel();
            if (_shellUi == null || _phone.Page == PhonePage.Off || !CanAct) return;
            _shellUi.Clock(DateTime.Now);
            _cameraUi?.SystemClock(DateTime.Now);
            DateTime now = DateTime.Now;
            _clockDeadline.Arm(TimeSpan.FromMilliseconds(60000 - now.Second * 1000 - now.Millisecond), () =>
            {
                if (!CanAct || _shellUi == null || _phone.Page == PhonePage.Off) return;
                _shellUi.Clock(DateTime.Now); ArmPhoneClock();
            });
        }
        private Action<Action> CapturePhonePoster(bool trackPage = true)
        {
            var permit = _permit;
            int owner = _captureEpoch, screen = _screenEpoch;
            var token = trackPage ? _pageLifetime.Token : _captureLifetime.Token;
            var context = _mainThread;
            return action => context.Post(_ =>
            {
                if (token.IsCancellationRequested || !CanAct || !permit.IsValid || _root == null || owner != _captureEpoch || trackPage && screen != _screenEpoch) return;
                action();
            }, null);
        }
        private void DestroyPhoneShell()
        {
            _orientationClock?.Dispose(); _orientationClock = null;
            ExitViewer(); _viewerUiDeadline?.Dispose(); _viewerUiDeadline = null;
            IPhoneAppApi.Changed -= PhoneAppsChanged;
            _idleDeadline?.Dispose(); _clockDeadline?.Dispose(); _navigationRepeat?.Dispose();
            _idleDeadline = _clockDeadline = _navigationRepeat = null;
            ++_screenEpoch; _pageLifetime?.Cancel(); _pageLifetime?.Dispose(); _pageLifetime = null;
            if (_extensionApp != null)
            {
                var app = _extensionApp; _extensionApp = null;
                try { app.Close(); } catch (Exception ex) { _status = "アプリ終了失敗：" + ex.Message; }
            }
            ClosePhoneSettings(); StopGalleryVideo(); ClearViewerImage(); ClearGalleryThumbnails();
            _shellUi?.Dispose(); _shellUi = null;
            if (_homeWallpaperTexture != null) Destroy(_homeWallpaperTexture);
            if (_lockWallpaperTexture != null) Destroy(_lockWallpaperTexture);
            _homeWallpaperTexture = _lockWallpaperTexture = null;
        }
    }
}
