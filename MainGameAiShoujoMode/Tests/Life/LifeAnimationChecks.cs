using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Life;

internal static class LifeAnimationChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string root)
    {
        var content = Path.GetFullPath(Path.Combine(root, "../../MainGameAiShoujoMode/Package/Content"));
        var catalog = AiLifeAnimationCatalog.Load(content);
        Require(catalog != null && catalog.Actions.Any(), "Original action index missing");
        foreach (var clip in catalog.clips.Where(v => v.status == "converted"))
        {
            var sequence = catalog.Sequence("clip:" + clip.id);
            Require(sequence.Single().Clip.id == clip.id && File.Exists(Path.Combine(content, "LifeAnimations", clip.bundle)), "Converted clip port missing");
        }
        foreach (var action in catalog.Actions)
        {
            if (action.status == "connected")
            {
                var sequence = catalog.Sequence(action.id);
                Require(sequence.Count(v => v.Phase == "main") == Math.Sign(sequence.Length), "Main phase missing or duplicated");
                Require(sequence.Length == catalog.bindings.Count(v => v.id == action.id), "Source sequence was shortened");
                var main = Array.FindIndex(sequence, v => v.Phase == "main");
                Require(sequence.Take(main).All(v => v.Phase.StartsWith("main:enter:", StringComparison.Ordinal))
                    && sequence.Skip(main + Math.Sign(sequence.Length)).All(v => v.Phase == "out" || v.Phase.StartsWith("out:enter:", StringComparison.Ordinal)), "Entrance/main/exit order changed");
            }
            else
            {
                var rejected = false;
                try { catalog.Sequence(action.id); } catch (InvalidDataException) { rejected = true; }
                Require(rejected && !string.IsNullOrWhiteSpace(action.reason), "Unavailable action claimed playable");
            }
        }
        var settings = AiLifeRuntimeSettings.LoadContent(content);
        settings.savePath = Path.Combine(root, "activity-checks", Guid.NewGuid().ToString("N"), "motion-world.json");
        var runtime = new AiLifeRuntime(settings, () => DateTime.Now, null);
        runtime.LoadItems(content); runtime.LoadActivities(content);
        var life = new AiLifeController(runtime, Guid.NewGuid().ToString("N"), "motion-check");
        var selected = catalog.Actions.First(v => v.status == "connected");
        var operation = Guid.NewGuid().ToString("N");
        life.BeginConversation(); life.QueueSourceMotion(selected.id, operation);
        var revision = runtime.Store.Read().revision;
        life.QueueSourceMotion(selected.id, operation);
        Require(runtime.Store.Read().revision == revision && !life.CompleteActivity(operation), "Motion replay or conversation committed result");
        life.EndConversation();
        Require(life.DesiredAction == "motion" && !life.CompleteActivity(operation), "Motion finished before playback");
        life.ActivityDestination(operation, "motion:" + selected.id, default, default, default); life.ActivityArrived(operation);
        var reopened = new AiLifeRuntime(settings, () => DateTime.Now, null);
        reopened.LoadItems(content); reopened.LoadActivities(content);
        var restored = new AiLifeController(reopened, life.ResidentId, "motion-check");
        Require(restored.State.activity.animationActionId == selected.id && restored.State.activity.stage == "performing", "Saved motion intent missing");
        Require(restored.CompleteActivity(operation) && !restored.CompleteActivity(operation) && restored.State.action == "normal", "Motion failed single completion and return");
        restored.QueueSourceMotion("clip:" + catalog.clips.First(v => v.status == "converted").id, Guid.NewGuid().ToString("N"));
        restored.CancelActivity("cancel test");
        Require(restored.State.activity == null, "Clip cancellation left a pending action");
        Console.WriteLine("AI_LIFE_ANIMATION_CHECK_PASS allClipPorts allActionPorts completeSequences blockedReasons persistedIntent duplicate noEarlyEffect cancel returnNormal");
    }
}
