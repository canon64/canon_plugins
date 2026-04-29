using System;
using System.IO;
using System.Text;

namespace MainGameCharacterAfterimage
{
    internal sealed class SimpleFileLogger
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly object _sync = new object();
        private string _path;

        internal string Path => _path;

        internal void Initialize(string path)
        {
            _path = path;
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Write("INFO", "=== session start ===");
        }

        internal void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(_path))
            {
                return;
            }

            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [" + level + "] " + message + Environment.NewLine;
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(_path, line, Utf8NoBom);
                }
            }
            catch
            {
                // ignore file logging failure
            }
        }
    }
}
