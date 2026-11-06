using System;
namespace MainGameVrScreenEffectProbe
{
    internal static class VrStateDiagnostics
    {
        // Diagnostics must not abort plugin initialization when VRGIN is not ready.
        internal static string Describe(Func<string> describe)
        {
            try { return describe(); }
            catch (Exception ex) { return "vr state unavailable: " + ex.GetType().Name + " (no retry scheduled)"; }
        }
    }
}
