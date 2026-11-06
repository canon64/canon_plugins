namespace MainGameUiHelp
{
    // One IMGUI invocation owns the result; it must never leak into another pass/window.
    internal sealed class HostHelpCapture
    {
        internal bool Active { get; private set; }
        internal bool Accepting { get; private set; }
        private string _text;

        internal void Begin()
        {
            Active = Accepting = true;
            _text = null;
        }

        internal bool Offer(string text)
        {
            if (!Active) return false;
            if (Accepting) _text = text;
            return true;
        }

        internal string Take(bool enabled)
        {
            if (!Active || !Accepting) return null;
            Accepting = false;
            string text = enabled ? _text : null;
            _text = null;
            return text;
        }

        internal void End()
        {
            Active = Accepting = false;
            _text = null;
        }
    }
}
