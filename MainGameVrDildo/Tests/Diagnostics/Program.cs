using System;
using System.Diagnostics;
using System.IO;
using MainGameVrDildo;
using CanonPlugins.Shared;

Console.WriteLine("START: actual diagnostic writer and performance aggregation");
if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) throw new ArgumentException("Explicit output directory required");
string output = Path.Combine(args[0], Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
var assembly = typeof(PerformanceTrace).Assembly;
string logPath = Path.Combine(output, "MotionDiagnostics.log");
string ReadLive() { using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var reader = new StreamReader(stream); return reader.ReadToEnd(); }
int warnings = 0;
using (var off = new MotionTraceLog(output, assembly, _ => warnings++, false, false))
{ off.Write("OFF", 0, 0, "disabled"); Check(!File.Exists(logPath), "Disabled logging wrote a file"); }
using (var log = new MotionTraceLog(output, assembly, _ => warnings++, true, false))
{
    Check(log.Enabled, "Logger did not open");
    log.Write("PERFORMANCE", 1, 1, "test=true");
    string live = ReadLive();
    Check(live.Contains("START") && live.Contains("sha256="), "Missing startup identity");
    Check(live.Contains("PERFORMANCE"), "Live reader cannot see flushed output");
}
long prior = new FileInfo(logPath).Length;
using (var reopened = new MotionTraceLog(output, assembly, _ => warnings++, true, false))
{ Check(reopened.Enabled && new FileInfo(logPath).Length > prior, "Re-enable erased prior evidence"); }
Check(File.ReadAllText(logPath).Contains("test=true"), "Re-enable lost old records");
using (var failed = new MotionTraceLog(logPath, assembly, _ => warnings++, true, false))
{ Check(!failed.Enabled && warnings == 1, "Write error was not reported"); }
var perf = new PerformanceTrace();
perf.Record(PerformanceStage.Voice, Stopwatch.Frequency / 1000 * 2);
perf.Record(PerformanceStage.Voice, Stopwatch.Frequency / 1000 * 8);
string summary = perf.TakeSummary();
Check(summary.Contains("VoiceCalls=2 avgMs=5.000 maxMs=8.000"), "Incorrect voice mean or maximum");
Check(perf.TakeSummary().Contains("VoiceCalls=0 avgMs=0.000 maxMs=0.000"), "Previous window leaked");
default(PerformanceTrace.Scope).Dispose();
using (perf.Measure(PerformanceStage.NativeVoice)) { }
Check(perf.TakeSummary().Contains("NativeVoiceCalls=1"), "Native voice scope not recorded");
Console.WriteLine("PASS: " + checks + " assertions; live file=" + logPath);
