using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MainGameAiShoujoMode;
using MainGameAiShoujoMode.Lighting;
using MapEnvironmentRuntime;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static readonly int One = Math.Sign(int.MaxValue);
    private sealed class Inline : SynchronizationContext { public override void Post(SendOrPostCallback callback, object state) => callback(state); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); ++_checks; }
    private static void Call(AiLightingSession session, string method, params object[] arguments) => typeof(AiLightingSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, arguments);
    private static LightingComparison Comparison(AiLightingSession session) => (LightingComparison)typeof(AiLightingSession).GetField("_comparison", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
    private static void Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(new Inline());
        var directory = Path.Combine(args.Single(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        Plugin.AiLightingProfilePath = Path.Combine(directory, "profile.json"); Plugin.AiLightingTracePath = Path.Combine(directory, "trace");
        Plugin.AiLightingEnabled = false; Plugin.AiLightingTrace = false;
        var world = new EnvironmentWorld(); var walking = new Camera(); var h = new Camera(); Camera.main = walking;
        var native = new Light { intensity = One + One }; var proc = new HSceneProc(native);
        using (var session = new AiLightingSession(world, new Transform(), walking))
        {
            session.BeforeH(); session.HReady(h, proc); session.WalkingReady(walking);
            Check(LightingUnity.SnapshotCalls == default(int) && LightingUnity.LightCaptures == default(int), "OFF bypasses snapshot discovery and light collection");
            Check(Camera.Subscribers == default(int) && !Directory.Exists(Plugin.AiLightingTracePath), "OFF creates no probe or files");
            Call(session, "BeginComparison");
            Check(!world.Playing && Plugin.AiLightingTrace, "explicit comparison freezes the clock without changing its persistent settings");
            Check(session.ClockPlayingForSave, "temporary diagnostic pause is not written into moment-save clock settings");
            Check(Camera.Subscribers == One, "one pending render capture");
            Camera.Render(walking); var captures = LightingUnity.SnapshotCalls;
            Camera.Render(walking);
            Check(Camera.Subscribers == default(int) && LightingUnity.SnapshotCalls == captures, "first render removes callback and does not monitor later frames");
            session.BeforeH(); session.HReady(h, proc);
            Call(session, "SelectComparison", "without-light");
            Check(!native.enabled, "A/B disables only the native H auxiliary light");
            Comparison(session).Confirm();
            Call(session, "SelectComparison", "baseline");
            Check(native.enabled && native.intensity == One + One, "baseline restores original H light");
            world.Hour += One;
            try { Call(session, "SaveConfirmed"); throw new Exception("different time was accepted"); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { ++_checks; }
            world.Hour -= One;
            Call(session, "SaveConfirmed");
            Check(File.Exists(Plugin.AiLightingProfilePath), "explicit confirmation saves profile");
            Check(Plugin.AiLightingEnabled && !Plugin.AiLightingTrace && world.Playing, "save ends diagnostics and restores time progression");
            Check(Camera.Subscribers == default(int), "save removes pending diagnostic hooks");
            session.WalkingReady(walking);
            var owned = (Light)typeof(AiLightingSession).GetField("_walkingLight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Check(owned != null && owned.enabled && owned.intensity == native.intensity, "walking receives confirmed values");
            session.BeforeH();
            Check(!owned.enabled, "owned walking light is disabled before H begins");
            session.HReady(h, proc);
            Plugin.AiLightingEnabled = false;
            Check(native.intensity == One + One && native.enabled, "feature OFF restores H's original settings");
            var recorded = LightingUnity.SnapshotCalls; Camera.Render(h);
            Check(recorded == LightingUnity.SnapshotCalls && Camera.Subscribers == default(int), "diagnostic OFF prevents late rendering callbacks");
        }
        Check(MainGameUnifiedUi.UnifiedUiApi.Tabs.Count == default(int), "dispose unregisters its own UI tab");
        Plugin.AiLightingEnabled = true;
        using (var fresh = new AiLightingSession(world, new Transform(), walking))
        {
            var owned = (Light)typeof(AiLightingSession).GetField("_walkingLight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fresh);
            Check(owned != null && owned.enabled, "next mode starts with confirmed profile without visiting H");
            fresh.BeforeH(); fresh.WalkingReady(walking);
            Check(!owned.enabled, "failed H startup does not leave old walking light alive");
            Call(fresh, "BeginComparison"); fresh.Dispose(); fresh.Dispose();
            Check(world.Playing && Camera.Subscribers == default(int), "double cleanup restores clock and cancels pending probe");
        }
        Plugin.AiLightingEnabled = false; Plugin.AiLightingTrace = false;
        var walkingEffect = walking.gameObject.AddComponent<TestEffect>(); walkingEffect.enabled = false;
        var hEffect = h.gameObject.AddComponent<TestEffect>(); hEffect.enabled = true;
        using (var effects = new AiLightingSession(world, new Transform(), walking))
        {
            Call(effects, "BeginComparison"); effects.BeforeH(); effects.HReady(h, proc);
            Call(effects, "SelectComparison", "without-effects");
            Check(!hEffect.enabled && native.enabled, "effects A/B leaves the native auxiliary light enabled");
            Comparison(effects).Confirm();
            try { Call(effects, "SaveConfirmed"); throw new Exception("all differing effects were blindly saved"); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { ++_checks; }
            Call(effects, "SelectComparison", "without-effect:" + typeof(TestEffect).FullName);
            Comparison(effects).Confirm();
            Call(effects, "SaveConfirmed"); effects.WalkingReady(walking);
            Check(walkingEffect.enabled, "only a separately confirmed effect is applied to walking");
            var vrOutput = new Camera { stereoEnabled = true };
            var vrEffect = vrOutput.gameObject.AddComponent<TestEffect>(); vrEffect.enabled = false;
            Camera.Render(vrOutput);
            Check(vrEffect.enabled && Camera.Subscribers == default(int), "first actual VR output receives profile once without frame monitoring");
            Plugin.AiLightingEnabled = false;
            Check(!walkingEffect.enabled && !vrEffect.enabled && hEffect.enabled, "feature OFF restores every affected camera without touching the H baseline");
        }
        Plugin.AiLightingTrace = false;
        var comparison = new LightingComparison();
        try { comparison.Confirm(); throw new Exception("baseline was incorrectly confirmed"); } catch (InvalidOperationException) { ++_checks; }
        var profile = LightingJson.Load(Plugin.AiLightingProfilePath); profile.confirmed = false;
        try { LightingJson.Save(Plugin.AiLightingProfilePath, profile); throw new Exception("unconfirmed profile was saved"); } catch (InvalidDataException) { ++_checks; }
        Check(LightingJson.Load(Plugin.AiLightingProfilePath).confirmed, "failed validation leaves last good saved profile unchanged");
        var before = new LightingSnapshot { weather = "clear", environment = new[] { new LightingValue { name = "ambient", kind = "scalar", text = "before" } } };
        var after = new LightingSnapshot { weather = "rain", environment = new[] { new LightingValue { name = "ambient", kind = "scalar", text = "after" } } };
        var differences = LightingDifference.Compare(before, after);
        Check(differences.Any(value => value.key == "weather") && differences.Any(value => value.key == "environment/ambient"), "diff separates environmental changes from lighting changes");
        NativeAutomaticRendering(world, walking, h, proc);
        Console.WriteLine("AI_LIGHTING_CHECKS_PASS assertions=" + _checks);
    }
    private static void NativeAutomaticRendering(EnvironmentWorld world, Camera walking, Camera h, HSceneProc proc)
    {
        Plugin.AiLightingTrace = false; Plugin.AiLightingEnabled = false;
        File.Delete(Plugin.AiLightingProfilePath); Plugin.AiNativeLighting = true;
        var snapshots = LightingUnity.SnapshotCalls; var captures = LightingUnity.LightCaptures;
        var initialHour = world.Hour; var weather = world.Weather;
        using (var session = new AiLightingSession(world, new Transform(), walking))
        {
            Check(NativeHLightingRenderer.WalkingLight && NativeHLightingRenderer.Created == One, "initial walking uses native H rendering without a profile, comparison, or old enable flag");
            Check(!File.Exists(Plugin.AiLightingProfilePath), "automatic rendering does not fabricate a confirmed diagnostic profile");
            Check(world.Playing && world.Hour == initialHour && world.Weather == weather, "automatic rendering preserves time progression and weather");
            Check(LightingUnity.SnapshotCalls == snapshots && LightingUnity.LightCaptures == captures, "diagnostics OFF bypasses snapshots and diagnostic light discovery");
            Camera.Render(walking); var cameraCalls = NativeHLightingRenderer.CameraCalls;
            Camera.Render(walking);
            Check(Camera.Subscribers == default(int) && NativeHLightingRenderer.CameraCalls == cameraCalls, "native output hookup runs only on first render");
            var walkingCalls = NativeHLightingRenderer.WalkingCalls; var dedicatedHCalls = NativeHLightingRenderer.HCalls;
            session.BeforeDedicatedH(); session.DedicatedReady(walking);
            Check(NativeHLightingRenderer.WalkingLight && NativeHLightingRenderer.WalkingCalls == walkingCalls && NativeHLightingRenderer.HCalls == dedicatedHCalls, "dedicated H keeps the exact owned renderer without duplicate light or native H dependency");
            Check(world.Playing && world.Hour == initialHour && world.Weather == weather, "dedicated H lighting leaves island clock and weather unchanged");
            Check(Camera.Subscribers == default(int) && LightingUnity.SnapshotCalls == snapshots, "dedicated H diagnostics OFF adds no rendering monitor or snapshot");
            session.BeforeH();
            Check(!NativeHLightingRenderer.WalkingLight && Camera.Subscribers == default(int), "walking rendering is removed before H startup");
            var intensity = proc.lightCamera.intensity;
            session.HReady(h, proc);
            Check(!NativeHLightingRenderer.WalkingLight && NativeHLightingRenderer.HCalls == One && proc.lightCamera.intensity == intensity, "H uses its own untouched native light");
            var hCalls = NativeHLightingRenderer.HCalls;
            Camera.Render(h); Camera.Render(h);
            Check(NativeHLightingRenderer.HCalls == hCalls + One && Camera.Subscribers == default(int), "actual H output settings are captured once without frame monitoring");
            session.WalkingReady(walking);
            Check(NativeHLightingRenderer.WalkingLight, "walking automatically resumes H rendering after H exit");
            session.BeforeH(); session.WalkingReady(walking);
            Check(NativeHLightingRenderer.WalkingLight, "failed H startup restores walking rendering");
            Plugin.AiNativeLighting = false;
            Check(!NativeHLightingRenderer.WalkingLight && NativeHLightingRenderer.Disposed == One && Camera.Subscribers == default(int), "OFF disposes automatic rendering and pending callback");
            Plugin.AiNativeLighting = true;
            Check(NativeHLightingRenderer.WalkingLight, "enabling automatic rendering needs no comparison");
            NativeHLightingRenderer.FailWalking = true;
            session.WalkingReady(walking);
            Check(!NativeHLightingRenderer.WalkingLight && Camera.Subscribers == default(int), "partial startup failure stops owned rendering");
            NativeHLightingRenderer.FailWalking = false;
            session.WalkingReady(walking);
            session.Dispose(); session.Dispose(); Camera.Render(walking);
            Check(!NativeHLightingRenderer.WalkingLight && Camera.Subscribers == default(int), "double disposal leaves no automatic light or stale render callback");
        }
        Check(world.Playing && world.Hour == initialHour && world.Weather == weather, "all transitions preserve environment clock and weather");
        Plugin.AiNativeLighting = false;
    }
}
