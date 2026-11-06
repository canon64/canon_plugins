using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameIPhoneCamera
{
    // File/encoder worker only: no Unity objects or gate access on worker/audio threads.
    internal sealed class MovieWriter
    {
        private sealed class Frame { internal byte[] Bytes; internal int Index; }
        private sealed class Audio { internal float[] Samples; internal int Channels; internal double Time; }
        private readonly BlockingCollection<Frame> _frames = new BlockingCollection<Frame>(4);
        private readonly BlockingCollection<Audio> _audio = new BlockingCollection<Audio>(128);
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Process _encoder;
        private readonly Task _videoTask, _audioTask;
        private readonly string _ffmpeg, _work, _videoPath, _audioPath, _output;
        private readonly int _width, _height, _rate, _sampleRate;
        private string _error;
        private string _encoderError = "";
        private int _closing, _audioBlocks;
        private double _duration;
        internal string Error => Volatile.Read(ref _error);
        internal double Seconds => _clock.Elapsed.TotalSeconds;
        internal string Output => _output;
        internal MovieWriter(string ffmpeg, string output, int width, int height, int fps, int sampleRate, int quarterTurns = 0)
        {
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException("FFmpegが見つかりません", ffmpeg);
            if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0 || fps <= 0 || sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            _ffmpeg = ffmpeg; _output = output; _width = width; _height = height; _rate = fps; _sampleRate = sampleRate;
            _work = output + ".work";
            Directory.CreateDirectory(_work);
            _videoPath = Path.Combine(_work, "video.mp4");
            _audioPath = Path.Combine(_work, "audio.wav");
            string args = "-hide_banner -loglevel error -nostdin -f rawvideo -pixel_format rgb24 -video_size " + width + "x" + height +
                " -framerate " + fps + " -i pipe:0 -vf " + CaptureOrientation.Filter(quarterTurns) + " -an -c:v libx264 -preset ultrafast -crf 20 -pix_fmt yuv420p -y " + Quote(_videoPath);
            _encoder = new Process { StartInfo = StartInfo(ffmpeg, args, true) };
            _encoder.ErrorDataReceived += (s, e) => { if (e.Data != null && _encoderError.Length < 8192) _encoderError += e.Data + "\n"; };
            try { _encoder.Start(); _encoder.BeginErrorReadLine(); }
            catch { _encoder.Dispose(); throw; }
            _clock.Restart();
            _videoTask = Task.Run((Action)WriteVideo);
            _audioTask = Task.Run((Action)WriteAudio);
        }
        internal static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new ArgumentException("無効なファイルパスです");
            return "\"" + value + "\"";
        }
        private static ProcessStartInfo StartInfo(string file, string arguments, bool input)
            => new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = input, RedirectStandardError = true };
        private void Fail(Exception ex) => Fail(ex.Message);
        private void Fail(string message) { Interlocked.CompareExchange(ref _error, message, null); }
        internal bool FrameBytes(byte[] pixels, int index)
        {
            if (Volatile.Read(ref _closing) != 0 || Error != null) return false;
            if (pixels == null || pixels.Length != _width * _height * 3) { Fail("録画フレームの大きさが変わりました"); return false; }
            try
            {
                if (_frames.TryAdd(new Frame { Bytes = pixels, Index = Math.Max(0, index) })) return true;
                Fail("映像の保存が追いつかないため録画を停止しました");
            }
            catch (InvalidOperationException) { }
            return false;
        }
        internal void AudioSamples(float[] samples, int channels)
        {
            if (Volatile.Read(ref _closing) != 0 || Error != null) return;
            if (channels < 1 || channels > 8 || samples.Length % channels != 0) { Fail("音声形式が不正です"); return; }
            // Never write files, wait for the encoder, modify samples, or call Unity from this callback.
            var copy = new float[samples.Length]; Array.Copy(samples, copy, copy.Length);
            var block = new Audio { Samples = copy, Channels = channels, Time = Seconds };
            try { if (!_audio.TryAdd(block)) Fail("音声の保存が追いつかないため録画を停止しました"); }
            catch (InvalidOperationException) { }
        }
        private void WriteVideo()
        {
            try
            {
                using (var input = _encoder.StandardInput.BaseStream)
                {
                    byte[] last = null; int written = 0;
                    foreach (var frame in _frames.GetConsumingEnumerable())
                    {
                        if (frame.Index < written) continue;
                        if (last == null) last = frame.Bytes;
                        while (written < frame.Index) { input.Write(last, 0, last.Length); written++; }
                        input.Write(frame.Bytes, 0, frame.Bytes.Length); written++; last = frame.Bytes;
                    }
                    int desired = Math.Max(1, (int)Math.Ceiling(_duration * _rate));
                    while (last != null && written < desired) { input.Write(last, 0, last.Length); written++; }
                    if (last == null) Fail("録画映像がありません");
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private void WriteAudio()
        {
            try
            {
                using (var file = new FileStream(_audioPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new BinaryWriter(file))
                {
                    writer.Write(new byte[44]); int channels = 0; long bytes = 0;
                    foreach (var block in _audio.GetConsumingEnumerable())
                    {
                        if (channels == 0)
                        {
                            channels = block.Channels;
                            // Align the first DSP buffer's beginning to the video clock.
                            double start = Math.Max(0, block.Time - (double)block.Samples.Length / channels / _sampleRate);
                            int silence = (int)Math.Round(start * _sampleRate) * channels;
                            for (int i = 0; i < silence; i++) writer.Write(0f);
                            bytes += (long)silence * 4;
                        }
                        if (block.Channels != channels) throw new InvalidOperationException("録画中に音声チャンネル数が変わりました");
                        var buffer = new byte[block.Samples.Length * 4];
                        Buffer.BlockCopy(block.Samples, 0, buffer, 0, buffer.Length);
                        writer.Write(buffer); bytes += buffer.Length; Interlocked.Increment(ref _audioBlocks);
                        if (bytes > uint.MaxValue - 44) throw new IOException("音声ファイルの上限に達しました");
                    }
                    file.Position = 0;
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((uint)(bytes + 36));
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((ushort)3); writer.Write((ushort)Math.Max(1, channels)); // IEEE float
                    writer.Write(_sampleRate); writer.Write(_sampleRate * Math.Max(1, channels) * 4);
                    writer.Write((ushort)(Math.Max(1, channels) * 4)); writer.Write((ushort)32);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((uint)bytes);
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        internal Task<string> FinishAsync()
        {
            if (Interlocked.Exchange(ref _closing, 1) != 0) throw new InvalidOperationException("録画はすでに停止しています");
            _duration = Math.Max(1.0 / _rate, Seconds);
            _frames.CompleteAdding(); _audio.CompleteAdding();
            return Task.Run(async () =>
            {
                try
                {
                    var writers = Task.WhenAll(_videoTask, _audioTask);
                    if (await Task.WhenAny(writers, Task.Delay(20000)).ConfigureAwait(false) != writers)
                    { TryKill(_encoder); throw new TimeoutException("録画の書き出しがタイムアウトしました"); }
                    await writers.ConfigureAwait(false);
                    if (!_encoder.WaitForExit(15000)) { TryKill(_encoder); throw new TimeoutException("動画エンコードがタイムアウトしました"); }
                    _encoder.WaitForExit();
                    if (_encoder.ExitCode != 0) Fail("動画エンコード失敗：" + _encoderError);
                    if (_audioBlocks == 0) Fail("ゲーム音声を取得できませんでした");
                    if (Error != null) throw new IOException(Error + "（途中データ：" + _work + "）");
                    string ready = Path.Combine(_work, "result.mp4");
                    string args = "-hide_banner -loglevel error -nostdin -i " + Quote(_videoPath) + " -i " + Quote(_audioPath) +
                        " -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -af apad -t " +
                        _duration.ToString("0.000000", CultureInfo.InvariantCulture) + " -movflags +faststart -y " + Quote(ready);
                    using (var mux = new Process { StartInfo = StartInfo(_ffmpeg, args, false) })
                    {
                        mux.Start(); var error = mux.StandardError.ReadToEndAsync();
                        if (!mux.WaitForExit(30000)) { TryKill(mux); throw new TimeoutException("音声付きMP4の保存がタイムアウトしました"); }
                        string message = await error.ConfigureAwait(false);
                        if (mux.ExitCode != 0) throw new IOException("MP4保存失敗：" + message);
                    }
                    if (new FileInfo(ready).Length == 0) throw new IOException("MP4が空です");
                    File.Move(ready, _output);
                    // Once the MP4 exists, a cleanup failure must not turn a saved movie into a reported failure.
                    try { File.Delete(_videoPath); File.Delete(_audioPath); Directory.Delete(_work); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                    return _output;
                }
                finally { TryKill(_encoder); _encoder.Dispose(); }
            });
        }
        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
