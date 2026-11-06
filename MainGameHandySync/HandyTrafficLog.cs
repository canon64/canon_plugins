using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MainGameHandySync
{
    internal static class HandyTrafficLog
    {
        private static readonly object Gate = new object();
        private static Func<bool> _enabled;
        private static Func<string, string> _redactSecrets;
        private static string _path;
        private static readonly Lazy<Regex> SecretFields = new Lazy<Regex>(() => new Regex(
            "(\"(?:token|access_token|refresh_token|apikey|api_key|connection_key|connectionKey|Authorization|X-Api-Key|X-Connection-Key|renew)\"\\s*:\\s*)\"(?:\\\\.|[^\"\\\\])*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));

        internal static bool Enabled => _enabled?.Invoke() == true;
        internal static void Configure(string path, Func<bool> enabled, Func<string, string> redactSecrets)
        { lock (Gate) { _path = path; _enabled = enabled; _redactSecrets = redactSecrets; } }

        internal static void Record(string kind, Func<string> data)
        {
            if (!Enabled) return;
            try
            {
                string value = data();
                value = SecretFields.Value.Replace(value, "$1\"[redacted]\"");
                if (_redactSecrets != null) value = _redactSecrets(value);
                lock (Gate)
                {
                    if (!Enabled || string.IsNullOrEmpty(_path)) return;
                    File.AppendAllText(_path, "{\"time\":" + Json(DateTimeOffset.Now.ToString("O"))
                        + ",\"kind\":" + Json(kind) + ",\"data\":" + Json(value) + "}" + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch { /* Recording must never change the motor command's result. */ }
        }

        private static string Json(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4"));
                else result.Append(c);
            }
            return result.Append('"').ToString();
        }
    }
}
