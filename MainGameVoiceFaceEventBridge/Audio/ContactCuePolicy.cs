namespace MainGameVoiceFaceEventBridge.Audio
{
    internal static class ContactCuePolicy
    {
        // A skin contact reaction replaces the touched actor's native line at playback start.
        // Ordinary impact cues retain their existing non-interrupting admission.
        internal static bool RejectNativeVoice(bool contact,bool thought,bool nativeBusy)=>!contact&&!thought&&nativeBusy;
        internal static bool IsShortMoan(double duration)=>duration>0&&duration<=4;
        internal static bool IsContactAsset(string name)=>!string.IsNullOrEmpty(name)&&
            !name.StartsWith("h_ko_",System.StringComparison.OrdinalIgnoreCase)&&
            !(name.StartsWith("h_on_",System.StringComparison.OrdinalIgnoreCase)&&name.EndsWith("_013",System.StringComparison.OrdinalIgnoreCase));
        internal static bool IsMoanText(string word)
        {
            if(string.IsNullOrWhiteSpace(word))return true; // Native breath table provides classification.
            const string sounds="あいうえおぁぃぅぇぉんっはふくゃやひゎアイウエオァィゥェォンッハフクャヤヒヮー゛゜";
            foreach(char c in word)
                if(!char.IsWhiteSpace(c)&&!char.IsPunctuation(c)&&!char.IsSymbol(c)&&sounds.IndexOf(c)<0)return false;
            return true;
        }
    }
}
