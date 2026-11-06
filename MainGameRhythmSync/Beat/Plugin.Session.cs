using System.Threading;
using CanonGate;
using MainGameRhythmSync;

namespace MainGameBeatSyncSpeed
{
    public partial class Plugin
    {
        private GateApi.Permit _sessionPermit;
        private ulong _lastSessionId;
        private SessionScene _sessionScene;
        private bool _savedVoiceSpeedMotion;
        internal bool SessionActive => isActiveAndEnabled && Host != null && Host.isActiveAndEnabled && _sessionPermit.IsValid && _sessionScene != null;
        internal void BeginSession()
        {
            var context = GateApi.Current;
            if (context == null || context.Kind != GateApi.SessionKind.H || context.Scene == null) return;
            if (_lastSessionId == context.Id) return;
            _lastSessionId = context.Id;
            EndSession();
            _sessionPermit = GateApi.Capture(Guid);
            if (!_sessionPermit.IsValid) return;
            _sessionScene = new SessionScene(context.Scene);
            _hSceneProc = _sessionScene;
            if (_sessionScene.flags.voice != null) _savedVoiceSpeedMotion = _sessionScene.flags.voice.speedMotion;
            _insideHScene = true;
            CanonPlugins.Shared.VmdRhythmSource.Prepare();
            ResolveSnapshotMethod();
            if (_blankMapPluginType != null) TryGetVideoUrlFromApi();
            enabled = true;
            BeginAutoBpmSession();
            LogInfo("[canon_gate] session started id=" + context.Id);
        }
        internal void EndSession()
        {
            bool wasActive = _sessionScene != null;
            if (!wasActive) { _sessionPermit = default; enabled = false; return; }
            _sessionPermit = default;
            EndAutoBpmSession();
            if (_sessionScene?.flags?.voice != null) _sessionScene.flags.voice.speedMotion = _savedVoiceSpeedMotion;
            _sessionScene = null;
            enabled = false;
            lock (_workerLock)
            {
                ++_workEpoch;
                ++_wavAnalysisGeneration;
                _wavAnalysisResult = null;
                _wavExtractionResult = null;
                _wavAnalysisRunning = _wavExtractionRunning = false;
                try { if (_ownedExtraction != null && !_ownedExtraction.HasExited) _ownedExtraction.Kill(); } catch { }
                _ownedExtraction = null;
            }
            _nextVideoRoomPollTime = _nextSongPathPollTime = _nextVideoPathChangeCheckTime = 0f;
            _smoothedIntensity01 = -1f;
            _analysisReady = false;
            _beatIntensities = null;
            _lastAnalyzedPath = _lastVideoPath = _effectSong = _wavExtractionSource = null;
            CurrentIntensity01 = _currentZoneRawTarget = -1f;
            _highSustainStart = _lowSustainStart = -1f;
            _currentSongPathKey = null;
            ResetVideoRoom();
            _insideHScene = false;
            _hSceneProc = null;
            _sessionScene = null;
            if (wasActive) LogInfo("[canon_gate] session stopped");
        }
        private int _workEpoch;
        private readonly object _workerLock = new object();
        private System.Diagnostics.Process _ownedExtraction;
        private void OnDisable() { if (_sessionScene != null) EndSession(); }
        private void OnDestroy() { EndSession(); if (Instance == this) Instance = null; }
    }
}
