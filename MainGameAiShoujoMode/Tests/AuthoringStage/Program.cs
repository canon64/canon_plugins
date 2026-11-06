using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using MainGameAiShoujoMode.Authoring;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static int count;
    private static void Check(bool value, string label) { count++; if (!value) throw new Exception(label); }
    private static void Main()
    {
        Console.WriteLine("EVENT_STAGE_BOUNDARY_BEGIN");
        var owner = new GameObject("Owner").AddComponent<MonoBehaviour>();
        var camera = new GameObject("Player camera").AddComponent<Camera>(); camera.fieldOfView = AuthoringDefaults.Current.defaultFieldOfView;
        var parent = new GameObject("Original actor placement"); var actor = new GameObject("Actor").AddComponent<ChaControl>(); actor.transform.SetParent(parent.transform, false);
        var source = new GameObject("Map A"); var renderer = source.AddComponent<Renderer>(); var collider = source.AddComponent<Collider>(); var light = source.AddComponent<Light>();
        var destination = new GameObject("Map B"); destination.SetActive(false);
        var native = new StageMap { Id = "kks:test", Name = "Native", Scene = "Native", Bundle = "map/test" };
        var maps = new List<StageMap> { new StageMap { Id = "a", Roots = new[] { source } }, new StageMap { Id = "b", Roots = new[] { destination } }, native };
        var store = new AuthoringStore(Path.Combine(Path.GetTempPath(), "authoring-stage-" + Guid.NewGuid().ToString("N")));
        var project = new AuthoringProject(); project.characters.characters.Add(new CharacterPreset { id = "girl" });
        bool blocked = false; int resumed = default(int);
        var stage = new AuthoringStage(owner, () => camera, binding => actor, () => () => resumed++, value => blocked = value, store, maps);
        stage.Begin(project); Check(blocked && !camera.enabled, "stage owns input and camera"); Check(stage.Camera != camera, "separate preview camera");
        stage.Place(new[] { new ActorPlacement { characterId = "girl", position = new Point { x = Numbers.One } } }); Check(parent.transform.position.x == Numbers.One, "real placement target");
        stage.Expression("girl", Numbers.One); Check(actor.GetEyesPtn() == Numbers.One, "expression applied");
        bool switched = false; stage.Map("b", () => switched = true, ex => throw ex); Check(switched && destination.activeSelf && !renderer.enabled && !collider.enabled && !light.enabled, "map hides originals");
        stage.Map("current", () => { }, ex => throw ex); Check(renderer.enabled && collider.enabled && light.enabled && !destination.activeSelf, "current restores visibility");
        stage.PlayVoice(new VoiceReference { source = "game", bundle = "voice", asset = "clip" }, "girl", ex => throw ex); Check(actor.asVoice != null && actor.asVoice.Playing, "native clip and lip sync");
        stage.Pause(true); Check(!actor.asVoice.Playing, "audio paused"); stage.Pause(false); Check(actor.asVoice.Playing, "audio resumed"); stage.StopVoice(); Check(actor.asVoice == null, "lip restored after stop");
        bool timed = false; stage.Wait(Numbers.One, () => timed = true); var timer = DOVirtual.Last; stage.Pause(true); timer.Complete(); Check(!timed, "timer paused"); stage.Pause(false); timer.Complete(); Check(timed, "timer resumed");
        stage.Wait(Numbers.One, () => timed = false); timer = DOVirtual.Last;
        stage.Dispose(); Check(!blocked && camera.enabled && resumed == Numbers.One, "session restored"); Check(parent.transform.position.x == default(float) && actor.GetEyesPtn() == default(int), "actor restored"); timer.Complete(); Check(timed, "stopped deadline cancelled");
        stage.Dispose(); Check(resumed == Numbers.One, "double dispose safe");
        stage.Begin(project); bool late = false; stage.Map(native.Id, () => late = true, ex => throw ex); var nativeLoad = AssetBundleManager.Pending; stage.Dispose(); var lateRoot = new GameObject("Late native"); SceneManager.Scenes[native.Scene] = new[] { lateRoot }; nativeLoad.Finish(); Check(!late && !SceneManager.Scenes.ContainsKey(native.Scene), "late native load released");
        stage.Begin(project); stage.Map(native.Id, () => switched = true, ex => throw ex); var import = new GameObject("Imported native"); var mapCamera = import.AddComponent<Camera>(); SceneManager.Scenes[native.Scene] = new[] { import }; AssetBundleManager.Pending.Finish(); Check(import.activeSelf && !mapCamera.enabled, "imported map camera disabled"); stage.Dispose(); Check(!SceneManager.Scenes.ContainsKey(native.Scene) && renderer.enabled, "owned scene unloaded");
        bool released = false; var customRoot = new GameObject("Custom");
        IEnumerator LoadCustom() { yield return null; }
        var custom = new StageMap { Id = "custom", Load = LoadCustom, GetRoots = () => new[] { customRoot }, Release = () => released = true }; maps.Add(custom);
        stage.Begin(project); stage.Map("custom", () => late = true, ex => throw ex); stage.Dispose(); Check(!released, "pending custom load retains cleanup owner"); MonoBehaviour.Tick(); Check(released, "pending custom load released after completion");
        Directory.CreateDirectory(store.DirectoryPath); var audio = Path.Combine(store.DirectoryPath, "sample.wav"); File.WriteAllBytes(audio, Array.Empty<byte>());
        stage.Begin(project); stage.PlayVoice(new VoiceReference { source = "file", path = audio }, "girl", ex => throw ex); var request = UnityWebRequest.Last; stage.Dispose(); Check(request.Aborted && request.Disposed, "pending file audio cancelled"); MonoBehaviour.Tick(); Check(actor.asVoice == null, "stale audio cannot restore playback");
        stage.Begin(project); AssetBundleData.Missing = true; bool missing = false; try { stage.PlayVoice(new VoiceReference { source = "game", bundle = "voice", asset = "missing" }, "girl", ex => throw ex); } catch (FileNotFoundException) { missing = true; } Check(missing, "missing game clip rejected"); stage.Dispose();
        var hOwner = new GameObject("H authoring owner").AddComponent<EventAuthoringEditor>();
        bool hReady = false, hEnded = false;
        var hItem = new AuthoringEvent { id = "h-event", sceneMode = "freeH", freeH = new FreeHSetup { femaleId = "girl" } }; project.events.events.Add(hItem);
        stage.Begin(project); stage.Map("a", () => { }, ex => throw ex);
        var bridge = new AuthoringFreeHBridge(hOwner, () => hReady = true, () => hEnded = true, ex => throw ex);
        bridge.Begin(stage, project, hItem, null, true, new MapEnvironmentRuntime.Parameters());
        Check(hReady && bridge.Ready && bridge.HoldingStage, "native H gets a fresh permit");
        Check(MainGameAiShoujoMode.AiModeHSession.Last.Location.Categories.Length > default(int), "entry categories remain nonempty");
        Check(!actor.visibleAll, "prepared source actor hidden during native H");
        bridge.Cancel(); Check(hEnded && !bridge.HoldingStage && camera.enabled, "native H returns and restores stage"); bridge.Dispose();
        stage.Begin(project); stage.Map("a", () => { }, ex => throw ex); CanonGate.GateApi.Denied = true;
        bridge = new AuthoringFreeHBridge(hOwner, () => { }, () => { }, ex => { }); bool rejected = false;
        try { bridge.Begin(stage, project, hItem, null, true, new MapEnvironmentRuntime.Parameters()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !bridge.HoldingStage && camera.enabled, "denied native H releases prepared stage"); bridge.Dispose(); CanonGate.GateApi.Denied = false;
        Directory.Delete(store.DirectoryPath, true);
        Console.WriteLine("EVENT_STAGE_BOUNDARY_PASS assertions=" + count);
    }
}
