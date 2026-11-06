using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class EventCgSceneChecks
{
    private static ADV.EventCG.Data.Scene.Chara Actor(JsonElement raw)
    {
        var actor = new ADV.EventCG.Data.Scene.Chara { no = raw.GetProperty("no").GetInt32() };
        var motion = raw.GetProperty("motionAndItem");
        actor.motionAndItem.motion = new Illusion.Game.Elements.EasyLoader.Motion(motion.GetProperty("motion").GetProperty("_bundle").GetString(), motion.GetProperty("motion").GetProperty("_asset").GetString());
        actor.motionAndItem.ik = new Illusion.Game.Elements.EasyLoader.IKMotion(motion.GetProperty("ik").GetProperty("_bundle").GetString(), motion.GetProperty("ik").GetProperty("_asset").GetString());
        actor.motionAndItem.yure = new Illusion.Game.Elements.EasyLoader.YureMotion(motion.GetProperty("yure").GetProperty("_bundle").GetString(), motion.GetProperty("yure").GetProperty("_asset").GetString());
        return actor;
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start EventCG scene planning from installed hierarchies; no Unity motion/IK playback");
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "adv-eventcg-asset-checks.json")));
        int assets = zero, scenesChecked = zero, steps = zero;
        foreach (var asset in receipt.RootElement.GetProperty("assets").EnumerateArray())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, asset.GetProperty("hierarchy").GetString())));
            var raw = document.RootElement.GetProperty("nodes").EnumerateArray().SelectMany(node => node.GetProperty("components").EnumerateArray())
                .Single(component => component.GetProperty("script").ValueKind == JsonValueKind.Object
                    && component.GetProperty("script").GetProperty("m_Namespace").GetString() == "ADV.EventCG"
                    && component.GetProperty("script").GetProperty("m_ClassName").GetString() == "Data").GetProperty("data");
            var scenes = raw.GetProperty("scenes").EnumerateArray().Select(scene => new ADV.EventCG.Data.Scene
                { charas = scene.GetProperty("charas").EnumerateArray().Select(Actor).ToArray() }).ToArray();
            var slots = scenes.SelectMany(scene => scene.charas).Select(actor => actor.no).Distinct().ToArray();
            for (var index = zero; index < scenes.Length; index++)
            {
                var plan = KksAdvEventCgScenePlan.Create(scenes, index, slots, zero, one);
                foreach (var entry in plan)
                {
                    var expected = scenes[index].charas.First(actor => actor.no == entry.Slot);
                    var previous = index > zero ? scenes[index - one].charas.FirstOrDefault(actor => actor.no == entry.Slot)?.motionAndItem : null;
                    Require(ReferenceEquals(entry.Step, expected)
                        && ReferenceEquals(entry.PreviousIk, string.IsNullOrEmpty(expected.motionAndItem.ik.bundle) ? previous : null)
                        && ReferenceEquals(entry.PreviousYure, string.IsNullOrEmpty(expected.motionAndItem.yure.bundle) ? previous : null), "Installed EventCG scene predecessor changed");
                    steps++;
                }
                scenesChecked++;
            }
            Require(!KksAdvEventCgScenePlan.Create(scenes, -one, slots, zero, one).Any()
                && !KksAdvEventCgScenePlan.Create(scenes, scenes.Length, slots, zero, one).Any(), "Invalid native scene index must be a no-op");
            assets++;
        }
        ADV.EventCG.Data.Scene.Chara Chara(int slot, string ik, string yure)
        {
            var actor = new ADV.EventCG.Data.Scene.Chara { no = slot };
            actor.motionAndItem.ik = new Illusion.Game.Elements.EasyLoader.IKMotion(ik, "");
            actor.motionAndItem.yure = new Illusion.Game.Elements.EasyLoader.YureMotion(yure, ""); return actor;
        }
        var first = Chara(zero, "first-ik", "first-yure");
        var second = Chara(zero, "second-ik", ""); var duplicate = Chara(zero, "ignored-ik", "ignored-yure");
        var third = Chara(zero, "", "third-yure");
        var synthetic = new[] { new ADV.EventCG.Data.Scene { charas = new[] { first } },
            new ADV.EventCG.Data.Scene { charas = new[] { second, duplicate } }, new ADV.EventCG.Data.Scene { charas = new[] { third } } };
        var middle = KksAdvEventCgScenePlan.Create(synthetic, one, new[] { int.MaxValue, zero }, zero, one).Single();
        Require(middle.Slot == zero && ReferenceEquals(middle.Step, second) && middle.PreviousIk == null
            && ReferenceEquals(middle.PreviousYure, first.motionAndItem), "Duplicate slots or independent IK/yure inheritance changed");
        var last = KksAdvEventCgScenePlan.Create(synthetic, one + one, new[] { zero }, zero, one).Single();
        Require(ReferenceEquals(last.PreviousIk, second.motionAndItem) && last.PreviousYure == null,
            "Jumping to a scene must use index-previous, not a last-played-state fallback");
        Require(!KksAdvEventCgScenePlan.Create(null, zero, new[] { zero }, zero, one).Any(), "Missing native scene array must be a no-op");
        File.WriteAllText(Path.Combine(directory, "adv-eventcg-scene-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production native-scene planner using copied installed scene records; engine IK/yure/actors/items/camera not executed",
            assets, scenes = scenesChecked, actorSteps = steps,
            predecessorBySceneIndex = true, independentIkAndYure = true, duplicateCharaFirstMatch = true,
            registeredSlotsOnly = true, invalidSceneNoOp = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS EventCG scene planner assets={assets} scenes={scenesChecked} actorSteps={steps}; engine playback=false");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
