using System;
using System.IO;
using System.Text;

namespace MainGameAiShoujoMode.Conversation
{
    // Request-scoped, lazy diagnostics. Never samples or creates files while disabled.
    internal sealed class CharacterLoadTrace : IDisposable
    {
        private readonly Func<bool> _enabled;
        private readonly Action<Exception> _error;
        private Action _cancel;
        private bool _disposed;
        internal readonly string FilePath;
        internal bool Enabled => !_disposed && _enabled();
        private CharacterLoadTrace(Func<bool> enabled, string directory, Action<Exception> error)
        { _enabled = enabled; _error = error; FilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".jsonl"); }
        internal static CharacterLoadTrace Create(Func<bool> enabled, string directory, Action<Exception> error)
            => enabled() ? new CharacterLoadTrace(enabled, directory, error) : null;
        internal void SetCancellation(Action cancel)
        {
            if (!Enabled) { cancel?.Invoke(); return; }
            _cancel = cancel;
        }
        internal void Mark(Func<string> snapshot)
        {
            if (!Enabled) return;
            try
            {
                var text = snapshot();
                if (!Enabled) return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, text + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception error) { if (Enabled) _error?.Invoke(error); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; var cancel = _cancel; _cancel = null; cancel?.Invoke();
        }
    }
}
