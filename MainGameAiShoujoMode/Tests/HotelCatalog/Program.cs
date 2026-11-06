using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode;

internal static class Program
{
    private static readonly JsonDocument Fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture.json")));
    private static int N(string key) => Fixture.RootElement.GetProperty(key).GetInt32();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static HSceneProc.AnimationListInfo Pose(string name, int category) => new HSceneProc.AnimationListInfo
    {
        nameAnimation = name, mode = "aibu", paramFemale = new HSceneProc.Param { path = new HSceneProc.ResourcePath { assetpath = "test.bundle", file = name } },
        lstCategory = new List<HSceneProc.Category> { new HSceneProc.Category { category = category } }
    };
    private static void Main()
    {
        int outfit = default(int), reloads = default(int);
        HotelRoomCoordinateRestore.Apply(N("saved"), N("available"), () => outfit, () => { reloads++; outfit = N("saved"); return true; });
        Require(outfit == N("saved") && reloads == N("one"), "Saved outfit was not reloaded before status application");
        HotelRoomCoordinateRestore.Apply(N("saved"), N("available"), () => outfit, () => { throw new Exception("Already-loaded outfit must not reload"); });
        foreach (int invalid in new[] { N("negative"), N("available") })
        {
            bool rejected = false;
            try { HotelRoomCoordinateRestore.Apply(invalid, N("available"), () => outfit, () => { reloads++; return true; }); }
            catch (System.IO.InvalidDataException) { rejected = true; }
            Require(rejected && outfit == N("saved") && reloads == N("one"), "Invalid outfit mutated the live actor");
        }
        foreach (bool reportedSuccess in new[] { false, true })
        {
            bool rejected = false;
            try { HotelRoomCoordinateRestore.Apply(N("one"), N("available"), () => outfit, () => reportedSuccess); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Failed or ineffective outfit reload was reported as applied");
        }
        int entryCategory = default(int), specialCategory = int.MaxValue;
        var proc = new HSceneProc(); proc.categorys.Add(entryCategory);
        var owner = AiModeHSession.Attach(proc, entryCategory, true);
        // This is the native call order: GetCloseCategory precedes catalog generation.
        Require(!AiModeHSession.Categories(proc), "Hotel failed to own its category lookup");
        Require(proc.categorys.SequenceEqual(new[] { entryCategory }) && proc.useCategorys.Contains(entryCategory), "Empty pre-catalog state erased the entry category");
        var native = Pose("native", entryCategory); var addon = Pose("addon", specialCategory);
        proc.lstAnimInfo = new[] { new List<HSceneProc.AnimationListInfo> { native, addon }, null };
        var mappings = new Dictionary<HSceneProc.AnimationListInfo, string> { [addon] = "AnimationLoader addon" };
        AiModeHSession.Catalog(proc);
        var selectable = proc.lstUseAnimInfo.SelectMany(list => list).ToArray();
        var initial = selectable.FirstOrDefault(info => info.lstCategory.Any(c => proc.categorys.Contains(c.category)));
        Require(ReferenceEquals(initial, native), "Initial selection became null or entered a special scene");
        Require(proc.useCategorys.Contains(specialCategory) && mappings.ContainsKey(selectable.Single(info => info.nameAnimation == "addon")), "Addon availability or reference identity was lost");
        Require(!ReferenceEquals(proc.lstAnimInfo.First(), proc.lstUseAnimInfo.First()), "UI list writes would mutate the source catalog");
        owner.Ready();
        var saved = new HotelRoomSelection { mode = addon.mode, id = addon.id, name = addon.nameAnimation, bundle = addon.paramFemale.path.assetpath, asset = addon.paramFemale.path.file };
        Require(owner.RestoreRoomSelection(saved) && ReferenceEquals(proc.flags.nowAnimationInfo, addon), "Saved addon pose could not be restored");
        saved.name = "removed";
        Require(!owner.RestoreRoomSelection(saved) && ReferenceEquals(proc.flags.nowAnimationInfo, addon), "Missing pose was replaced with an unrelated item");
        Require(!owner.RestoreRoomSelection(saved, out string absentReason) && absentReason.Contains("候補数=0"), "Missing candidate reason lost");
        saved.name = addon.nameAnimation;
        var duplicate = Pose(addon.nameAnimation, specialCategory);
        proc.lstUseAnimInfo.First().Add(duplicate);
        Require(!owner.RestoreRoomSelection(saved, out string duplicateReason) && duplicateReason.Contains("候補数=2") && ReferenceEquals(proc.flags.nowAnimationInfo, addon), "Ambiguous candidate changed current pose or lost reason");
        var trace = new List<string>(); bool traceEnabled = true;
        owner.RoomPoseDiagnostics = () => traceEnabled;
        owner.RoomPoseTrace = (stage, text) => trace.Add(stage + " " + text());
        owner.DiagnosticSavedSelection = saved;
        proc.lstUseAnimInfo = new[] { new List<HSceneProc.AnimationListInfo> { native }, new List<HSceneProc.AnimationListInfo>() };
        owner.TraceRoomPoseCatalog("before-clone");
        proc.lstUseAnimInfo.First().Add(addon);
        owner.TraceRoomPoseCatalog("after-clone");
        AiModeHSession.Catalog(proc);
        Require(trace.Any(line => line.StartsWith("pose-catalog.before-clone") && line.Contains("exact=0"))
            && trace.Any(line => line.StartsWith("pose-catalog.after-clone") && line.Contains("exact=1")), "Unadded and added candidates were not distinguished");
        // A UI-only entry disappears when the hotel copies the source catalog again.
        var uiOnly = Pose("ui-only", entryCategory);
        owner.DiagnosticSavedSelection = new HotelRoomSelection { mode = uiOnly.mode, id = uiOnly.id, name = uiOnly.nameAnimation, bundle = uiOnly.paramFemale.path.assetpath, asset = uiOnly.paramFemale.path.file };
        proc.lstUseAnimInfo.First().Add(uiOnly); AiModeHSession.Catalog(proc);
        Require(trace.Any(line => line.StartsWith("pose-catalog.hotel-rebuild.before") && line.Contains("name=ui-only") && line.Contains("exact=1"))
            && trace.Any(line => line.StartsWith("pose-catalog.hotel-rebuild.after") && line.Contains("name=ui-only") && line.Contains("exact=0")), "List reconstruction loss was not observable");
        var finalList = proc.lstUseAnimInfo.First();
        owner.PrepareRoomPoseCatalog = scene =>
        {
            Require(ReferenceEquals(scene, proc), "Provider received a foreign scene");
            if (!scene.lstUseAnimInfo.First().Contains(uiOnly)) scene.lstUseAnimInfo.First().Add(uiOnly);
        };
        Require(owner.RestoreRoomSelection(owner.DiagnosticSavedSelection) && ReferenceEquals(proc.flags.nowAnimationInfo, uiOnly), "Final startup list did not receive its provider clone before restoration");
        Require(owner.RestoreRoomSelection(owner.DiagnosticSavedSelection) && finalList.Count(item => ReferenceEquals(item, uiOnly)) == N("one"), "Provider preparation duplicated a clone on repeated restoration");
        owner.PrepareRoomPoseCatalog = null;
        var wrongBundle = new HotelRoomSelection { mode = saved.mode, id = saved.id, name = saved.name, bundle = "different", asset = saved.asset };
        HotelPoseCatalogDiagnostics.Write(() => true, (stage, text) => trace.Add(stage + " " + text()), "field-mismatch", () => new[] { wrongBundle }, saved, () => "context", true);
        Require(trace.Last().Contains("match(mode/id/name/bundle/asset)=True/True/True/False/True"), "Identity field mismatch not isolated");
        int enumerations = default(int), contexts = default(int), messages = trace.Count;
        HotelPoseCatalogDiagnostics.Write(() => false, (stage, text) => trace.Add(text()), "off", () => { enumerations++; return new[] { saved }; }, saved, () => { contexts++; return "context"; }, true);
        traceEnabled = false; owner.TraceRoomPoseCatalog("off-session");
        Require(enumerations == default(int) && contexts == default(int) && messages == trace.Count, "OFF collected diagnostic candidates/context or output");
        var foreign = new HSceneProc();
        Require(AiModeHSession.Categories(foreign), "Foreign native scene was intercepted");
        AiModeHSession.Catalog(foreign);
        Require(foreign.lstUseAnimInfo.All(list => list == null), "Foreign catalog was modified");
        var ai = new HSceneProc(); AiModeHSession.Attach(ai, entryCategory, false);
        AiModeHSession.Catalog(ai);
        Require(ai.lstUseAnimInfo.All(list => list == null), "Existing AI mode received hotel catalog expansion");
        Console.WriteLine("HOTEL_CATALOG_PASS emptyBeforeBuild=true nonNullEntry=true addonIdentity=true savedPose=true missingPosePreserved=true foreignScenePreserved=true aiModePreserved=true");
    }
}
