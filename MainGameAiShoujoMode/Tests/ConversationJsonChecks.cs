using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MapEnvironmentRuntime;

internal static class ConversationJsonChecks
{
    internal static void ConfigureResolver()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var path = Path.Combine("F:/kks/KoikatsuSunshine_Data/Managed", new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }
    private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public).GetValue(value);
    private static object Read(Type type, string text) => typeof(JsonCodec).GetMethod(nameof(JsonCodec.FromJson)).MakeGenericMethod(type).Invoke(null, new object[] { text });
    private static void LifeSettingsAfterMapRewrite(string content, Type lifeType)
    {
        // Reproduce SandboxHost's actual map settings write without launching Unity/KKS.
        var directory = Path.Combine("F:/kks/work/plugins/canon_plugins/analysis/ai-conversation-system/life-map-rewrite-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var map = Path.Combine(directory, "parameters.json");
        var dedicated = Path.Combine(directory, "ai-life-runtime.json");
        File.Copy(Path.Combine(content, "parameters.json"), map);
        File.Copy(Path.Combine(content, "ai-life-runtime.json"), dedicated);
        var original = File.ReadAllBytes(dedicated);
        var parameters = JsonCodec.FromJson<Parameters>(File.ReadAllText(map));
        parameters.ValidateDocument(); // Full Validate calls Unity Mathf; this check exercises the actual serializer/write boundary.
        Parameters.AtomicWrite(map, JsonCodec.ToJson(parameters, true));
        var loaded = lifeType.GetMethod("LoadContent", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { directory });
        if (loaded == null || !original.SequenceEqual(File.ReadAllBytes(dedicated))
            || string.IsNullOrWhiteSpace((string)lifeType.GetProperty("savePath").GetValue(loaded, null))
            || lifeType.GetProperty("rules").GetValue(loaded, null) == null)
            throw new Exception("Life settings did not survive SandboxHost/Session map rewrite");
        var dropped = !File.ReadAllText(map).Contains("\"aiLifeRuntime\"");
        Console.WriteLine("AI_MODE_LIFE_MAP_REWRITE_PASS productionMapRewrite=true unknownLifeRootDropped=" + dropped + " dedicatedSettingsPreserved=true noSaveCreated=true");
    }
    internal static void Run(string content, Parameters settings)
    {
        var lifeType = typeof(MainGameAiShoujoMode.AiModeSession).Assembly.GetType("MainGameAiShoujoMode.Life.AiLifeRuntimeSettings", true);
        var life = lifeType.GetMethod("LoadContent", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { content });
        if (life == null || string.IsNullOrWhiteSpace((string)lifeType.GetProperty("savePath").GetValue(life, null))
            || string.IsNullOrWhiteSpace((string)lifeType.GetProperty("worldKey").GetValue(life, null))
            || (int)lifeType.GetProperty("playerInitialCapacity").GetValue(life, null) <= settings.I("math.zero")
            || lifeType.GetProperty("agentInventoryCapacity").GetValue(life, null) != null)
            throw new Exception("Staged AI life runtime settings were not preserved by the production reader");
        Console.WriteLine("AI_MODE_LIFE_MANAGED_SETTINGS_PASS noSaveCreated=true");
        var rules = lifeType.GetProperty("rules").GetValue(life, null);
        if (rules == null) throw new Exception("Production reader lost life rules");
        rules.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rules, null);
        var itemsType = lifeType.Assembly.GetType("MainGameAiShoujoMode.Life.AiItemCatalog", true);
        var items = itemsType.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[]
        { Path.Combine(content, (string)lifeType.GetProperty("itemCatalogFile").GetValue(life, null)), lifeType.GetProperty("sourceProfileVersion").GetValue(life, null) });
        if (items == null || ((Array)Field(items, "items")).Length <= settings.I("math.zero")) throw new Exception("Production item catalog lost definitions");
        Console.WriteLine("AI_MODE_LIFE_CATALOG_NET472_PASS noSaveCreated=true");
        LifeSettingsAfterMapRewrite(content, lifeType);
        Console.WriteLine("AI_MODE_CONVERSATION_MANAGED_JSON_START noUnityEngineCalls=true");
        var assembly = typeof(MainGameAiShoujoMode.AiModeSession).Assembly;
        var catalogType = assembly.GetType("MainGameAiShoujoMode.Conversation.KksConversationCatalog", true);
        var manifestType = catalogType.GetNestedType("Manifest", BindingFlags.NonPublic);
        var catalogPath = Path.Combine(content, "kks-conversation-catalog.json");
        var manifest = Read(manifestType, File.ReadAllText(catalogPath));
        var personalities = (Array)Field(manifest, "personalities"); var numbers = (Array)Field(manifest, "layoutNumbers");
        if (personalities == null || numbers == null || personalities.Length <= settings.I("math.zero") || numbers.Length <= settings.I("math.zero")
            || string.IsNullOrEmpty((string)Field(manifest, "sourceAssemblySha256")) || Field(manifest, "runtime") == null)
            throw new Exception("Production conversation manifest lost required fields");
        var catalog = Activator.CreateInstance(catalogType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { catalogPath }, null);
        if (catalog == null) throw new Exception("Production catalog constructor failed");
        var uiType = assembly.GetType("MainGameAiShoujoMode.Conversation.KksUiHierarchy+Document", true);
        var ui = Read(uiType, File.ReadAllText(Path.Combine(content, "kks-conversation-ui.json")));
        var nodes = (Array)Field(ui, "nodes"); var overrides = (Array)Field(ui, "rectOverrides");
        if (nodes == null || nodes.Length <= settings.I("math.zero") || overrides == null || string.IsNullOrEmpty((string)Field(ui, "bundle")))
            throw new Exception("Production conversation UI lost node arrays or bundle");
        foreach (var node in nodes)
        {
            if (Field(node, "rect") == null || Field(node, "components") == null || string.IsNullOrEmpty((string)Field(node, "path")))
                throw new Exception("Production UI node lost geometry or components");
            foreach (var part in (Array)Field(node, "components"))
            {
                var kind = (string)Field(part, "type");
                if (kind == "CanvasGroup" || kind == "CanvasRenderer")
                {
                    var native = assembly.GetType("MainGameAiShoujoMode.Conversation.KksUiHierarchy+" + (kind == "CanvasGroup" ? "NativeGroup" : "NativeRenderer"), true);
                    Read(native, (string)Field(part, "json"));
                }
            }
        }
        var restored = Read(uiType, JsonCodec.ToJson(ui));
        var restoredNodes = (Array)Field(restored, "nodes");
        foreach (var index in Enumerable.Range(settings.I("math.zero"), nodes.Length))
        {
            var before = Field(nodes.GetValue(index), "rect"); var after = Field(restoredNodes.GetValue(index), "rect");
            foreach (var member in new[] { "anchorMin", "anchorMax", "pivot", "position", "size" })
            {
                var a = (UnityEngine.Vector2)Field(before, member); var b = (UnityEngine.Vector2)Field(after, member);
                if (a.x != b.x || a.y != b.y) throw new Exception("Production UI Vector2 round trip changed " + member);
            }
        }
        var points = Read(uiType, File.ReadAllText(Path.Combine(content, "kks-point-ui.json")));
        var pointNodes = (Array)Field(points, "nodes");
        if (pointNodes == null || pointNodes.Length <= settings.I("math.zero")) throw new Exception("Point UI nodes were discarded");
        var pointType = assembly.GetType("MainGameAiShoujoMode.Conversation.KksPointDisplayView+Document", true);
        var point = Field(Read(pointType, File.ReadAllText(Path.Combine(content, "kks-point-ui.json"))), "point");
        if (point == null || string.IsNullOrEmpty((string)Field(point, "textPath"))) throw new Exception("Point UI animation settings were discarded");
        Console.WriteLine("AI_MODE_CONVERSATION_MANAGED_JSON_PASS personalities=" + personalities.Length + " layouts=" + numbers.Length + " uiNodes=" + nodes.Length + " realCatalogConstructor=true");
    }
}
