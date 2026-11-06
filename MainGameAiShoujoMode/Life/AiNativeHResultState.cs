using System;

namespace MainGameAiShoujoMode.Life
{
    internal static class AiNativeHResultState
    {
        // Native H mood belongs to its explicit native profile. The pre-existing
        // life mood has independent update rules and is not an asserted synonym.
        internal static void SetNativeMood(AiResidentState state, float value)
        {
            if (state == null || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Invalid native H mood.");
            state.nativeMood = value;
        }
    }
}
