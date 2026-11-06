using System;
using System.Globalization;
using System.IO;
using System.Threading;
using MainGameRhythmSync;
using Plugin = MainGameBeatSyncSpeed.Plugin;

internal static class Program
{
    private static int checks;
    private static void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks++; }
    private static void Main(string[] args)
    {
        if (args[0] == "analyze")
        {
            var bytes = File.ReadAllBytes(args[1]); var audio = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, audio, 0, bytes.Length);
            Console.WriteLine(AudioBpmAnalyzer.Analyze(audio, CancellationToken.None)); return;
        }
        if (args[0] == "scores")
        {
            var bytes = File.ReadAllBytes(args[1]); var onset = new int[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, onset, 0, bytes.Length);
            foreach (double score in AudioBpmAnalyzer.Scores(onset, CancellationToken.None)) Console.WriteLine(score.ToString("R", CultureInfo.InvariantCulture));
            return;
        }
        string ffmpeg = args[0], folder = Path.GetFullPath(args[1]), a = Path.Combine(folder, "beat120.wav"), b = Path.Combine(folder, "beat138.wav");
        var context = new MainContext(); SynchronizationContext.SetSynchronizationContext(context);
        var plugin = new Plugin(folder, ffmpeg, a); plugin.Saved[a] = 107; plugin.Begin();
        Check(!plugin.Running && plugin.Saves == 0 && plugin.Applied == 107, "registered song loads without analysis");
        Media.Load(a); Media.Play(a);
        Check(!plugin.Running, "repeated media events never analyze registered song");
        Check(plugin.RequestBpmAnalysis(a), "button explicitly reanalyzes registered song");
        Check(!plugin.RequestBpmAnalysis(a), "duplicate button request rejected");
        context.Complete();
        Check(plugin.Saves == 1 && plugin.Saved[a] == 120 && plugin.Applied == 120, "explicit analysis updates and publishes");
        plugin.Source(b); Check(plugin.Running, "unregistered song automatically starts");
        context.Complete(); Check(plugin.Saved[b] == 138, "unregistered song automatically saved");
        Check(plugin.RequestBpmAnalysis(b), "manual reanalysis starts"); plugin.Manual(111); context.Complete();
        Check(plugin.Saved[b] == 111, "manual correction wins over asynchronous result");
        Check(plugin.RequestBpmAnalysis(b), "second reanalysis starts"); plugin.Tap(); context.Complete();
        Check(plugin.Saved[b] == 111, "first Ctrl tap cancels before final BPM exists");
        Check(plugin.RequestBpmAnalysis(b), "third reanalysis starts"); plugin.Source(a); context.Complete();
        Check(plugin.Saved[a] == 120 && plugin.Saved[b] == 111, "song change discards old result");
        Check(plugin.RequestBpmAnalysis(a), "fourth reanalysis starts"); plugin.Stop(); context.Complete();
        Check(plugin.Saved[a] == 120 && !plugin.RequestBpmAnalysis(a), "session stop discards old result and refuses API");
        plugin.Restart(); Check(plugin.RequestBpmAnalysis(a), "new session can explicitly analyze");
        plugin.Stop(); plugin.Restart(); context.Complete();
        Check(plugin.Saved[a] == 120 && !plugin.Running, "old completion cannot update next H session"); plugin.Stop();
        plugin = new Plugin(folder, ffmpeg, b); plugin.Begin(); plugin.Saved[b] = 222; context.Complete();
        Check(plugin.Saved[b] == 222 && plugin.Saves == 0, "automatic completion rechecks newly registered value"); plugin.Stop();
        plugin = new Plugin(folder, ffmpeg, Path.Combine(folder, "silence.wav")); plugin.Begin(); context.Complete();
        Check(plugin.Saves == 0 && plugin.BpmAnalysisStatus(plugin.Current).StartsWith("解析失敗"), "silence fails without saving fallback BPM");
        Media.Play(plugin.Current); Check(!plugin.Running, "failed song is not automatically retried"); plugin.Stop();
        plugin = new Plugin(folder, null, a); plugin.Begin(); Check(!plugin.Running && plugin.Saves == 0, "missing decoder does not save"); plugin.Stop();
        plugin = new Plugin(folder, ffmpeg, a) { SessionActive = false }; plugin.Begin();
        Check(!plugin.Running && !plugin.RequestBpmAnalysis(a), "denied session cannot analyze"); plugin.Stop();
        foreach (int width in new[] { 640, 720, 980, 1280, 1400, 1920 })
        {
            bool separate = MainGameBlankMapAdd.PlaybackTransportLayout.SeparateAudio(width);
            double available = separate ? width - 12 : width - 12 - (width - 18) / 4d * 1.25 - 12;
            double fixedWidth = available < 500 ? 76+64+80+34+38+65+60+28 : 90+64+80+58+72+110+60+28;
            Check(available - fixedWidth >= 12, "BPM button and audio controls fit " + width);
        }
        var cancel = new CancellationToken(true);
        try { AudioBpmAnalyzer.Analyze(new float[22050 * 8], cancel); throw new Exception("cancellation ignored"); } catch (OperationCanceledException) { checks++; }
        Console.WriteLine("PASS " + checks + " BPM analysis/controller/layout checks");
    }
}
