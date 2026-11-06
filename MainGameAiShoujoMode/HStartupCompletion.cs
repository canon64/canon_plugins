using System;

namespace MainGameAiShoujoMode
{
    internal static class HStartupCompletion
    {
        // Native scene startup and optional item UI have different failure boundaries.
        // Once H is ready, a UI construction error must never request H unload.
        internal static void Complete(Action ready, Action buildItems, Action<Exception> itemFailure)
        {
            ready();
            if (buildItems == null) return;
            try { buildItems(); }
            catch (Exception error) { itemFailure(error); }
        }
    }
}
