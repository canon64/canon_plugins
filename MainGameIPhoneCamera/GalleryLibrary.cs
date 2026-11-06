using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameIPhoneCamera
{
    internal sealed class GalleryItem
    {
        internal string Path;
        internal bool Video;
        internal DateTime Modified;
        internal string Name => System.IO.Path.GetFileName(Path);
    }
    internal sealed class GalleryScan
    {
        internal GalleryItem[] Items;
        internal string Error;
    }
    internal static class GalleryLibrary
    {
        private static readonly HashSet<string> Images = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Movies = new HashSet<string>(new[] { ".mp4", ".m4v", ".mov", ".webm", ".wmv", ".avi", ".mkv" }, StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim ThumbnailSlots = new SemaphoreSlim(2);
        private static readonly SemaphoreSlim PlaybackSlot = new SemaphoreSlim(1);
        internal static async Task<string> CompatibleVideo(string ffmpeg, string cache, string source, CancellationToken token)
        {
            await PlaybackSlot.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var info = new FileInfo(source);
                string key = info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks + "|h264-aac-v1";
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
                Directory.CreateDirectory(cache);
                string result = System.IO.Path.Combine(cache, hash + ".mp4");
                if (File.Exists(result)) return result;
                string temporary = result + "." + Guid.NewGuid().ToString("N") + ".mp4";
                try
                {
                    using (var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg,
                        "-hide_banner -loglevel error -nostdin -i " + MovieWriter.Quote(source) +
                        " -map 0:v:0 -map 0:a? -vf scale=trunc(iw/2)*2:trunc(ih/2)*2 -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart -y " + MovieWriter.Quote(temporary))
                        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } })
                    {
                        process.Start(); var error = process.StandardError.ReadToEndAsync();
                        using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }))
                        {
                            if (!process.WaitForExit(120000)) { process.Kill(); throw new TimeoutException("動画の互換変換がタイムアウトしました"); }
                            string message = await error.ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (process.ExitCode != 0) throw new IOException(message);
                        }
                    }
                    if (!File.Exists(result)) File.Move(temporary, result);
                    return result;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            finally { PlaybackSlot.Release(); }
        }
        internal static GalleryScan Scan(string capture, string folders, CancellationToken token)
        {
            var items = new Dictionary<string, GalleryItem>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            var sources = new List<Tuple<string, bool>> { Tuple.Create(capture, true) };
            sources.AddRange((folders ?? "").Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => Tuple.Create(x.Trim(), false)));
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.Exists(source.Item1)) { if (!source.Item2) errors.Add("フォルダーなし：" + source.Item1); continue; }
                    foreach (string path in Directory.EnumerateFiles(source.Item1))
                    {
                        token.ThrowIfCancellationRequested();
                        string extension = System.IO.Path.GetExtension(path);
                        if (!Images.Contains(extension) && !Movies.Contains(extension)) continue;
                        if (source.Item2 && !System.IO.Path.GetFileName(path).StartsWith("iPhone_", StringComparison.OrdinalIgnoreCase)) continue;
                        string full = System.IO.Path.GetFullPath(path);
                        items[full] = new GalleryItem { Path = full, Video = Movies.Contains(extension), Modified = File.GetLastWriteTimeUtc(full) };
                    }
                }
                catch (IOException ex) { errors.Add(ex.Message); }
                catch (UnauthorizedAccessException ex) { errors.Add(ex.Message); }
                catch (ArgumentException ex) { errors.Add(ex.Message); }
            }
            return new GalleryScan { Items = items.Values.OrderByDescending(x => x.Modified).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray(), Error = string.Join("\n", errors) };
        }
        internal static async Task<byte[]> Thumbnail(string ffmpeg, string cache, GalleryItem item, CancellationToken token)
        {
            await ThumbnailSlots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(item.Path);
                string key = info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
                Directory.CreateDirectory(cache);
                string result = System.IO.Path.Combine(cache, hash + ".jpg");
                if (!File.Exists(result))
                {
                    string temporary = result + "." + Guid.NewGuid().ToString("N") + ".jpg";
                    try
                    {
                        using (var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg,
                            "-hide_banner -loglevel error -nostdin -i " + MovieWriter.Quote(item.Path) + " -frames:v 1 -vf scale=192:256:force_original_aspect_ratio=decrease -y " + MovieWriter.Quote(temporary))
                            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } })
                        {
                            process.Start(); var error = process.StandardError.ReadToEndAsync();
                            using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }))
                            {
                                if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("サムネイルの取得がタイムアウトしました"); }
                                token.ThrowIfCancellationRequested();
                                if (process.ExitCode != 0) throw new IOException(await error.ConfigureAwait(false));
                            }
                        }
                        if (!File.Exists(result)) File.Move(temporary, result);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                return File.ReadAllBytes(result);
            }
            finally { ThumbnailSlots.Release(); }
        }
    }
}
