using System;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class CameraHostChecks
{
    private sealed class Backend : KksAdvMachine.IHost, KksAdvCameraHost.IBackend
    {
        internal bool Active, Local;
        internal float X, Y;
        internal int Actor, Changes;
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException(context.Entry.Row.Command);
        public void SetLightActive(bool active) { Active = active; Changes++; }
        public void SetLightAngle(bool local, float x, float y) { Local = local; X = x; Y = y; Changes++; }
        public void SetDepthOfFieldTarget(int actor) { Actor = actor; Changes++; }
    }
    private sealed class Assets : KksAdvCoreHost.IAssets
    {
        public string ResolveBundle(string bundle, string loadBundle) => bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start native camera-command defaults, coordinates and lookup checks; renderer mocked");
        var backend = new Backend(); int lookups = zero;
        var camera = new KksAdvCameraHost(backend, backend, value => value == "actor" ? one.ToString() : value == "position" ? "alias" : value,
            name => { lookups++; return name == "alias" ? new KksAdvCameraHost.Vector(one, -one, one) : null; });
        var core = new KksAdvCoreHost(camera, new Assets(), value => value, (bundle, asset) => { }, null, zero);
        using var machine = new KksAdvMachine(core, zero, one);
        KksAdvScript.Row Row(string name, params string[] args) => new KksAdvScript.Row(zero, zero, false, name, args);
        Exception error = null;
        void Run(string command, params string[] args)
        {
            error = null;
            machine.Start(new KksAdvScript("test", "camera", new[] { Row(command, args), Row("Close") }), () => { }, exception => error = exception);
        }
        Run("CameraLightActive"); Require(error == null && backend.Active, "Native light-correction default changed");
        Run("CameraLightActive", bool.FalseString); Require(error == null && !backend.Active, "Light-correction flag was ignored");
        Run("DOFTarget"); Require(error == null && backend.Actor == int.MaxValue, "Default DOF target lost current-character sentinel");
        Run("DOFTarget", "actor"); Require(error == null && backend.Actor == one, "DOF target replacement was lost");
        Run("CameraLightAngle"); Require(error == null && backend.Local && backend.X == zero && backend.Y == zero && lookups == zero, "Missing light coordinates must remain zero");
        Run("CameraLightAngle", bool.FalseString, one.ToString(), (-one).ToString(), "unused-z");
        Require(error == null && !backend.Local && backend.X == one && backend.Y == -one && lookups == zero, "Numeric light coordinates or local/world flag shifted");
        Run("CameraLightAngle", bool.TrueString, "position", "ignored", "ignored");
        Require(error == null && backend.Local && backend.X == one && backend.Y == -one && lookups == one, "Named vector replacement/lookup failed");
        Run("CameraLightAngle", bool.TrueString, "unknown", one.ToString());
        Require(error == null && backend.X == zero && backend.Y == one, "Missing vector alias must fall back to native TryParse coordinates");
        Run("CameraLightAngle", bool.TrueString, one.ToString(), "invalid-y");
        Require(error == null && backend.X == one && backend.Y == zero, "Invalid numeric coordinate must use native zero fallback");
        var before = backend.Changes;
        Run("CameraLightActive", "invalid-bool"); Require(error is FormatException && backend.Changes == before, "Malformed light flag mutated renderer");
        Run("DOFTarget", "invalid-actor"); Require(error is FormatException && backend.Changes == before, "Malformed DOF actor mutated renderer");
        Run("CameraLightAngle", "invalid-local", one.ToString()); Require(error is FormatException && backend.Changes == before, "Malformed angle mode mutated renderer");
        File.WriteAllText(Path.Combine(directory, "adv-camera-host-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production camera-command host and installed native argument defaults; camera/light/DOF renderer mocked",
            currentActorSentinel = true, localWorldAngles = true, vectorDictionaryAndVariableReplacement = true,
            numericCoordinatesDoNotLookup = true, missingInvalidCoordinatesRemainZero = true, ignoredZ = true,
            invalidActorOrFlagRejectedBeforeMutation = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS camera-command argument/default/vector checks; renderer mocked=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
