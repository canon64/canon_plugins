using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

internal static class Media
{
    public static event Action<string> OnVideoLoaded, OnVideoStarted;
    internal static void Load(string path) => OnVideoLoaded?.Invoke(path);
    internal static void Play(string path) => OnVideoStarted?.Invoke(path);
}
namespace MainGameBeatSyncSpeed
{
    public partial class Plugin
    {
        internal bool SessionActive = true, Enabled = true;
        internal string Current, Ffmpeg;
        internal readonly Dictionary<string, int> Saved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        internal int Saves, Applied, Invalidations;
        private sealed class Setting { internal int Value; }
        private struct Permit { internal bool IsValid; }
        private Permit _sessionPermit = new Permit { IsValid = true };
        private Setting _cfgBpm = new Setting();
        private bool _suppressSongBpmPersist;
        private int _workEpoch, _tapCount;
        private float _lastTapTime;
        private string _pluginDir, _currentSongPathKey;
        private Type _blankMapPluginType = typeof(Media);
        internal Plugin(string dir, string ffmpeg, string current) { _pluginDir = dir; Ffmpeg = ffmpeg; Current = current; }
        private static string NormalizeSongPathKey(string path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        internal int ReadRegisteredBpm(string path) => path != null && Saved.TryGetValue(NormalizeSongPathKey(path), out int value) ? value : 0;
        private bool TryGetVideoFilePath(out string path) { path = Current; return File.Exists(path); }
        private string FindFfmpegPath() => Ffmpeg;
        private void InvalidateAnalysis() { Invalidations++; }
        private void TryApplyToSpeedLimitBreak(int bpm, string reason) { Applied = bpm; }
        private void PersistCurrentSongBpm(int bpm, string reason) { OnBpmManualInput(); Saved[Current] = bpm; Saves++; }
        internal void Begin() => BeginAutoBpmSession();
        internal void Stop() { SessionActive = false; _sessionPermit.IsValid = false; _workEpoch++; EndAutoBpmSession(); }
        internal void Restart() { SessionActive = true; _sessionPermit.IsValid = true; BeginAutoBpmSession(); }
        internal void Manual(int bpm) { OnBpmManualInput(); Saved[Current] = bpm; }
        internal bool Running => _bpmRequest.Running;
        internal void Source(string path) { Current = path; ObserveBpmSong(path); }
        internal void Tap() => OnBpmManualInput();
    }
}

internal sealed class MainContext : SynchronizationContext
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue = new System.Collections.Concurrent.ConcurrentQueue<Action>();
    private readonly AutoResetEvent _ready = new AutoResetEvent(false);
    public override void Post(SendOrPostCallback callback, object state) { _queue.Enqueue(() => callback(state)); _ready.Set(); }
    internal void Complete()
    {
        if (!_ready.WaitOne(15000)) throw new Exception("worker completion timeout");
        while (_queue.TryDequeue(out Action action)) action();
    }
}
