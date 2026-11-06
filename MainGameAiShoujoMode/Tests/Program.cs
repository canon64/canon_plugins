using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;

internal static class Program
{
    private static void Main(string[] args)
    {
        ConversationJsonChecks.ConfigureResolver();
        try { if (args.First() == "--traversal")
            {
                var content = args.Last();
                SpecialMovementChecks.Run(JsonCodec.FromJson<Parameters>(File.ReadAllText(Path.Combine(content,"parameters.json"))),content);
            }
            else if (args.First() == "--hotel") HotelWalkChecks.Run(args.Last());
            else if(args.First()=="--layout")HotelWalkChecks.CheckLayoutPersistence(Path.GetDirectoryName(args.Last()),JsonCodec.FromJson<Parameters>(File.ReadAllText(args.Last())));
            else Check(args); }
        catch (Exception ex) { Console.WriteLine("AI_MODE_JSON_CHECKS_FAILED " + ex); Environment.ExitCode = -int.MaxValue; }
    }
    private static void Check(string[] args)
    {
        Console.WriteLine("AI_MODE_JSON_CHECKS_START");
        var settings = JsonCodec.FromJson<Parameters>(File.ReadAllText(args.First()));
        ConversationJsonChecks.Run(Path.GetDirectoryName(args.First()), settings);
        SpecialMovementChecks.Run(settings,Path.GetDirectoryName(args.First()));
        if (settings.numbers == null || settings.text == null || settings.geometry == null || string.IsNullOrEmpty(settings.revision)) throw new Exception("Parameter read lost required arrays");
        var copy = settings.Copy();
        if (copy.numbers.Length != settings.numbers.Length || copy.text.Length != settings.text.Length || copy.geometry.Length != settings.geometry.Length || copy.revision != settings.revision)
            throw new Exception("Parameter round trip lost fields");
        foreach (var item in settings.numbers) if (copy.N(item.key) != item.value) throw new Exception("Numeric round trip mismatch: " + item.key);
        foreach (var item in settings.text) if (copy.T(item.key) != item.value) throw new Exception("Text round trip mismatch: " + item.key);
        Console.WriteLine("AI_MODE_JSON_PARAMETER_ROUNDTRIP_OK");
        if (!Directory.Exists(settings.T("player.card.directory"))) throw new Exception("Male card directory is missing");
        foreach (string axis in new[] { "x", "y", "z" })
        {
            var eye = settings.numbers.Single(item => item.key == "player.eyeOffset." + axis);
            if (eye.value < eye.min || eye.value > eye.max || eye.unit != "m") throw new Exception("Player eye offset invalid: " + axis);
        }
        Console.WriteLine("AI_MODE_PLAYER_PARAMETERS_PASS");
        var audio = JsonCodec.FromJson<AiModeAudio.Manifest>(File.ReadAllText(args.Skip(settings.I("math.one")).First()));
        Console.WriteLine("AI_MODE_JSON_AUDIO_READ_OK");
        if (!audio.clips.Any() || !audio.environmentSources.Any() || !audio.areaVolumes.Any() || !audio.environmentSources.All(item => item.points.Any() && item.clips.Any())) throw new Exception("Audio manifest lost nested arrays");
        var doors = JsonCodec.FromJson<SandboxIslandDoors.Manifest>(File.ReadAllText(args.Last()));
        if (!doors.doors.Any() || doors.doors.Any(item => string.IsNullOrEmpty(item.animatorPath) || string.IsNullOrEmpty(item.closedIdleState) || item.obstacles == null)) throw new Exception("Door manifest lost required data");
        if (doors.doors.Where(item => item.kind == "harbor").Any(item => string.IsNullOrEmpty(item.obstacles.closed) || string.IsNullOrEmpty(item.obstacles.openRight))) throw new Exception("Harbor gate blockers are disconnected from the animation");
        if (doors.sessionHiddenObjects == null || !doors.sessionHiddenObjects.Any() || doors.sessionHiddenObjects.Any(string.IsNullOrWhiteSpace)) throw new Exception("Original ordinary-exploration initialization targets missing");
        var restoredDoors = JsonCodec.FromJson<SandboxIslandDoors.Manifest>(JsonCodec.ToJson(doors));
        if (!restoredDoors.sessionHiddenObjects.SequenceEqual(doors.sessionHiddenObjects)) throw new Exception("Ordinary-exploration initialization targets lost during JSON round trip");
        if (doors.sessionShownObjects == null || !doors.sessionShownObjects.Any() || doors.sessionShownObjects.Intersect(doors.sessionHiddenObjects).Any()) throw new Exception("Bridge open and closed states are missing or overlap");
        if (!restoredDoors.sessionShownObjects.SequenceEqual(doors.sessionShownObjects)) throw new Exception("Opened bridge targets lost during JSON round trip");
        Console.WriteLine("AI_MODE_JSON_HARBOR_PASSAGE_INITIALIZATION_OK");
        if (doors.doors.Where(item => item.kind == "ordinary").Any(item => string.IsNullOrEmpty(item.obstacles.closed) || string.IsNullOrEmpty(item.closeLeftState))) throw new Exception("Ordinary door manifest incomplete");
        var animation = JsonCodec.FromJson<AiAnimationTrial.Report>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(args.First()), "animation-application.json")));
        if (animation.mapping == null || !animation.mapping.Any() || !animation.nativeSamplingPassed || animation.kksVerified || animation.clips == null || !animation.clips.Any(clip => clip.name == "Idle") || !animation.clips.Any(clip => clip.name == "Walk")) throw new Exception("Animation application report lost required data");
        var animationCopy = JsonCodec.FromJson<AiAnimationTrial.Report>(JsonCodec.ToJson(animation));
        if (animationCopy.mapping.Length != animation.mapping.Length || animationCopy.controller != animation.controller || animationCopy.clips.Length != animation.clips.Length) throw new Exception("Animation report round trip failed");
        Console.WriteLine("AI_MODE_JSON_ANIMATION_APPLICATION_OK mappings=" + animation.mapping.Length);
        var content = Path.GetDirectoryName(args.First());
        var locations = JsonCodec.FromJson<AiModeHLocations.Manifest>(File.ReadAllText(Path.Combine(content, Path.GetFileName(settings.T("hSession.locations.path")))));
        if (locations.points == null || !locations.points.Any() || locations.points.Length != locations.pointCount || locations.points.Select(point => point.id).Distinct().Count() != locations.pointCount) throw new Exception("H location IDs/count missing or duplicated");
        foreach (var point in locations.points)
        {
            var rotation = point.sourceRotation;
            float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (Math.Abs(norm - settings.N("math.one")) > settings.N("render.epsilon") || point.categories == null) throw new Exception("H location quaternion/categories invalid: " + point.id);
        }
        var locationCopy = JsonCodec.FromJson<AiModeHLocations.Manifest>(JsonCodec.ToJson(locations));
        if (locationCopy.points.Length != locations.points.Length || locationCopy.points.Where((point, index) => point.sourcePosition.x != locations.points[index].sourcePosition.x || point.sourceRotation.w != locations.points[index].sourceRotation.w || !point.categories.SequenceEqual(locations.points[index].categories)).Any()) throw new Exception("H locations lost transforms or categories during JSON round trip");
        Console.WriteLine("AI_MODE_H_LOCATIONS_JSON_PASS points=" + locations.points.Length + " candidates=" + locations.points.Count(point => point.categories.Any()));
        var nav = JsonCodec.FromJson<NavigationWorld.SourceDocument>(File.ReadAllText(Path.Combine(content, settings.T("navigation.source"))));
        var waypoints = JsonCodec.FromJson<NavigationWorld.WaypointDocument>(File.ReadAllText(Path.Combine(content, settings.T("navigation.waypoints"))));
        var pods = JsonCodec.FromJson<AiPodSession.Manifest>(File.ReadAllText(Path.Combine(content, settings.T("pod.manifest"))));
        if (nav.nodes == null || nav.meshes == null || !nav.nodes.Any(node => node.tagged) || nav.meshes.Any(mesh => mesh.vertices == null || mesh.triangles == null || !mesh.triangles.Any())) throw new Exception("Navigation geometry lost required arrays");
        if (waypoints.sets == null || !waypoints.sets.Any(set => set.points != null && set.points.Any())) throw new Exception("Navigation waypoints missing");
        if (pods.pods == null || !pods.pods.Any() || pods.pods.Any(pod => pod.command == null || pod.recover == null || pod.pivot == null || pod.command.Concat(pod.recover).Concat(pod.pivot).Any(pose => pose.rotation == null))) throw new Exception("Pod interaction transforms missing");
        var podsCopy = JsonCodec.FromJson<AiPodSession.Manifest>(JsonCodec.ToJson(pods));
        if (podsCopy.pods.First().recover.First().position != pods.pods.First().recover.First().position || podsCopy.pods.First().recover.First().rotation.w != pods.pods.First().recover.First().rotation.w) throw new Exception("Pod transform round trip failed");
        Console.WriteLine("AI_MODE_JSON_NAVIGATION_PODS_PASS meshes=" + nav.meshes.Length + " waypoints=" + waypoints.sets.Sum(set => set.points.Length) + " pods=" + pods.pods.Length);
        var podAnimation = JsonCodec.FromJson<PodCharacterAnimation.Report>(File.ReadAllText(Path.Combine(content, "pod-animation-application.json")));
        string sequence = settings.T("podAnimation.sequence");
        if (!podAnimation.nativeSamplingPassed || podAnimation.clips == null || !podAnimation.clips.Any(clip => clip.name == sequence + "_M") || !podAnimation.clips.Any(clip => clip.name == sequence + "_out_M")) throw new Exception("Pod character animation is incomplete");
        Console.WriteLine("AI_MODE_POD_CHARACTER_ANIMATION_PASS clips=" + podAnimation.clips.Length);
        var capture = new CaptureRecord { parameters = settings, cameraPosition = settings.V("spawn"), state = new SessionStatus { nightAmbient = UnityEngine.Color.white, animationApplication = new AnimationApplicationStatus { state = "Walk", sourceClip = "mc_f_move_01", mappedBones = animation.mapping.Length, nativeSamplingPassed = true, kksVisualVerified = false } } };
        Console.WriteLine("AI_MODE_JSON_CAPTURE_CREATED");
        var restored = JsonCodec.FromJson<CaptureRecord>(JsonCodec.ToJson(capture, true));
        if (restored.parameters.numbers.Length != settings.numbers.Length) throw new Exception("Capture lost parameters");
        if (restored.cameraPosition.x != capture.cameraPosition.x || restored.state.nightAmbient.a != capture.state.nightAmbient.a) throw new Exception("Capture vector/color round trip failed");
        if (restored.state.animationApplication == null || restored.state.animationApplication.sourceClip != capture.state.animationApplication.sourceClip || restored.state.animationApplication.mappedBones != animation.mapping.Length || restored.state.animationApplication.kksVisualVerified) throw new Exception("Animation capture round trip failed");
        bool rejected = false;
        try { JsonCodec.FromJson<Parameters>("{}").ValidateDocument(); } catch (InvalidDataException) { rejected = true; }
        if (!rejected) throw new Exception("Incomplete JSON was accepted");
        Console.WriteLine("AI_MODE_JSON_CHECKS_PASS parameters=" + settings.numbers.Length + " clips=" + audio.clips.Length + " sources=" + audio.environmentSources.Length);
    }
}
