using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using Forms = System.Windows.Forms;

namespace MainGameIPhoneCamera
{
    public sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer;
        public string DispName;
        public int Order;
    }
    public sealed partial class Plugin
    {
        private ConfigEntry<string> _homeWallpaper, _lockWallpaper, _galleryFolders;
        private ConfigEntry<float> _playbackVolume, _captureSoundVolume;
        private readonly PhoneConfigWindow _configWindow = new PhoneConfigWindow();
        private string PhoneDataDirectory => Path.Combine(Paths.GameRootPath, "UserData", "IPhoneCamera");
        private void BindPhoneSettings()
        {
            _captureSoundVolume = Config.Bind("Phone.Camera", "CaptureSoundVolume", 1f, new ConfigDescription("写真のシャッター音と録画開始・停止音の音量。0で無音。", new AcceptableValueRange<float>(0, 1)));
            _homeWallpaper = Config.Bind("Phone.Wallpaper", "MenuImage", "", "メニュー画面の待受画像。写真アプリで位置・倍率を編集して保存できます。");
            _lockWallpaper = Config.Bind("Phone.Wallpaper", "LockImage", "", "ロック画面の待受画像。メニュー用とは別に保存します。");
            _galleryFolders = Config.Bind("Phone.Photos", "AdditionalFolders", "", "写真アプリへ追加するフォルダー。セミコロン区切り。各フォルダー直下を表示します。");
            _playbackVolume = Config.Bind("Phone.Photos", "VideoVolume", 1f, new ConfigDescription("端末から再生する動画の音量。", new AcceptableValueRange<float>(0, 1)));
            Config.Bind("Phone.Photos", "AddFolder", false, new ConfigDescription("写真アプリに表示するフォルダーを選びます。", null,
                new ConfigurationManagerAttributes { DispName = "フォルダーを追加", Order = -1, CustomDrawer = _ =>
                {
                    if (!GUILayout.Button("フォルダーを選んで追加", GUILayout.Width(230))) return;
                    using (var dialog = new Forms.FolderBrowserDialog { Description = "iPhoneの写真アプリに表示するフォルダー" })
                    {
                        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                        string addition = dialog.SelectedPath;
                        var existing = (_galleryFolders.Value ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (Array.FindIndex(existing, x => string.Equals(x.Trim(), addition, StringComparison.OrdinalIgnoreCase)) < 0)
                            _galleryFolders.Value = string.Join(";", existing.Length == 0 ? new[] { addition } : new System.Collections.Generic.List<string>(existing) { addition }.ToArray());
                    }
                } }));
            _homeWallpaper.SettingChanged += HomeWallpaperChanged;
            _lockWallpaper.SettingChanged += LockWallpaperChanged;
            _galleryFolders.SettingChanged += GalleryFoldersChanged;
            _playbackVolume.SettingChanged += PlaybackVolumeChanged;
        }
        private void UnbindPhoneSettings()
        {
            _homeWallpaper.SettingChanged -= HomeWallpaperChanged; _lockWallpaper.SettingChanged -= LockWallpaperChanged;
            _galleryFolders.SettingChanged -= GalleryFoldersChanged; _playbackVolume.SettingChanged -= PlaybackVolumeChanged;
        }
        private void HomeWallpaperChanged(object sender, EventArgs e) { if (CanAct && _shellUi != null) LoadWallpaper(false); }
        private void LockWallpaperChanged(object sender, EventArgs e) { if (CanAct && _shellUi != null) LoadWallpaper(true); }
        private void GalleryFoldersChanged(object sender, EventArgs e) { if (CanAct && _phone.Page == PhonePage.Gallery) ReloadGallery(); }
        private void PlaybackVolumeChanged(object sender, EventArgs e)
        {
            if (CanAct && _galleryPlayerObject != null) _galleryPlayerObject.GetComponent<AudioSource>().volume = _playbackVolume.Value;
        }
        private void OpenPhoneSettings()
        {
            if (!CanAct) return;
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue("com.bepis.bepinex.configurationmanager", out var info) || info.Instance == null)
                    throw new InvalidOperationException("F1のコンフィグマネージャーが読み込まれていません");
                _configWindow.Open(info.Instance);
                // ConfigurationManager expands matching plugin sections whenever SearchString is non-empty.
                _shellUi.Message("F1設定：MainGameIPhoneCamera を表示中");
            }
            catch (Exception ex) { _shellUi?.Message("設定を開けません：" + ex.GetBaseException().Message); }
        }
        private void ClosePhoneSettings()
        {
            try { _configWindow.Close(); }
            catch (Exception ex) { _status = "設定画面の復帰失敗：" + ex.GetBaseException().Message; }
        }
    }
}
