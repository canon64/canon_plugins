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
        private VolumeBoostFilter _boostFilter;
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

        // ── 一過性ボリュームブースト (エンベロープ) ──
        // 外部 (例: VoiceImpactBoost) からの要求で、再生中に一瞬だけ
        // AudioSource.volume を baseVolume * envelope(t) に持ち上げる。
        // エンベロープ形状: attack 上昇 → hold 維持 → release 下降。
        private bool  _boostActive;
        private float _boostStartTime;
        private float _boostStartGain = 1f;
        private float _boostAttackSec;
        private float _boostHoldSec;
        private float _boostReleaseSec;
        private float _boostPeakMultiplier;
        private float _boostBaseVolume; // ブースト開始時の volume をスナップ
        private float _boostSilenceSec;
        private float _boostSilenceFadeOutSec = 0.08f;
        private float _boostSilenceFadeInSec = 0.08f;
        private PublicApi.EasingShape _boostEasing;
        private SilencePhase _silencePhase;
        private float _silenceUntilUnscaled;
        private float _silencePhaseStartUnscaled;
        private float _silenceFadeStartGain = 1f;

        private enum SilencePhase
        {
            None = 0,
            FadingOut = 1,
            Paused = 2,
            FadingIn = 3,
        }

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
            }

            TickVolumeEnvelope();
            if (_source.isPlaying || IsSilenceTransitionActive())
            {
                return;
            }

            _logInfo?.Invoke("[audio] update: isPlaying=False currentClip=" + (_currentClip != null ? _currentClip.name : "null") + " -> releasing");
            ReleaseCurrentClip(deleteFile: true);
            _currentQueueItem = null;
            _boostActive = false;
            ResetSilenceState();
            if (_boostFilter != null) _boostFilter.Gain = 1f;
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
            _boostActive = false;
            ResetSilenceState();
            if (_boostFilter != null) _boostFilter.Gain = 1f;

            if (hadSomething && writeLog)
            {
                _logInfo?.Invoke("[audio] stop reason=" + reason);
            }
        }

        /// <summary>
        /// 再生中の AudioSource に対して、attack→hold→release 形のエンベロープで
        /// volume を一時的に baseVolume * peakMultiplier まで持ち上げる。
        /// 再生していない場合は false を返して何もしない。
        /// 既にブースト中なら新パラメータで上書き (リトリガー)。
        /// </summary>
        internal bool RequestTransientBoost(float peakMultiplier, float attackSec, float holdSec, float releaseSec)
        {
            return RequestTransientBoost(peakMultiplier, attackSec, holdSec, releaseSec, 0f, PublicApi.EasingShape.CosineInOut);
        }

        internal bool RequestTransientBoost(
            float peakMultiplier,
            float attackSec,
            float holdSec,
            float releaseSec,
            float silenceSec,
            PublicApi.EasingShape easing)
        {
            return RequestTransientBoost(
                peakMultiplier,
                attackSec,
                holdSec,
                releaseSec,
                silenceSec,
                0.08f,
                0.08f,
                easing);
        }

        internal bool RequestTransientBoost(
            float peakMultiplier,
            float attackSec,
            float holdSec,
            float releaseSec,
            float silenceSec,
            float silenceFadeOutSec,
            float silenceFadeInSec,
            PublicApi.EasingShape easing)
        {
            if (_source == null || !_source.isPlaying) return false;
            if (peakMultiplier <= 0f) return false;

            float currentGain = GetCurrentOutputGain();
            ResetSilenceState();

            // baseVolume は AudioSource.volume を据え置きで使う。
            // ブースト本体は VolumeBoostFilter.Gain (OnAudioFilterRead) で行うので
            // baseVol は記録のみ (互換、無くてもOK)。
            float baseVol = _boostActive ? _boostBaseVolume : _source.volume;

            _boostActive = true;
            _boostStartTime = Time.unscaledTime;
            _boostStartGain = currentGain;
            _boostAttackSec = Mathf.Max(0f, attackSec);
            _boostHoldSec = Mathf.Max(0f, holdSec);
            _boostReleaseSec = Mathf.Max(0.0001f, releaseSec);
            _boostPeakMultiplier = peakMultiplier;
            _boostBaseVolume = baseVol;
            _boostSilenceSec = Mathf.Max(0f, silenceSec);
            _boostSilenceFadeOutSec = Mathf.Max(0f, silenceFadeOutSec);
            _boostSilenceFadeInSec = Mathf.Max(0f, silenceFadeInSec);
            _boostEasing = easing;
            return true;
        }

        /// <summary>
        /// Update から毎フレーム呼ぶ。エンベロープに従い AudioSource.volume を更新。
        /// 終了時は baseVolume に戻して非アクティブ化。
        /// </summary>
        private void TickVolumeEnvelope()
        {
            if (TickSilencePhase())
            {
                return;
            }

            if (!_boostActive) return;
            if (_source == null)
            {
                _boostActive = false;
                if (_boostFilter != null) _boostFilter.Gain = 1f;
                return;
            }

            float t = Time.unscaledTime - _boostStartTime;
            float total = _boostAttackSec + _boostHoldSec + _boostReleaseSec;
            if (t >= total)
            {
                float endGain = EvaluateBoostGain(Time.unscaledTime);
                _boostActive = false;

                // 無音挿入要求があれば、まず短いフェードアウトへ入る
                if (_boostSilenceSec > 0f && _source != null && _source.isPlaying)
                {
                    BeginSilenceFadeOut(endGain);
                }
                else if (_boostFilter != null)
                {
                    _boostFilter.Gain = 1f;
                }
                return;
            }

            // PCMサンプル直接×envelope (Unity AudioSource.volume の 1.0 上限をバイパス)
            if (_boostFilter != null) _boostFilter.Gain = EvaluateBoostGain(Time.unscaledTime);
        }

        private float GetCurrentOutputGain()
        {
            if (_boostActive)
            {
                return EvaluateBoostGain(Time.unscaledTime);
            }

            if (_boostFilter == null)
            {
                return 1f;
            }

            return Mathf.Clamp(_boostFilter.Gain, 0f, 10f);
        }

        private float EvaluateBoostGain(float now)
        {
            float t = Mathf.Max(0f, now - _boostStartTime);
            float releaseStart = _boostAttackSec + _boostHoldSec;

            if (_boostAttackSec > 0f && t < _boostAttackSec)
            {
                float progress = Mathf.Clamp01(t / _boostAttackSec);
                float eased = ApplyEasing(progress, _boostEasing);
                return Mathf.Lerp(_boostStartGain, _boostPeakMultiplier, eased);
            }

            if (t < releaseStart)
            {
                return _boostPeakMultiplier;
            }

            float releaseProgress = Mathf.Clamp01((t - releaseStart) / _boostReleaseSec);
            float releaseEased = ApplyEasing(releaseProgress, _boostEasing);
            return Mathf.Lerp(_boostPeakMultiplier, 1f, releaseEased);
        }

        private bool TickSilencePhase()
        {
            if (_silencePhase == SilencePhase.None)
            {
                return false;
            }

            if (_source == null)
            {
                ResetSilenceState();
                return false;
            }

            float now = Time.unscaledTime;
            switch (_silencePhase)
            {
                case SilencePhase.FadingOut:
                {
                    float progress = GetPhaseProgress(now, _silencePhaseStartUnscaled, _boostSilenceFadeOutSec);
                    float eased = ApplyEasing(progress, PublicApi.EasingShape.CosineInOut);
                    if (_boostFilter != null)
                    {
                        _boostFilter.Gain = Mathf.Lerp(_silenceFadeStartGain, 0f, eased);
                    }

                    if (progress >= 1f)
                    {
                        try { _source.Pause(); } catch { }
                        _silencePhase = SilencePhase.Paused;
                        _silenceUntilUnscaled = now + _boostSilenceSec;
                        if (_boostFilter != null)
                        {
                            _boostFilter.Gain = 0f;
                        }
                    }
                    return true;
                }

                case SilencePhase.Paused:
                    if (_boostFilter != null)
                    {
                        _boostFilter.Gain = 0f;
                    }

                    if (now >= _silenceUntilUnscaled)
                    {
                        try { _source.UnPause(); } catch { }
                        _silencePhase = SilencePhase.FadingIn;
                        _silencePhaseStartUnscaled = now;
                    }
                    return true;

                case SilencePhase.FadingIn:
                {
                    float progress = GetPhaseProgress(now, _silencePhaseStartUnscaled, _boostSilenceFadeInSec);
                    float eased = ApplyEasing(progress, PublicApi.EasingShape.CosineInOut);
                    if (_boostFilter != null)
                    {
                        _boostFilter.Gain = eased;
                    }

                    if (progress >= 1f)
                    {
                        if (_boostFilter != null)
                        {
                            _boostFilter.Gain = 1f;
                        }
                        ResetSilenceState();
                    }
                    return true;
                }

                default:
                    ResetSilenceState();
                    return false;
            }
        }

        private void BeginSilenceFadeOut(float startGain)
        {
            _silenceFadeStartGain = Mathf.Clamp(startGain, 0f, 10f);
            _silencePhase = SilencePhase.FadingOut;
            _silencePhaseStartUnscaled = Time.unscaledTime;
            if (_boostFilter != null)
            {
                _boostFilter.Gain = _silenceFadeStartGain;
            }
        }

        private bool IsSilenceTransitionActive()
        {
            return _silencePhase != SilencePhase.None;
        }

        private void ResetSilenceState()
        {
            _silencePhase = SilencePhase.None;
            _silenceUntilUnscaled = 0f;
            _silencePhaseStartUnscaled = 0f;
            _silenceFadeStartGain = 1f;
        }

        private static float GetPhaseProgress(float now, float start, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01((now - start) / duration);
        }

        private static float ApplyEasing(float x, PublicApi.EasingShape shape)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            switch (shape)
            {
                case PublicApi.EasingShape.CosineInOut:
                    // 0..1 を 0..1 にマップ、両端でなめらか (-cos π * x の半分シフト)
                    return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * x);
                case PublicApi.EasingShape.Linear:
                default:
                    return x;
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
            // VolumeBoostFilter は AudioSource と同じ GameObject に貼り付ける必要がある
            // (OnAudioFilterRead が同 GO の AudioSource 出力をフックする仕様)
            _boostFilter = _host.AddComponent<VolumeBoostFilter>();
            _boostFilter.Enabled = true;
            _boostFilter.Gain = 1f;
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
