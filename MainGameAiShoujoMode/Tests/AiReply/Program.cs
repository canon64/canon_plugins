using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MainGameAiShoujoMode.Conversation;

internal static class Program
{
    private static int _checks;
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); _checks++; }
    private static void Reject(Action action, string message)
    { try { action(); } catch { _checks++; return; } throw new Exception(message); }
    private sealed class Lease : IDisposable
    {
        internal bool Disposed;
        public void Dispose() { Disposed = true; }
    }
    private sealed class Backend : AiReplyRun.IBackend
    {
        internal string Revision;
        internal bool RejectAction, Presented, Executed, Finished, FailedFlag, Immediate;
        internal int Executions;
        internal CancellationToken Cancellation;
        internal Action<byte[]> Ready;
        internal Action<Exception> Failure;
        internal Action VoiceEnd;
        internal readonly Lease Voice = new Lease();
        public string Stamp => Revision;
        public void Validate(AiReplyResponse reply) { if (RejectAction) throw new InvalidOperationException(); }
        public void Present(AiReplyResponse reply) { Presented = true; }
        public void Synthesize(string text, CancellationToken cancellation, Action<byte[]> ready, Action<Exception> failed)
        { Ready = ready; Failure = failed; Cancellation = cancellation; if (Immediate) Ready(Array.Empty<byte>()); }
        public IDisposable Play(byte[] bytes, Action completed, Action<Exception> failed)
        { VoiceEnd = completed; if (Immediate) completed(); return Voice; }
        public void Execute(AiReplyResponse reply) { Executed = true; Executions++; }
        public void Completed() { Finished = true; }
        public void Failed(Exception error) { FailedFlag = true; }
    }
    private static byte[] Wave(AiReplySettings p, JsonElement f, bool wrongFormat = false)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(default(int)); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(sizeof(ushort) + sizeof(ushort) + sizeof(int) + sizeof(int) + sizeof(ushort) + sizeof(ushort));
        writer.Write((ushort)(wrongFormat ? f.GetProperty("invalidFormat").GetInt32() : p.one)); writer.Write((ushort)p.one);
        var rate = f.GetProperty("sampleRate").GetInt32(); writer.Write(rate); writer.Write(rate * sizeof(short));
        writer.Write((ushort)sizeof(short)); writer.Write((ushort)(sizeof(short) * p.bitsPerByte));
        writer.Write(Encoding.ASCII.GetBytes("data"));
        var samples = f.GetProperty("samples").EnumerateArray().Select(x => x.GetInt16()).ToArray();
        writer.Write(samples.Length * sizeof(short)); foreach (var sample in samples) writer.Write(sample);
        stream.Position = "RIFF".Length; writer.Write((int)stream.Length - "RIFF".Length - sizeof(int));
        return stream.ToArray();
    }
    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.First());
        var settings = AiReplySettings.Load(Path.Combine(root, "MainGameAiShoujoMode/Content"));
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "MainGameAiShoujoMode/Tests/AiReply/fixtures.json")));
        var f = fixture.RootElement;
        AiReplyResponse Parse(string key) => AiReplyResponse.Parse(f.GetProperty(key).GetRawText(), settings);
        var reply = Parse("reply");
        Require(reply.action == "talk" && reply.target_id == null, "Actual four-field JSON rejected");
        Require(Parse("h").action == "h" && Parse("receiveItem").action == "receive_item", "H or item receive reply was rejected");
        var bedTarget="bed:"+Guid.NewGuid().ToString("N")+"/sleep-slot";
        var bedAction=new AiReplyAction{Action="rest",Target=bedTarget,Label="屋外用ベッドで休む"};
        Require(bedAction.Matches(new AiReplyResponse{action="rest",target_id=bedTarget}),"rest reply accepts exact bed identity");
        Require(!bedAction.Matches(new AiReplyResponse{action="rest",target_id=bedTarget+"other"})&&!bedAction.Matches(new AiReplyResponse{action="rest"}),"bed identity cannot silently fall back to another bed or ground");
        foreach (var key in new[] { "badAction", "badEmotion", "emptyText", "missingField" }) Reject(() => Parse(key), key);
        Require(Parse("injectedState").action == "talk", "Ignored state fields must not become commands");
        var original = f.GetProperty("revision").GetString(); var changed = f.GetProperty("changedRevision").GetString();
        var backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, reply))
        {
            run.Start(); Require(backend.Presented && !backend.Executed, "Action executed before audio");
            backend.Ready(Array.Empty<byte>()); backend.VoiceEnd(); backend.VoiceEnd();
            Require(backend.Finished && backend.Executions == settings.one && backend.Voice.Disposed, "Completion duplicated or voice leaked");
        }
        backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, reply))
        {
            run.Start(); run.Dispose(); backend.Ready(Array.Empty<byte>());
            Require(!backend.Executed && backend.VoiceEnd == null && backend.Cancellation.IsCancellationRequested, "Late synthesis applied after cancel");
        }
        backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, reply))
        {
            run.Start(); backend.Revision = changed; backend.Ready(Array.Empty<byte>());
            Require(backend.FailedFlag && !backend.Executed, "Stale generation applied");
        }
        backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, reply))
        {
            run.Start(); backend.Ready(Array.Empty<byte>()); backend.Revision = changed; backend.VoiceEnd();
            Require(backend.FailedFlag && !backend.Executed && backend.Voice.Disposed, "State changed during speech applied action");
        }
        backend = new Backend { Revision = original, RejectAction = true }; using (var run = new AiReplyRun(backend, reply))
        { run.Start(); Require(backend.FailedFlag && !backend.Presented, "Invalid action produced partial presentation"); }
        backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, reply))
        {
            run.Start(); backend.Failure(new IOException("fixture"));
            Require(backend.FailedFlag && !backend.Executed && backend.Cancellation.IsCancellationRequested, "Synthesis failure executed action");
        }
        backend = new Backend { Revision = original, Immediate = true }; using (var run = new AiReplyRun(backend, reply))
        { run.Start(); Require(backend.Finished && backend.Voice.Disposed, "Synchronous callback leaked returned lease"); }
        backend = new Backend { Revision = original }; using (var run = new AiReplyRun(backend, Parse("follow")))
        {
            run.Start(); backend.Ready(Array.Empty<byte>()); backend.RejectAction = true; backend.VoiceEnd();
            Require(!backend.Executed && backend.FailedFlag, "Action conditions were not revalidated");
        }
        var wave = Wave(settings, f); var decoded = AiReplyWave.Decode(wave, settings);
        Require(decoded.Samples.Length == f.GetProperty("samples").GetArrayLength() && decoded.Frequency == f.GetProperty("sampleRate").GetInt32(), "PCM decode");
        Require(decoded.Samples.First() == -settings.one && decoded.Samples.Last() < settings.one, "PCM signed normalization");
        Reject(() => AiReplyWave.Decode(wave.Take(wave.Length - settings.one).ToArray(), settings), "Truncated WAV accepted");
        Reject(() => AiReplyWave.Decode(Wave(settings, f, true), settings), "Non-PCM WAV accepted");
        var uri = AiReplySbv2.VoiceUri(settings, reply.text);
        Require(Uri.UnescapeDataString(uri.Query).Contains("model_name=" + settings.model) && uri.AbsolutePath == "/voice", "SBV2 request arguments");
        if (args.Contains("--live"))
        {
            var bytes = AiReplySbv2.Generate(settings, reply.text, CancellationToken.None).GetAwaiter().GetResult();
            var actual = AiReplyWave.Decode(bytes, settings);
            Require(actual.Samples.Length > settings.zero, "Live SBV2 returned no samples");
            Console.WriteLine("AI_REPLY_LIVE_SBV2_PASS bytes=" + bytes.Length + " sampleRate=" + actual.Frequency);
        }
        Console.WriteLine("AI_REPLY_PASS checks=" + _checks);
        return default(int);
    }
}
