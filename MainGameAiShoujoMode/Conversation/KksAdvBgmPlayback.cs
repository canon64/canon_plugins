using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    // Own only this ADV's tracks. The device never mutates Manager.Sound.currentBGM.
    internal sealed class KksAdvBgmPlayback : KksAdvBgmHost.IBackend, IDisposable
    {
        internal interface ITrack : KksAdvBgmHost.ISource, IDisposable
        {
            void Start(float fadeSeconds);
            void FadeOut(float fadeSeconds, Action completed);
        }
        internal interface IDevice : IDisposable
        {
            ITrack Create(Illusion.Game.Utils.Sound.SettingBGM setting);
        }
        private sealed class Playing
        {
            internal string Bundle, Asset;
            internal ITrack Track;
        }
        private readonly IDevice _device;
        private readonly Func<IDisposable> _suspendMusic;
        private readonly float _minimumFade;
        private Playing _current, _old;
        private IDisposable _suspension;
        private bool _disposed;
        internal KksAdvBgmPlayback(IDevice device, Func<IDisposable> suspendMusic, float minimumFade)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _suspendMusic = suspendMusic ?? throw new ArgumentNullException(nameof(suspendMusic)); _minimumFade = minimumFade;
        }
        public KksAdvBgmHost.ISource Play(Illusion.Game.Utils.Sound.SettingBGM setting)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvBgmPlayback));
            var loader = (setting ?? throw new ArgumentNullException(nameof(setting))).loader;
            if (!loader.isAssetEqualPlay && _current != null && _current.Bundle == loader.bundle && _current.Asset == loader.asset) return _current.Track;
            var track = _device.Create(setting);
            if (track == null) return null; // Missing assets leave both the old ADV and island music untouched.
            var acquired = _suspension == null;
            try
            {
                if (acquired) _suspension = _suspendMusic() ?? throw new InvalidOperationException("Island BGM suspension was not acquired.");
                track.Start(Math.Max(_minimumFade, loader.fadeTime));
            }
            catch
            {
                try { track.Dispose(); }
                finally { if (acquired) { var suspension = _suspension; _suspension = null; suspension?.Dispose(); } }
                throw;
            }
            var obsolete = _old; _old = null;
            try { obsolete?.Track.Dispose(); }
            catch { track.Dispose(); throw; }
            var previous = _current;
            _current = new Playing { Bundle = loader.bundle, Asset = loader.asset, Track = track }; _old = previous;
            if (previous != null)
            {
                try
                {
                    previous.Track.FadeOut(Math.Max(_minimumFade, loader.fadeTime), () =>
                    {
                        if (!ReferenceEquals(_old, previous)) return;
                        _old = null; previous.Track.Dispose();
                    });
                }
                catch { if (ReferenceEquals(_old, previous)) { _old = null; previous.Track.Dispose(); } throw; }
            }
            return track;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var current = _current; var old = _old; var suspension = _suspension;
            _current = null; _old = null; _suspension = null;
            var errors = new List<Exception>();
            try { current?.Track.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            try { old?.Track.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            try { _device.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            try { suspension?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Count != default(int)) throw new AggregateException("KKS ADV BGM cleanup failed.", errors);
        }
    }
}
