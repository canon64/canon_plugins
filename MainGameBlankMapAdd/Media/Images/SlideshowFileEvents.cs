using System;
using System.IO;
using System.Threading;

namespace MainGameBlankMapAdd.Media.Images
{
    // Coalesced OS notifications; no timer, directory polling, or Unity calls on workers.
    internal sealed class SlideshowFileEvents : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action<Action> _post;
        private readonly Action _changed;
        private volatile bool _disposed;
        private int _queued;

        internal SlideshowFileEvents(string folder, bool recursive, Action<Action> post, Action changed)
        {
            _post = post; _changed = changed;
            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Created += Changed; _watcher.Changed += Changed;
            _watcher.Deleted += Changed; _watcher.Renamed += Renamed;
            _watcher.Error += Failed;
            try { _watcher.EnableRaisingEvents = true; }
            catch { _watcher.Dispose(); throw; }
        }
        private void Changed(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath);
            if (string.IsNullOrEmpty(ext) || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)) Signal();
        }
        private void Renamed(object sender, RenamedEventArgs e) { Changed(sender, e); }
        private void Failed(object sender, ErrorEventArgs e) { Signal(); }
        private void Signal()
        {
            if (_disposed || Interlocked.Exchange(ref _queued, 1) != 0) return;
            _post(() =>
            {
                Interlocked.Exchange(ref _queued, 0);
                if (!_disposed) _changed();
            });
        }
        public void Dispose()
        {
            _disposed = true;
            _watcher.Dispose();
        }
    }
}
