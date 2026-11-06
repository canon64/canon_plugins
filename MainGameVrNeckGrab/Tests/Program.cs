using System;
using MainGameVrNeckGrab;

internal static class Program
{
    static int _checks;
    static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    static int Main()
    {
        try
        {
        TestState(); TestRuntimeHooks(); TestProximityRouting(); TestPalmPlacement();
        Check(MotionValue.TryResolve("WLoop", true, .52f, true, 0, out float w) && w == .52f, "W motion lock");
        Check(MotionValue.TryResolve("A_SLoop", true, .52f, true, 0, out float s) && s == 0, "S motion lock");
        Check(!MotionValue.TryResolve("A_Loop", true, .52f, true, 0, out _) && !MotionValue.TryResolve("WLoop", false, .52f, true, 0, out _), "unrelated motion overridden");
        var cleanup = new CleanupBatch(); bool released = false;
        cleanup.Run("故障した購読", () => { throw new InvalidOperationException("expected"); });
        cleanup.Run("操作権", () => released = true);
        Check(released && cleanup.Errors.Count == 1 && cleanup.Errors[0].Contains("expected"), "cleanup failure prevented subsequent release");
        Console.WriteLine("PASS " + _checks + " neck state / real Harmony hook boundary checks (no live VR verification)");
        return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL after " + _checks + " checks: " + e.GetType().FullName + ": " + e.Message);
            if (e.InnerException != null) Console.Error.WriteLine(e.InnerException.GetType().FullName + ": " + e.InnerException.Message);
            return 1;
        }
    }
    static void TestState()
    {
        foreach (int hand in new[] { 0, 1 })
        {
            var s = new NeckState();
            Check(!s.End(-1) && !s.Begin(-1, 0) && !s.Begin(hand, double.NaN), "invalid input accepted");
            Check(s.Begin(hand, 10), "hold rejected"); long first = s.Ticket;
            Check(!s.Begin(1 - hand, 11), "other hand stole hold");
            Check(!s.CanEnter(first, 39.999), "entered before 30 seconds");
            Check(!s.End(1 - hand) && s.Muted, "wrong hand released hold");
            Check(s.End(hand) && s.HandPlaced && !s.Muted && !s.CanEnter(first, 40), "release lost hand or stale deadline remained");
            Check(s.Begin(hand, 60) && !s.CanEnter(s.Ticket, 89.99), "hold accumulated old time");
            Check(s.CanEnter(s.Ticket, 90), "30 seconds did not enter"); s.EnterDoll();
            Check(!s.CanEnter(s.Ticket, 91), "duplicate doll entry");
            s.End(hand); Check(s.Doll && s.HandPlaced, "release cancelled doll or hand");
            Check(!s.AddAwakening(-10) && !s.AddAwakening(float.NaN) && !s.AddAwakening(float.PositiveInfinity) && s.Awakening == 0, "invalid or negative awakening");
            Check(!s.AddAwakening(99.9f) && s.Doll, "woke too early");
            Check(s.Begin(hand, 100), "cannot hold during doll");
            Check(s.AddAwakening(1) && !s.Doll && !s.Muted && s.HandPlaced && s.Awakening == 100, "wake while held failed");
            long stale = s.Ticket; s.Clear(); Check(!s.CanEnter(stale, 999) && !s.HandPlaced && s.Awakening == 0, "cleanup retained state");
        }
    }
    static void TestRuntimeHooks()
    {
        var p = new Plugin(); var target = p.AddActor(); var other = p.AddActor();
        p.Install();
        try
        {
            p._flags.gaugeFemale = 42; p._flags.gaugeMale = 17;
            target.FrozenGauge = 42; target.State.Begin(0, 0); target.State.EnterDoll();
            target.NativeHand.Stimulate(10);
            Check(p._flags.gaugeFemale == 42 && target.State.Awakening == 10 && p._flags.gaugeMale == 17, "native gain was not redirected");
            target.NativeHand.Stimulate(-8); Check(target.State.Awakening == 10 && p._flags.gaugeFemale == 42, "decay changed frozen gauge");
            other.NativeHand.Stimulate(5); Check(p._flags.gaugeFemale == 47 && target.State.Awakening == 10, "other female gain was stolen");
            Check(new HSprite { flags = p._flags }.Display() == 42, "shared other-female gauge changed displayed doll sensitivity");
            target.NativeHand.DirectWrite(8); Check(p._flags.gaugeFemale == 47 && target.State.Awakening == 18, "direct field write bypassed hook");
            p._flags.lockGugeFemale = true;
            target.NativeHand.Stimulate(10); Check(target.State.Awakening == 18, "native gauge lock ignored");
            p._flags.lockGugeFemale = false; p._flags.potion = 2;
            target.NativeHand.Stimulate(41); Check(p.Wakes == 1 && target.State.Awakening == 100 && !target.State.Muted, "potion/wake failed");
            target.NativeHand.Stimulate(1); Check(p._flags.gaugeFemale == 49, "normal gauge did not resume");
            p.Allowed = false; target.NativeHand.DirectWrite(3); Check(p._flags.gaugeFemale == 52, "inactive hook changed native behavior"); p.Allowed = true;

            // Actor-specific dialogue suppression preserves breath and short breath categories.
            target.State.Begin(0, 50);
            Check(!p._voice.VoiceProc(target.Actor, 0) && p._voice.Calls == 0, "spoken dialogue was not blocked");
            Check(p._voice.VoiceProc(other.Actor, 1) && p._voice.Calls == 1, "other actor was muted");
            Check(p._voice.PlayVoice(target.Actor, 0) && p._voice.PlayVoice(target.Actor, 2), "breath or short breath was muted");
            Check(!p._voice.PlayVoice(target.Actor, 1), "explicit spoken voice bypassed mute");
            target.State.End(0);

            // A native load issued before the hold must stay cancelled after the hold ends.
            var loader = new Manager.Voice.Loader(); p._voice.Loader = loader;
            p._voice.VoiceProc(target.Actor, 0);
            target.State.Begin(0, 60); p.Silence(target); target.State.End(0);
            var source = new UnityEngine.AudioSource(); Manager.Voice.Play_Standby(source, loader);
            Check(!source.Playing && source.gameObject.Destroyed, "cancelled native async load revived after release");
            var breath = new UnityEngine.AudioSource(); Manager.Voice.Play_Standby(breath, new Manager.Voice.Loader());
            Check(breath.Playing && !breath.gameObject.Destroyed, "unrelated native breath was destroyed");

            var player = MainGameVoiceFaceEventBridge.Plugin.Instance._externalVoicePlayer;
            target.State.Begin(1, 100);
            Check(!player.Play("a.wav", target.Actor) && !player.StartLoad(target.Actor), "external dialogue bypassed mute");
            Check(player.Play("b.wav", other.Actor) && player.RequestImpactCue(target.Actor), "other dialogue or moan cue was blocked");
            player._pendingFemale = target.Actor; player._loadPending = true; player._pendingReady = true;
            player._dialogueFemale = other.Actor; player._sequenceFemale = target.Actor;
            p.Silence(target);
            Check(player._loadGeneration == 1 && !player._loadPending && !player._pendingReady && player.QueueClears == 1, "pending external load/queue was not cancelled");
            Check(player.DialogueStops == 0, "other playing female was stopped");
            player._dialogueFemale = target.Actor; p.Silence(target); Check(player.DialogueStops == 1, "target external playback survived hold");
            target.State.End(1); Check(player.Play("a.wav", target.Actor), "dialogue did not resume after release");

            target.State.Begin(0, 150); target.State.EnterDoll();
            target.Doll = new DollSnapshot();
            target.Actor.setAnimatorParamFloat("motion", .9f); other.Actor.setAnimatorParamFloat("motion", .9f);
            Check(target.Actor.Motion == .52f && other.Actor.Motion == .9f, "actor-scoped motion lock changed partner");
            target.Actor.ChangeEyesPtn(9); Check(target.Actor.Face == 0, "doll expression overwritten");
            other.Actor.ChangeEyesPtn(8); Check(other.Actor.Face == 8, "other face blocked");
            DollSnapshot.InternalWrite = true; target.Actor.ChangeEyesPtn(3); DollSnapshot.InternalWrite = false;
            Check(target.Actor.Face == 3, "owned restore was blocked");
            target.NativeHand.DetachAllItem(); Check(p.Removed == 1 && !target.State.HandPlaced && target.State.Doll, "remove-all did not clear hand independently");
        }
        finally { p.Uninstall(); }
        other.NativeHand.Stimulate(1); Check(p._flags.gaugeFemale == 54, "patch cleanup failed");
    }
    static void TestProximityRouting()
    {
        Check(NeckProximity.Closest(new[] { .11 * .11 }, .12) == 0, "near neck without collider contact rejected");
        Check(NeckProximity.Closest(new[] { .13 * .13 }, .12) == -1, "outside radius captured");
        Check(NeckProximity.Closest(new[] { .01 }, .1) == 0, "radius boundary rejected");
        Check(NeckProximity.Closest(new[] { .01, .0025 }, .12) == 1, "closest of two necks not chosen");
        Check(NeckProximity.Closest(new[] { double.NaN, double.PositiveInfinity, -.2, .0025 }, .12) == 3, "invalid neck sample accepted");
        Check(NeckProximity.Closest(new[] { .0025, .0025 }, .12) == 0, "equidistant neck choice unstable");
        var router = new NeckPressRouter(); int starts = 0;
        Check(router.Capture(0, 0, () => ++starts, 1) && router.Capture(0, 0, () => ++starts, 1) && starts == 1, "same press delivered twice");
        Check(router.Capture(0, 0, () => ++starts, 2) && starts == 2, "fresh press after missed Up rejected");
        router.Release(1); Check(router.Actor(0) == 0, "wrong-hand Up released priority");
        router.Release(0); Check(router.Actor(0) == -1, "Up left priority behind");
        Check(router.Capture(0, 0, () => { }, 3), "busy neck fell through to breast");
        router.Clear(); Check(router.Actor(0) == -1 && router.Actor(1) == -1, "focus loss retained press claim");

        var p = new Plugin(); p.AddActor(); p.Install();
        try
        {
            object left = new object(), right = new object(); int routes = 0, captures = 0;
            p.NativePress = controller =>
            {
                ++routes; if (!ReferenceEquals(controller, left)) return false;
                return p.Press.Capture(0, 0, () => ++captures);
            };
            var nativeLeft = new KKS_VR.Caress.CaressController(left);
            var nativeRight = new KKS_VR.Caress.CaressController(right);
            nativeLeft.Input.Down = false; nativeLeft.Tick();
            Check(routes == 0, "proximity work added on non-press frames");
            nativeLeft.Input.Down = true; nativeLeft.Tick();
            Check(captures == 1 && nativeLeft.NativeClicks == 0, "native caress read press first and fired breast/kiss");
            nativeLeft.Tick(); Check(captures == 1 && nativeLeft.NativeClicks == 0, "duplicate native event restarted hold");
            nativeRight.Input.Down = true; nativeRight.Tick();
            Check(nativeRight.NativeClicks == 1, "unrelated controller caress was blocked");
            var mouth = new KKS_VR.Caress.VRMouth(); mouth._aibuTracker.Proc = p._scene;
            mouth.TryKiss(); Check(mouth.Kisses == 0, "automatic kiss won over captured neck");
            var otherScene = new KKS_VR.Caress.VRMouth(); otherScene._aibuTracker.Proc = new object();
            otherScene.TryKiss(); Check(otherScene.Kisses == 1, "another session's kiss blocked");
            p.Press.Release(0); mouth.TryKiss(); Check(mouth.Kisses == 1, "kiss remained blocked after Up");
            p.Press.Capture(0, 0, () => ++captures); nativeLeft.Tick();
            Check(captures == 2 && nativeLeft.NativeClicks == 0, "Steam callback first leaked same down to native caress");
            p.Allowed = false; nativeLeft.Tick(); Check(nativeLeft.NativeClicks == 1, "inactive neck plugin changed native input");
        }
        finally { p.Uninstall(); }
    }
    static void TestPalmPlacement()
    {
        foreach (int side in new[] { 0, 1 })
        {
            var previous = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)(side == 0 ? Math.PI * .5 : -Math.PI * .5))
                * System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ, (float)(Math.PI * .5));
            var corrected = PalmPose.CorrectRotation(previous);
            var localPalm = side == 0 ? System.Numerics.Vector3.UnitY : -System.Numerics.Vector3.UnitY;
            var normal = System.Numerics.Vector3.Transform(localPalm, corrected);
            Check(System.Numerics.Vector3.Dot(normal, -System.Numerics.Vector3.UnitZ) > .9999f, "palm still faces viewer instead of neck");
            var fingers = System.Numerics.Vector3.Transform(side == 0 ? -System.Numerics.Vector3.UnitX : System.Numerics.Vector3.UnitX, corrected);
            Check(Math.Abs(fingers.Y) < .0001f && Math.Abs(fingers.X) > .9999f, "fingers did not roll sideways");
            var wrist = new System.Numerics.Vector3(1, 2, 3);
            var index = wrist + new System.Numerics.Vector3(side == 0 ? -.056f : .056f, .001f, .021f);
            var little = wrist + new System.Numerics.Vector3(side == 0 ? -.047f : .047f, -.005f, -.022f);
            var center = PalmPose.Center(wrist, index, little);
            var skin = new System.Numerics.Vector3(.1f, .2f, .3f);
            var delta = PalmPose.AlignmentDelta(center, normal, skin);
            Check(System.Numerics.Vector3.Distance(center + normal * .006f + delta, skin) < .00001f, "palm pad does not meet neck skin");
            Check(System.Numerics.Vector3.Distance(center, wrist) > .02f, "wrist used as palm center");
        }
    }
}
