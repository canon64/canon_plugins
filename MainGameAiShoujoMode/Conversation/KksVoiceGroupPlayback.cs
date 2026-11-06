using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksVoiceGroupPlayback : IDisposable
    {
        private sealed class Speech
        {
            internal ADV.Commands.Base.Voice.Data Data;
            internal ChaControl Actor;
            internal Transform Target;
            internal Tuple<int, Transform> Key => Tuple.Create(Data.personality, Target);
        }
        private readonly KksVoicePlayback _root;
        private readonly Dictionary<Tuple<int, Transform>, KksVoicePlayback> _channels = new Dictionary<Tuple<int, Transform>, KksVoicePlayback>();
        private readonly KksVoiceGroupSequence<Speech> _sequence;
        private object _token;
        private bool _disposed;
        internal KksVoiceGroupPlayback(KksVoicePlayback root, int zero)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _sequence = new KksVoiceGroupSequence<Speech>(speech => speech.Key, PlayChannel, zero);
        }
        internal IDisposable Play(IReadOnlyList<ADV.Commands.Base.Voice.Data[]> groups, Action changed, Action completed, Action<Exception> failed, bool replay = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksVoiceGroupPlayback));
            _sequence.Stop(); _root.Stop();
            var speeches = groups.Select(group => group.Select(data => new Speech
            { Data = data, Target = replay ? null : data.transform, Actor = replay || data.transform == null ? null : data.chaCtrl }).ToArray()).ToArray();
            var lips = speeches.SelectMany(group => group).Select(speech => speech.Actor).Where(actor => actor != null).Distinct()
                .Select(actor => new { Actor = actor, Voice = actor.asVoice }).ToArray();
            var token = new object(); _token = token;
            return _sequence.Play(speeches, changed, completed, failed, () =>
            {
                if (!ReferenceEquals(_token, token)) return;
                _token = null;
                foreach (var lip in lips)
                    if (lip.Actor != null && (lip.Actor.asVoice == null || _channels.Values.Any(channel => channel.OwnsSource(lip.Actor.asVoice))))
                        lip.Actor.SetLipSync(lip.Voice);
            });
        }
        private IDisposable PlayChannel(Speech speech, Action completed, Action<Exception> failed)
        {
            if (!_channels.TryGetValue(speech.Key, out var channel)) _channels.Add(speech.Key, channel = _root.CreateSibling());
            var data = speech.Data;
            return channel.Play(new[] { new KksVoicePlayback.Clip { Bundle = data.bundle, Asset = data.asset } }, data.personality, data.pitch,
                speech.Actor, completed, failed, null, data.eco, !data.isNotMoveMouth);
        }
        internal void Stop() => _sequence.Stop();
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            try { _sequence.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            foreach (var channel in _channels.Values) try { channel.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            _channels.Clear();
            if (errors.Any()) throw new AggregateException("KKS grouped voice playback cleanup failed.", errors);
        }
    }
}
