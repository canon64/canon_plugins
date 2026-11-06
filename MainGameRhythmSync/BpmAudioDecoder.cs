using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MainGameRhythmSync
{
    internal static class BpmAudioDecoder
    {
        internal static float[] Decode(string source, string ffmpeg, string temporary, CancellationToken cancel)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(temporary));
                using (var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg,
                    "-nostdin -v error -y -i \"" + source + "\" -t 90 -vn -ac 1 -ar 22050 -f f32le \"" + temporary + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } })
                {
                    cancel.ThrowIfCancellationRequested();
                    process.Start();
                    using (cancel.Register(() => { try { if (!process.HasExited) process.Kill(); } catch { } }))
                    {
                        var errors = process.StandardError.ReadToEndAsync();
                        if (!process.WaitForExit(120000))
                        {
                            try { process.Kill(); } catch { }
                            process.WaitForExit();
                            throw new TimeoutException("音声の読み出しがタイムアウトしました");
                        }
                        cancel.ThrowIfCancellationRequested();
                        if (process.ExitCode != 0) throw new IOException("音声を読み出せませんでした");
                        errors.GetAwaiter().GetResult();
                    }
                }
                byte[] bytes = File.ReadAllBytes(temporary);
                if (bytes.Length % 4 != 0 || bytes.Length > 22050 * 90 * 4) throw new IOException("音声データが不正です");
                var samples = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                return samples;
            }
            finally { try { File.Delete(temporary); } catch { } }
        }
    }
}
