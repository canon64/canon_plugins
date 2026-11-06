using System;
using System.Linq;

namespace MainGameAiShoujoH.Presentation
{
    // All UI selection paths dispatch the same validated motion request once.
    internal static class MotionSelectionAction
    {
        internal static bool Choose(string id, string current, string[] available, Func<string, bool> apply)
        {
            if (id == null || available == null || !available.Contains(id)) throw new ArgumentException("表示中の体位を選択してください。", nameof(id));
            if (id == current) return false;
            return apply(id);
        }
    }
}
