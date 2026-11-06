using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using MainGameDollMode;

class Program
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static PluginSettings ReadSettings(string path)
    {
        using (var stream = File.OpenRead(path)) return (PluginSettings)new DataContractJsonSerializer(typeof(PluginSettings)).ReadObject(stream);
    }
    static void Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--capture-basic")
            {
                Console.WriteLine("[DollPresets] Capturing current deployed settings as basic (existing presets are preserved).");
                if (File.Exists(args[2])) { DollPresetLibrary.Load(args[2]); Console.WriteLine("Existing presets preserved."); return; }
                DollPresetLibrary.Create(ReadSettings(args[1])).Save(args[2]);
                Check(DollPresetLibrary.Load(args[2]).Find("basic").Values.Matches(DollPresetValues.Capture(ReadSettings(args[1]))), "basic capture mismatch");
                Console.WriteLine("PASS: current deployed settings captured exactly."); return;
            }
            Run(args[0]); Console.WriteLine("PASS: " + checks + " checks; actual settings, preset library, migration, immutable basic, save/reload, validation and atomic backup.");
        }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); Environment.ExitCode = 1; }
    }
    static void Run(string source)
    {
        var current = ReadSettings(source);
        var captured = DollPresetValues.Capture(current);
        var library = DollPresetLibrary.Create(current);
        Check(library.SelectedId == "basic" && library.Find("basic").Name == "基本", "initial selection");
        foreach (var field in typeof(DollPresetValues).GetFields(BindingFlags.Instance | BindingFlags.Public))
            Check(Equals(field.GetValue(captured), typeof(PluginSettings).GetField(field.Name).GetValue(current)), "current value lost: " + field.Name);
        foreach (var field in typeof(PluginSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (field.Name.StartsWith("DollMode") && field.Name != "DollModeEnabled") Check(typeof(DollPresetValues).GetField(field.Name) != null, "missing preset field: " + field.Name);
        bool rejected = false;
        try { library.Overwrite("basic", captured); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "basic overwritten"); rejected = false;
        try { library.Delete("basic"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "basic deleted");
        var custom = captured.Clone(); custom.DollModeMouthOpen = .71f; custom.DollModeEyeOverlayPngPath = "日本語の目.png";
        custom.DollModeModEffectsEnabled = true; custom.DollModeSpiralOverlayEnabled = true;
        custom.DollModeModTears = 2; custom.DollModeModDrool = 1; custom.DollModeModSaliva = 3;
        custom.DollModeModSweat = 2; custom.DollModeModNose = 1;
        custom.DollModeSpiralOffsetX = -.125f; custom.DollModeSpiralOffsetY = .05f;
        custom.DollModeModEffectsEnabled = true; custom.DollModeModBukkake = 3;
        string id = library.Add("テスト", custom); library.SelectedId = id;
        custom.DollModeMouthOpen = .2f;
        Check(library.Find(id).Values.DollModeMouthOpen == .71f, "custom values aliased");
        Check(library.Find("basic").Values.Matches(captured), "basic changed after adding");
        var settings = new PluginSettings { Enabled = false, DollModeEnabled = true, InfoLogEnabled = false, EyeMovementTraceLog = false };
        library.Find(id).Values.ApplyTo(settings);
        Check(!settings.Enabled && settings.DollModeEnabled && !settings.InfoLogEnabled && !settings.EyeMovementTraceLog, "mode or logs changed by preset");
        Check(settings.DollModeMouthOpen == .71f, "preset did not apply");
        Check(settings.DollModeSpiralOffsetX == -.125f && settings.DollModeSpiralOffsetY == .05f, "signed spiral position did not apply");
        Check(settings.DollModeModEffectsEnabled && settings.DollModeSpiralOverlayEnabled && settings.DollModeModTears == 2 && settings.DollModeModDrool == 1 && settings.DollModeModSaliva == 3 && settings.DollModeModSweat == 2 && settings.DollModeModNose == 1, "mod/overlay values did not apply");
        Check(settings.DollModeModEffectsEnabled && settings.DollModeModBukkake == 3, "bukkake did not apply");
        string results = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "results", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(results);
        string file = Path.Combine(results, "presets.json"); library.Save(file);
        var loaded = DollPresetLibrary.Load(file);
        Check(loaded.SelectedId == id && loaded.Find(id).Values.Matches(library.Find(id).Values), "save/reload");
        Check(loaded.Find(id).Values.DollModeModBukkake == 3, "bukkake lost on save/reload");
        string legacy = Path.Combine(results, "legacy.json");
        File.WriteAllText(legacy, File.ReadAllText(file).Replace("\"DollModeModBukkake\":3,", "").Replace("\"DollModeModBukkake\":0,", ""));
        Check(!File.ReadAllText(legacy).Contains("DollModeModBukkake"), "legacy fixture still has bukkake");
        Check(DollPresetLibrary.Load(legacy).Find(id).Values.DollModeModBukkake == 0, "legacy bukkake default");
        var outOfRange = captured.Clone(); outOfRange.DollModeModBukkake = 99;
        Check(outOfRange.Normalized().DollModeModBukkake == 3, "bukkake upper limit");
        outOfRange.DollModeModBukkake = -1;
        Check(outOfRange.Normalized().DollModeModBukkake == 0, "bukkake lower limit");
        string before = File.ReadAllText(file);
        var newValues = captured.Clone(); newValues.DollModeCheekRate = .64f; loaded.Overwrite(id, newValues); loaded.Save(file);
        Check(File.ReadAllText(file + ".bak") == before && !File.Exists(file + ".tmp"), "atomic backup");
        Check(DollPresetLibrary.Load(file).Find("basic").Values.Matches(captured), "basic lost on overwrite");
        loaded.Delete(id); Check(loaded.SelectedId == "basic" && loaded.Find(id) == null, "delete selected");
        rejected = false; try { loaded.Add("基本", captured); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "duplicate name");
        rejected = false; try { loaded.Add(" ", captured); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "empty name");
        string broken = Path.Combine(results, "broken.json"); File.WriteAllText(broken, "bad json");
        rejected = false; try { DollPresetLibrary.Load(broken); } catch { rejected = true; } Check(rejected && File.ReadAllText(broken) == "bad json", "corrupt file overwritten");
        string json = File.ReadAllText(file); Check(!json.Contains("\"DollModeEnabled\"") && !json.Contains("\"InfoLogEnabled\""), "global flags persisted inside preset");
        var invalid = captured.Clone(); invalid.DollModeMouthOpen = float.NaN; invalid.ApplyFemaleCharacters = false; invalid.ApplyMaleCharacters = false;
        var normalized = invalid.Normalized(); Check(normalized.DollModeMouthOpen == .30f && normalized.ApplyFemaleCharacters, "normalization");
        invalid.DollModeSpiralOffsetX = float.NaN; invalid.DollModeSpiralOffsetY = 99f;
        normalized = invalid.Normalized(); Check(normalized.DollModeSpiralOffsetX == 0 && normalized.DollModeSpiralOffsetY == .5f, "spiral offset invalid/range normalization");
        invalid.DollModeTearsLevel = 10;
        Check(invalid.Normalized().DollModeTearsLevel == 3, "native tears overflow not prevented");
    }
}
