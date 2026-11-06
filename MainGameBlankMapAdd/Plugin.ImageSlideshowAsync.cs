using System;
using System.IO;
using UnityEngine;
using MainGameBlankMapAdd.Media.Images;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly SlideshowBackgroundJob<SlideshowCatalogSnapshot> _slideshowScan = new SlideshowBackgroundJob<SlideshowCatalogSnapshot>();
        private readonly SlideshowBackgroundJob<SlideshowImageBytes> _slideshowImageRead = new SlideshowBackgroundJob<SlideshowImageBytes>();
        private readonly SlideshowFolderScanner _slideshowFolderScanner = new SlideshowFolderScanner();
        private SlideshowCatalogSnapshot _slideshowCatalog = new SlideshowCatalogSnapshot();
        private string _slideshowScanInput, _slideshowScanSort;
        private bool _slideshowScanAutoFolder, _slideshowScanAscending, _slideshowScanForced;
        private bool _slideshowIoActive, _slideshowShowLatestAfterScan;
        private string _slideshowLoadPath, _slideshowFailedPath;
        private DateTime _slideshowLoadWriteTime, _slideshowFailedWriteTime;
        private bool _slideshowLoadResetTimer;
        private float _slideshowLoadRetryAt;
        private SlideshowUvMesh _slideshowUvMesh, _slideshowTransitionUvMesh;
        private SlideshowFileEvents _slideshowFileEvents;

        private void ResetImageSlideshowIo()
        {
            _slideshowFileEvents?.Dispose();
            _slideshowFileEvents = null;
            _slideshowScan.Reset();
            _slideshowImageRead.Reset();
            _slideshowIoActive = false;
            _slideshowScanInput = null;
            _slideshowLoadPath = _slideshowFailedPath = null;
            _slideshowShowLatestAfterScan = false;
            _nextImageSlideshowScanTime = 0f;
        }

        private void ScanImageSlideshowIfNeeded(bool force)
        {
            if (ImageSlideshowBlockedByRoomOverlay) return;
            if (_settings == null || !_settings.ImageSlideshowEnabled || _videoRoomRoot == null) return;
            string input = _settings.ImageSlideshowFolderPath ?? string.Empty;
            string sort = _settings.ImageSlideshowSortMode;
            bool auto = _settings.ImageSlideshowAutoLatestSubfolder;
            bool ascending = _settings.ImageSlideshowSortAscending;
            bool sourceChanged = !_slideshowIoActive || input != _slideshowScanInput || auto != _slideshowScanAutoFolder;
            bool changed = sourceChanged || sort != _slideshowScanSort || ascending != _slideshowScanAscending;
            if (changed)
            {
                _slideshowScan.Reset();
                _slideshowScanInput = input;
                _slideshowScanSort = sort;
                _slideshowScanAutoFolder = auto;
                _slideshowScanAscending = ascending;
                _slideshowIoActive = true;
                _nextImageSlideshowScanTime = 0f;
                force = true;
                if (sourceChanged)
                {
                    _slideshowImageRead.Reset();
                    _slideshowLoadPath = _slideshowFailedPath = null;
                    ClearImageSlideshowFileList();
                    HideAllImageSlideshowQuads();
                }
            }

            if (force) _slideshowScan.Reset();
            if (!force && (_slideshowScan.HasWork || Time.unscaledTime < _nextImageSlideshowScanTime)) return;
            // Capture plain values; the worker must not touch plugin state or Unity APIs.
            string root = GetImageSlideshowBaseFolder(input);
            DateTime day = DateTime.Now.Date;
            double now = Time.realtimeSinceStartup;
            SlideshowFolderScanner scanner = _slideshowFolderScanner;
            _slideshowScanForced = force;
            var dispatcher = _mainThreadDispatcher;
            var permit = CanonPlugins.Shared.PluginSessionGate.Capture();
            if (sourceChanged || _slideshowFileEvents == null)
            {
                _slideshowFileEvents?.Dispose();
                _slideshowFileEvents = null;
                try
                {
                    _slideshowFileEvents = new SlideshowFileEvents(root, auto,
                        action => { dispatcher?.Enqueue(action); },
                        () => { if (permit.IsValid && _slideshowIoActive) ScanImageSlideshowIfNeeded(true); });
                }
                catch (Exception ex) { LogWarn("[slideshow] file notifications unavailable: " + ex.Message); }
            }
            _slideshowScan.Request(() => scanner.Scan(root, auto, day, now, ImageSlideshowExtensions, sort, ascending, force),
                () => { dispatcher?.Enqueue(() =>
                {
                    if (permit.IsValid && _slideshowIoActive && !ImageSlideshowBlockedByRoomOverlay)
                        ApplyCompletedImageSlideshowScan();
                }); });
        }

        private void ApplyCompletedImageSlideshowScan()
        {
            if (_slideshowScan.TryTake(out SlideshowCatalogSnapshot snapshot, out Exception error))
            {
                if (error != null) LogWarn("[slideshow] background scan failed: " + error.Message);
                else
                {
                    ApplyImageSlideshowCatalog(snapshot, _slideshowScanForced);
                    if (_slideshowShowLatestAfterScan)
                    {
                        _slideshowShowLatestAfterScan = false;
                        _imageSlideshowPendingPath = string.Empty;
                        TryShowLatestImageSlideshow(true);
                    }
                }
                _nextImageSlideshowScanTime = Time.unscaledTime + Mathf.Max(0.25f, _settings.ImageSlideshowScanIntervalSec);
            }
        }

        private string GetImageSlideshowBaseFolder(string input)
        {
            string normalized = NormalizeFolderPathForList(input);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            try { return Path.IsPathRooted(normalized) ? normalized : Path.Combine(_pluginDir, normalized); }
            catch { return string.Empty; }
        }

        private bool RequestImageSlideshowLoad(string path, DateTime writeTime, bool resetTimer)
        {
            if (ImageSlideshowBlockedByRoomOverlay) return false;
            if (path == _slideshowLoadPath && writeTime == _slideshowLoadWriteTime)
            {
                _slideshowLoadResetTimer |= resetTimer;
                return true;
            }
            if (path == _slideshowFailedPath && writeTime == _slideshowFailedWriteTime && Time.unscaledTime < _slideshowLoadRetryAt)
                return false;
            _slideshowLoadPath = path;
            _slideshowLoadWriteTime = writeTime;
            _slideshowLoadResetTimer = resetTimer;
            var dispatcher = _mainThreadDispatcher;
            var permit = CanonPlugins.Shared.PluginSessionGate.Capture();
            _slideshowImageRead.Request(() => SlideshowImageBytes.Read(path), () =>
            {
                dispatcher?.Enqueue(() =>
                {
                    if (permit.IsValid && _slideshowIoActive && !ImageSlideshowBlockedByRoomOverlay)
                        ApplyCompletedImageSlideshowLoad();
                });
            });
            if (resetTimer) ScheduleNextImageSlideshowSlideTime(Time.unscaledTime);
            return true;
        }

        private void CancelImageSlideshowLoad()
        {
            if (string.IsNullOrEmpty(_slideshowLoadPath)) return;
            _slideshowImageRead.Reset();
            _slideshowLoadPath = null;
        }

        private void ApplyCompletedImageSlideshowLoad()
        {
            if (!_slideshowImageRead.TryTake(out SlideshowImageBytes loaded, out Exception error)) return;
            string path = _slideshowLoadPath;
            DateTime requestedWrite = _slideshowLoadWriteTime;
            bool resetTimer = _slideshowLoadResetTimer;
            _slideshowLoadPath = null;
            int index = IndexOfPath(_imageSlideshowFiles, path);
            if (index < 0) return;
            try
            {
                if (error != null) throw error;
                Texture2D texture = LoadImageSlideshowTexture(path, loaded.Bytes);
                bool transition = TryStartImageSlideshowTransition(index, path, loaded.WriteTime, texture);
                if (!transition) ApplyImageSlideshowTextureImmediate(index, path, loaded.WriteTime, texture);
                if (resetTimer) RestartImageSlideshowTimer();
                _slideshowFailedPath = null;
                LogImageSlideshowVerbose("[slideshow] image shown [" + (index + 1) + "/" + _imageSlideshowFiles.Length + "] path=" + path
                    + " transition=" + (transition ? _imageSlideshowTransitionRuntimeMode : "None"));
            }
            catch (Exception ex)
            {
                _slideshowFailedPath = path;
                _slideshowFailedWriteTime = requestedWrite;
                _slideshowLoadRetryAt = Time.unscaledTime + Mathf.Max(1f, _settings.ImageSlideshowScanIntervalSec);
                if (!_settings.ImageSlideshowLatestOnly) _imageSlideshowPendingPath = path;
                _nextImageSlideshowSlideTime = _slideshowLoadRetryAt;
                LogWarn("[slideshow] image load failed path=" + path + " error=" + ex.Message);
            }
        }

        private void RestartImageSlideshowTimer()
        {
            // Keep the outgoing image's pan position when a new image is crossfading.
            if (_imageSlideshowTransitionActive)
                ScheduleNextImageSlideshowSlideTime(_imageSlideshowTransitionDisplayStartTime);
            else
            {
                _imageSlideshowDisplayStartTime = Time.unscaledTime;
                ScheduleNextImageSlideshowSlideTime(_imageSlideshowDisplayStartTime);
            }
        }
    }
}
