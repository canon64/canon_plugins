using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MainGameRhythmSync;

namespace MainGameBeatSyncSpeed
{
    public partial class Plugin
    {
        private SynchronizationContext _bpmMainContext;
        private EventInfo _bpmVideoLoadedEvent, _bpmVideoStartedEvent;
        private Action<string> _bpmSongHandler;
        private readonly SongBpmAnalysisRequest _bpmRequest = new SongBpmAnalysisRequest();
        private readonly HashSet<string> _bpmAttempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _bpmCancellation;
        private string _bpmObservedSong, _bpmStatusSong, _bpmStatus;

        private void BeginAutoBpmSession()
        {
            _bpmMainContext = SynchronizationContext.Current;
            _bpmSongHandler = OnBpmMediaLoaded;
            if (_blankMapPluginType != null)
            {
                _bpmVideoLoadedEvent = _blankMapPluginType.GetEvent("OnVideoLoaded", BindingFlags.Public | BindingFlags.Static);
                _bpmVideoStartedEvent = _blankMapPluginType.GetEvent("OnVideoStarted", BindingFlags.Public | BindingFlags.Static);
                _bpmVideoLoadedEvent?.AddEventHandler(null, _bpmSongHandler);
                _bpmVideoStartedEvent?.AddEventHandler(null, _bpmSongHandler);
            }
            OnBpmMediaLoaded(null);
        }

        private void EndAutoBpmSession()
        {
            _bpmVideoLoadedEvent?.RemoveEventHandler(null, _bpmSongHandler);
            _bpmVideoStartedEvent?.RemoveEventHandler(null, _bpmSongHandler);
            _bpmVideoLoadedEvent = _bpmVideoStartedEvent = null;
            _bpmSongHandler = null;
            CancelBpmAnalysis();
            _bpmObservedSong = _bpmStatusSong = _bpmStatus = null;
            _bpmAttempted.Clear();
            _tapCount = 0; _lastTapTime = -1;
        }

        private void OnBpmMediaLoaded(string ignored)
        {
            if (SessionActive && Enabled && TryGetVideoFilePath(out string path)) ObserveBpmSong(path);
        }

        // Invoked by media load/start or the existing VMD source-change branch, never a new monitor.
        private void ObserveBpmSong(string path)
        {
            if (!SessionActive || !Enabled) return;
            string key = NormalizeSongPathKey(path);
            if (key == null || string.Equals(key, _bpmObservedSong, StringComparison.OrdinalIgnoreCase)) return;
            CancelBpmAnalysis();
            _bpmObservedSong = _currentSongPathKey = key;
            _tapCount = 0; _lastTapTime = -1;
            InvalidateAnalysis();
            int saved = ReadRegisteredBpm(key);
            if (saved > 0)
            {
                _suppressSongBpmPersist = true;
                try { _cfgBpm.Value = saved; }
                finally { _suppressSongBpmPersist = false; }
                TryApplyToSpeedLimitBreak(saved, "song-auto-load");
                return;
            }
            if (_bpmAttempted.Add(key)) StartBpmAnalysis(key, false);
        }

        internal bool RequestBpmAnalysis(string path)
        {
            if (!SessionActive || !Enabled || !TryGetVideoFilePath(out string current)) return false;
            string key = NormalizeSongPathKey(path);
            if (key == null || !string.Equals(key, NormalizeSongPathKey(current), StringComparison.OrdinalIgnoreCase)) return false;
            if (_bpmRequest.Running) return false;
            _bpmObservedSong = _currentSongPathKey = key;
            _bpmAttempted.Add(key);
            return StartBpmAnalysis(key, true);
        }

        internal string BpmAnalysisStatus(string path)
        {
            string key = NormalizeSongPathKey(path);
            if (key == null) return "音源なし";
            if (!SessionActive || !Enabled) return "リズム同期OFF";
            if (_bpmRequest.Running && string.Equals(key, _bpmRequest.Path, StringComparison.OrdinalIgnoreCase)) return "解析中";
            return string.Equals(key, _bpmStatusSong, StringComparison.OrdinalIgnoreCase) ? _bpmStatus : "";
        }

        private bool StartBpmAnalysis(string song, bool explicitRequest)
        {
            // Recheck here: automatic requests may never decode a registered song.
            if (!SessionActive || !Enabled || (!explicitRequest && ReadRegisteredBpm(song) > 0)) return false;
            string ffmpeg = FindFfmpegPath();
            _bpmStatusSong = song;
            if (ffmpeg == null || _bpmMainContext == null)
            {
                _bpmStatus = ffmpeg == null ? "ffmpegなし" : "解析開始不可";
                return false;
            }
            CancelBpmAnalysis();
            int generation = _bpmRequest.Start(song);
            int epoch = _workEpoch;
            var permit = _sessionPermit;
            var cancellation = _bpmCancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var context = _bpmMainContext;
            string temporary = Path.Combine(_pluginDir, "cache", "bpm-" + System.Guid.NewGuid().ToString("N") + ".f32");
            _bpmStatus = "解析中";
            try
            {
                new Thread(() =>
                {
                    int bpm = 0; string failure = null;
                    try { bpm = AudioBpmAnalyzer.Analyze(BpmAudioDecoder.Decode(song, ffmpeg, temporary, token), token); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { failure = ex.Message; }
                    // Unity's main-thread context receives exactly one completion; no result polling.
                    context.Post(_ =>
                    {
                        if (!_bpmRequest.Matches(generation, song)) { cancellation.Dispose(); return; }
                        bool valid = SessionActive && Enabled && permit.IsValid && epoch == _workEpoch && !token.IsCancellationRequested;
                        string current = valid && TryGetVideoFilePath(out string path) ? NormalizeSongPathKey(path) : null;
                        bool apply = _bpmRequest.CanApply(generation, song, current, valid, explicitRequest, ReadRegisteredBpm(song) > 0);
                        _bpmRequest.Cancel();
                        _bpmCancellation = null;
                        cancellation.Dispose();
                        if (!apply) { _bpmStatus = ""; return; }
                        if (bpm <= 0) { _bpmStatus = "解析失敗: " + (failure ?? "解析を中止しました"); return; }
                        _currentSongPathKey = song;
                        _suppressSongBpmPersist = true;
                        try { _cfgBpm.Value = bpm; }
                        finally { _suppressSongBpmPersist = false; }
                        PersistCurrentSongBpm(bpm, "audio-auto-analysis");
                        InvalidateAnalysis();
                        TryApplyToSpeedLimitBreak(bpm, "audio-auto-analysis");
                        _bpmStatus = "解析完了";
                    }, null);
                }) { IsBackground = true, Name = "RhythmSync BPM analysis" }.Start();
                return true;
            }
            catch (Exception)
            {
                CancelBpmAnalysis(); cancellation.Dispose(); _bpmStatus = "解析開始失敗"; return false;
            }
        }

        private void CancelBpmAnalysis()
        {
            _bpmRequest.Cancel();
            var cancellation = _bpmCancellation;
            _bpmCancellation = null;
            if (cancellation != null) { cancellation.Cancel(); /* worker owns its token until completion */ }
        }

        private void OnBpmManualInput()
        {
            CancelBpmAnalysis();
            _bpmStatusSong = _currentSongPathKey;
            _bpmStatus = "";
        }
    }
}
