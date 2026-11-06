using System;
using System.IO;
using System.Text;

namespace MainGameLgsMode
{
    // Called only after ModeController checks EnableLogs; OFF never creates this file.
    internal sealed class DiagnosticOutput
    {
        private readonly string _path;
        private readonly Action<string> _commonInfo;
        private readonly Action<Exception> _commonError;
        internal DiagnosticOutput(string root, Action<string> info, Action<Exception> error)
        { _path = Path.Combine(root, "MainGameLgsMode.log"); _commonInfo = info; _commonError = error; }
        internal void Info(string message) { Append(message); _commonInfo(message); }
        internal void Error(Exception error) { Append(error.ToString()); _commonError(error); }
        private void Append(string message)
        {
            try { File.AppendAllText(_path, DateTime.Now.ToString("o") + " " + message + Environment.NewLine, new UTF8Encoding(false)); }
            catch (IOException) { /* Optional diagnostics cannot interrupt mode switching. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
