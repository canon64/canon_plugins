using System;

namespace CanonPlugins.Shared
{
    internal sealed class GadgetBarCycle
    {
        internal bool NextShowsGizmo { get; private set; }
        internal void Press(bool exists, Func<bool> recall, Func<bool> showGizmo)
        {
            if (!exists || !NextShowsGizmo)
            {
                if (recall()) NextShowsGizmo = true;
            }
            else if (showGizmo()) NextShowsGizmo = false;
        }
        internal void Reset() { NextShowsGizmo = false; }
    }
}
