using System;
using System.IO;
using System.Text;

namespace MainGameDisplayEffectViewer
{
    internal sealed class SimpleFileLogger : IDisposable
    {
        private readonly object _gate = new object();
        private StreamWriter _writer;
        private bool _autoFlush;

        internal void Initialize(string path, bool autoFlush)
        {
            lock (_gate)
            {
                DisposeNoLock();

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, new UTF8Encoding(false));
                _autoFlush = autoFlush;
                _writer.AutoFlush = _autoFlush;
            }
        }

        internal void SetAutoFlush(bool autoFlush)
        {
            lock (_gate)
            {
                _autoFlush = autoFlush;
                if (_writer != null)
                    _writer.AutoFlush = _autoFlush;
            }
        }

        internal void Write(string level, string message)
        {
            lock (_gate)
            {
                if (_writer == null)
                    return;

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " [" + (level ?? "INFO") + "] "
                    + (message ?? string.Empty);
                _writer.WriteLine(line);
                if (_autoFlush)
                    _writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                DisposeNoLock();
            }
        }

        private void DisposeNoLock()
        {
            if (_writer == null)
                return;

            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch
            {
                // ignore
            }

            _writer = null;
        }
    }
}
