using System;

namespace MainGameBlankMapAdd.Diagnostics
{
    internal static class HLocationTrace
    {
        // Gate before collecting state; a failed diagnostic must not interrupt restoration.
        internal static void Write(bool enabled, Func<string> collect, Action<string> output)
        {
            if (!enabled) return;
            try { output(collect()); }
            catch (Exception) { /* Diagnostic failure never changes the save/restore result. */ }
        }
    }
}
