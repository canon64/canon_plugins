using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    // ADV.Voice.Do retains the last explicitly specified bundle for each voice personality.
    internal sealed class KksVoiceBundleContext
    {
        private readonly Dictionary<int, string> _bundles = new Dictionary<int, string>();
        internal void Reset() => _bundles.Clear();
        internal string Resolve(int voice, string specified)
        {
            if (!string.IsNullOrEmpty(specified)) { _bundles[voice] = specified; return specified; }
            return _bundles.TryGetValue(voice, out var inherited) ? inherited : string.Empty;
        }
    }
}
