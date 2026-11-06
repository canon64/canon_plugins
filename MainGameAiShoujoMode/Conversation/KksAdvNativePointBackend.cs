using System;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvNativePointBackend : KksAdvPointHost.IBackend, IDisposable
    {
        private readonly KksAdvSessionScope _lifetime = new KksAdvSessionScope();
        private readonly KksAdvPointBackend _backend;
        private int _points;
        private bool _disposed;
        internal KksAdvNativePointBackend(MonoBehaviour host, Transform parent, string contentDirectory,
            KksConversationStateStore store, Action<int> setPlayerPoints, int zero, int maximum)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (setPlayerPoints == null) throw new ArgumentNullException(nameof(setPlayerPoints));
            try
            {
                _points = store.ReadPlayer().points;
                var view = _lifetime.Own(new KksPointDisplayView(host, parent, contentDirectory));
                var display = _lifetime.Own(new KksPointDisplayPlayback(view, _points));
                _backend = new KksAdvPointBackend(store, value => { _points = value; setPlayerPoints(value); },
                    (change, award, completed, failed) => display.Present(_points, award, completed, failed), zero, maximum);
            }
            catch { _lifetime.Dispose(); throw; }
        }
        public IDisposable Add(KksConversationStateStore.PointChange change, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvNativePointBackend));
            return _backend.Add(change, completed, failed);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _lifetime.Dispose(); }
    }
}
