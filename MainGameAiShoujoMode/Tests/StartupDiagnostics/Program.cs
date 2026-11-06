using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MapEnvironmentRuntime;

namespace UnityEngine
{
    public class GameObject { }
    public class Camera
    {
        public delegate void CameraCallback(Camera camera);
        public static event CameraCallback onPreCull, onPreRender, onPostRender;
        public static bool Subscribed => onPreCull != null || onPreRender != null || onPostRender != null;
        public void Cull() => onPreCull?.Invoke(this);
        public void Render() => onPreRender?.Invoke(this);
        public void Post() => onPostRender?.Invoke(this);
    }
}
namespace MapEnvironmentRuntime
{
    public sealed partial class StartupTrace
    {
        public static readonly List<string> Captured = new();
        public void CameraCheckpoint(string stage, UnityEngine.Camera camera) => Mark(stage, () => { Captured.Add(stage); return "camera"; });
        public void Snapshot(string stage, UnityEngine.GameObject root = null) => Mark(stage, () => { Captured.Add(stage); return "state"; });
        public void RenderInventory(string stage, UnityEngine.Camera camera) => Mark(stage, () => { Captured.Add(stage); return "native renderer inventory"; });
    }
}
internal static class Program
{
    private sealed class NativeObject { private IntPtr m_CachedPtr = new IntPtr(Convert.ToInt64(true)); }
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    private static string ReadLive(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    private static void Main(string[] args)
    {
        string directory = Path.Combine(args.Single(), Guid.NewGuid().ToString("N"));
        Check(NativeAddressReader.Read(new NativeObject(), typeof(NativeObject)) == "0x1", "cached native address is recorded without dereferencing it");
        Check(NativeAddressReader.Read(null, typeof(NativeObject)) == "null"
            && NativeAddressReader.Read(new object(), typeof(object)) == "unavailable", "null and absent native field are safe");
        bool enabled = false;
        Check(StartupTrace.Create(() => enabled, directory, "off") == null && !Directory.Exists(directory), "default OFF creates no directory or writer");
        enabled = true;
        using var trace = StartupTrace.Create(() => enabled, directory, "on");
        Check(trace != null, "ON creates writer");
        bool detailed = false;
        bool inventoryCollected = false;
        trace.Artifact("default-inventory-off", () => { inventoryCollected = true; throw new Exception("default must not collect"); });
        Check(!inventoryCollected && !Directory.GetFiles(directory, "*.inventory.jsonl").Any(), "detailed inventory defaults OFF without capture or file creation");
        trace.DetailedInventoryEnabled = () => detailed;
        bool collected = false;
        trace.Mark("durable-checkpoint", () => { collected = true; return "before native render"; });
        var rows = ReadLive(trace.FilePath).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        using (var row = JsonDocument.Parse(rows.Last())) Check(collected && row.RootElement.GetProperty("stage").GetString() == "durable-checkpoint", "checkpoint readable while writer remains open");
        trace.Mark("capture-exception", () => throw new InvalidOperationException("capture failed"));
        Check(ReadLive(trace.FilePath).Contains("capture-exception.capture-failed"), "capture failure is recorded without escaping");
        detailed = true;
        trace.Artifact("renderers", () => new[] { new Dictionary<string, string> { { "path", "island/tree" } } });
        string inventory = Directory.GetFiles(directory, "*.inventory.jsonl").Single();
        using (var row = JsonDocument.Parse(File.ReadAllLines(inventory).Single())) Check(row.RootElement.GetProperty("path").GetString() == "island/tree", "inventory is valid independent JSON");
        detailed = false;
        long beforeDetailOff = new FileInfo(trace.FilePath).Length;
        trace.Artifact("detailed-off", () => { inventoryCollected = true; throw new Exception("disabled must not collect"); });
        trace.Mark("phase-still-enabled");
        Check(!inventoryCollected && Directory.GetFiles(directory, "*.inventory.jsonl").Single() == inventory && new FileInfo(trace.FilePath).Length > beforeDetailOff,
            "turning detailed inventory OFF stops collection while phase checkpoints continue");
        int released = default(int);
        trace.Own(() => released++);
        var camera = new UnityEngine.Camera();
        var other = new UnityEngine.Camera();
        trace.ArmNextRender("first-render", camera);
        int captured = StartupTrace.Captured.Count;
        other.Cull(); other.Render(); other.Post();
        Check(StartupTrace.Captured.Count == captured, "probe ignores unrelated cameras");
        camera.Cull(); camera.Render(); camera.Post();
        Check(StartupTrace.Captured.IndexOf("first-render.preRender.renderers") > StartupTrace.Captured.IndexOf("first-render.preRender.begin")
            && StartupTrace.Captured.IndexOf("first-render.preRender.return-to-unity") > StartupTrace.Captured.IndexOf("first-render.preRender.renderers"), "inventory completes before final return-to-Unity checkpoint");
        captured = StartupTrace.Captured.Count;
        camera.Cull(); camera.Render(); camera.Post();
        Check(!UnityEngine.Camera.Subscribed && StartupTrace.Captured.Count == captured, "render hooks remove themselves after first matching events");
        trace.ArmNextRender("old-probe", camera);
        trace.ArmNextRender("replacement", other);
        camera.Cull(); camera.Render(); camera.Post();
        Check(!StartupTrace.Captured.Contains("old-probe.preCull"), "rearming cancels previous probe");
        enabled = false;
        trace.Dispose(); // Same disposal path invoked by ConfigEntry.SettingChanged.
        long length = new FileInfo(trace.FilePath).Length;
        int files = Directory.GetFiles(directory).Length;
        collected = false;
        trace.Mark("off", () => { collected = true; return "must never be collected"; });
        trace.Artifact("off", () => { collected = true; throw new Exception("must never be enumerated"); });
        trace.ArmNextRender("off", camera);
        camera.Cull(); other.Render(); other.Post();
        trace.Dispose();
        Check(!collected && new FileInfo(trace.FilePath).Length == length && Directory.GetFiles(directory).Length == files, "OFF stops capture, formatting, files and writes");
        Check(!UnityEngine.Camera.Subscribed && released == Convert.ToInt32(true), "OFF removes pending hooks and releases resources once");
        Check(StartupTrace.Create(() => enabled, Path.Combine(directory, "persisted-off"), "off") == null, "saved false remains inert on new session");
        enabled = true;
        using (var restarted = StartupTrace.Create(() => enabled, directory, "re-enabled")) Check(restarted != null && restarted.Enabled, "ON again starts fresh session");
        string invalid = Path.Combine(directory, "not-a-directory"); File.WriteAllText(invalid, "existing file");
        bool reported = false;
        Check(StartupTrace.Create(() => enabled, invalid, "failure", _ => reported = true) == null && reported, "writer initialization failure contained");
        Console.WriteLine("STARTUP_DIAGNOSTICS_PASS " + directory);
    }
}
