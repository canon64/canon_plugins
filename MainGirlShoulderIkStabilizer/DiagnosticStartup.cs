using System;

namespace MainGirlShoulderIkStabilizer
{
    internal static class DiagnosticStartup
    {
        // The caller opens diagnostics before entering code that resolves optional runtime APIs.
        internal static void Run(Action initialize, Func<bool> enabled, Action<string> write)
        {
            if (enabled()) write("BOOT stage=awake_begin");
            try { initialize(); }
            catch (Exception ex)
            {
                if (enabled()) write("BOOT_FAILED " + ex);
                throw;
            }
        }
    }
}
