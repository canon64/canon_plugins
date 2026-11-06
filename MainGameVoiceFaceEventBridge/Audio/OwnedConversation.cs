using System;

namespace MainGameVoiceFaceEventBridge.Audio
{
    // Correlation only; ordinary sequences never enter this owner.
    internal sealed class OwnedConversation
    {
        private string _conversation, _request;
        private Action<string, string, string> _notify;
        private bool _started;
        internal bool Active => _conversation != null;
        internal bool Begin(string conversation, string request, Action<string, string, string> notify)
        {
            if (Active || string.IsNullOrWhiteSpace(conversation) || string.IsNullOrWhiteSpace(request) || notify == null) return false;
            _conversation = conversation; _request = request; _notify = notify; _started = false; return true;
        }
        internal bool Matches(string conversation, string request) => Active && conversation == _conversation && (request == null || request == _request);
        internal void Started()
        { if (!Active || _started) return; _started = true; _notify?.Invoke(_conversation, _request, "started"); }
        internal void Finish(string status)
        {
            if (!Active) return;
            var conversation = _conversation; var request = _request; var notify = _notify;
            _conversation = null; _request = null; _notify = null;
            notify?.Invoke(conversation, request, status);
        }
    }
}
