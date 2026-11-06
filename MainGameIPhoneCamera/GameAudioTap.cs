using System.Threading;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    // Added to the active, existing AudioListener only for an explicit recording.
    internal sealed class GameAudioTap : MonoBehaviour
    {
        internal System.Action Lost;
        private MovieWriter _sink;
        internal void Bind(MovieWriter sink) => Volatile.Write(ref _sink, sink);
        private void OnAudioFilterRead(float[] data, int channels) => Volatile.Read(ref _sink)?.AudioSamples(data, channels);
        private void OnDisable() { Bind(null); Lost?.Invoke(); }
        private void OnDestroy() => Bind(null);
    }
}
