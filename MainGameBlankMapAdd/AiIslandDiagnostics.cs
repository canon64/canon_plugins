using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MainGameFrameTimingProbe;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    // Opt-in, bounded capture. No disk writes or camera/recorder callbacks remain after Dispose.
    internal sealed class AiIslandDiagnostics : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly NativeTimingSampler _native;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly Dictionary<Camera, int> _cameras = new Dictionary<Camera, int>();
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly float _duration, _interval;
        private long _previous, _lastReport;
        private double _sum, _max, _engineStamp, _cpu, _gpu;
        private int _frames, _engineSamples;
        private bool _disposed, _engineAvailable = true;
        internal bool Completed { get; private set; }
        internal string Path { get; }
        internal AiIslandDiagnostics(string directory, float duration, float interval)
        {
            _duration = duration; _interval = interval;
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, "AiIsland.Performance.log");
            // Keep at most this capture plus the previous bounded log.
            if (File.Exists(Path) && new FileInfo(Path).Length >= 4 * 1024 * 1024)
            {
                string previous = Path + ".previous";
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(Path, previous);
            }
            _writer = new StreamWriter(Path, true, System.Text.Encoding.UTF8);
            try
            {
                _native = new NativeTimingSampler();
                _previous = _lastReport = _started;
                Camera.onPreCull += CameraRendered;
                _writer.WriteLine("START " + DateTime.Now.ToString("o") + " durationSeconds=" + duration +
                    " intervalSeconds=" + interval + " counts=renderer candidates, NOT measured draw calls; native markers are cumulative since START and may overlap");
                _writer.Flush();
            }
            catch
            {
                Camera.onPreCull -= CameraRendered;
                _native?.Dispose(); _writer.Dispose();
                throw;
            }
        }
        private void CameraRendered(Camera camera)
        {
            if (_disposed || camera == null) return;
            _cameras.TryGetValue(camera, out int count); _cameras[camera] = count + 1;
        }
        internal void Tick(AiIslandVisibility visibility, Camera camera, Terrain terrain, string settings)
        {
            if (_disposed || Completed) return;
            long now = Stopwatch.GetTimestamp();
            double frame = (now - _previous) * 1000.0 / Stopwatch.Frequency;
            _previous = now; _sum += frame; _max = Math.Max(_max, frame); _frames++;
            _native.Sample();
            if (_engineAvailable)
            {
                try
                {
                    if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].cpuTimeFrameComplete > 0 &&
                        _timing[0].cpuTimeFrameComplete != _engineStamp)
                    {
                        _engineStamp = _timing[0].cpuTimeFrameComplete;
                        _cpu += _timing[0].cpuFrameTime; _gpu += _timing[0].gpuFrameTime; _engineSamples++;
                    }
                    FrameTimingManager.CaptureFrameTimings();
                }
                catch { _engineAvailable = false; }
            }
            bool finished = (now - _started) / (double)Stopwatch.Frequency >= _duration;
            if (!finished && (now - _lastReport) / (double)Stopwatch.Frequency < _interval) return;
            _lastReport = now;
            _writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "SAMPLE {0:o} fps={1:F1} avgFrameMs={2:F2} maxFrameMs={3:F2} focused={4} vsync={5} targetFps={6} engineSamples={7} cpuMs={8} gpuMs={9} {10}",
                DateTime.Now, _sum > 0 ? _frames * 1000.0 / _sum : 0, _frames > 0 ? _sum / _frames : 0, _max,
                UnityEngine.Application.isFocused, QualitySettings.vSyncCount, UnityEngine.Application.targetFrameRate, _engineSamples,
                _engineSamples > 0 ? (_cpu / _engineSamples).ToString("F2", CultureInfo.InvariantCulture) : "unavailable",
                _engineSamples > 0 && _gpu > 0 ? (_gpu / _engineSamples).ToString("F2", CultureInfo.InvariantCulture) : "unavailable", settings));
            _writer.WriteLine("CULL " + visibility.Snapshot());
            if (camera != null) _writer.WriteLine("MAIN camera=" + camera.name + " position=" + camera.transform.position + " farClip=" + camera.farClipPlane);
            if (terrain != null) _writer.WriteLine("TERRAIN draw=" + terrain.drawHeightmap + " foliage=" + terrain.drawTreesAndFoliage +
                " pixelError=" + terrain.heightmapPixelError + " basemapDistance=" + terrain.basemapDistance);
            foreach (var pair in _cameras)
                if (pair.Key != null) _writer.WriteLine("CAMERA name=" + pair.Key.name + " renders=" + pair.Value +
                    " pixels=" + pair.Key.pixelWidth + "x" + pair.Key.pixelHeight + " far=" + pair.Key.farClipPlane);
            foreach (var marker in _native.Snapshot())
                _writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "NATIVE {0} avgMs={1} maxMs={2} blocks={3} error={4}",
                    marker.Marker, marker.MeanMsPerFrame, marker.MaxMs, marker.Blocks, marker.Error));
            _writer.Flush();
            _cameras.Clear(); _sum = _max = _cpu = _gpu = 0; _frames = _engineSamples = 0;
            Completed = finished || _writer.BaseStream.Length >= 4 * 1024 * 1024;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Camera.onPreCull -= CameraRendered;
            _native.Dispose();
            try { _writer.WriteLine("STOP " + DateTime.Now.ToString("o")); }
            finally { _writer.Dispose(); _cameras.Clear(); }
        }
    }
}
