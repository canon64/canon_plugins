using System;
using System.Collections.Generic;
using MainGameHScenePoseInjector;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static void Main()
    {
        var plugin = new Plugin();
        var actor = new ChaControl
        {
            eyebrowCtrl = Controller(4),
            mouthCtrl = Controller(7, 6),
            lstCtrl = new ChaListControl()
        };
        actor.lstCtrl.Entries[1000007] = new ListInfoBase("MOD eye");
        actor.lstCtrl.Entries[0] = new ListInfoBase(null);
        plugin.Actors.Add(actor);
        plugin.Actors.Add(new ChaControl());
        plugin.Reload();
        Check(plugin.Reads == 0, "denied sessions must not inspect characters");
        CanonPlugins.Shared.PluginSessionGate.Allowed = true;
        plugin.Reload();
        Check(plugin.Reads == 1, "one explicit reload reads characters once");
        Check(plugin.Eyebrows.Count == 4, "append installed eyebrow patterns");
        Check(plugin.Mouth.Count == 6, "do not expose indices absent from a controller target");
        Check(plugin.Eyebrows[0].Name == "standard", "preserve standard labels");
        Check(plugin.Eyebrows[3].Name.Contains("shape3"), "name additional shapes from the installed mesh");
        Check(plugin.Eyes[1000007].Name == "MOD eye", "use actual sparse registered eye ID");
        Check(plugin.Eyes.Count == 2, "never invent eye IDs from blend-shape indices");
        Check(plugin.Eyes[0].Name == "standard", "missing mod label keeps standard name");
        var sorted = ExpressionPatternCatalog.Sorted(plugin.Eyes);
        Check(sorted[0].Id == 0 && sorted[1].Id == 1000007, "sort sparse IDs numerically");
        actor.lstCtrl.Entries[1000007] = new ListInfoBase("updated eye");
        Check(plugin.Eyes[1000007].Name == "MOD eye", "cache remains unchanged without explicit reload");
        plugin.Reload();
        Check(plugin.Eyes[1000007].Name == "updated eye", "explicit reload picks up late mod additions");
        var table = new Dictionary<int, PatternEntry>();
        ExpressionPatternCatalog.AddControllerPatterns(table, 2, _ => null);
        Check(!string.IsNullOrWhiteSpace(table[1].Name), "nameless patterns get a selectable label");
        ExpressionPatternCatalog.AddRegistered(table, -1, "invalid");
        Check(!table.ContainsKey(-1), "reject negative pattern IDs");
        plugin.Actors.Clear();
        plugin.Reload();
        Check(plugin.Mouth.Count == 1 && plugin.Eyes.Count == 1, "next character catalog must not retain previous additions");
        Console.WriteLine("PASS " + checks + " expression catalog checks");
    }

    private static FBSBase Controller(params int[] counts)
    {
        var targets = new FBSTargetInfo[counts.Length];
        for (int i = 0; i < counts.Length; i++)
        {
            var patterns = new FBSTargetInfo.CloseOpen[counts[i]];
            for (int id = 0; id < patterns.Length; id++)
                patterns[id] = new FBSTargetInfo.CloseOpen { Close = id, Open = id };
            targets[i] = new FBSTargetInfo { PtnSet = patterns };
        }
        return new FBSBase { FBSTarget = targets };
    }
}

// Unity/game boundaries only. Catalog merge and controller reader are production sources.
internal class FBSBase { public FBSTargetInfo[] FBSTarget; }
internal class FBSTargetInfo
{
    internal class CloseOpen { public int Close, Open; }
    public CloseOpen[] PtnSet;
    public Renderer GetSkinnedMeshRenderer() => new Renderer();
}
internal class Renderer { public Mesh sharedMesh = new Mesh(); }
internal class Mesh
{
    public int blendShapeCount => 20;
    public string GetBlendShapeName(int id) => "shape" + id;
}
internal class ChaControl
{
    public FBSBase eyebrowCtrl, mouthCtrl;
    public ChaListControl lstCtrl;
}
internal class ChaListControl
{
    public Dictionary<int, ListInfoBase> Entries = new Dictionary<int, ListInfoBase>();
    public Dictionary<int, ListInfoBase> GetCategoryInfo(ChaListDefine.CategoryNo _) => Entries;
}
internal class ListInfoBase
{
    private readonly string name;
    public ListInfoBase(string name) { this.name = name; }
    public string GetInfo(ChaListDefine.KeyType _) => name;
}
internal static class ChaListDefine
{
    internal enum CategoryNo { cha_eyeset }
    internal enum KeyType { Name }
}
namespace CanonPlugins.Shared { internal static class PluginSessionGate { internal static bool Allowed; } }
namespace MainGameHScenePoseInjector
{
    public sealed partial class Plugin
    {
        internal readonly List<ChaControl> Actors = new List<ChaControl>();
        internal int Reads;
        internal Dictionary<int, PatternEntry> Eyebrows, Eyes, Mouth;
        private bool _patternListLoadAttempted;
        internal void Reload() => ReloadExpressionPatterns();
        private List<ChaControl> ResolveFemales() { Reads++; return Actors; }
        private static Dictionary<int, PatternEntry> Standard() => new Dictionary<int, PatternEntry>
        {
            [0] = new PatternEntry { Id = 0, Name = "standard" }
        };
        private void EnsurePatternsLoaded()
        {
            if (_patternListLoadAttempted) return;
            _patternListLoadAttempted = true;
            Eyebrows = Standard(); Eyes = Standard(); Mouth = Standard();
            MergeRuntimePatternLists(Eyebrows, Eyes, Mouth);
        }
        private static void LogWarn(string message) { throw new Exception(message); }
    }
}
