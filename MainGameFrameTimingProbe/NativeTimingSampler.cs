using System;
using System.Runtime.Serialization;
using UnityEngine.Profiling;

namespace MainGameFrameTimingProbe
{
    internal sealed class NativeTimingSampler : IDisposable
    {
        private static readonly string[] Names = {
            "PlayerLoop", "BehaviourUpdate", "Update.ScriptRunBehaviourUpdate", "Camera.Render",
            "UpdateAllSkinnedMeshes", "Physics.Simulate", "GC.Collect", "Gfx.WaitForPresentOnGfxThread"
        };
        private readonly string[] _names, _errors;
        private readonly Recorder[] _recorders;
        private readonly bool[] _owned, _active;
        private readonly long[] _sum, _max, _blocks;
        private readonly int[] _sampleFrames;
        private int _frames;
        private bool _disposed;
        private bool _paused;

        internal NativeTimingSampler(string[] names = null)
        {
            _names = (string[])(names ?? Names).Clone();
            int count = _names.Length;
            _errors = new string[count]; _recorders = new Recorder[count];
            _owned = new bool[count]; _active = new bool[count];
            _sum = new long[count]; _max = new long[count]; _blocks = new long[count];
            _sampleFrames = new int[count];
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Recorder recorder = _recorders[i] = Recorder.Get(_names[i]);
                    if (recorder == null || !recorder.isValid) { _errors[i] = "marker unavailable"; continue; }
                    _owned[i] = !recorder.enabled;
                    if (_owned[i]) recorder.enabled = true;
                    _active[i] = true;
                }
                catch (Exception ex) { _errors[i] = ex.GetType().Name + ": " + ex.Message; }
            }
        }

        // Preserve the recorder's thread filter. Counts can include multiple threads;
        // nested/overlapping markers must never be summed as a frame budget.
        internal void Sample()
        {
            if (_disposed || _paused) return;
            _frames++;
            for (int i = 0; i < _recorders.Length; i++)
            {
                if (!_active[i]) continue;
                try
                {
                    long blocks = _recorders[i].sampleBlockCount;
                    long elapsed = _recorders[i].elapsedNanoseconds;
                    if (blocks <= 0 || elapsed < 0) continue;
                    _blocks[i] += blocks; _sum[i] += elapsed;
                    _max[i] = Math.Max(_max[i], elapsed); _sampleFrames[i]++;
                }
                catch { _active[i] = false; _errors[i] = "native recorder read failed"; }
            }
        }

        internal NativeMarkerReport[] Snapshot()
        {
            var result = new NativeMarkerReport[_names.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new NativeMarkerReport {
                Marker = _names[i], Error = _errors[i], ObservedFrames = _frames,
                FramesWithSamples = _sampleFrames[i], Blocks = _blocks[i],
                MeanMsPerFrame = _sampleFrames[i] > 0 ? (double?) (_sum[i] / 1000000.0 / _frames) : null,
                MaxMs = _sampleFrames[i] > 0 ? (double?) (_max[i] / 1000000.0) : null
            };
            return result;
        }

        internal void Pause() => SetPaused(true);
        internal void Resume() => SetPaused(false);
        private void SetPaused(bool paused)
        {
            if (_disposed || _paused == paused) return;
            _paused = paused;
            for (int i = 0; i < _recorders.Length; i++)
            {
                if (!_owned[i] || _recorders[i] == null) continue;
                try { _recorders[i].enabled = !paused && _active[i]; }
                catch { _active[i] = false; _errors[i] = "native recorder pause/resume failed"; }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            for (int i = 0; i < _recorders.Length; i++)
            {
                _active[i] = false;
                if (!_owned[i] || _recorders[i] == null) continue;
                try { _recorders[i].enabled = false; _owned[i] = false; }
                catch { _errors[i] = "native recorder restore failed"; }
            }
        }
    }

    [DataContract]
    internal sealed class NativeMarkerReport
    {
        [DataMember] public string Marker, Error;
        [DataMember] public int ObservedFrames, FramesWithSamples;
        [DataMember] public long Blocks;
        [DataMember] public double? MeanMsPerFrame, MaxMs;
    }
}
