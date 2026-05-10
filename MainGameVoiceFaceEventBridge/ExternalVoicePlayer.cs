using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed class ExternalVoicePlaybackItem
    {
        internal int Index;
        internal int Total;
        internal int SequencePosition;
        internal string Path;
        internal string Subtitle;
        internal string FullSubtitle;
        internal float FullHoldSeconds;
        internal float DurationSeconds;
        internal float HoldSeconds;
        internal bool DeleteAfterPlay;
    }

    internal sealed class ExternalVoicePlaybackStartedEvent
    {
        internal string SessionId;
        internal int Index;
        internal int Total;
        internal int SequencePosition;
        internal string Path;
        internal string Subtitle;
        internal string FullSubtitle;
        internal float FullHoldSeconds;
        internal float DurationSeconds;
        internal float HoldSeconds;
    }

    internal sealed class ExternalVoicePlayer : IDisposable
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private readonly Action<string> _logError;

        private GameObject _host;
        private AudioSource _source;
        private AudioClip _currentClip;
        private string _currentPath;
        private bool _deleteAfterPlayback;
        private ChaControl _lipSyncBoundFemale;
        private bool _hasMouthFixedSnapshot;
        private bool _mouthFixedBeforeExternalPlay;
        private string _sequenceSessionId = string.Empty;
        private readonly object _queueLock = new object();
        private readonly Queue<ExternalVoicePlaybackItem> _queue = new Queue<ExternalVoicePlaybackItem>();
        private ExternalVoicePlaybackItem _pendingQueueItem;
        private ExternalVoicePlaybackItem _currentQueueItem;
        private ChaControl _sequenceFemale;
        private bool _sequenceDefaultDeleteAfterPlay;
        private float _sequenceVolume = 1f;
        private float _sequencePitch = 1f;

        // バックグラウンドロード用
        private volatile int _loadGeneration;          // ロードごとにインクリメント。古いロードのキャンセル判定に使う
        private PcmData _pendingPcm;                   // バックグラウンドが完了したPCMデータ（メインスレッドで消費）
        private string _pendingError;                  // バックグラウンドでのエラー
        private bool _pendingReady;                    // trueならUpdate()でAudioClip生成して再生
        private readonly object _pendingLock = new object();

        // 再生パラメータ（バックグラウンド完了後にUpdate()で使う）
        private ChaControl _pendingFemale;
        private float _pendingVolume;
        private float _pendingPitch;
        private bool _pendingDeleteAfterPlay;

        internal event Action<ExternalVoicePlaybackStartedEvent> PlaybackStarted;

        internal bool IsPlaying
        {
            get
            {
                bool queued;
                lock (_queueLock) { queued = _queue.Count > 0 || _pendingQueueItem != null; }
                // ロード中 or 再生中 or clipが残っている
                return (_source != null && _source.isPlaying) || _currentClip != null || _loadGeneration > 0 || queued;
            }
        }

        internal ExternalVoicePlayer(Action<string> logInfo, Action<string> logWarn, Action<string> logError)
        {
            _logInfo = logInfo;
            _logWarn = logWarn;
            _logError = logError;
        }

        internal bool Play(
            string absolutePath,
            ChaControl female,
            bool interruptCurrent,
            bool deleteAfterPlay,
            float volume,
            float pitch)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            EnsureSource();

            if (IsPlaying)
            {
                if (!interruptCurrent)
                {
                    return false;
                }

                Interlocked.Increment(ref _loadGeneration);
                lock (_pendingLock)
                {
                    _pendingPcm = null;
                    _pendingError = null;
                    _pendingReady = false;
                }
                ClearQueuedItems();
                StopInternal("interrupt", writeLog: true);
            }

            ClearQueuedItems();
            return StartLoad(absolutePath, female, deleteAfterPlay, volume, pitch, null);
        }

        internal bool PlaySequence(
            IList<ExternalVoicePlaybackItem> items,
            string sessionId,
            ChaControl female,
            bool interruptCurrent,
            bool defaultDeleteAfterPlay,
            float volume,
            float pitch)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            EnsureSource();

            if (IsPlaying)
            {
                if (!interruptCurrent)
                {
                    return false;
                }

                Interlocked.Increment(ref _loadGeneration);
                lock (_pendingLock)
                {
                    _pendingPcm = null;
                    _pendingError = null;
                    _pendingReady = false;
                }
                StopInternal("sequence interrupt", writeLog: true);
            }

            ClearQueuedItems();
            _sequenceSessionId = sessionId ?? string.Empty;
            _sequenceFemale = female;
            _sequenceDefaultDeleteAfterPlay = defaultDeleteAfterPlay;
            _sequenceVolume = volume;
            _sequencePitch = pitch;
            lock (_queueLock)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ExternalVoicePlaybackItem item = items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.Path))
                    {
                        continue;
                    }

                    if (item.Index <= 0)
                    {
                        item.Index = i + 1;
                    }

                    item.Total = items.Count;
                    item.SequencePosition = i + 1;
                    _queue.Enqueue(item);
                }
            }

            if (!StartNextQueued())
            {
                ClearQueuedItems();
                return false;
            }

            _logInfo?.Invoke("[audio-seq] queued session=" + _sequenceSessionId + " count=" + items.Count);
            return true;
        }

        private bool StartLoad(
            string absolutePath,
            ChaControl female,
            bool deleteAfterPlay,
            float volume,
            float pitch,
            ExternalVoicePlaybackItem queueItem)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            // 古いバックグラウンドロードをキャンセル
            int gen = Interlocked.Increment(ref _loadGeneration);
            lock (_pendingLock)
            {
                _pendingPcm = null;
                _pendingError = null;
                _pendingReady = false;
            }
            lock (_queueLock)
            {
                _pendingQueueItem = queueItem;
            }

            _pendingFemale = female;
            _pendingVolume = volume;
            _pendingPitch = pitch;
            _pendingDeleteAfterPlay = deleteAfterPlay;

            _logInfo?.Invoke("[audio] load start (bg) gen=" + gen + " path=" + absolutePath);

            string path = absolutePath;
            int capturedGen = gen;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (capturedGen != _loadGeneration) return;

                bool ok = WavFileLoader.TryLoadPcm(path, out PcmData data, out string error);

                if (capturedGen != _loadGeneration) return;

                lock (_pendingLock)
                {
                    if (capturedGen != _loadGeneration) return;
                    _pendingPcm = ok ? data : null;
                    _pendingError = ok ? null : error;
                    _pendingReady = true;
                }
            });

            return true;
        }

        internal void Stop(string reason)
        {
            // バックグラウンドロードもキャンセル
            Interlocked.Increment(ref _loadGeneration);
            lock (_pendingLock)
            {
                _pendingPcm = null;
                _pendingError = null;
                _pendingReady = false;
            }
            ClearQueuedItems();
            StopInternal(reason, writeLog: true);
        }

        internal void Update()
        {
            // バックグラウンドロード完了チェック
            bool ready;
            PcmData pcmData;
            string loadError;
            lock (_pendingLock)
            {
                ready = _pendingReady;
                pcmData = _pendingPcm;
                loadError = _pendingError;
                if (ready)
                {
                    _pendingReady = false;
                    _pendingPcm = null;
                    _pendingError = null;
                    _loadGeneration = 0;
                }
            }

            if (ready)
            {
                ExternalVoicePlaybackItem startedQueueItem;
                lock (_queueLock)
                {
                    startedQueueItem = _pendingQueueItem;
                    _pendingQueueItem = null;
                }

                if (loadError != null)
                {
                    _logWarn?.Invoke("[audio] load failed (bg): " + loadError);
                    StartNextQueued();
                }
                else if (pcmData != null)
                {
                    ReleaseCurrentClip(deleteFile: true);
                    try
                    {
                        AudioClip clip = WavFileLoader.CreateClip(pcmData);
                        _currentClip = clip;
                        _currentPath = pcmData.Path;
                        _deleteAfterPlayback = _pendingDeleteAfterPlay;
                        _currentQueueItem = startedQueueItem;

                        _source.clip = clip;
                        _source.volume = Mathf.Clamp01(_pendingVolume);
                        _source.pitch = Mathf.Clamp(_pendingPitch, 0.1f, 3f);
                        _source.loop = false;
                        _source.spatialBlend = 0f;
                        _source.Play();
                        _logInfo?.Invoke("[audio] after Play(): isPlaying=" + _source.isPlaying + " clipSamples=" + clip.samples + " sampleRate=" + clip.frequency);

                        TryBindLipSync(_pendingFemale);
                        _logInfo?.Invoke("[audio] play (bg complete) path=" + pcmData.Path);
                        NotifyPlaybackStarted(startedQueueItem, pcmData.Path, clip.length);
                    }
                    catch (Exception ex)
                    {
                        _logError?.Invoke("[audio] clip create failed: " + ex.Message);
                        StartNextQueued();
                    }
                }
            }

            // 再生完了チェック
            if (_source == null || _currentClip == null)
            {
                return;
            }

            if (_source.isPlaying)
            {
                RefreshLipSyncBinding();
                return;
            }

            _logInfo?.Invoke("[audio] update: isPlaying=False currentClip=" + (_currentClip != null ? _currentClip.name : "null") + " -> releasing");
            ReleaseCurrentClip(deleteFile: true);
            _currentQueueItem = null;
            _logInfo?.Invoke("[audio] completed");
            StartNextQueued();
        }

        private void StopInternal(string reason, bool writeLog)
        {
            bool hadSomething = (_source != null && _source.isPlaying) || _currentClip != null;
            if (_source != null)
            {
                _source.Stop();
                _logInfo?.Invoke("[audio] stopInternal: after Stop() isPlaying=" + _source.isPlaying + " reason=" + reason);
            }

            ReleaseCurrentClip(deleteFile: true);
            _currentQueueItem = null;

            if (hadSomething && writeLog)
            {
                _logInfo?.Invoke("[audio] stop reason=" + reason);
            }
        }

        private void ClearQueuedItems()
        {
            lock (_queueLock)
            {
                _queue.Clear();
                _pendingQueueItem = null;
            }

            _currentQueueItem = null;
            _sequenceSessionId = string.Empty;
            _sequenceFemale = null;
            _sequenceDefaultDeleteAfterPlay = false;
            _sequenceVolume = 1f;
            _sequencePitch = 1f;
        }

        private bool StartNextQueued()
        {
            ExternalVoicePlaybackItem next = null;
            lock (_queueLock)
            {
                if (_queue.Count > 0)
                {
                    next = _queue.Dequeue();
                }
            }

            if (next == null)
            {
                return false;
            }

            bool deleteAfterPlay = next.DeleteAfterPlay || _sequenceDefaultDeleteAfterPlay;
            _logInfo?.Invoke("[audio-seq] load next session=" + _sequenceSessionId + " index=" + next.Index + " path=" + next.Path);
            return StartLoad(next.Path, _sequenceFemale, deleteAfterPlay, _sequenceVolume, _sequencePitch, next);
        }

        private void NotifyPlaybackStarted(ExternalVoicePlaybackItem item, string path, float clipLength)
        {
            if (PlaybackStarted == null)
            {
                return;
            }

            float duration = item != null && item.DurationSeconds > 0f ? item.DurationSeconds : clipLength;
            float hold = item != null && item.HoldSeconds > 0f ? item.HoldSeconds : Mathf.Max(0.1f, duration + 0.2f);
            var payload = new ExternalVoicePlaybackStartedEvent
            {
                SessionId = _sequenceSessionId ?? string.Empty,
                Index = item != null ? item.Index : -1,
                Total = item != null ? item.Total : 0,
                SequencePosition = item != null ? item.SequencePosition : -1,
                Path = path ?? string.Empty,
                Subtitle = item != null ? (item.Subtitle ?? string.Empty) : string.Empty,
                FullSubtitle = item != null ? (item.FullSubtitle ?? string.Empty) : string.Empty,
                FullHoldSeconds = item != null ? item.FullHoldSeconds : 0f,
                DurationSeconds = duration,
                HoldSeconds = hold
            };

            try
            {
                PlaybackStarted(payload);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio-seq] playback-start callback failed: " + ex.Message);
            }
        }

        private void TryBindLipSync(ChaControl female)
        {
            if (female == null || _source == null)
            {
                return;
            }

            try
            {
                female.SetLipSync(_source);
                _lipSyncBoundFemale = female;
                TryDisableMouthFixedDuringExternalPlay(female);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio] SetLipSync failed: " + ex.Message);
            }
        }

        private void RefreshLipSyncBinding()
        {
            if (_lipSyncBoundFemale == null || _source == null || !_source.isPlaying)
            {
                return;
            }

            try
            {
                float t0 = UnityEngine.Time.realtimeSinceStartup;
                _lipSyncBoundFemale.SetLipSync(_source);
                float t1 = UnityEngine.Time.realtimeSinceStartup;
                TryDisableMouthFixedDuringExternalPlay(_lipSyncBoundFemale);
                float t2 = UnityEngine.Time.realtimeSinceStartup;
                float setLipMs = (t1 - t0) * 1000f;
                float mouthMs = (t2 - t1) * 1000f;
                if (setLipMs > 5f || mouthMs > 5f)
                {
                    _logWarn?.Invoke($"[audio] RefreshLipSync slow: SetLipSync={setLipMs:F1}ms MouthFixed={mouthMs:F1}ms samples={_source.clip?.samples}");
                }
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio] refresh lip sync failed: " + ex.Message);
            }
        }

        private void TryDisableMouthFixedDuringExternalPlay(ChaControl female)
        {
            if (female == null)
            {
                return;
            }

            try
            {
                bool isFixed = female.GetMouthFixed();
                if (!_hasMouthFixedSnapshot)
                {
                    _hasMouthFixedSnapshot = true;
                    _mouthFixedBeforeExternalPlay = isFixed;
                }

                if (isFixed)
                {
                    female.ChangeMouthFixed(false);
                }
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio] mouth-fixed disable failed: " + ex.Message);
            }
        }

        private void TryRestoreMouthFixedAfterExternalPlay(ChaControl female)
        {
            if (!_hasMouthFixedSnapshot || female == null)
            {
                _hasMouthFixedSnapshot = false;
                _mouthFixedBeforeExternalPlay = false;
                return;
            }

            try
            {
                female.ChangeMouthFixed(_mouthFixedBeforeExternalPlay);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio] mouth-fixed restore failed: " + ex.Message);
            }
            finally
            {
                _hasMouthFixedSnapshot = false;
                _mouthFixedBeforeExternalPlay = false;
            }
        }

        private void TryClearLipSyncBinding()
        {
            if (_lipSyncBoundFemale == null)
            {
                _hasMouthFixedSnapshot = false;
                _mouthFixedBeforeExternalPlay = false;
                return;
            }

            ChaControl female = _lipSyncBoundFemale;
            try
            {
                female.SetLipSync(null);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("[audio] clear lip sync failed: " + ex.Message);
            }
            finally
            {
                TryRestoreMouthFixedAfterExternalPlay(female);
                _lipSyncBoundFemale = null;
            }
        }

        private void EnsureSource()
        {
            if (_source != null)
            {
                return;
            }

            _host = new GameObject("MainGameVoiceFaceEventBridge.ExternalVoicePlayer");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _source = _host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        private void ReleaseCurrentClip(bool deleteFile)
        {
            string finishedPath = _currentPath;
            bool shouldDelete = deleteFile && _deleteAfterPlayback;
            TryClearLipSyncBinding();

            if (_source != null)
            {
                _source.clip = null;
            }

            if (_currentClip != null)
            {
                UnityEngine.Object.Destroy(_currentClip);
                _currentClip = null;
            }

            _currentPath = null;
            _deleteAfterPlayback = false;

            if (shouldDelete && !string.IsNullOrEmpty(finishedPath))
            {
                TryDeleteAudioFile(finishedPath);
            }
        }

        private void TryDeleteAudioFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logInfo?.Invoke("[audio] deleted file: " + path);
                }
            }
            catch (Exception ex)
            {
                _logError?.Invoke("[audio] delete failed: " + ex.Message + " path=" + path);
            }
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _loadGeneration);
            lock (_pendingLock)
            {
                _pendingPcm = null;
                _pendingError = null;
                _pendingReady = false;
            }
            ClearQueuedItems();
            StopInternal("dispose", writeLog: false);

            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }

            _source = null;
        }
    }
}
