using System;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelFinalSaveBoundary
    {
        private bool _attempted, _success;
        internal void Reset() { _attempted = false; _success = false; }
        internal bool Save(Func<bool> ready, Func<bool> persist)
        {
            if (_attempted) return _success;
            if (!ready()) return false;
            _attempted = true;
            _success = persist();
            return _success;
        }
    }
}
