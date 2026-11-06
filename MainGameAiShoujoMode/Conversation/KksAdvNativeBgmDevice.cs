using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvNativeBgmDevice : KksAdvBgmPlayback.IDevice
    {
        private sealed class Track : KksAdvBgmPlayback.ITrack
        {
            private AudioSource _source;
            private Tween _fade;
            private readonly KksConversationRuntimeSettings _settings;
            internal Track(AudioSource source, KksConversationRuntimeSettings settings) { _source = source; _settings = settings; }
            private AudioSource Source => _source != null ? _source : throw new ObjectDisposedException(nameof(Track));
            public float Pitch { set { Source.pitch = value; } }
            public float PanStereo { set { Source.panStereo = value; } }
            public float Time { set { Source.time = value; } }
            public float Volume { set { Source.volume = value; } }
            public void Start(float fadeSeconds)
            {
                var source = Source;
                source.volume = _settings.zero; source.Play();
                // Native FadePlayer always fades in to currentVolume=one, even after a volume tail override.
                Fade(_settings.one, fadeSeconds, null);
            }
            public void FadeOut(float fadeSeconds, Action completed) => Fade(_settings.zero, fadeSeconds, completed);
            private void Fade(float end, float duration, Action completed)
            {
                _fade?.Kill(false);
                var source = Source;
                var initial = source.volume;
                _fade = DOTween.To(() => initial, value => { if (source != null) source.volume = value; }, end, duration)
                    .SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
                    {
                        _fade = null; completed?.Invoke();
                    });
            }
            public void Dispose()
            {
                _fade?.Kill(false); _fade = null;
                var source = _source; _source = null;
                if (source != null) { source.Stop(); source.clip = null; UnityEngine.Object.Destroy(source.gameObject); }
            }
        }
        private readonly Transform _root;
        private readonly KksConversationRuntimeSettings _settings;
        private readonly Action<string> _log;
        private readonly Dictionary<string, AssetBundleData> _assets = new Dictionary<string, AssetBundleData>(StringComparer.Ordinal);
        private bool _disposed;
        internal KksAdvNativeBgmDevice(Transform root, KksConversationRuntimeSettings settings, Action<string> log)
        { _root = root; _settings = settings ?? throw new ArgumentNullException(nameof(settings)); _log = log; }
        public KksAdvBgmPlayback.ITrack Create(Illusion.Game.Utils.Sound.SettingBGM setting)
        {
            if (_disposed || _root == null) throw new ObjectDisposedException(nameof(KksAdvNativeBgmDevice));
            if (!Manager.Sound.initialized) throw new InvalidOperationException("KKS Sound manager is not initialized.");
            var loader = setting.loader; var key = loader.bundle + "|" + loader.asset;
            if (!_assets.TryGetValue(key, out var asset)) _assets.Add(key, asset = new AssetBundleData(loader.bundle, loader.asset));
            var clip = asset.GetAsset<AudioClip>();
            if (clip == null) { _log?.Invoke("KKS_ADV_BGM_MISSING bundle=" + loader.bundle + " asset=" + loader.asset); return null; }
            clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                throw new InvalidDataException("KKS ADV BGM audio is not ready for synchronous playback: " + key + " state=" + clip.loadState);
            AudioSource source = null;
            try
            {
                source = Manager.Sound.Create(Manager.Sound.Type.BGM);
                if (source == null) throw new InvalidOperationException("KKS BGM AudioSource could not be created.");
                source.transform.SetParent(_root, false); source.name = "AI mode ADV BGM " + loader.asset;
                source.playOnAwake = false; source.clip = clip;
                Manager.Sound.AudioSettingData(source, loader.settingNo);
                var metadata = source.GetComponent<Sound_Component>() ?? source.gameObject.AddComponent<Sound_Component>(); metadata.Bind(loader);
                return new Track(source, _settings);
            }
            catch { if (source != null) UnityEngine.Object.Destroy(source.gameObject); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            foreach (var asset in _assets.Values) try { asset.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            _assets.Clear();
            if (errors.Count != _settings.zero) throw new AggregateException("KKS ADV BGM bundle cleanup failed.", errors);
        }
    }
}
