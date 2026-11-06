using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace MainGameLgsMode
{
    [DataContract]
    public sealed class ModeSettings
    {
        [DataMember] public int Version = 1;
        [DataMember] public bool Enabled = true;
        [DataMember] public bool EnableLogs = false;
        [DataMember] public int FreeHMode = 1;
        [DataMember] public int WalkingMode = 2;
        [DataMember] public int CommandLifetimeSeconds = 15;
        [DataMember] public bool ManualCycleEnabled = true;
        [DataMember] public string ManualCycleKey = "M";
        [OnDeserializing] private void Defaults(StreamingContext context)
        { Version = 1; Enabled = true; FreeHMode = 1; WalkingMode = 2; CommandLifetimeSeconds = 15; ManualCycleEnabled = true; ManualCycleKey = "M"; }
        public void Validate()
        {
            if (Version != 1 || FreeHMode < 0 || FreeHMode > 3 || WalkingMode < 0 || WalkingMode > 3
                || CommandLifetimeSeconds < 1 || CommandLifetimeSeconds > 120)
                throw new SerializationException("Invalid LGS settings; previous configuration retained.");
            ManualHotkey.Validate(ManualCycleKey);
        }
    }

    public sealed class SettingsStore : IDisposable
    {
        private readonly string _path;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounce;
        private readonly object _sync = new object();
        private bool _disposed;
        public ModeSettings Current { get; private set; }
        public event Action<ModeSettings> Changed;
        public event Action<Exception> Error;

        public SettingsStore(string root)
        {
            Directory.CreateDirectory(root);
            _path = Path.Combine(root, "settings.json");
            if (!File.Exists(_path))
            {
                using (var stream = File.Create(_path))
                    new DataContractJsonSerializer(typeof(ModeSettings)).WriteObject(stream, new ModeSettings());
            }
            Current = Read();
            _debounce = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(root, "settings.json")
            { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            _watcher.Changed += Schedule; _watcher.Created += Schedule; _watcher.Renamed += Renamed;
            _watcher.EnableRaisingEvents = true;
        }

        private ModeSettings Read()
        {
            using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var settings = (ModeSettings)new DataContractJsonSerializer(typeof(ModeSettings)).ReadObject(stream);
                if (settings == null) throw new SerializationException("LGS settings are empty.");
                settings.Validate(); return settings;
            }
        }
        private void Schedule(object sender, FileSystemEventArgs args)
        { lock (_sync) { if (!_disposed) _debounce.Change(150, Timeout.Infinite); } }
        private void Renamed(object sender, RenamedEventArgs args) => Schedule(sender, args);
        private void Reload()
        {
            lock (_sync)
            {
                if (_disposed) return;
                try { var next = Read(); Current = next; Changed?.Invoke(next); }
                catch (Exception error) { Error?.Invoke(error); }
            }
        }
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true; _watcher.Dispose(); _debounce.Dispose();
                Changed = null; Error = null;
            }
        }
    }
}
