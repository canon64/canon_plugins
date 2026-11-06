using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MainGameDevelopmentBridge
{
    // Atomic rename delivers commands. The watcher wakes only for actual commands, never each frame.
    internal sealed class MailboxServer : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Func<Dictionary<string, object>, Task<object>> _dispatch;
        private readonly string _output;
        private readonly HashSet<string> _claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile bool _disposed;
        internal MailboxServer(string root, Func<Dictionary<string, object>, Task<object>> dispatch)
        {
            var input = Path.Combine(root, "in"); _output = Path.Combine(root, "out");
            Directory.CreateDirectory(input); Directory.CreateDirectory(_output); _dispatch = dispatch;
            _watcher = new FileSystemWatcher(input, "*.json") { NotifyFilter = NotifyFilters.FileName };
            _watcher.Created += Created; _watcher.Renamed += Renamed; _watcher.EnableRaisingEvents = true;
            foreach (var file in Directory.GetFiles(input, "*.json")) Queue(file);
        }
        private void Created(object sender, FileSystemEventArgs args) => Queue(args.FullPath);
        private void Renamed(object sender, RenamedEventArgs args) => Queue(args.FullPath);
        private void Queue(string path)
        {
            lock (_claimed) { if (_disposed || !_claimed.Add(path)) return; }
            Task.Run(async () =>
            {
                try
                {
                    var json = new BridgeJson(); object response;
                    try
                    {
                        var request = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                        var result = await _dispatch(request).ConfigureAwait(false);
                        response = new { ok = true, result };
                    }
                    catch (Exception ex) { response = new { ok = false, error = ex.GetBaseException().ToString() }; }
                    if (_disposed) return;
                    var destination = Path.Combine(_output, Path.GetFileName(path)); var temporary = destination + ".tmp";
                    File.WriteAllText(temporary, json.Serialize(response), new UTF8Encoding(false)); File.Move(temporary, destination);
                    File.Delete(path);
                }
                finally { lock (_claimed) _claimed.Remove(path); }
            });
        }
        public void Dispose()
        {
            _disposed = true; _watcher.EnableRaisingEvents = false;
            _watcher.Created -= Created; _watcher.Renamed -= Renamed; _watcher.Dispose();
        }
    }
}
