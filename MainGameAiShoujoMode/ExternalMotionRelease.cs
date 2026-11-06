using System;

namespace MainGameAiShoujoMode
{
    // Clear the occupancy before resuming: restore can fail or synchronously request another action.
    public sealed class ExternalMotionRelease
    {
        private Func<bool> _current;
        private Action _clear, _restore, _ready;
        public ExternalMotionRelease(Func<bool> current, Action clear, Action restore, Action ready)
        { _current = current; _clear = clear; _restore = restore; _ready = ready; }
        public void Release()
        {
            var current = _current; var clear = _clear; var restore = _restore; var ready = _ready;
            _current = null; _clear = null; _restore = null; _ready = null;
            if (current == null || !current()) return;
            clear();
            try { restore(); } finally { ready(); }
        }
    }
}
