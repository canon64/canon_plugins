using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace MainGameLgsMode
{
    public interface ICommandMailbox { void Write(int mode, int lifetimeSeconds); }

    public sealed class CommandMailbox : ICommandMailbox
    {
        private readonly string _directory;
        public string CommandPath => Path.Combine(_directory, "command.lua");
        public CommandMailbox(string pluginRoot) { _directory = Path.Combine(pluginRoot, "runtime"); }

        public void Write(int mode, int lifetimeSeconds)
        {
            if (mode < 0 || mode > 3) throw new ArgumentOutOfRangeException(nameof(mode));
            if (lifetimeSeconds < 1 || lifetimeSeconds > 120) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            Directory.CreateDirectory(_directory);
            string token = System.Guid.NewGuid().ToString("N");
            string expires = DateTime.Now.AddSeconds(lifetimeSeconds).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string content = "return {version=1,token=\"" + token + "\",mode=" + mode.ToString(CultureInfo.InvariantCulture)
                + ",expires=\"" + expires + "\"}\n";
            string temporary = Path.Combine(_directory, token + ".tmp");
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            try
            {
                for (int attempt = 0; ; ++attempt)
                {
                    try
                    {
                        if (File.Exists(CommandPath)) File.Replace(temporary, CommandPath, null);
                        else File.Move(temporary, CommandPath);
                        return;
                    }
                    catch (IOException) when (attempt < 3) { Thread.Sleep(5); }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
