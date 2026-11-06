using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class EventCgHostChecks
{
    private sealed class Backend : KksAdvMachine.IHost, KksAdvEventCgHost.IBackend
    {
        internal readonly List<string> Trace = new List<string>();
        internal bool Loaded, FailLoad, FailInit;
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException(context.Entry.Row.Command);
        public bool Release() { Trace.Add("release:" + Loaded); var loaded = Loaded; Loaded = false; return loaded; }
        public void Load(string bundle, string asset) { Trace.Add("load:" + bundle + "/" + asset); if (FailLoad) throw new IOException("load"); Loaded = true; }
        public void SetCameraCorrection(bool active) => Trace.Add("camera:" + active);
        public void Initialize() { Trace.Add("initialize"); if (FailInit) throw new IOException("initialize"); }
        public void ResetHeroineMotions() => Trace.Add("default-motions");
        public void StartCrossFade() => Trace.Add("crossfade");
        public void Next(int scene) { Trace.Add("next:" + scene); if (!Loaded) throw new InvalidOperationException("no CG"); }
    }
    private sealed class Assets : KksAdvCoreHost.IAssets
    {
        public string ResolveBundle(string bundle, string loadBundle) => bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start EventCG host native ordering and conditional release checks; CG objects mocked");
        var backend = new Backend();
        var cg = new KksAdvEventCgHost(backend, backend, value => value == "scene" ? one.ToString() : value == "asset-var" ? "sceneA" : value);
        var core = new KksAdvCoreHost(cg, new Assets(), value => value, (bundle, asset) => { }, null, zero);
        using var machine = new KksAdvMachine(core, zero, one);
        KksAdvScript.Row Row(string command, params string[] args) => new KksAdvScript.Row(zero, zero, false, command, args);
        Exception error = null;
        void Run(string command, params string[] args)
        {
            backend.Trace.Clear(); error = null;
            machine.Start(new KksAdvScript("test", "cg", new[] { Row(command, args), Row("Close") }), () => { }, exception => error = exception);
        }
        Run("EventCGSetting", "bundle", "asset-var");
        Require(error == null && backend.Trace.SequenceEqual(new[] { "release:False", "load:bundle/sceneA", "camera:False", "initialize" }), "Setting order or argument replacement changed");
        Run("EventCGNext", "scene"); Require(error == null && backend.Trace.SequenceEqual(new[] { "crossfade", "next:" + one }), "Next must crossfade before changing scene");
        Run("EventCGNext"); Require(error == null && backend.Trace.Last() == "next:" + zero, "Next default scene changed");
        Run("EventCGRelease"); Require(error == null && backend.Trace.SequenceEqual(new[] { "release:True", "default-motions", "camera:True" }), "Release must restore objects before default motion and camera");
        Run("EventCGRelease", "bad-flag"); Require(error == null && backend.Trace.SequenceEqual(new[] { "release:False" }), "Empty release parsed an unused flag or changed camera");
        backend.Loaded = true; Run("EventCGRelease", bool.TrueString);
        Require(error == null && backend.Trace.SequenceEqual(new[] { "release:True", "camera:True" }), "Motion continuation reset heroine motions");
        backend.Loaded = true; Run("EventCGRelease", "bad-flag");
        Require(error is FormatException && !backend.Loaded && backend.Trace.SequenceEqual(new[] { "release:True" }), "Malformed release flag must fail after object restore");
        Run("EventCGNext", "invalid-scene"); Require(error is FormatException && !backend.Trace.Any(), "Malformed scene triggered crossfade");
        Run("EventCGNext"); Require(error is InvalidOperationException && backend.Trace.First() == "crossfade", "Next with no CG silently succeeded");
        backend.Loaded = true; backend.FailLoad = true; Run("EventCGSetting", "missing", "missing");
        Require(error is IOException && !backend.Loaded && backend.Trace.SequenceEqual(new[] { "release:True", "load:missing/missing" }), "Failed load retained the previous CG or initialized a missing object");
        backend.FailLoad = false; backend.FailInit = true; Run("EventCGSetting", "bundle", "asset-var");
        Require(error is IOException && backend.Loaded && backend.Trace.Last() == "initialize", "Initialization failure was hidden");
        File.WriteAllText(Path.Combine(directory, "adv-eventcg-host-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production EventCG host and native argument defaults; CG assets, camera, actors, items and crossfade mocked",
            releaseBeforeLoad = true, cameraBeforeInitialization = true, releaseBeforeFlagParse = true,
            emptyReleaseDoesNotParseFlag = true, continueMotion = true, crossfadeBeforeNext = true,
            defaultSceneAndVariables = true, loadAndInitializationFailuresPropagate = true,
            nativeObjectBackendImplemented = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS EventCG host ordering and failure checks; CG objects mocked=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
