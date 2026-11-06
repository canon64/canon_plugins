using System;
using System.Collections.Generic;

namespace MainGameVrNeckGrab
{
    internal sealed class CleanupBatch
    {
        internal readonly List<string> Errors = new List<string>();
        internal void Run(string name, Action action)
        {
            try { action(); } catch (Exception e) { Errors.Add(name + ": " + e.Message); }
        }
    }
}
