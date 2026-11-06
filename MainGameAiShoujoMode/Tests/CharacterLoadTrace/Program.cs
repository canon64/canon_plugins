using System;
using System.IO;
using MainGameAiShoujoMode.Conversation;

internal static class Program
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[default(int)]);
        var enabled = false;
        var collection = default(int); var errors = default(int); var cancellations = default(int);
        string Snapshot() { collection++; return "{\"phase\":\"test\"}"; }
        var trace = CharacterLoadTrace.Create(() => enabled, root, error => errors++);
        Check(trace == null && !Directory.Exists(root), "OFF creates no trace or directory");
        enabled = true;
        trace = CharacterLoadTrace.Create(() => enabled, root, error => errors++);
        Check(!Directory.Exists(root), "enabled trace remains lazy before first mark");
        enabled = false; trace.Mark(Snapshot);
        Check(collection == default(int) && !Directory.Exists(root), "OFF bypasses state collection and formatting");
        trace.SetCancellation(() => cancellations++);
        Check(cancellations == Math.Sign(int.MaxValue), "disabled trace immediately cancels delayed work");
        enabled = true;
        trace.Mark(Snapshot);
        Check(File.ReadAllLines(trace.FilePath).Length == Math.Sign(int.MaxValue), "enabled operation writes one snapshot");
        var before = File.ReadAllText(trace.FilePath);
        trace.SetCancellation(() => cancellations++);
        trace.Dispose(); trace.Dispose(); trace.Mark(Snapshot);
        Check(cancellations == Math.Sign(int.MaxValue) + Math.Sign(int.MaxValue), "disposal cancellation runs exactly once");
        Check(collection == Math.Sign(int.MaxValue) && before == File.ReadAllText(trace.FilePath), "disposed trace cannot collect or append");
        trace = CharacterLoadTrace.Create(() => enabled, root, error => errors++);
        trace.Mark(() => throw new InvalidOperationException("synthetic capture failure"));
        Check(errors == Math.Sign(int.MaxValue), "capture error is isolated from caller");
        enabled = false;
        trace.Mark(() => throw new Exception("must never run"));
        Check(errors == Math.Sign(int.MaxValue), "OFF cannot emit capture errors");
        trace.Dispose();
        Console.WriteLine("CHARACTER_LOAD_TRACE_CHECKS_PASS");
    }
}
