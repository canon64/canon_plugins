using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;

internal static class LoaderProbe
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(args[0])) return 2;
        string core = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string candidate = Path.Combine(core, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        try { return Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    // Resolve BepInEx only after the resolver in Main is registered.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run()
    {
        string fixtureRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures"));
        string sandbox = Path.Combine(fixtureRoot, "MainGameStartupLogReset-loader-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "BepInEx");
        Directory.CreateDirectory(root);
        try
        {
            string emptyManaged = Path.Combine(sandbox, "Managed");
            Directory.CreateDirectory(emptyManaged);
            typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { Path.Combine(sandbox, "KoikatsuSunshine.exe"), root, emptyManaged, new string[0] });
            string patchers = Path.Combine(root, "patchers", "canon_plugins", "MainGameStartupLogReset");
            Directory.CreateDirectory(patchers);
            File.Copy(typeof(MainGameStartupLogReset.Patcher).Assembly.Location, Path.Combine(patchers, "MainGameStartupLogReset.dll"));
            string oldLog = Path.Combine(root, "plugins", "canon_plugins", "CoordCatalog", "_logs", "previous.log");
            Directory.CreateDirectory(Path.GetDirectoryName(oldLog));
            File.WriteAllText(oldLog, "previous session");
            ExercisePreloader(emptyManaged, oldLog);
            Console.WriteLine("PASS installed BepInEx preloader: recursive discovery, Initialize, empty TargetDLLs, once-only cleanup, current session retained");
            return 0;
        }
        finally
        {
            // This probe never supplies the real installation as Paths.BepInExRootPath.
            if (Path.GetFullPath(sandbox).StartsWith(Path.Combine(fixtureRoot, "MainGameStartupLogReset-loader-"), StringComparison.OrdinalIgnoreCase))
            {
                // Assembly.LoadFile holds the copied DLL until this process exits.
                // The calling shell can remove this task-owned directory afterwards.
                try { Directory.Delete(sandbox, true); }
                catch (UnauthorizedAccessException) { Console.WriteLine("LOADER_PROBE_TEMP=" + sandbox); }
                catch (IOException) { Console.WriteLine("LOADER_PROBE_TEMP=" + sandbox); }
            }
        }
    }

    // AssemblyPatcher has a beforefieldinit initializer which needs Paths configured
    // before this method is JIT-compiled, as it is in the real preloader.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ExercisePreloader(string emptyManaged, string oldLog)
    {
        using (var listener = new Listener())
        {
            Logger.Listeners.Add(listener);
            AssemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);
            if (AssemblyPatcher.PatcherPlugins.Count != 1) throw new Exception("preloader did not discover exactly one patcher");
            var patcher = AssemblyPatcher.PatcherPlugins.Single();
            if (patcher.Initializer == null || patcher.TargetDLLs().Any()) throw new Exception("expected initializer and no patched assemblies");
            AssemblyPatcher.PatchAndLoad(emptyManaged);
            if (File.Exists(oldLog)) throw new Exception("BepInEx initializer did not clean the isolated fixture");
            if (!listener.Lines.Any(x => x.Contains("Startup log cleanup: deleted=1 failed=0 linked=0")))
                throw new Exception("cleanup result missing: " + string.Join("\n", listener.Lines));
            File.WriteAllText(oldLog, "current session");
            AssemblyPatcher.PatchAndLoad(emptyManaged);
            if (File.ReadAllText(oldLog) != "current session") throw new Exception("initializer ran twice");
            if (listener.Lines.Count(x => x.Contains("Startup log cleanup:")) != 1) throw new Exception("expected exactly one cleanup");
            Logger.Listeners.Remove(listener);
        }
    }

    private sealed class Listener : ILogListener
    {
        internal readonly List<string> Lines = new List<string>();
        public LogLevel LogLevelFilter => LogLevel.All;
        public void LogEvent(object sender, LogEventArgs e) => Lines.Add(e.Data?.ToString() ?? "");
        public void Dispose() { }
    }
}
