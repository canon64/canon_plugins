using System;

namespace MainGameVoiceFaceEventBridge
{
    public static partial class PublicApi
    {
        /// <summary>Main-thread only. Reuses normal audio/lip sync; never emits normal subtitles or text triggers.</summary>
        public static bool TryPlayAiConversation(string conversation, string request, ChaControl actor, string path,
            Action<string, string, string> notification)
            => Plugin.Instance != null && Plugin.Instance.TryPlayAiConversation(conversation, request, actor, path, notification);
        public static void CancelAiConversation(string conversation, string request)
            => Plugin.Instance?.CancelAiConversation(conversation, request);
    }
    internal sealed partial class Plugin
    {
        internal bool TryPlayAiConversation(string conversation, string request, ChaControl actor, string path,
            Action<string, string, string> notification)
            => _externalVoicePlayer.TryPlayAiConversation(conversation, request, actor, path, notification);
        internal void CancelAiConversation(string conversation, string request)
            => _externalVoicePlayer.CancelAiConversation(conversation, request);
    }
}
