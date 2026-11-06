using System;

namespace MainGameAiShoujoMode
{
    // Pause playback rather than letting the opening voice finish behind the loading screen.
    internal sealed class HotelLoadingAudioScope : IDisposable
    {
        private readonly Action<bool> _setPaused;
        private readonly bool _previous;
        private bool _disposed;
        internal HotelLoadingAudioScope(Func<bool> readPaused, Action<bool> setPaused)
        {
            _setPaused = setPaused; _previous = readPaused(); _setPaused(true);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _setPaused(_previous);
        }
    }
}
