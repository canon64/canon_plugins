using System;
using System.Reflection;

namespace MainGameAiShoujoMode.Conversation
{
    // Optional API binding only on AI-mode speech. Older/disabled Bridge must not break normal conversation.
    internal static class AiConversationVoice
    {
        private delegate bool PlayDelegate(string conversation, string request, ChaControl actor, string path, Action<string, string, string> notify);
        private delegate void CancelDelegate(string conversation, string request);
        private static PlayDelegate _play;
        private static CancelDelegate _cancel;
        private static bool _resolved;
        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var type = Type.GetType("MainGameVoiceFaceEventBridge.PublicApi, MainGameVoiceFaceEventBridge", false);
            var play = type?.GetMethod("TryPlayAiConversation", BindingFlags.Public | BindingFlags.Static);
            var cancel = type?.GetMethod("CancelAiConversation", BindingFlags.Public | BindingFlags.Static);
            if (play == null || cancel == null) return;
            _play = (PlayDelegate)Delegate.CreateDelegate(typeof(PlayDelegate), play);
            _cancel = (CancelDelegate)Delegate.CreateDelegate(typeof(CancelDelegate), cancel);
        }
        internal static bool TryPlay(string conversation, string request, ChaControl actor, string path, Action<string, string, string> notify)
        { Resolve(); return _play != null && _play(conversation, request, actor, path, notify); }
        internal static void Cancel(string conversation) { _cancel?.Invoke(conversation, null); }
    }
}
