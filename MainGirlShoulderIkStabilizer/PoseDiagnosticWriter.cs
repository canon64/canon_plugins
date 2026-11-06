using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MainGirlShoulderIkStabilizer
{
    // Explicit opt-in diagnostic evidence, independent of the shared relay's global switch.
    // No timer or open background stream; writes only when an existing IK/event emits a record.
    internal sealed class PoseDiagnosticWriter
    {
        private readonly string path;
        private readonly long maxBytes;
        private readonly Action<string> reportFailure;
        private bool failed;
        internal PoseDiagnosticWriter(string path, Action<string> reportFailure, long maxBytes = 8 * 1024 * 1024)
        { this.path = Path.GetFullPath(path); this.reportFailure = reportFailure; this.maxBytes = maxBytes; }
        internal void Write(string record)
        {
            if (failed) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && new FileInfo(path).Length >= maxBytes)
                {
                    File.WriteAllText(path, DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture) +
                        " SEGMENT reason=size_limit maxBytes=" + maxBytes + Environment.NewLine, new UTF8Encoding(false));
                }
                File.AppendAllText(path, DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture) + " " + record + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                failed = true;
                reportFailure?.Invoke("Shoulder diagnostic file failed: " + path + " : " + ex.Message);
            }
        }
    }
}
