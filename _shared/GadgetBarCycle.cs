using System;

namespace CanonPlugins.Shared
{
    internal sealed class GadgetBarCycle
    {
        internal bool NextShowsGizmo { get; private set; }
        internal void Press(bool exists, Func<bool> recall, Func<bool> showGizmo, Action hideGizmo, bool gizmoVisible)
        {
            if (!exists)
            {
                if (recall()) NextShowsGizmo = true;
            }
            else if (gizmoVisible)
            {
                hideGizmo();
                NextShowsGizmo = true;
            }
            else if (showGizmo()) NextShowsGizmo = false;
        }
        internal void Reset() { NextShowsGizmo = false; }
    }
}
