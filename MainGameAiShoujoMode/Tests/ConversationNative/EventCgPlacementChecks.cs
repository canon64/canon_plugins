using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class EventCgPlacementChecks
{
    internal static void Run(string directory, JsonElement settings, int zero, int one)
    {
        Console.WriteLine("Start EventCG ground-frame rebase against installed camera/heroine poses; no Unity placement");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "eventcg-placement-inventory.json")));
        using var parameters = JsonDocument.Parse(File.ReadAllText("F:/kks/work/tools/KksMapSandbox/parameters.json"));
        var epsilon = parameters.RootElement.GetProperty("numbers").EnumerateArray().Single(value => value.GetProperty("key").GetString() == "render.epsilon").GetProperty("value").GetDouble();
        var assets = document.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        var allowed = settings.GetProperty("advEventCgActorRelativeAssets").EnumerateArray().Select(value => value.GetString()).ToArray();
        Require(allowed.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(assets.Select(asset => asset.GetProperty("bundle").GetString() + ":" + asset.GetProperty("asset").GetString())), "Configured relative placements differ from audited assets");
        int checks = zero;
        KksEventCgReferenceFrame.Point Point(JsonElement raw) => new KksEventCgReferenceFrame.Point
        { X = raw.GetProperty("x").GetDouble(), Y = raw.GetProperty("y").GetDouble(), Z = raw.GetProperty("z").GetDouble() };
        foreach (var asset in assets)
        {
            Require(asset.GetProperty("nativeMap").GetString() == settings.GetProperty("advEventCgActorRelativeMap").GetString(), "Placement source map differs from configured map");
            var nodes = asset.GetProperty("transforms").EnumerateArray().ToArray();
            Require(nodes.Length == one + one + one && nodes.Any(node => node.GetProperty("path").GetString().EndsWith("/camPos"))
                && nodes.Count(node => node.GetProperty("path").GetString().EndsWith("/chaPos")) == one, "Relative placement requires a new layout adapter");
            Require(asset.GetProperty("motions").EnumerateArray().All(motion => motion.GetProperty("slot").GetInt32() == zero
                && motion.GetProperty("items").GetArrayLength() == zero && motion.GetProperty("removes").GetArrayLength() == zero), "Relative placement contains additional slots/world items");
            var reference = nodes.Single(node => node.GetProperty("path").GetString().EndsWith("/chaPos"));
            var camera = Point(nodes.Single(node => node.GetProperty("path").GetString().EndsWith("/camPos")).GetProperty("position"));
            var position = Point(reference.GetProperty("position")); var rotation = reference.GetProperty("rotation");
            var x = rotation.GetProperty("x").GetDouble(); var y = rotation.GetProperty("y").GetDouble();
            var z = rotation.GetProperty("z").GetDouble(); var w = rotation.GetProperty("w").GetDouble();
            var source = new KksEventCgReferenceFrame.Pose
            { X = position.X, Y = position.Y, Z = position.Z, ForwardX = (one + one) * (x * z + y * w), ForwardZ = one - (one + one) * (x * x + y * y) };
            foreach (var heading in new[] { (X: zero, Z: one), (X: one, Z: zero), (X: -one, Z: zero), (X: zero, Z: -one) })
            {
                var target = new KksEventCgReferenceFrame.Pose { X = one + one, Y = one, Z = -one, ForwardX = heading.X, ForwardZ = heading.Z };
                var frame = KksEventCgReferenceFrame.Align(source, target);
                var moved = frame.Transform(position); var movedCamera = frame.Transform(camera);
                Require(Math.Abs(moved.X - target.X) <= epsilon && Math.Abs(moved.Z - target.Z) <= epsilon
                    && Math.Abs(moved.Y - target.Y - position.Y) <= epsilon, "Rebase lost foot position or authored vertical offset");
                var originalDistance = (camera.X - position.X) * (camera.X - position.X) + (camera.Z - position.Z) * (camera.Z - position.Z);
                var newDistance = (movedCamera.X - moved.X) * (movedCamera.X - moved.X) + (movedCamera.Z - moved.Z) * (movedCamera.Z - moved.Z);
                Require(Math.Abs(originalDistance - newDistance) <= epsilon && Math.Abs(movedCamera.Y - moved.Y - camera.Y + position.Y) <= epsilon,
                    "Rebase changed authored camera/actor separation");
                var forward = frame.Transform(new KksEventCgReferenceFrame.Point { X = source.X + source.ForwardX, Y = source.Y, Z = source.Z + source.ForwardZ });
                var length = Math.Sqrt(source.ForwardX * source.ForwardX + source.ForwardZ * source.ForwardZ);
                Require(Math.Abs((forward.X - moved.X) / length - heading.X) <= epsilon && Math.Abs((forward.Z - moved.Z) / length - heading.Z) <= epsilon,
                    "Rebase failed requested horizontal heading");
                checks++;
            }
        }
        try { KksEventCgReferenceFrame.Align(default, default); throw new Exception("Zero heading accepted"); } catch (ArgumentException) { }
        try
        { KksEventCgReferenceFrame.Align(new KksEventCgReferenceFrame.Pose { ForwardZ = one, X = double.NaN }, new KksEventCgReferenceFrame.Pose { ForwardZ = one }); throw new Exception("Nonfinite reference accepted"); }
        catch (ArgumentException) { }
        File.WriteAllText(Path.Combine(directory, "eventcg-placement-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production numeric rebase using installed pose inputs; no Unity anchor/prefab construction, ground, camera collision or rendering",
            assets = assets.Length, rebaseCases = checks, preserveFootPosition = true, preserveAuthoredHeight = true,
            preserveCameraSeparation = true, headingChanged = true, invalidReferenceRejected = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS EventCG reference frames assets=" + assets.Length + " cases=" + checks + "; Unity placement=false");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
