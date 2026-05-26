using System;
using System.IO;
using System.Text;

namespace MainGameObjectComposer
{
    internal sealed class SimpleFileLogger
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly object _sync = new object();
        private string _logPath;

        internal string LogPath => _logPath;

        internal void Initialize(string path)
        {
            _logPath = path;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Write("INFO", "=== session start ===");
        }

        internal void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                return;
            }

            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [" + level + "] " + message + Environment.NewLine;

            try
            {
                lock (_sync)
                {
                    File.AppendAllText(_logPath, line, Utf8NoBom);
                }
            }
            catch
            {
                // ignore file logging failures
            }
        }
    }
}
