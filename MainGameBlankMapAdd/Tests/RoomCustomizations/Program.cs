using System;
using System.IO;
using MainGameBlankMapAdd;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    private static void Reject(Action action, string name) { try { action(); } catch { checks++; return; } throw new Exception(name); }
    private static CategoryLocation Place(string key, int tag, float x) => new CategoryLocation { MapKey = key, Category = tag,
        Placement = new HLocationPreset { Id = "category-" + tag, MapKey = key, Name = "tag", Legacy = true,
            Position = new[] { x, 0f, 0f }, Rotation = new[] { 0f, 90f, 0f }, Center = new float[3] } };
    private static void Main()
    {
        Console.WriteLine("Testing real room customization models and atomic persistence");
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../.build/room-customizations", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Check(CategoryLocationSelection.Resolve(new[] { 4, 7 }, new[] { 7, 4 }) == 4, "native declaration order wins");
        Check(CategoryLocationSelection.Resolve(new[] { 4, 7 }, new[] { 7 }) == 7, "active native tag selected");
        Check(CategoryLocationSelection.Resolve(new[] { 4, 7 }, new[] { 4, 7 }, new[] { 7 }) == 7, "native local-move category wins over empty file entry");
        Check(CategoryLocationSelection.Resolve(new[] { 4, 7 }, new[] { 4, 7 }, Array.Empty<int>()) == 4, "no local-move file uses first matching place tag");
        Check(CategoryLocationSelection.Resolve(new[] { 4, 7 }, new[] { 99 }) == 4, "hotel all-pose fallback deterministic");
        Check(CategoryLocationSelection.Resolve(Array.Empty<int>(), new[] { 4 }) == null, "empty tag never invents a category");
        Check(CategoryLocationSelection.Resolve(null, null) == null, "unready pose cannot be saved");
        var doc = new CategoryLocationDocument();
        doc.Entries.Add(Place("source:hotel-room-201", 4, 1));
        doc.Entries.Add(Place("source:hotel-room-202", 4, 2));
        doc.Entries.Add(Place("source:hotel-room-reception-suite", 4, 3));
        doc.Entries.Add(Place("source:hotel-room-201", 7, 9));
        string path = Path.Combine(root, "locations.json");
        RoomCustomizationStore.Save(path, doc, d => d.Validate());
        var loaded = RoomCustomizationStore.Load<CategoryLocationDocument>(path, d => d.Validate());
        Check(loaded.Find("source:hotel-room-201", 4).Placement.Position[0] == 1, "room 201 restored");
        Check(loaded.Find("source:hotel-room-202", 4).Placement.Position[0] == 2, "room 202 isolated");
        Check(loaded.Find("source:hotel-room-reception-suite", 4).Placement.Position[0] == 3, "reception suite isolated");
        Check(loaded.Find("source:hotel-room-201", 7).Placement.Position[0] == 9, "different category isolated");
        Check(loaded.Find("map-other", 4) == null, "other map has no borrowed placement");
        var copy = loaded.Clone(); copy.Find("source:hotel-room-201", 4).Placement.Position[0] = 44;
        Check(loaded.Find("source:hotel-room-201", 4).Placement.Position[0] == 1, "draft clone cannot corrupt original");
        copy.Automatic = false; RoomCustomizationStore.Save(path, copy, d => d.Validate());
        Check(!RoomCustomizationStore.Load<CategoryLocationDocument>(path, d => d.Validate()).Automatic, "automatic-off persists");
        Check(File.Exists(path + ".bak"), "overwrite retains backup");
        string before = File.ReadAllText(path);
        copy.Entries.Add(copy.Entries[0].Clone());
        Reject(() => RoomCustomizationStore.Save(path, copy, d => d.Validate()), "duplicate map/tag rejected");
        Check(File.ReadAllText(path) == before, "invalid save preserves exact previous bytes");
        var bad = loaded.Clone(); bad.Entries[0].Placement.Position[0] = float.NaN;
        Reject(() => bad.Validate(), "nonfinite coordinates rejected");
        var reverbs = new RoomReverbDocument();
        reverbs.Entries.Add(new RoomReverbSetting { MapKey = "source:hotel-room-201", Preset = "Bathroom", Strength = .3f });
        reverbs.Entries.Add(new RoomReverbSetting { MapKey = "source:hotel-room-202", Enabled = false, Strength = 0 });
        string audio = Path.Combine(root, "reverb.json");
        RoomCustomizationStore.Save(audio, reverbs, d => d.Validate());
        var restored = RoomCustomizationStore.Load<RoomReverbDocument>(audio, d => d.Validate());
        Check(restored.Find("source:hotel-room-201").Strength == .3f, "reverb strength restored");
        Check(!restored.Find("source:hotel-room-202").Enabled, "room-specific off restored");
        Check(restored.Find("source:hotel-room-reception-suite") == null, "reverb never leaks to reception");
        restored.Entries[0].Strength = float.PositiveInfinity;
        Reject(() => restored.Validate(), "invalid reverb strength rejected");
        File.WriteAllText(audio, "broken");
        Reject(() => RoomCustomizationStore.Load<RoomReverbDocument>(audio, d => d.Validate()), "broken file surfaced");
        Check(File.ReadAllText(audio) == "broken", "broken input is never replaced with defaults");
        var poses = new PoseLocationDocument();
        poses.Initial.Add(Place("hotel-a", 0, 10).Placement);
        poses.Initial.Add(Place("hotel-b", 0, 20).Placement);
        poses.Entries.Add(new PoseLocation { Pose = "aibu\n1\nbundle\nclip-a", Placement = Place("hotel-a", 4, 100).Placement });
        poses.Entries.Add(new PoseLocation { Pose = "aibu\n2\nbundle\nclip-b", Placement = Place("hotel-a", 4, 200).Placement });
        string posePath = Path.Combine(root, "poses.json");
        RoomCustomizationStore.Save(posePath, poses, p => p.Validate());
        var poseReload = RoomCustomizationStore.Load<PoseLocationDocument>(posePath, p => p.Validate());
        for (int i = 0; i < 3; i++)
        {
            Check(poseReload.Resolve("hotel-a", "aibu\n1\nbundle\nclip-a", null).Position[0] == 100, "pose A recalls its own position");
            Check(poseReload.Resolve("hotel-a", "unregistered", Place("hotel-a", 0, 30).Placement).Position[0] == 30, "unsaved pose uses fresh map default rather than saved initial");
            Check(poseReload.Resolve("hotel-a", "aibu\n2\nbundle\nclip-b", null).Position[0] == 200, "same-tag pose B has independent position");
        }
        Check(poseReload.Resolve("hotel-b", "aibu\n1\nbundle\nclip-a", null) == null, "other hotel room never borrows a saved initial");
        Check(poseReload.Resolve("hotel-new", "aibu\n1\nbundle\nclip-a", Place("hotel-new", 0, 30).Placement).Position[0] == 30, "new room uses captured initial position without borrowing");
        Check(poseReload.Resolve("hotel-missing", "unregistered", null) == null, "missing initial position is not invented");
        var edited = poseReload.Clone();
        edited.Entries[0].Placement.Position[0] = 999;
        Check(edited.BaseFor("hotel-a").Position[0] == 10 && poseReload.Entries[0].Placement.Position[0] == 100, "editing pose preserves baseline and previous document");
        edited.Entries.RemoveAt(0);
        Check(edited.Resolve("hotel-a", "aibu\n1\nbundle\nclip-a", Place("hotel-a", 0, 30).Placement).Position[0] == 30, "removed pose falls back to current map default");
        edited.Entries.Add(edited.Entries[0].Clone());
        Reject(() => edited.Validate(), "duplicate pose rejected");
        var orphan = poseReload.Clone(); orphan.Initial.Clear();
        orphan.Validate();
        Check(orphan.Resolve("hotel-a", "aibu\n1\nbundle\nclip-a", null).Position[0] == 100, "V2 individual placement does not require saved initial");
        var legacy = poseReload.Clone(); legacy.Version = 1;
        RoomCustomizationStore.Save(posePath, legacy, p => p.Validate());
        string legacyBytes = File.ReadAllText(posePath);
        var migrated = RoomCustomizationStore.Load<PoseLocationDocument>(posePath, p => p.Validate()).ForSave();
        Check(File.ReadAllText(posePath) == legacyBytes, "reading V1 never writes migration");
        Check(migrated.Version == 2 && migrated.Initial.Count == 0 && migrated.Entries.Count == 2, "V1 upgrades only on explicit save without losing individual placements");
        RoomCustomizationStore.Save(posePath, migrated, p => p.Validate());
        Check(File.ReadAllText(posePath + ".bak") == legacyBytes, "V2 save backs up exact V1 bytes");
        Console.WriteLine("PASS " + checks + " room customization checks");
    }
}
