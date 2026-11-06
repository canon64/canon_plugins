using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MainGameMagicPaint
{
    internal sealed class DiagnosticTrace : IDisposable
    {
        readonly string _path;
        StreamWriter _writer;
        Dictionary<string, State> _states;
        sealed class State { internal string Value; internal int Changes; }
        internal bool Enabled { get; private set; }
        internal DiagnosticTrace(string path) { _path = path; }
        internal void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (!enabled) { _writer?.Dispose(); _writer = null; _states = null; }
        }
        internal void ResetTransitions() { if (Enabled) _states = null; }
        internal void Write(string category, Func<string> detail)
        {
            if (!Enabled) return;
            string message = detail();
            if (_writer == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
            }
            _writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + category + "] " + message);
        }
        internal void Transition(string key, string signature, Func<string> detail)
        {
            if (!Enabled) return;
            if (_states == null) _states = new Dictionary<string, State>();
            if (!_states.TryGetValue(key, out var state)) _states.Add(key, state = new State());
            if (state.Value == signature) return;
            state.Value = signature;
            // Preserve the first transitions and their instability, without frame-rate log spam.
            if (++state.Changes <= 12) Write(key, detail);
            else if (state.Changes == 13) Write(key, () => "further transitions suppressed until next grip; trigger/stroke summaries remain enabled");
        }
        public void Dispose() => SetEnabled(false);
    }
}
