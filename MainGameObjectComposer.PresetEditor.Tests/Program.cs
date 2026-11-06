using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MainGameObjectComposer;
using UnityEngine;

internal static class Program
{
    private sealed class Pose
    {
        public string id = "object-1", name = "original";
        public float radius = 2f, phaseContinuityOffsetTurns = 7f;
        public Vector3 position = new Vector3(1f, 2f, 3f);
        public bool visible = true;
    }
    private static int _checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        _checks++;
    }
    private static void Main()
    {
        var before = new Pose();
        var after = new Pose { radius = 3f, position = new Vector3(4, 2, 3), visible = false, name = "edited" };
        var l1 = new Pose();
        var l2 = new Pose { radius = 10f, position = new Vector3(20, 30, 40) };
        Check(PresetEditFields.Apply(before, after, l1, true), "both L1 changed");
        Check(PresetEditFields.Apply(before, after, l2, true), "both L2 changed");
        Check(l1.radius == 3 && l2.radius == 11, "both preserve radius difference");
        Check(l1.position.x == 4 && l2.position.x == 23, "both apply equal position delta");
        Check(l2.position.y == 30 && l2.position.z == 40, "unmodified axes preserved");
        Check(!l1.visible && !l2.visible && l2.name == "edited", "non-numeric edits shared");
        Check(before.radius == 2 && before.position.x == 1 && before.visible, "original snapshot untouched");
        var onlyL1 = new Pose(); var untouchedL2 = new Pose { radius = 10 };
        PresetEditFields.Apply(before, after, onlyL1, false);
        Check(onlyL1.radius == 3 && untouchedL2.radius == 10, "L1 only leaves L2 untouched");
        var onlyL2 = new Pose { radius = 10 };
        PresetEditFields.Apply(before, after, onlyL2, false);
        Check(onlyL2.radius == 3 && before.radius == 2, "L2 absolute edit leaves source untouched");
        var same = new Pose();
        Check(!PresetEditFields.Apply(before, same, onlyL2, true), "no-op does not dirty target");
        var invalid = new Pose { radius = float.NaN, position = new Vector3(float.PositiveInfinity, 0, 0) };
        var valid = new Pose();
        Check(!PresetEditFields.Apply(before, invalid, valid, false), "non-finite input rejected");
        Check(valid.radius == 2 && valid.position.x == 1, "invalid input preserves saved values");
        var runtime = new Pose { id = "different", phaseContinuityOffsetTurns = 123 };
        Check(!PresetEditFields.Apply(before, runtime, valid, false), "identity and runtime phase excluded");
        var overflow = new Pose { radius = float.MaxValue };
        PresetEditFields.Apply(new Pose { radius = -float.MaxValue }, overflow, valid, true);
        Check(valid.radius == 2, "overflow rejected");
        foreach (float viewportHeight in new[] { 180f, 400f, 628f, 776f })
        {
            Rect layoutBody = ComposerViewportGeometry.Body(new Rect(0, 0, 1, 1), 796, viewportHeight);
            Rect paintBody = ComposerViewportGeometry.Body(new Rect(4, 32, 796, viewportHeight - 34), 796, viewportHeight);
            Check(layoutBody.height == paintBody.height && layoutBody.width == paintBody.width,
                "Layout placeholder cannot collapse child areas at viewport height " + viewportHeight);
            Check(layoutBody.height - 54 > 0, "preset panes and switch prompt have positive space");
        }
        CheckProductionSaveGuards();
        Console.WriteLine("PASS: " + _checks + " preset slot edit assertions");
    }

    private static void CheckProductionSaveGuards()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.."));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            string file = new AssemblyName(args.Name).Name + ".dll";
            foreach (string dir in new[] {
                @"F:\kks\KoikatsuSunshine_Data\Managed", @"F:\kks\BepInEx\core",
                Path.Combine(root, @"canon_gate\bin\Release\net472"),
                @"F:\kks\BepInEx\plugins\canon_plugins\MainGameTransformGizmo",
                Path.Combine(root, @"MainGameUnifiedUi\bin\Release\net472"),
                Path.Combine(root, @"MainGameUiInputCapture\bin\Release\net472") })
            { string path = Path.Combine(dir, file); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        Assembly product = Assembly.LoadFrom(Path.Combine(root, @"MainGameObjectComposer\bin\Release\net472\MainGameObjectComposer.dll"));
        Type pluginType = product.GetType("MainGameObjectComposer.Plugin", true);
        object plugin = FormatterServices.GetUninitializedObject(pluginType);
        object draft = Activator.CreateInstance(product.GetType("MainGameObjectComposer.PresetEntry", true), true);
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        pluginType.GetField("_presetDraft", fields).SetValue(plugin, draft);
        string presetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guard-presets.json");
        string layoutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guard-layout.json");
        File.WriteAllText(presetPath, "saved preset sentinel"); File.WriteAllText(layoutPath, "saved layout sentinel");
        pluginType.GetField("_presetsPath", fields).SetValue(plugin, presetPath);
        pluginType.GetField("_layoutPath", fields).SetValue(plugin, layoutPath);
        pluginType.GetMethod("SavePresets", fields).Invoke(plugin, null);
        pluginType.GetMethod("SaveLayout", fields).Invoke(plugin, new object[] { true });
        Check(File.ReadAllText(presetPath) == "saved preset sentinel", "production draft guard prevents preset writes");
        Check(File.ReadAllText(layoutPath) == "saved layout sentinel", "production draft guard prevents layout writes");
        Check((bool)pluginType.GetField("_presetDraftDirty", fields).GetValue(plugin), "production save request marks draft dirty");
        bool left = false;
        Action leave = () => left = true;
        bool allowed = (bool)pluginType.GetMethod("RequestPresetLeave", fields).Invoke(plugin, new object[] { leave });
        Check(!allowed && !left, "dirty workspace defers navigation without losing destination");
        ((Action)pluginType.GetField("_presetPendingAction", fields).GetValue(plugin))();
        Check(ReferenceEquals(pluginType.GetField("_presetLeaveAction", fields).GetValue(plugin), leave),
            "deferred navigation presents save-discard-cancel with original destination");
        pluginType.GetField("_presetLeaveAction", fields).SetValue(plugin, null);
        Check(!left && ReferenceEquals(pluginType.GetField("_presetDraft", fields).GetValue(plugin), draft),
            "cancelling navigation retains edited draft and original workspace");
    }
}
