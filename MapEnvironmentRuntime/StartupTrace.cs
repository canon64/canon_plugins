using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace MapEnvironmentRuntime
{
    // Explicit startup checkpoints, never a polling service. All capture callbacks are lazy.
    public sealed partial class StartupTrace : IDisposable
    {
        [DataContract]
        private sealed class Row
        {
            [DataMember] public string utc, run, mode, stage, details;
            [DataMember] public long sequence;
            [DataMember] public int thread;
        }
        private readonly object _sync = new object();
        private readonly Func<bool> _enabled;
        private readonly Action<Exception> _failed;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(Row));
        private readonly string _run, _mode;
        private FileStream _stream;
        private StreamWriter _writer;
        private long _sequence;
        private volatile bool _stopped;
        public string FilePath { get; private set; }
        public bool Enabled => !_stopped && _enabled();
        public Func<bool> DetailedInventoryEnabled { private get; set; }
        private bool CanCaptureInventory => Enabled && DetailedInventoryEnabled?.Invoke() == true;

        private StartupTrace(Func<bool> enabled, string directory, string mode, Action<Exception> failed)
        {
            _enabled = enabled; _failed = failed; _mode = mode; _run = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture)
                + "-" + Process.GetCurrentProcess().Id + "-" + mode + "-" + _run + ".jsonl");
            _stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false));
        }
        public static StartupTrace Create(Func<bool> enabled, string directory, string mode, Action<Exception> failed = null)
        {
            if (!enabled()) return null;
            StartupTrace trace = null;
            try
            {
                trace = new StartupTrace(enabled, directory, mode, failed);
                trace.Mark("trace.begin");
                return trace;
            }
            catch (Exception ex) { trace?.Dispose(); if (enabled()) failed?.Invoke(ex); return null; }
        }
        public void Own(Action cleanup)
        {
            if (!Enabled) { cleanup(); return; }
            lock (_sync) { if (_stopped) cleanup(); else _cleanup.Add(cleanup); }
        }
        public void Mark(string stage, Func<string> capture = null)
        {
            if (!Enabled) return;
            string details;
            try { details = capture?.Invoke(); }
            catch (Exception ex) { stage += ".capture-failed"; details = ex.ToString(); }
            if (!Enabled) return;
            try
            {
                lock (_sync)
                {
                    if (!Enabled || _writer == null) return;
                    var row = new Row { utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), run = _run,
                        mode = _mode, sequence = ++_sequence, stage = stage, details = details, thread = Thread.CurrentThread.ManagedThreadId };
                    using (var memory = new MemoryStream())
                    {
                        _serializer.WriteObject(memory, row);
                        _writer.WriteLine(Encoding.UTF8.GetString(memory.ToArray()));
                    }
                    _writer.Flush(); _stream.Flush(true);
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        public void Artifact(string stage, Func<IEnumerable<Dictionary<string, string>>> capture)
        {
            if (!CanCaptureInventory) return;
            string file = Path.ChangeExtension(FilePath, Guid.NewGuid().ToString("N") + ".inventory.jsonl");
            Mark(stage + ".inventory.begin", () => file);
            try
            {
                if (!CanCaptureInventory) return;
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                long count = default(long);
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    foreach (var row in capture())
                    {
                        if (!CanCaptureInventory) break;
                        using (var memory = new MemoryStream())
                        {
                            serializer.WriteObject(memory, row);
                            writer.WriteLine(Encoding.UTF8.GetString(memory.ToArray()));
                        }
                        count++;
                    }
                    writer.Flush(); stream.Flush(true);
                }
                Mark(stage + ".inventory.end", () => "rows=" + count + " file=" + file);
            }
            catch (Exception ex) { Mark(stage + ".inventory.failed", () => ex.ToString()); }
        }
        private void Fail(Exception error)
        {
            Dispose();
            try { if (_enabled()) _failed?.Invoke(error); } catch { }
        }
        public void Dispose()
        {
            List<Action> cleanup;
            lock (_sync)
            {
                if (_stopped) return;
                _stopped = true; cleanup = new List<Action>(_cleanup); _cleanup.Clear();
                try { _writer?.Dispose(); } catch { }
                try { _stream?.Dispose(); } catch { }
                _writer = null; _stream = null;
            }
            foreach (var release in cleanup) { try { release(); } catch { } }
        }
    }
}
