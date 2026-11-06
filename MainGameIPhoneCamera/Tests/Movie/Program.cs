using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MainGameIPhoneCamera;

internal static partial class Program
{
    private static int _checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private static async Task<string> Run(string executable, string arguments)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        p.Start(); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0) throw new Exception(await error);
        return await output;
    }
    private static async Task Main(string[] args)
    {
        Console.WriteLine("[MovieTests] Testing production state and FFmpeg worker with synthetic image/audio, no game execution.");
        string ffmpeg = args[0], ffprobe = args[1], root = args[2];
        Directory.CreateDirectory(root);
        await OrientationTests(ffmpeg, ffprobe, root);
        var state = new CameraAppState();
        Check(state.Mode == CaptureMode.Photo && !state.BeginRecording(), "photo mode cannot begin video");
        Check(state.Select(CaptureMode.Video) && state.BeginRecording(), "video begins");
        Check(!state.BeginRecording() && !state.Select(CaptureMode.Photo), "duplicate start and mode switch denied");
        Check(state.SetZoom(3) && !state.SetZoom(float.NaN) && state.Zoom == 3, "finite zoom while recording");
        state.BeginSaving(); Check(!state.BeginRecording(), "saving blocks next recording");
        state.Complete(false); Check(state.Select(CaptureMode.Photo), "failure allows recovery");
        state.Reset(); Check(state.Zoom == 1 && state.Phase == CapturePhase.Ready && state.Mode == CaptureMode.Photo, "reset clears session state");
        bool quoted = false; try { MovieWriter.Quote("bad\"path"); } catch (ArgumentException) { quoted = true; }
        Check(quoted, "argument injection rejected");
        var pixels = new byte[64 * 128 * 3];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 64; x++) pixels[(y * 64 + x) * 3 + (y < 64 ? 0 : 2)] = 230;
        string movie = Path.Combine(root, "synthetic " + Guid.NewGuid().ToString("N") + ".mp4");
        var writer = new MovieWriter(ffmpeg, movie, 64, 128, 30, 48000);
        int last = -1, samples = 0;
        while (writer.Seconds < 1.25)
        {
            double now = writer.Seconds;
            int index = (int)(now * 30);
            if (index > last) { Check(writer.FrameBytes(pixels, index), "queue synthetic frame"); last = index; }
            int desired = (int)(now * 48000);
            if (desired > samples)
            {
                int count = desired - samples;
                var audio = new float[count * 2];
                for (int i = 0; i < count; i++) audio[i * 2] = audio[i * 2 + 1] = .2f * (float)Math.Sin(2 * Math.PI * 440 * (samples + i) / 48000);
                writer.AudioSamples(audio, 2); samples = desired;
            }
            await Task.Delay(37); // Deliberately less than 30 fps: test real-time resampling.
        }
        double duration = writer.Seconds;
        var finalization = writer.FinishAsync();
        Check(!writer.FrameBytes(pixels, last + 1), "late video producer rejected after stop");
        writer.AudioSamples(new float[960], 2);
        Check(await finalization == movie && File.Exists(movie), "MP4 completed");
        Check(!Directory.Exists(movie + ".work"), "successful intermediate files removed");
        using var info = JsonDocument.Parse(await Run(ffprobe, "-v error -show_streams -show_format -of json " + MovieWriter.Quote(movie)));
        var streams = info.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.Single(x => x.GetProperty("codec_type").GetString() == "video");
        var sound = streams.Single(x => x.GetProperty("codec_type").GetString() == "audio");
        Check(video.GetProperty("codec_name").GetString() == "h264" && video.GetProperty("width").GetInt32() == 64 && video.GetProperty("height").GetInt32() == 128, "portrait H264 video stream");
        Check(sound.GetProperty("codec_name").GetString() == "aac" && sound.GetProperty("channels").GetInt32() == 2, "stereo AAC game audio stream");
        double encodedDuration = double.Parse(info.RootElement.GetProperty("format").GetProperty("duration").GetString(), System.Globalization.CultureInfo.InvariantCulture);
        Check(Math.Abs(encodedDuration - duration) < .12, "recording duration follows wall clock despite skipped frames");
        string rgb = Path.Combine(root, "first.rgb"), pcm = Path.Combine(root, "sound.f32");
        await Run(ffmpeg, "-v error -y -i " + MovieWriter.Quote(movie) + " -frames:v 1 -f rawvideo -pix_fmt rgb24 " + MovieWriter.Quote(rgb));
        var decoded = File.ReadAllBytes(rgb);
        Check(decoded[2] > decoded[0] + 100 && decoded[decoded.Length - 3] > decoded[decoded.Length - 1] + 100, "Unity bottom-up image encoded upright");
        await Run(ffmpeg, "-v error -y -i " + MovieWriter.Quote(movie) + " -vn -f f32le " + MovieWriter.Quote(pcm));
        var audioBytes = File.ReadAllBytes(pcm); double energy = 0;
        for (int i = 0; i < audioBytes.Length; i += 4) { double v = BitConverter.ToSingle(audioBytes, i); energy += v * v; }
        Check(Math.Sqrt(energy / (audioBytes.Length / 4)) > .08, "encoded audio contains supplied signal");
        string silentPath = Path.Combine(root, "noaudio-" + Guid.NewGuid().ToString("N") + ".mp4");
        var silent = new MovieWriter(ffmpeg, silentPath, 64, 128, 30, 48000);
        Check(silent.FrameBytes(pixels, 0), "missing-audio fixture frame");
        bool missingAudio = false;
        try { await silent.FinishAsync(); } catch (IOException ex) { missingAudio = ex.Message.Contains("音声"); }
        Check(missingAudio && !File.Exists(silentPath), "missing audio reports failure instead of claiming a sound MP4");
        Console.WriteLine("PASS " + _checks + " checks; actual synthetic H264/AAC MP4: " + movie);
    }
}
