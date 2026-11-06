using MainGameUiHelp;
using Mono.Cecil;
using System.Text.Json;

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}

var data = JsonSerializer.Deserialize<HelpCatalogFile>(File.ReadAllText(Path.Combine(root, "help_catalog.json")),
    new JsonSerializerOptions { IncludeFields = true });
var catalog = new HelpCatalog();
var keys = new HashSet<string>();
foreach (var entry in data.entries)
{
    Check(!string.IsNullOrWhiteSpace(entry.text), "Empty explanation: " + entry.label);
    Check(keys.Add(entry.owner + "\n" + HelpCatalog.Normalize(entry.label)), "Duplicate normalized key: " + entry.owner + "/" + entry.label);
    catalog.Add(entry.owner, entry.label, entry.text, entry.fallback);
}
foreach (string gadget in new[] { "ディルド", "鎖" })
{
    Check(catalog.Find("MainGameBlankMapAdd", gadget).Contains("右クリック"), "Gadget popup lacks right-click deletion: " + gadget);
    Check(catalog.Find("MainGameBlankMapAdd", gadget, "交互操作と次の左クリック").Contains("次の左クリック"), "Old catalog text masks the gadget's live action hint: " + gadget);
}
if (args.Contains("--gadget-popups"))
{
    Console.WriteLine("GADGET_POPUP_HELP_OK: catalog validation and live tooltip precedence");
    return;
}
using (var audit = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "embedded_ui_audit.json"))))
{
    var tabs = audit.RootElement.GetProperty("tabs");
    Check(tabs.GetArrayLength() == 13, "Review additions/removals to the embedded tab registration inventory.");
    foreach (var source in audit.RootElement.GetProperty("sources").EnumerateObject())
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "..", source.Name)))) == source.Value.GetString(),
            "Source inventory is stale; rerun UiAudit: " + source.Name);
    foreach (var site in audit.RootElement.GetProperty("controls").EnumerateArray())
    {
        string owner = site.GetProperty("owner").GetString();
        foreach (var label in site.GetProperty("labels").EnumerateArray())
            Check(catalog.Find(owner, label.GetString()) != null, "Compiler-audited UI label missing: " + owner + "/" + label.GetString());
        string api = site.GetProperty("api").GetString();
        if (site.GetProperty("labels").GetArrayLength() != 0 || site.GetProperty("forwarded").GetBoolean()
            || site.GetProperty("hasTooltip").GetBoolean() || !new[] { "Button", "Toggle", "Toolbar", "SelectionGrid", "RepeatButton" }.Contains(api)) continue;
        string kind = api == "Toggle" ? "toggle" : api == "Toolbar" || api == "SelectionGrid" ? "selection" : "button";
        string method = site.GetProperty("method").GetString();
        Check(catalog.Find(owner, "@" + method + ":" + kind) != null, "Dynamic control has no source tooltip/context help: " + owner + "/" + method + "/" + site.GetProperty("expression").GetString());
    }
}
Check(catalog.Find("SimpleAfterimage", "▼ VMDのBPM別設定").Contains("BPM"), "Foldout markers must not hide help.");
Check(HelpCatalog.Normalize("<no name>") == "<no name>", "Placeholder names are not rich-text tags.");
Check(catalog.Find("MainGameCameraLookControl", "首 Z 左右注視との連動").Contains("7.5"), "Data-driven bone rows need their own explanations.");
var precedence = new HelpCatalog();
precedence.Add("owner", "item", "generic", true);
Check(precedence.Find("owner", "item", "source explanation") == "source explanation", "Generated text must not mask a control's actual tooltip.");
Check(precedence.Find("owner", "item", null, "list action") == "list action", "Context must take precedence over generated label wording.");
precedence.Add("owner", "item", "reviewed");
Check(precedence.Find("owner", "item", "source explanation", "list action") == "reviewed", "Keep reviewed item-specific wording.");

using (var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "ui_inventory.json"))))
{
    foreach (var owner in inventory.RootElement.EnumerateObject())
        foreach (var control in owner.Value.EnumerateArray())
        {
            string label = control.GetProperty("label").GetString();
            if (HelpCatalog.Normalize(label).Length == 0) continue;
            Check(catalog.Find(owner.Name, label) != null, "Missing UI explanation: " + owner.Name + "/" + label);
        }
}
string cap = catalog.Find("MainGameVrDildo", "深い一撃\n距離上限の挿入路割合");
Check(cap != null && cap.Contains("13 cm") && cap.Contains("10 cm"), "Deep distance cap needs both shorter-of-two examples.");
Check(catalog.Find("MainGameVrDildo", "深い一撃の到達率").Contains("14 cm"), "Depth threshold needs its separate example.");
Check(catalog.Find("MainGameObjectComposer", "他").Contains("O"), "Other lane must explain O.");
Check(catalog.Find("MainGameClothColliderCards", "X").Contains("削除"), "Cloth X is delete.");
Check(!catalog.Find("MainGameObjectComposer", "X").Contains("削除"), "Composer X is not delete.");
Check(catalog.Find("MainGameFaceSweatControl", "Face Sweat Level: 0.5") != null, "Value suffix lookup.");
Check(HelpCatalog.Range(.65f, .1f, 1f).Contains("0.65"), "Range formatting.");
Check(HelpCatalog.IsSameRow(0, 160, 10, 46, 166, 10, 26), "Two-line label + slider.");
Check(!HelpCatalog.IsSameRow(0, 160, 10, 30, 166, 35, 55), "Do not borrow help from another row.");
Check(!HelpCatalog.IsSameRow(0, 160, 10, 30, 20, 10, 30), "Do not borrow from a control to the left.");

// Exercise ownership across IMGUI passes, including OFF, missing entries and early exits.
var capture = new HostHelpCapture();
Check(!capture.Offer("standalone"), "Outside hosts, the normal popup still owns help.");
capture.Begin();
Check(capture.Offer("詳しい説明\n現在: 0.65　設定範囲: 0.1 ～ 1"), "Hosted slider help must not reach the standalone popup.");
Check(capture.Take(true).Contains("0.65"), "Transfer the full explanation and range without a display delay.");
Check(capture.Offer("popup drawing"), "The host still owns observations while rendering its popup.");
Check(capture.Take(true) == null, "A popup cannot capture itself or read a result twice.");
capture.End();
Check(!capture.Active && !capture.Accepting && !capture.Offer("standalone"), "End restores standalone operation.");
capture.Begin();
Check(capture.Take(true) == null, "A new empty pass must fall back to original help, not the previous item.");
capture.End();
capture.Begin();
capture.Offer("hidden explanation");
Check(capture.Take(false) == null, "Disabled help must not be supplied to the host.");
capture.End();
try
{
    capture.Begin();
    capture.Offer("interrupted tab");
    throw new InvalidOperationException("GUI early exit");
}
catch (InvalidOperationException) { }
finally { capture.End(); }
Check(!capture.Active && capture.Take(true) == null, "Early exit cannot leak a tab's explanation.");
capture.Begin();
capture.Offer("old pass");
capture.Begin();
Check(capture.Take(true) == null, "Begin discards stale results even after an interrupted caller.");
capture.End();

// Inspect the actual Unity 2019 binary: these hooks must sit on managed paths
// reached by both GUILayout and GUI controls. This does not execute Unity.
using var unity = ModuleDefinition.ReadModule(@"F:\kks\KoikatsuSunshine_Data\Managed\UnityEngine.IMGUIModule.dll");
var gui = unity.GetType("UnityEngine.GUI");
var hooks = new[] { ("DoLabel", 3), ("DoButton", 4), ("DoToggle", 5), ("DoTextField", 8), ("Slider", 10), ("DoButtonGrid", 11) };
foreach (var hook in hooks)
{
    var targets = gui.Methods.Where(m => m.Name == hook.Item1 && m.Parameters.Count == hook.Item2).ToArray();
    Check(targets.Length == 1 && targets[0].HasBody && targets[0].IsStatic, "Unsupported Unity hook: " + hook);
}
var clip = unity.GetType("UnityEngine.GUIClip");
Check(clip.Methods.Any(m => m.Name == "get_visibleRect" && m.IsStatic && m.ReturnType.FullName == "UnityEngine.Rect"), "Clip visibility getter.");

bool Reaches(MethodDefinition method, string goal, HashSet<string> visited)
{
    if (method.Name == goal && method.DeclaringType.FullName == "UnityEngine.GUI"
        && (goal != "DoTextField" || method.Parameters.Count == 8)) return true;
    if (!visited.Add(method.FullName) || !method.HasBody) return false;
    foreach (var instruction in method.Body.Instructions)
        if (instruction.Operand is MethodReference reference)
        {
            var type = unity.GetType(reference.DeclaringType.FullName);
            if (type == null) continue;
            var next = type.Methods.FirstOrDefault(m => m.FullName == reference.FullName);
            if (next != null && Reaches(next, goal, visited)) return true;
        }
    return false;
}
foreach (string api in new[] { "UnityEngine.GUI", "UnityEngine.GUILayout" })
    foreach (var control in new[] { ("Label", "DoLabel"), ("Button", "DoButton"), ("Toggle", "DoToggle"), ("HorizontalSlider", "Slider"), ("TextField", "DoTextField") })
    {
        var methods = unity.GetType(api).Methods.Where(m => m.Name == control.Item1 && m.IsPublic && m.IsStatic).ToArray();
        Check(methods.Length > 0, "Missing API: " + api + "/" + control);
        foreach (var method in methods)
            Check(Reaches(method, control.Item2, new HashSet<string>()), "Unobserved control overload: " + method.FullName);
    }

using var plugin = ModuleDefinition.ReadModule(Path.Combine(root, "bin", "Release", "net472", "MainGameUiHelp.dll"));
var pluginType = plugin.GetType("MainGameUiHelp.Plugin");
Check(!pluginType.Methods.Any(m => m.Name == "Update" || m.Name == "LateUpdate" || m.Name == "FixedUpdate"), "Help configuration must be acquired on UI use, without polling.");
foreach (var api in new[] { ("BeginHostCapture", "System.Void"), ("TakeHostHelp", "System.String"), ("EndHostCapture", "System.Void") })
    Check(pluginType.Methods.Any(m => m.Name == api.Item1 && m.IsPublic && m.IsStatic
        && m.Parameters.Count == 0 && m.ReturnType.FullName == api.Item2), "Optional host API signature: " + api.Item1);

// Verify the actual host binary has a finally-protected capture and retains its own renderer.
using var host = ModuleDefinition.ReadModule(Path.Combine(root, "..", "MainGameBlankMapAdd", "bin", "Release", "net472", "MainGameBlankMapAdd.dll"));
var hostType = host.GetType("MainGameBlankMapAdd.Plugin");
var onGui = hostType.Methods.Single(m => m.Name == "OnGUI");
Check(onGui.Body.ExceptionHandlers.Any(h => h.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Finally
    && onGui.Body.Instructions.SkipWhile(i => i != h.HandlerStart).TakeWhile(i => i != h.HandlerEnd)
        .Any(i => i.Operand is MethodReference m && m.Name == "EndPlaybackHelpCapture")), "Host must release capture on all early returns/exceptions.");
var draw = hostType.Methods.Single(m => m.Name == "DrawPlaybackBarGui");
var drawCalls = draw.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().Select(m => m.Name).ToList();
Check(drawCalls.IndexOf("TakePlaybackHelp") >= 0 && drawCalls.IndexOf("TakePlaybackHelp") < drawCalls.IndexOf("DrawPlaybackBarHelpPopup"), "Take help before rendering the original popup.");
var popup = hostType.Methods.Single(m => m.Name == "DrawPlaybackBarHelpPopup");
Check(popup.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "EnableUiHelpPopup"), "Existing Explanation toggle remains authoritative.");
Check(popup.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_tooltip"), "Original tooltip fallback remains available.");
Check(popup.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "CalcHeight"), "Long explanations use wrapped height calculation.");
Check(popup.Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "PlaybackHelpWindow" && m.Name == "Draw") == 2, "Register cached help on non-Repaint events as well as current Repaint.");
Check(host.GetType("MainGameBlankMapAdd.UI.PlaybackHelpOverlay") == null, "Remove the ordinary GUI overlay that remains behind native windows.");
var helpWindow = host.GetType("MainGameBlankMapAdd.UI.PlaybackHelpWindow");
var registerWindow = helpWindow.Methods.Single(m => m.Name == "Draw");
Check(registerWindow.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Window")
    && registerWindow.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "BringWindowToFront"), "Use the native floating-window layer and explicitly bring help to front.");
Check(!registerWindow.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_type" && m.DeclaringType.Name == "Event"), "Native window registration must not be Repaint-only.");
Check(!helpWindow.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m
    && new[] { "set_depth", "FocusWindow", "DragWindow", "Use", "set_hotControl" }.Contains(m.Name)), "Help uses window ordering without changing focus, drag ownership or GUI behaviour depth.");
Check(hostType.Methods.Single(m => m.Name == "HideUiBlocker").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "ClearPlaybackHelpWindow"), "Closing/disabling/focus loss clears the help window with the bar.");
Check(hostType.Methods.Single(m => m.Name == "OnDestroy").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "ClearPlaybackHelpWindow"), "Clear the native help window on destruction.");
var embedded = hostType.Methods.Single(m => m.Name == "DrawEmbeddedPlaybackWindowBody");
Check(embedded.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "_embeddedHoveredHelp"), "Preserve embedded tooltip fallback before leaving the window.");
Check(!host.AssemblyReferences.Any(r => r.Name == "MainGameUiHelp"), "Help provider must remain optional.");
foreach (var (owner, methodName, expectedCalls) in new[] { ("SimpleAfterimage", "Number", 1), ("MainGameVMDPlay", "DrawAdjustments", 2) })
{
    using var client = ModuleDefinition.ReadModule(Path.Combine(root, "..", owner, "bin", "Release", "net472", owner + ".dll"));
    Check(!client.AssemblyReferences.Any(r => r.Name == "MainGameUiHelp"), "Explicit control reporting stays optional: " + owner);
    Check(client.GetType(owner + ".Plugin").Methods.Single(m => m.Name == methodName).Body.Instructions.Count(i =>
        i.Operand is MethodReference m && m.Name == "ReportLastControl") == expectedCalls, "Table fields report their actual help rectangles: " + owner);
}
var positions = new Dictionary<string, (string Target, int Count)>
{
    ["LabelPrefix"] = ("DoLabel", 3), ["ButtonPrefix"] = ("DoButton", 4),
    ["TogglePrefix"] = ("DoToggle", 5), ["TextPrefix"] = ("DoTextField", 8),
    ["SliderPrefix"] = ("Slider", 10), ["GridPrefix"] = ("DoButtonGrid", 11)
};
foreach (var pair in positions)
{
    var prefix = pluginType.Methods.Single(m => m.Name == pair.Key);
    var original = gui.Methods.Single(m => m.Name == pair.Value.Target && m.Parameters.Count == pair.Value.Count);
    Check(prefix.ReturnType.FullName == "System.Void", "Observation must not cancel a control.");
    foreach (var parameter in prefix.Parameters)
    {
        Check(!parameter.ParameterType.IsByReference, "Observation must not replace control values.");
        int index = int.Parse(parameter.Name.Substring(2));
        Check(parameter.ParameterType.FullName == original.Parameters[index].ParameterType.FullName, "Hook parameter mismatch: " + pair.Key + "/" + parameter.Name);
    }
}
var calls = pluginType.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
    .Select(i => i.Operand).OfType<MethodReference>().Select(m => m.FullName).ToArray();
Check(!calls.Any(m => m.Contains("UnityEngine.Event::Use") || m.Contains("set_hotControl") || m.Contains("Application::Quit")), "Help must not consume input or control the game.");
Console.WriteLine($"PASS: {assertions} checks; {catalog.Count} unique help entries; actual Unity control paths and prefix signatures verified. In-game rendering remains untested.");
