using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
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

        internal bool IsPlaying
        {
            get
            {
                bool loading;
                lock (_pendingLock) { loading = _pendingReady || (_pendingPcm == null && _pendingError == null && _loadGeneration > 0 && !_pendingReady); }
                // ロード中 or 再生中 or clipが残っている
                return (_source != null && _source.isPlaying) || _currentClip != null || _loadGeneration > 0;
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

            if (_source.isPlaying || _currentClip != null)
            {
                if (!interruptCurrent)
                {
                    return false;
                }

                StopInternal("interrupt", writeLog: true);
            }

            // 古いバックグラウンドロードをキャンセル
            int gen = Interlocked.Increment(ref _loadGeneration);
            lock (_pendingLock)
            {
                _pendingPcm = null;
                _pendingError = null;
                _pendingReady = false;
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
                if (loadError != null)
                {
                    _logWarn?.Invoke("[audio] load failed (bg): " + loadError);
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

                        _source.clip = clip;
                        _source.volume = Mathf.Clamp01(_pendingVolume);
                        _source.pitch = Mathf.Clamp(_pendingPitch, 0.1f, 3f);
                        _source.loop = false;
                        _source.spatialBlend = 0f;
                        _source.Play();
                        _logInfo?.Invoke("[audio] after Play(): isPlaying=" + _source.isPlaying + " clipSamples=" + clip.samples + " sampleRate=" + clip.frequency);

                        TryBindLipSync(_pendingFemale);
                        _logInfo?.Invoke("[audio] play (bg complete) path=" + pcmData.Path);
                    }
                    catch (Exception ex)
                    {
                        _logError?.Invoke("[audio] clip create failed: " + ex.Message);
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
            _logInfo?.Invoke("[audio] completed");
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

            if (hadSomething && writeLog)
            {
                _logInfo?.Invoke("[audio] stop reason=" + reason);
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
