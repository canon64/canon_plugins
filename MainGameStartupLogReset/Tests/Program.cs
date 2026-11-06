using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MainGameStartupLogReset;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    private static void Put(string path, string text = "previous session")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }

    private static int Main()
    {
        string fixtureRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures"));
        string sandbox = Path.Combine(fixtureRoot, "MainGameStartupLogReset-test-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "BepInEx");
        string canon = Path.Combine(root, "plugins", "canon_plugins");
        var warnings = new List<string>();
        try
        {
            Directory.CreateDirectory(root);
            Check(LogCleaner.PluginFolders.Length == 65 && LogCleaner.PluginFolders.Distinct().Count() == 65, "65 unique owners");
            var delete = new List<string>();
            foreach (string owner in LogCleaner.PluginFolders)
            {
                // This includes owners whose normal logging is disabled or which have no installed DLL.
                delete.Add(Path.Combine(canon, owner, "old.log"));
                delete.Add(Path.Combine(canon, owner, "disabled_20260628", "old.LOG.1"));
            }
            foreach (string relative in new[]
            {
                "CoordCatalog/_logs/coordcatalog_20260316_143220.log",
                "MainGameObjectComposer/MotionDiagnostics.log.previous",
                "MainGameVrDildo/MotionDiagnostics.log.previous",
                "MainGameVrDildo/VrDildoMotion.log",
                "MainGameBlankMapAdd/_logs/info.txt", "MainGameBlankMapAdd/_logs/info.txt.4",
                "MainGameBlankMapAdd/PlaybackBarInput.latest.log", "MainGameBlankMapAdd/webcam_devices.txt",
                "MainGameStudioItemSummon/_logs/info.txt.2",
                "UnityResourceDiagnostics/_logs/resource_diagnostics.txt",
                "UnityResourceDiagnostics/_logs/bake_mesh_trace.txt",
                "MainGameVrScreenEffectProbe/MainGameVrScreenEffectProbe.txt",
                "MainGameVrScreenEffectProbe/disabled_20260628_123239/MainGameVrScreenEffectProbe.txt",
                "MainGameMilkSpray/_logs/milk-performance.csv", "MainGameMilkSpray/_logs/milk-performance.1.csv",
                "MainGameMilkSpray/_logs/milk-performance.2.csv",
                "MainGameVoiceFaceEventBridge/receive-timing/receive-20261023-094246.csv",
                "MainGameVoiceFaceEventBridge/semantic_pose_diagnostic.log",
                "MainGameUiInputCapture/focus_diagnostic.log",
                "MainGameVMDPlay/diagnostics/session.log", "MainGameVMDPlay/neck_trace_20261027.log",
                "MainGamePoseCatalogCapture/pose_catalog_captures/pose_custom_names.log",
                "com/log/com.kks.legacy.log", "unknown_owner/log/unknown_owner.log",
                "MainGameLogRelay/log/main/shared-owner.log"
            }) delete.Add(Path.Combine(canon, relative.Replace('/', Path.DirectorySeparatorChar)));
            delete.Add(Path.Combine(root, "clothes-load-probe", "trace-20261023-010101.csv"));
            foreach (string path in delete) Put(path);

            var keep = new[]
            {
                Path.Combine(root, "LogOutput.log"), Path.Combine(root, "config", "BepInEx.cfg"),
                Path.Combine(root, "plugins", "another-plugin", "other.log"),
                Path.Combine(canon, "not-an-owner", "keep.log"),
                Path.Combine(canon, "release_artifacts", "build.log"),
                Path.Combine(canon, "MainGameVrDildo", "VrDildoSettings.json"),
                Path.Combine(canon, "MainGameVrDildo", "presets.json"),
                Path.Combine(canon, "MainGameMilkSpray", "_logs", "data.csv"),
                Path.Combine(canon, "MainGameMilkSpray", "milk-performance.csv"),
                Path.Combine(canon, "MainGameVoiceFaceEventBridge", "receive-timing", "data.csv"),
                Path.Combine(canon, "MainGameVoiceFaceEventBridge", "receive-unrelated.csv"),
                Path.Combine(canon, "MainGameBlankMapAdd", "README.txt"),
                Path.Combine(canon, "MainGameBlankMapAdd", "_logs", "info.txt.settings"),
                Path.Combine(canon, "MainGameBlankMapAdd", "other", "info.txt"),
                Path.Combine(canon, "MainGamePoseCatalogCapture", "pose_catalog_captures", "pose_custom_names.csv"),
                Path.Combine(canon, "MainGamePoseCatalogCapture", "pose_catalog_captures", "shot.png"),
                Path.Combine(canon, "MainGamePoseCatalogCapture", "pose_catalog_captures", "pose_index.sqlite"),
                Path.Combine(canon, "MainGameVMDPlay", "diagnostics", "command.txt"),
                Path.Combine(root, "clothes-load-probe", "data.csv")
            };
            foreach (string path in keep) Put(path, "preserve exactly");
            CleanupResult result = LogCleaner.Clean(root, (p, e) => warnings.Add(p + ": " + e), _ => { });
            Check(result.Deleted == delete.Count && result.Failed == 0 && result.Linked == 0, "all intended files removed without errors");
            foreach (string path in delete) Check(!File.Exists(path), "remaining log: " + path);
            foreach (string path in keep) Check(File.ReadAllText(path) == "preserve exactly", "changed non-log: " + path);
            Check(warnings.Count == 0, "unexpected warnings");
            Check(LogCleaner.Clean(root, null, null).Deleted == 0, "cleaning is idempotent");
            Check(LogCleaner.Clean(Path.Combine(sandbox, "missing"), null, null).Failed == 0, "missing roots are harmless");

            string locked = Path.Combine(canon, "AstralMode", "locked.log");
            string survivor = Path.Combine(canon, "MainGameVrDildo", "after-failure.log");
            Put(locked); Put(survivor);
            using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                warnings.Clear();
                result = LogCleaner.Clean(root, (p, e) => warnings.Add(p + ": " + e), null);
                Check(result.Failed == 1 && result.Deleted == 1, "continue after locked file");
                Check(File.Exists(locked) && !File.Exists(survivor), "locked file isolated");
                Check(warnings.Count == 1 && warnings[0].Contains(locked) && warnings[0].Length > locked.Length + 2, "failure path and reason reported");
            }
            File.Delete(locked);

            string outside = Path.Combine(sandbox, "outside");
            string externalLog = Path.Combine(outside, "outside.log");
            Put(externalLog, "outside untouched");
            string nestedLink = Path.Combine(canon, "AstralMode", "linked");
            Junction(nestedLink, outside);
            // Owner-directory and intermediate-directory links must also be skipped.
            Directory.Delete(Path.Combine(canon, "MainGameVrNeckChain", "disabled_20260628"));
            Directory.Delete(Path.Combine(canon, "MainGameVrNeckChain"));
            string ownerLink = Path.Combine(canon, "MainGameVrNeckChain");
            Junction(ownerLink, outside);
            string fileLink = Path.Combine(canon, "AstralMode", "linked-file.log");
            File.CreateSymbolicLink(fileLink, externalLog);
            result = LogCleaner.Clean(root, null, _ => { });
            Check(result.Linked == 3 && result.Deleted == 0, "skip directory and file reparse points");
            Check(File.ReadAllText(externalLog) == "outside untouched", "linked target preserved");
            Check((File.GetAttributes(fileLink) & FileAttributes.ReparsePoint) != 0, "file link itself preserved");

            string linkedInstall = Path.Combine(sandbox, "linked-install");
            Junction(linkedInstall, root);
            Put(survivor, "new session");
            result = LogCleaner.Clean(linkedInstall, null, _ => { });
            Check(result.Linked == 1 && File.ReadAllText(survivor) == "new session", "skip linked installation root");
            string nestedUnderLink = Path.Combine(linkedInstall, "plugins", "canon_plugins", "AstralMode");
            result = LogCleaner.Clean(nestedUnderLink, null, _ => { });
            Check(result.Linked == 1 && result.Deleted == 0, "skip linked ancestor above root");
            Console.WriteLine("PASS startup log cleanup checks=" + _checks + " files=" + delete.Count + " reparse=4 fixtureRoot=" + fixtureRoot);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // The sole delete target is our random task-owned test directory.
            if (Path.GetFullPath(sandbox).StartsWith(Path.Combine(fixtureRoot, "MainGameStartupLogReset-test-"), StringComparison.OrdinalIgnoreCase))
                RemoveTestDirectory(sandbox);
        }
    }

    private static void Junction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Exception("junction failed: " + output);
    }

    private static void RemoveTestDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (string child in Directory.GetFileSystemEntries(path))
        {
            FileAttributes attrs = File.GetAttributes(child);
            if ((attrs & FileAttributes.Directory) != 0)
            {
                if ((attrs & FileAttributes.ReparsePoint) != 0) Directory.Delete(child);
                else RemoveTestDirectory(child);
            }
            else File.Delete(child);
        }
        Directory.Delete(path);
    }
}
