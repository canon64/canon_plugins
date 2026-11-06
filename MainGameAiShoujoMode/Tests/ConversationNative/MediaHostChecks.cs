using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class MediaHostChecks
{
    private sealed class Lease : IDisposable { public void Dispose() { } }
    private sealed class Immediate : KksAdvMachine.ICommand
    {
        private readonly Action _action;
        internal Immediate(Action action = null) { _action = action; }
        public void Start(Action completed, Action<Exception> failed) { _action?.Invoke(); completed(); }
        public void End(bool processEnd) { }
    }
    private sealed class Backend : KksAdvMediaHost.IBackend, KksAdvActorHost.IBackend, KksAdvBgmHost.IBackend, KksAdvBgmHost.ISource, KksAdvSceneFadeHost.IBackend, KksAdvCameraHost.IBackend, KksAdvEventCgHost.IBackend, KksAdvPointHost.IBackend, KksAdvCrossFadeHost.IBackend, IDisposable
    {
        private readonly KksVoiceGroupSequence<ADV.Commands.Base.Voice.Data> _sequence;
        private readonly Manager.Game.Expression _template = new Manager.Game.Expression();
        private readonly KksVoiceBundleContext _bundles = new KksVoiceBundleContext();
        private readonly int _one;
        internal int VoiceRecords, VoiceStarts, Captions, Expressions, Motions, MotionWaits, Clears;
        internal readonly List<string> Trace = new List<string>();
        internal readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>();
        internal string LastColorKey, LastName, LastText;
        internal int EyePatterns, NeckPatterns, EyeTargets, NeckTargets, DefaultMotions, LastActor, LastPattern;
        internal float LastNeckRate;
        internal KksAdvActorHost.LookTarget LastTarget;
        internal int BgmPlays;
        internal int SceneFades, SceneFadeEnds;
        internal int LightActiveChanges, LightAngles, DepthTargets;
        internal int CgLoads, CgInitializations, CgReleaseCalls, CgNexts, CgCrossFades;
        internal bool CgLoaded;
        internal int PointChanges, PointRequested;
        internal int CrossFades, CrossFadeNaturalEnds;
        private sealed class CrossFadeLease : KksAdvCrossFadeHost.IOperation
        {
            internal Action Ended;
            public void End(bool processEnd) { if (!processEnd) throw new Exception("Fixture unexpectedly forced CrossFade"); var end = Ended; Ended = null; end?.Invoke(); }
            public void Dispose() { }
        }
        public KksAdvCrossFadeHost.IOperation Start(float duration, Action completed, Action<Exception> failed)
        { CrossFades++; completed(); return new CrossFadeLease { Ended = () => CrossFadeNaturalEnds++ }; }
        public IDisposable Add(KksConversationStateStore.PointChange change, Action completed, Action<Exception> failed)
        { PointChanges++; PointRequested += change.delta; completed(); return new Lease(); }
        internal readonly HashSet<(string Bundle, string Asset)> CgAssets = new HashSet<(string, string)>();
        public bool Release() { CgReleaseCalls++; var loaded = CgLoaded; CgLoaded = false; return loaded; }
        public void Load(string bundle, string asset) { CgLoads++; CgAssets.Add((bundle, asset)); CgLoaded = true; }
        public void SetCameraCorrection(bool active) { }
        public void Initialize() { CgInitializations++; }
        public void ResetHeroineMotions() { }
        public void StartCrossFade() { CgCrossFades++; }
        public void Next(int scene) { if (!CgLoaded) throw new InvalidOperationException("CG was not loaded"); CgNexts++; }
        public void SetLightActive(bool active) { LightActiveChanges++; }
        public void SetLightAngle(bool local, float x, float y) { LightAngles++; }
        public void SetDepthOfFieldTarget(int actor) { DepthTargets++; }
        public bool IsFading => false;
        private sealed class FadeLease : KksAdvSceneFadeHost.IOperation
        {
            internal Action End;
            public void Finish() { var end = End; End = null; end?.Invoke(); }
            public void Dispose() { }
        }
        public KksAdvSceneFadeHost.IOperation StartFade(bool fadeIn, Action completed, Action<Exception> failed)
        { SceneFades++; completed(); return new FadeLease { End = () => SceneFadeEnds++ }; }
        public void SetTime(float duration) { }
        public void SetColor(string color) { }
        internal readonly HashSet<(string Bundle, string Asset)> BgmAssets = new HashSet<(string, string)>();
        internal Illusion.Game.Utils.Sound.SettingBGM LastBgm;
        internal bool MissingBgm;
        internal readonly List<string> BgmProperties = new List<string>();
        public float Pitch { set { BgmProperties.Add("pitch:" + value); } }
        public float PanStereo { set { BgmProperties.Add("pan:" + value); } }
        public float Time { set { BgmProperties.Add("time:" + value); } }
        public float Volume { set { BgmProperties.Add("volume:" + value); } }
        public KksAdvBgmHost.ISource Play(Illusion.Game.Utils.Sound.SettingBGM setting)
        { BgmPlays++; LastBgm = setting; BgmAssets.Add((setting.loader.bundle, setting.loader.asset)); return MissingBgm ? null : this; }
        internal Backend(int zero, int one)
        {
            _one = one;
            _sequence = new KksVoiceGroupSequence<ADV.Commands.Base.Voice.Data>(voice => Tuple.Create(voice.no, voice.personality), (voice, completed, failed) =>
            { VoiceStarts++; Trace.Add("voice:" + voice.asset); completed(); return new Lease(); }, zero);
        }
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { Clears++; }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException("No fixture command handler: " + context.Entry.Row.Command);
        public void ResolveVoice(ADV.Commands.Base.Voice.Data voice)
        {
            if (!voice.usePersonality) voice.personality = voice.no;
            if (!voice.usePitch) voice.pitch = _one;
            voice.bundle = _bundles.Resolve(voice.personality, voice.bundle); VoiceRecords++;
        }
        public Manager.Game.Expression FindExpression(int actor, string name) => int.TryParse(name.Split(',').First(), out _) ? null : _template;
        public string[] FindMotionAlias(string name) => Aliases.TryGetValue(name, out var alias) ? alias : null;
        public void ApplyExpressions(IReadOnlyList<ADV.Commands.Base.Expression.Data> expressions)
        { foreach (var expression in expressions) { Expressions++; Trace.Add("expression:" + expression.no); } }
        public void ApplyMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions)
        { foreach (var motion in motions) { Motions++; Trace.Add("motion:" + motion.stateName); } }
        public IDisposable WaitMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions, Action completed, Action<Exception> failed)
        { MotionWaits++; completed(); return new Lease(); }
        public IDisposable PlayVoices(IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices, Action changed, Action completed, Action<Exception> failed)
            => _sequence.Play(voices, changed, completed, failed, null);
        public void PresentText(string colorKey, string name, string text, IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices)
        { LastColorKey = colorKey; LastName = name; LastText = text; Captions++; }
        public void LookEyes(int actor, int pattern) { EyePatterns++; LastActor = actor; LastPattern = pattern; }
        public void LookNeck(int actor, int pattern, float rate) { NeckPatterns++; LastActor = actor; LastPattern = pattern; LastNeckRate = rate; }
        public void LookEyesTarget(KksAdvActorHost.LookTarget target) { EyeTargets++; LastTarget = target; }
        public void LookNeckTarget(KksAdvActorHost.LookTarget target) { NeckTargets++; LastTarget = target; }
        public void DefaultMotion(int actor) { DefaultMotions++; LastActor = actor; }
        public void Dispose() => _sequence.Dispose();
    }
    private sealed class Assets : KksAdvCoreHost.IAssets
    {
        internal readonly Dictionary<(string Bundle, string Asset), KksAdvScript> Scripts = new Dictionary<(string, string), KksAdvScript>();
        internal readonly Dictionary<string, KksAdvScript> Parameters = new Dictionary<string, KksAdvScript>();
        public string ResolveBundle(string bundle, string loadBundle) => string.IsNullOrEmpty(bundle) ? loadBundle : bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => Scripts[(ResolveBundle(bundle, loadBundle), asset)];
        public bool TryParameter(string name, out KksAdvScript script) => Parameters.TryGetValue(name, out script);
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start production native media host with managed constructor/voice events; UI/world effects mocked");
        KksAdvScript.Row R(string command, bool multi = false, params string[] cells) => new KksAdvScript.Row(zero, zero, multi, command, cells);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "runtime-manifest.json")));
        var settings = JsonSerializer.Deserialize<KksConversationRuntimeSettings>(manifest.RootElement.GetProperty("runtime").GetRawText(), new JsonSerializerOptions { IncludeFields = true });
        using (var backend = new Backend(zero, one))
        {
            var host = new KksAdvBgmHost(backend, backend, value => value == "music" ? zero.ToString() : value, zero);
            var core = new KksAdvCoreHost(host, new Assets(), value => value, (bundle, asset) => { }, null, zero);
            using var machine = new KksAdvMachine(core, zero, one);
            Exception error = null;
            void Run(params string[] cells) => machine.Start(new KksAdvScript("test", "bgm", new[] { R("BGMPlay", false, cells), R("Close") }), () => { }, exception => error = exception);
            var expected = new Illusion.Game.Utils.Sound.SettingBGM(zero);
            Run("music");
            Require(error == null && backend.BgmPlays == one && backend.LastBgm.bundle == expected.bundle
                && backend.LastBgm.loader.asset == expected.loader.asset && backend.LastBgm.loader.fadeTime == expected.loader.fadeTime
                && !backend.LastBgm.loader.isAssetEqualPlay && backend.BgmProperties.Count == zero, "Numeric BGM defaults or replacement changed");
            var enumName = Enum.GetNames(typeof(Illusion.Game.BGM)).First();
            var enumIndex = Illusion.Utils.Enum<Illusion.Game.BGM>.FindIndex(enumName, true);
            Run(enumName.ToLowerInvariant());
            Require(error == null && backend.LastBgm.bundle == new Illusion.Game.Utils.Sound.SettingBGM(enumIndex).bundle, "Case-insensitive BGM name lookup changed");
            Run("sound/custom/music.unity3d", "override", one.ToString(), one.ToString(), bool.FalseString,
                one.ToString(), zero.ToString(), one.ToString(), zero.ToString(), "ignored");
            Require(error == null && backend.LastBgm.bundle == "sound/custom/music.unity3d" && backend.LastBgm.loader.asset == "override"
                && backend.LastBgm.loader.fadeTime == one && backend.BgmProperties.SequenceEqual(new[]
                { "pitch:" + one, "pan:" + zero, "time:" + one, "volume:" + zero }), "BGM optional properties reordered or ignored");
            backend.MissingBgm = true;
            Run("music", "", "", "", "", "invalid-tail");
            Require(error == null, "Missing BGM must not parse unused source properties");
            backend.MissingBgm = false;
            Run("music", "", "", "", "", "invalid-tail");
            Require(error is FormatException && !machine.Active, "Present BGM must reject malformed source properties");
            error = null; var before = backend.BgmPlays;
            Run("music", "", "invalid-delay");
            Require(error is FormatException && backend.BgmPlays == before, "Native unused delay must still validate before playback");
            error = null;
            Run("music", "", "", "", "invalid-async");
            Require(error is FormatException && backend.BgmPlays == before, "Native unused isAsync must still validate before playback");
        }
        using (var backend = new Backend(zero, one))
        {
            var actorHost = new KksAdvActorHost(backend, backend, value => value == "actor" ? zero.ToString() : value, settings);
            var core = new KksAdvCoreHost(actorHost, new Assets(), value => value, (bundle, asset) => { }, null, zero);
            using var machine = new KksAdvMachine(core, zero, one);
            machine.Start(new KksAdvScript("test", "actor-defaults", new[]
            { R("CharaLookEyes", false, "actor", one.ToString(), "ignored"), R("CharaLookNeck", false, zero.ToString(), one.ToString()),
                R("CharaLookEyesTarget", false, zero.ToString(), one.ToString()), R("CharaMotionDefault"), R("Close") }), () => { }, exception => throw exception);
            Require(backend.EyePatterns == one && backend.NeckPatterns == one && backend.DefaultMotions == one && backend.LastActor == int.MaxValue
                && backend.LastPattern == one && backend.LastNeckRate == one, "Actor command native defaults or ignored extra cells changed");
            var defaults = typeof(ChaControl).GetMethod("ChangeLookEyesTarget").GetParameters();
            Require(backend.LastTarget.Rate == (float)defaults.Single(parameter => parameter.Name == "rate").DefaultValue
                && backend.LastTarget.Distance == (float)defaults.Single(parameter => parameter.Name == "dis").DefaultValue
                && backend.LastTarget.Rotation == (float)defaults.Single(parameter => parameter.Name == "rotDeg").DefaultValue
                && backend.LastTarget.Range == (float)defaults.Single(parameter => parameter.Name == "range").DefaultValue,
                "Configured eye-target defaults differ from installed KKS API");
            machine.Start(new KksAdvScript("test", "neck-target", new[]
            { R("CharaLookNeckTarget", false, zero.ToString(), one.ToString(), "focus", one.ToString(), one.ToString(), one.ToString(), one.ToString()), R("Close") }), () => { }, exception => throw exception);
            Require(backend.NeckTargets == one && backend.LastTarget.Name == "focus" && backend.LastTarget.Distance == one
                && backend.LastTarget.Rate == one && backend.LastTarget.Rotation == one && backend.LastTarget.Range == one, "Explicit neck-target cells shifted");
        }
        using (var backend = new Backend(zero, one))
        {
            backend.Aliases["aliasA"] = new[] { "stateA" }; backend.Aliases["aliasB"] = new[] { "stateB" };
            var media = new KksAdvMediaHost(backend, token => token == "actor" ? zero.ToString() : token,
                (text, once) => text.Replace("[H名]", "Heroine").Replace("[P名]", "Player"), zero, one);
            var core = new KksAdvCoreHost(media, new Assets(), token => token, (bundle, asset) => { }, null, zero);
            using var machine = new KksAdvMachine(core, zero, one);
            int done = zero;
            machine.Start(new KksAdvScript("test", "packed", new[]
            {
                R("Voice", true, "actor", "bundleA", "clipA", zero.ToString(), one.ToString(), one.ToString(), "bundleB", "clipB", one.ToString(), one.ToString()),
                R("Expression", true, zero.ToString(), "namedA", one.ToString(), "namedB"),
                R("Motion", true, zero.ToString(), "aliasA", one.ToString(), "aliasB"), R("Text", false, "[H名]", "[P名]"), R("Close")
            }), () => done++, exception => throw exception);
            Require(backend.Trace.SequenceEqual(new[] { "voice:clipA", "voice:clipB", "expression:" + zero, "expression:" + one, "motion:stateA", "motion:stateB" }),
                "Packed media records were flattened, applied before voices, or reordered");
            Require(backend.LastColorKey == "[H名]" && backend.LastName == "Heroine" && backend.LastText == "Player", "Text color key or name/body replacement changed");
            machine.Advance(); Require(done == one && backend.MotionWaits == zero, "Voiced text waited for motion instead of voice groups");
        }
        using (var backend = new Backend(zero, one))
        {
            var media = new KksAdvMediaHost(backend, text => text, (text, once) => text, zero, one);
            var core = new KksAdvCoreHost(media, new Assets(), text => text, (bundle, asset) => { }, null, zero);
            using var machine = new KksAdvMachine(core, zero, one);
            machine.Start(new KksAdvScript("test", "batch-clear", new[]
            { R("Voice", false, zero.ToString(), "bundle", "must-not-play"), R("Text", false, "", "caption"), R("Close") }), () => { }, exception => throw exception);
            Require(backend.VoiceRecords == one && backend.VoiceStarts == zero, "Non-text batch failed to clear prepared voice rows"); machine.Advance();
        }

        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "scenario-graph.json")));
        var needed = graph.RootElement.GetProperty("nodes").EnumerateArray().Select(node =>
            (Bundle: node.GetProperty("bundle").GetString(), Asset: node.GetProperty("asset").GetString())).ToHashSet();
        var assets = new Assets();
        foreach (var bundle in needed.Select(key => key.Bundle).Distinct())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "bundles", bundle.Replace('/', '_') + ".json")));
            foreach (var asset in document.RootElement.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString(); if (!needed.Contains((bundle, name))) continue;
                var script = new KksAdvScript(bundle, name, asset.GetProperty("data").GetProperty("list").EnumerateArray().Select(raw =>
                    new KksAdvScript.Row(raw.GetProperty("_hash").GetInt32(), raw.GetProperty("_version").GetInt32(), raw.GetProperty("_multi").GetInt32() != zero,
                        raw.GetProperty("command").GetString(), raw.GetProperty("_args").EnumerateArray().Select(cell => cell.GetString()))));
                assets.Scripts.Add((bundle, name), script);
            }
        }
        foreach (var parameter in graph.RootElement.GetProperty("parameterOverrides").EnumerateObject())
        {
            var key = (parameter.Value.GetProperty("bundle").GetString(), parameter.Value.GetProperty("asset").GetString());
            if (assets.Scripts.TryGetValue(key, out var script)) assets.Parameters.Add(parameter.Name, script);
        }
        using var live = new Backend(zero, one);
        var liveMedia = new KksAdvMediaHost(live, text => text, (text, once) => text, zero, one);
        var liveCore = new KksAdvCoreHost(liveMedia, assets, text => text, (bundle, asset) => { }, null, zero);
        var timeline = new FakeDeadlineTimeline(zero);
        using var clock = new KksAdvProcessClock(() => timeline.Now, timeline.Schedule, zero);
        int fadeRegulations = zero;
        var commands = KksAdvHostPipeline.Build(liveCore, new KksAdvHostPipeline.Devices
        { Clock = clock, Actors = live, Bgm = live, Fade = live, Camera = live, EventCg = live, Points = live, CrossFade = live, FindVector = name => null },
            text => text, settings, () => fadeRegulations++);
        int preparationCaptures = zero;
        using var runner = new KksAdvMachine(commands, zero, one, clock.SetSuspended, KksAdvFramePreparation.Create((ready, failed) =>
        { preparationCaptures++; ready(); return new Lease(); }));
        int roots = zero, completed = zero;
        foreach (var root in graph.RootElement.GetProperty("roots").EnumerateArray())
        {
            var script = assets.Scripts[(root.GetProperty("bundle").GetString(), root.GetProperty("asset").GetString())];
            runner.Start(script, () => completed++, exception => throw exception);
            var before = completed;
            // Every displayed caption receives one input event; motion/voice ends are synthetic.
            var limit = assets.Scripts.Values.Sum(value => value.Rows.Count);
            int inputs = zero;
            while (runner.Active)
            {
                if (runner.HardWaiting) Require(timeline.AdvanceNext(), "Native hard wait has no processing-clock deadline");
                else runner.Advance();
                inputs++; Require(inputs <= limit, "Native media host stalled");
            }
            Require(completed == before + one, "Native media host did not close an entry point"); roots++;
        }
        int Expected(string command) => graph.RootElement.GetProperty("commands").GetProperty(command).GetInt32();
        Require(roots == completed && live.Captions == Expected("Text") && live.VoiceRecords == Expected("Voice")
            && live.Expressions == Expected("Expression") + Expected("CharaExpression") && live.Motions == Expected("Motion") + Expected("CharaMotion"),
            "Production host dropped installed media rows or duplicated their application");
        Require(timeline.Alarms.Count == Expected("WaitAbs") && clock.Pending == zero, "Installed WaitAbs commands were skipped or leaked deadlines");
        Require(live.EyePatterns == Expected("CharaLookEyes") && live.NeckPatterns == Expected("CharaLookNeck")
            && live.EyeTargets == Expected("CharaLookEyesTarget") && live.DefaultMotions == Expected("CharaMotionDefault"), "Installed actor commands were dropped");
        Require(live.BgmPlays == Expected("BGMPlay"), "Installed BGM commands were dropped");
        Require(live.SceneFades == Expected("SceneFade") && live.SceneFadeEnds == live.SceneFades && fadeRegulations == Expected("SceneFadeRegulate"), "Installed scene fades or regulation commands were dropped or not finalized");
        Require(live.LightActiveChanges == Expected("CameraLightActive") && live.LightAngles == Expected("CameraLightAngle")
            && live.DepthTargets == Expected("DOFTarget"), "Installed camera commands were dropped");
        Require(live.CgLoads == Expected("EventCGSetting") && live.CgInitializations == live.CgLoads
            && live.CgReleaseCalls == live.CgLoads + Expected("EventCGRelease") && live.CgNexts == Expected("EventCGNext")
            && live.CgCrossFades == live.CgNexts, "Installed EventCG command order/count changed");
        Require(live.PointChanges == Expected("KoikatsuPointAdd"), "Installed point commands were dropped");
        Require(live.CrossFades == Expected("CrossFade") && live.CrossFadeNaturalEnds == live.CrossFades, "Installed crossfades were dropped or not finalized");
        File.WriteAllText(Path.Combine(directory, "adv-native-media-host-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production machine and explicit command hosts for all commands in the current entry fixture; installed managed constructors, controlled deadlines; UI/audio/animation/motion ends/fade/camera/CG/point save and display/crossfade frame device mocked",
            roots, captions = live.Captions, voices = live.VoiceRecords, voiceStarts = live.VoiceStarts, expressions = live.Expressions, motions = live.Motions,
            packedActors = true, sourceOrder = true, batchClear = true, colorKeyAndTextReplacement = true,
            namedLookup = "Synthetic template, not installed expression assets",
            processingClockWaits = timeline.Alarms.Count,
            syntheticMotionCompletionWaits = live.MotionWaits,
            eyePatterns = live.EyePatterns, neckPatterns = live.NeckPatterns, eyeTargets = live.EyeTargets, defaultMotions = live.DefaultMotions,
            bgmRequests = live.BgmPlays, bgmBackend = "Mock source; no engine playback",
            sceneFades = live.SceneFades, sceneFadeEnds = live.SceneFadeEnds, sceneFadeRegulationChanges = fadeRegulations,
            lightActiveChanges = live.LightActiveChanges, lightAngles = live.LightAngles, depthTargets = live.DepthTargets,
            eventCgLoads = live.CgLoads, eventCgInitializations = live.CgInitializations, eventCgReleaseCalls = live.CgReleaseCalls,
            eventCgNexts = live.CgNexts, eventCgCrossFadeRequests = live.CgCrossFades,
            pointChanges = live.PointChanges, pointRequested = live.PointRequested,
            crossFades = live.CrossFades, crossFadeNaturalEnds = live.CrossFadeNaturalEnds, unhandledCommandsRejected = true,
            preMutationFrameCaptures = preparationCaptures, preparationFrameDevice = "Synchronous mock; no pixel capture",
            eventCgAssets = live.CgAssets.OrderBy(value => value.Bundle).ThenBy(value => value.Asset).Select(value => new { bundle = value.Bundle, asset = value.Asset }).ToArray(),
            bgmAssets = live.BgmAssets.OrderBy(value => value.Bundle).ThenBy(value => value.Asset).Select(value => new { bundle = value.Bundle, asset = value.Asset }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS native media host roots={roots} captions={live.Captions} voices={live.VoiceRecords} expressions={live.Expressions} motions={live.Motions} processingWaits={timeline.Alarms.Count} syntheticMotionWaits={live.MotionWaits} mockedEffects=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
