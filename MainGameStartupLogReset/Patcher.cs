using System.Collections.Generic;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace MainGameStartupLogReset
{
    // BepInEx 5 calls Initialize before loading the Unity entrypoint/Chainloader,
    // including patchers whose TargetDLLs is empty. No game assembly is patched.
    public static class Patcher
    {
        private static int _initialized;

        public static IEnumerable<string> TargetDLLs => new string[0];

        public static void Patch(AssemblyDefinition assembly) { }

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
            var logger = Logger.CreateLogSource("MainGameStartupLogReset");
            try
            {
                var result = LogCleaner.Clean(Paths.BepInExRootPath,
                    (path, error) => logger.LogWarning("Could not remove previous log: " + path + " : " + error),
                    path => logger.LogWarning("Skipped linked path: " + path));
                logger.LogInfo("Startup log cleanup: deleted=" + result.Deleted
                    + " failed=" + result.Failed + " linked=" + result.Linked);
            }
            catch (System.Exception ex)
            {
                logger.LogError("Startup log cleanup failed: " + ex);
            }
            finally
            {
                Logger.Sources.Remove(logger);
                logger.Dispose();
            }
        }
    }
}
