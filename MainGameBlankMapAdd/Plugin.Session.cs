using System;
using System.Linq;
using CanonPlugins.Shared;
using MainGameBlankMapAdd.Application;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private volatile bool _sessionCommandsOpen;
        private bool _sessionStarted;
        private bool _hotelPlaybackSession;
        private bool HasPlaybackBarHost => _sessionStarted;

        private void StartOwnedSession()
        {
            if (_sessionStarted) return;
            _sessionStarted = true;
            _hotelPlaybackSession = CanonGate.GateApi.Current?.IsHotelMode == true;
            BindOwnedMapContext();
            if (_hotelPlaybackSession)
            {
                _playbackBarCtrlHidden = false;
                _playbackBarHiddenByUser = false;
                _playbackBarHideLatchActive = false;
            }
            LoadMapWarpPoints();
            LoadHLocations();
            LoadRoomCustomizations();
            _mainThreadDispatcher?.Dispose();
            _mainThreadDispatcher = new MainThreadDispatcher();
            _sessionCommandsOpen = true;
            ConnectVrGadgetDelete();
            _settings = _settingsRepository.LoadCurrent();
            SyncConfigEntriesFromSettings();
            _aiDisposed = false; _aiCancelRequested = false;
            _roomOverlayHScene = PluginSessionGate.HScene;

            _femaleChara = PluginSessionGate.ReadScene<System.Collections.Generic.List<ChaControl>>("lstFemale")
                ?.FirstOrDefault(actor => actor != null && actor.sex == 1);
            _hasFemaleBaseY = _femaleChara != null;
            if (_hasFemaleBaseY) _femaleBaseY = _femaleChara.transform.position.y;
            _configDirty = true;
            EnsureSessionPlayback();
            ApplyConfigChangesIfNeeded();
            ForceReapplyCurrentMap();
            TryMapWarpContext(out _, out _, out _);
            QueueDefaultHPlace();
            _impactEntriesResolved = false;
            UpdateImpactBoost();
            LogAudioDiagnosticsSnapshot("session-start", true);
            NormalizeImageSlideshowSettings();
            if (_settings.ImageSlideshowEnabled) ScanImageSlideshowIfNeeded(true);
        }

        private void StopOwnedSession()
        {
            _hotelPoseLocationsReady = false;
            DisconnectVrGadgetDelete();
            _hotelPlaybackSession = false;
            if (!_sessionStarted) { _mapSession?.CancelSourceMapPreparation(); return; }
            _sessionStarted = false;
            StopHLocations();
            if (ReferenceEquals(_roomFeatureOwner, this)) _roomFeatureOwner = null;
            RestoreSourceMapPlacement();
            ResetMapWarpNavigation();
            _mapWarpUiKey = null; _mapWarpSelectedId = null; _mapWarpRows.Clear();
            _sessionCommandsOpen = false;
            ResetImageSlideshowIo();
            RestoreImpactEntries();
            _mainThreadDispatcher?.Dispose();
            _externalMedia?.Release();
            _externalMusicSources?.ReleaseAll();
            _astralController?.Dispose();
            StopUiCapture("gate-session-end");
            HideUiBlocker();
            _uiResources?.SyncPlaybackCursor(false);
            if (_roomOverlayLoading)
            {
                _roomOverlayAbandonedSceneName = _roomOverlaySceneName;
                _roomOverlayAbandonedBundleName = _roomOverlayBundleName;
            }
            DisposeRoomOverlay();
            StopAllCoroutines();
            _mapSession?.Stop();
            DestroySessionPlayback();
            _roomOverlayHScene = null;
            ClearOwnedMapContext();
            _femaleChara = null;
            _hasFemaleBaseY = false;
            _femaleBaseY = 0f;
        }
    }
}
