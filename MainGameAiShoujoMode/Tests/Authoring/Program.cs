using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Authoring;

internal static class Program
{
    private static int count;
    private static void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { Check(true, message); return; } throw new Exception("Accepted invalid data: " + message); }
    private static AuthoringProject Example(string name = "minimal") => AuthoringJson.Read<AuthoringProject>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", name + ".json")));
    private static void Main()
    {
        Console.WriteLine("EVENT_AUTHORING_CHECKS_BEGIN");
        var root = Path.Combine(Path.GetTempPath(), "event-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Documents(); Persistence(root); Flow(); Console.WriteLine("EVENT_AUTHORING_CHECKS_PASS assertions=" + count); }
        finally { Directory.Delete(root, true); }
    }
    private static void Documents()
    {
        foreach (var name in new[] { "minimal", "staging", "freeh" }) { var p = Example(name); p.Validate(); Check(AuthoringJson.Write(AuthoringJson.Copy(p)) == AuthoringJson.Write(p), name + " roundtrip"); }
        var valid = Example(); var identity = valid.events.events[default(int)].nodes[default(int)].id;
        var p2 = AuthoringJson.Copy(valid); p2.events.events[default(int)].startNode = identity; p2.events.events[default(int)].nodes.Reverse(); p2.Validate(); Check(p2.events.events[default(int)].nodes.Single(n => n.id == p2.events.events[default(int)].startNode).text == valid.events.events[default(int)].nodes[default(int)].text, "reorder preserves explicit references");
        var invalid = AuthoringJson.Copy(valid); invalid.characters.characters.Add(AuthoringJson.Copy(invalid.characters.characters[default(int)])); Reject(invalid.Validate, "duplicate character id");
        invalid = AuthoringJson.Copy(valid); invalid.events.events.Add(AuthoringJson.Copy(invalid.events.events[default(int)])); Reject(invalid.Validate, "duplicate event id");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes.Add(AuthoringJson.Copy(invalid.events.events[default(int)].nodes[default(int)])); Reject(invalid.Validate, "duplicate node id");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes[default(int)].next = "missing"; Reject(invalid.Validate, "missing next");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes[default(int)].characterId = "missing"; Reject(invalid.Validate, "missing actor");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes[default(int)].seconds = float.NaN; Reject(invalid.Validate, "nonfinite time");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].camera = new CameraCue { fieldOfView = float.PositiveInfinity }; Reject(invalid.Validate, "nonfinite camera");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes[default(int)].type = "not-supported"; Reject(invalid.Validate, "unknown node type");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].nodes[default(int)].type = "choice"; Reject(invalid.Validate, "empty choices");
        invalid = AuthoringJson.Copy(valid); invalid.characters.characters[default(int)].voice = new VoiceReference { source = "file" }; Reject(invalid.Validate, "empty file path");
        invalid = AuthoringJson.Copy(valid); invalid.characters.characters[default(int)].voice = new VoiceReference { source = "game", bundle = "sound/a" }; Reject(invalid.Validate, "missing game asset");
        invalid = AuthoringJson.Copy(valid); invalid.characters.characters[default(int)].voice = new VoiceReference { pitch = default(int) }; Reject(invalid.Validate, "invalid pitch");
        invalid = AuthoringJson.Copy(valid); invalid.events.events[default(int)].afterEvent = invalid.events.events[default(int)].id; Reject(invalid.Validate, "self trigger");
        invalid = AuthoringJson.Copy(valid); invalid.events.schemaVersion = Numbers.One + Numbers.One; Reject(invalid.Validate, "schema version");
        var draft = new AuthoringDraft(valid); Check(!draft.Dirty, "initial draft clean");
        draft.Change(p => p.events.events[default(int)].nodes[default(int)].text = "自分の会話"); Check(draft.Dirty, "text dirty");
        draft.Undo(); Check(!draft.Dirty && draft.Project.events.events[default(int)].nodes[default(int)].text == valid.events.events[default(int)].nodes[default(int)].text, "undo");
        draft.Redo(); Check(draft.Dirty && draft.Project.events.events[default(int)].nodes[default(int)].text == "自分の会話", "redo");
        draft.MarkSaved(); draft.Change(p => p.characters.characters[default(int)].name = "別名"); draft.Discard(); Check(!draft.Dirty && draft.Project.characters.characters[default(int)].name == valid.characters.characters[default(int)].name, "discard");
        var before = AuthoringJson.Write(draft.Project);
        try { draft.Change(p => { p.events.events.Clear(); throw new IOException("injected"); }); } catch (IOException) { }
        Check(AuthoringJson.Write(draft.Project) == before, "failed mutation rollback");
        SceneDocuments();
    }
    private static void SceneDocuments()
    {
        var project = Example("freeh"); project.Validate(); Check(project.events.events.First().sceneMode == "freeH", "free H scene stored");
        project.events.events.First().freeH.femaleId = "missing"; Reject(project.Validate, "free H missing participant");
        project = Example("freeh"); project.events.events.First().freeH.position = null; Reject(project.Validate, "free H missing position");
        project = Example(); var item = project.events.events.First(); item.placements.Add(new ActorPlacement { characterId = "girl", condition = new ActorCondition { clothing = "invalid" } }); Reject(project.Validate, "unknown clothing state");
        item.placements.First().condition.clothing = "half"; project.Validate(); Check(AuthoringJson.Copy(project).events.events.First().placements.First().condition.clothing == "half", "actor condition roundtrip");
        var motion = new MotionCue { characterId = "girl", source = "controller", bundle = "motion/controller", asset = "controller", state = "Idle" };
        item.nodes.First().animations = new List<MotionCue> { motion }; project.Validate(); Check(AuthoringJson.Copy(project).events.events.First().nodes.First().animations.First().state == "Idle", "per line animation roundtrip");
        motion.characterId = "missing"; Reject(project.Validate, "animation actor reference"); motion.characterId = "girl"; motion.speed = default(float); Reject(project.Validate, "animation speed"); motion.speed = Numbers.One;
        item.items = new List<ItemPlacement> { new ItemPlacement { id = "chair", source = "map" } }; Reject(project.Validate, "missing item source path");
        item.items.First().path = "Furniture/Chair"; project.Validate(); Check(AuthoringJson.Copy(project).events.events.First().items.First().path == "Furniture/Chair", "item placement roundtrip");
        item.items.Add(AuthoringJson.Copy(item.items.First())); Reject(project.Validate, "duplicate item id");
        var optional = AuthoringJson.Read<AuthoringProject>("{\"characters\":{\"schemaVersion\":1,\"characters\":[]},\"events\":{\"schemaVersion\":1,\"events\":[{\"id\":\"event\",\"title\":\"event\",\"placements\":[],\"nodes\":[{\"id\":\"line\",\"type\":\"dialogue\",\"choices\":[],\"placements\":[]}]}]}}"); optional.Validate();
        Check(optional.events.events.First().nodes.First().expression == -Numbers.One && optional.events.events.First().nodes.First().next == "", "JSON omitted fields use documented defaults");
    }
    private static void Persistence(string root)
    {
        var store = new AuthoringStore(root); Check(store.Load().events.events.Count == default(int), "new project empty"); var project = Example(); store.Save(project);
        Check(AuthoringJson.Write(new AuthoringStore(root).Load()) == AuthoringJson.Write(project), "disk roundtrip");
        var second = new AuthoringStore(root); var other = second.Load(); project.events.events[default(int)].title = "変更"; store.Save(project);
        bool conflict = false; try { second.Save(other); } catch (IOException) { conflict = true; } Check(conflict, "external write rejected");
        Check(new AuthoringStore(root).Load().events.events[default(int)].title == "変更", "conflict preserves committed data");
        Check(Directory.GetDirectories(Path.Combine(root, "Backups")).Length == AuthoringDefaults.Current.two, "backups");
        Check(store.Resolve("Audio/voice.wav").StartsWith(root), "relative audio path"); Check(store.Resolve(Path.Combine(root, "voice.wav")) == Path.Combine(root, "voice.wav"), "absolute audio path");
        var chars = Path.Combine(root, "characters.json"); var saved = File.ReadAllText(chars); File.WriteAllText(chars, "broken");
        bool corrupt = false; try { second.Load(); } catch { corrupt = true; } Check(corrupt, "broken JSON rejected"); Check(File.ReadAllText(chars) == "broken", "no corrupt overwrite"); File.WriteAllText(chars, saved);
        var eventPath = Path.Combine(root, "events.json"); var backup = Path.Combine(root, "Backups", "interrupted"); Directory.CreateDirectory(backup);
        var backupCharacters = Path.Combine(backup, "characters.json"); var backupEvents = Path.Combine(backup, "events.json"); File.Copy(chars, backupCharacters); File.Copy(eventPath, backupEvents);
        File.WriteAllText(Path.Combine(root, "pending-save.json"), AuthoringJson.Write(new Dictionary<string, string> { [chars] = backupCharacters, [eventPath] = backupEvents })); File.WriteAllText(eventPath, "incomplete");
        Check(new AuthoringStore(root).Load().events.events[default(int)].title == "変更", "interrupted pair recovery"); Check(!File.Exists(Path.Combine(root, "pending-save.json")), "recovery journal cleared");
    }
    private sealed class Host : AuthoringRunner.IHost
    {
        internal readonly List<string> Log = new List<string>(); internal Action Pending; internal Action<Exception> Fail; internal int Restores; internal bool AsyncEnter, AutoCues = true;
        public void Enter(AuthoringEvent item, Action completed, Action<Exception> failed) { Log.Add("event:" + item.id); Fail = failed; if (AsyncEnter) Pending = completed; else completed(); }
        public void Execute(EventNode node, Action completed, Action<Exception> failed) { Log.Add(node.id); Fail = failed; if (AutoCues && node.type != "dialogue" && node.type != "choice") completed(); else Pending = completed; }
        public void Pause(bool paused) { Log.Add(paused ? "pause" : "resume"); }
        public void Restore() { Restores++; }
    }
    private static void Flow()
    {
        var project = Example(); var host = new Host(); var run = new AuthoringRunner(host); run.Start(project, "hello"); Check(run.Active && run.Node.id == "greeting", "dialogue waits");
        var stale = host.Pending; run.SetPaused(true); run.Advance(); Check(run.Node.id == "greeting", "paused advance ignored"); run.SetPaused(false); run.Advance(); Check(!run.Active && host.Restores == Numbers.One, "complete restores once"); stale(); Check(!run.Active && host.Restores == Numbers.One, "stale completion rejected");
        run.Stop(); Check(host.Restores == Numbers.One, "double stop");
        host = new Host(); run = new AuthoringRunner(host); run.Start(Example("staging"), "branch-demo"); Check(run.Node.id == "question", "choice waits"); run.Choose("no"); Check(!run.Active && !host.Log.Contains("answer"), "choice bypasses other path");
        run.Start(Example("staging"), "branch-demo"); run.Choose("yes"); Check(run.Node.id == "answer", "choice route"); run.Advance(); Check(!run.Active && host.Log.Contains("delay"), "synchronous finite cue");
        host = new Host { AsyncEnter = true }; run = new AuthoringRunner(host); run.Start(project, "hello"); var old = host.Pending; run.Stop(); run.Start(project, "hello"); old(); Check(run.Node == null, "old map completion cannot affect next run"); host.Pending(); Check(run.Node.id == "greeting", "new map completion");
        host = new Host { AsyncEnter = true }; run = new AuthoringRunner(host); run.Start(project, "hello"); run.SetPaused(true); host.Pending(); Check(run.Node == null, "paused map completion deferred"); run.SetPaused(false); Check(run.Node.id == "greeting", "resume deferred map"); run.Stop();
        host = new Host(); run = new AuthoringRunner(host); bool failed = false; run.Failed += ex => failed = true; run.Start(project, "hello"); host.Fail(new IOException("missing audio")); Check(failed && !run.Active && host.Restores == Numbers.One, "audio failure restores");
        var chain = Example(); var next = new AuthoringEvent { id = "next", afterEvent = "hello" }; next.nodes.Add(new EventNode { id = "next-line", text = "続き" }); chain.events.events.Add(next);
        host = new Host(); run = new AuthoringRunner(host); run.Start(chain, "hello"); run.Advance(); Check(run.Active && run.EventId == "next", "after event trigger"); Check(chain.events.events[default(int)].nodes[default(int)].text == Example().events.events[default(int)].nodes[default(int)].text, "source project unchanged"); run.Stop();
        var cycle = Example(); cycle.events.events[default(int)].nodes.Clear(); cycle.events.events[default(int)].nodes.Add(new EventNode { id = "loop", type = "wait", next = "loop" });
        host = new Host(); run = new AuthoringRunner(host); failed = false; run.Failed += ex => failed = true; run.Start(cycle, "hello"); Check(failed && !run.Active && host.Restores == Numbers.One, "immediate cycle bounded");
        project = Example(); host = new Host(); run = new AuthoringRunner(host); run.Start(project, "hello"); var failOld = host.Fail; run.Stop(); run.Start(project, "hello"); failOld(new Exception("old")); Check(run.Active, "old failure rejected");
        host = new Host(); run = new AuthoringRunner(host); project = Example(); run.Start(project, "hello"); var previousCompletion = host.Pending;
        run.SetPaused(true); var edited = AuthoringJson.Copy(project); edited.events.events.First().nodes.First().text = "再生しながら修正"; run.ApplyDraft(edited); run.SetPaused(false);
        Check(run.Node.text == "再生しながら修正", "paused edits applied to current line"); previousCompletion(); Check(run.Active && run.Node.id == "greeting", "old completion rejected after edit"); run.Advance(); Check(!run.Active, "edited dialogue continues");
    }
}
