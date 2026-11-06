using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksVoicePlayback : IDisposable
    {
        internal sealed class Clip { internal string Bundle, Asset; }
        private sealed class Lease : IDisposable
        {
            internal Action Cancel;
            internal bool Finishing;
            public void Dispose() { var cancel = Cancel; Cancel = null; cancel?.Invoke(); }
        }
        private readonly MonoBehaviour _host;
        private readonly KksConversationRuntimeSettings _settings;
        private readonly Action<string> _log;
        private readonly Dictionary<string, AssetBundleData> _assets = new Dictionary<string, AssetBundleData>(StringComparer.Ordinal);
        private AudioSource _source;
        private KksVoiceGroupPlayback _groups;
        private Lease _current;
        private bool _disposed;
        internal KksCharacterVoiceCatalog Catalog { get; set; }
        internal string ResolveBundle(int voice, string bundle, string asset) => Catalog?.Resolve(voice, bundle, asset) ?? bundle;
        internal KksVoicePlayback(MonoBehaviour host, KksConversationRuntimeSettings settings, Action<string> log)
        { _host = host; _settings = settings; _log = log; }
        internal IDisposable Play(IEnumerable<Clip> clips, int voice, float pitch, ChaControl actor, Action completed, Action<Exception> failed, Action<int> clipStarted = null,
            ADV.Info.Audio.Eco echo = null, bool moveMouth = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksVoicePlayback));
            if (float.IsNaN(pitch) || float.IsInfinity(pitch) || pitch < _settings.audioMinimumPitch)
                throw new InvalidDataException("KKSの音声ピッチが範囲外です。");
            _groups?.Stop(); _current?.Dispose();
            var lease = new Lease(); _current = lease; Coroutine routine = null;
            var previousLip = actor == null ? null : actor.asVoice;
            lease.Cancel = () =>
            {
                try { if (routine != null && !lease.Finishing && _host != null) _host.StopCoroutine(routine); }
                finally
                {
                    if (ReferenceEquals(_current, lease))
                    {
                        _current = null;
                        try { if (_source != null) { _source.Stop(); _source.clip = null; } }
                        finally { if (actor != null && (actor.asVoice == _source || (!moveMouth && actor.asVoice == null))) actor.SetLipSync(previousLip); }
                    }
                }
            };
            routine = _host.StartCoroutine(Guard(Series(clips.ToArray(), voice, pitch, actor, clipStarted, echo, moveMouth), lease, completed, failed));
            if (!ReferenceEquals(_current, lease) && !lease.Finishing && routine != null && _host != null) _host.StopCoroutine(routine);
            return lease;
        }
        private IEnumerator Guard(IEnumerator operation, Lease lease, Action completed, Action<Exception> failed)
        {
            while (ReferenceEquals(_current, lease))
            {
                bool next; object instruction;
                try { next = operation.MoveNext(); instruction = next ? operation.Current : null; }
                catch (Exception exception) { lease.Finishing = true; lease.Dispose(); _log?.Invoke("KKS_CONVERSATION_VOICE_ERROR " + exception); failed?.Invoke(exception); yield break; }
                if (!next)
                {
                    // Avoid stopping the coroutine from its own completion callback.
                    lease.Finishing = true; lease.Dispose();
                    completed?.Invoke(); yield break;
                }
                yield return instruction;
            }
        }
        private IEnumerator Series(Clip[] clips, int voice, float pitch, ChaControl actor, Action<int> clipStarted, ADV.Info.Audio.Eco echo, bool moveMouth)
        {
            for (var index = _settings.zero; index < clips.Length; index++)
            {
                if (_disposed) yield break;
                var cue = clips[index];
                var bundle = ResolveBundle(voice, cue.Bundle, cue.Asset);
                if (string.IsNullOrEmpty(bundle)) throw new FileNotFoundException("音声の参照先を解決できません：" + cue.Asset);
                var key = bundle + "|" + cue.Asset;
                if (!_assets.TryGetValue(key, out var asset)) _assets.Add(key, asset = new AssetBundleData(bundle, cue.Asset));
                var clip = asset.GetAsset<AudioClip>();
                if (clip == null) throw new FileNotFoundException("音声素材が見つかりません：" + bundle + " / " + cue.Asset);
                if (_source == null)
                {
                    _source = Manager.Voice.Create(_host.transform);
                    if (_source == null) throw new InvalidOperationException("KKSの音声用AudioSourceを生成できません。");
                    _source.name = "AI mode KKS conversation voice"; _source.playOnAwake = false;
                }
                var head = actor == null ? null : actor.GetReferenceInfo(ChaReference.RefObjKey.HeadParent);
                _source.transform.SetParent(head == null ? _host.transform : head.transform, false);
                _source.transform.localPosition = Vector3.zero; _source.transform.localRotation = Quaternion.identity;
                // Native ADV.Voice uses a head transform for lip sync but then sets speech to 2D.
                _source.loop = false; _source.spatialBlend = _settings.zero;
                _source.pitch = pitch; _source.volume = Manager.Voice.GetVolume(voice); _source.clip = clip;
                _source.GetComponent<Voice_Component>()?.Bind(new Manager.Voice.Loader { no = voice, bundle = bundle, asset = cue.Asset, pitch = pitch, voiceTrans = head == null ? null : head.transform });
                var filter = _source.GetComponent<AudioEchoFilter>();
                if (echo != null)
                {
                    if (filter == null) filter = _source.gameObject.AddComponent<AudioEchoFilter>();
                    filter.delay = echo.delay; filter.decayRatio = echo.decayRatio; filter.wetMix = echo.wetMix; filter.dryMix = echo.dryMix;
                }
                if (filter != null) filter.enabled = echo != null;
                if (actor != null) actor.SetLipSync(moveMouth ? _source : null);
                clip.LoadAudioData();
                var start = AudioSettings.dspTime + _settings.audioLeadSeconds;
                var duration = clip.length / _source.pitch;
                _source.PlayScheduled(start); _source.SetScheduledEndTime(start + duration);
                clipStarted?.Invoke(index);
                yield return new WaitForSecondsRealtime(_settings.audioLeadSeconds + duration);
                if (_source != null) _source.Stop();
            }
        }
        internal void Stop() { _groups?.Stop(); _current?.Dispose(); }
        internal KksVoiceGroupPlayback Groups
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(KksVoicePlayback));
                return _groups ?? (_groups = new KksVoiceGroupPlayback(this, _settings.zero));
            }
        }
        internal bool OwnsSource(AudioSource source) => _source != null && _source == source;
        internal KksVoicePlayback CreateSibling()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksVoicePlayback));
            return new KksVoicePlayback(_host, _settings, _log);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var failures = new List<Exception>();
            try { _groups?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            try { _current?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            try { if (_source != null) UnityEngine.Object.Destroy(_source.gameObject); } catch (Exception exception) { failures.Add(exception); }
            foreach (var asset in _assets.Values) try { asset.UnloadBundle(); } catch (Exception exception) { failures.Add(exception); }
            _assets.Clear();
            if (failures.Any()) throw new AggregateException("KKS conversation voice cleanup failed.", failures);
        }
    }
}
