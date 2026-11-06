using System;
using System.IO;
using System.Threading;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class ExternalVoicePlayer
    {
        private readonly Audio.OwnedConversation _aiOwner = new Audio.OwnedConversation();
        private ChaControl _aiLipActor;
        private UnityEngine.AudioSource _aiPreviousLip;
        internal bool AiConversationOwned => _aiOwner.Active;
        internal bool TryPlayAiConversation(string conversation, string request, ChaControl actor, string path,
            Action<string, string, string> notify)
        {
            if (!SessionGate.Allowed || SessionGate.Actor != actor || actor == null || IsPlaying || AiConversationOwned
                || string.IsNullOrWhiteSpace(conversation) || string.IsNullOrWhiteSpace(request) || notify == null
                || !Path.IsPathRooted(path) || !File.Exists(path)) return false;
            if (!_aiOwner.Begin(conversation, request, notify)) return false;
            _aiLipActor = actor; _aiPreviousLip = actor.asVoice;
            try
            {
                if (PlaySequence(new[] { new ExternalVoicePlaybackItem { Path = path } }, request, actor, false, false,
                    UnityEngine.Vector3.one.x, UnityEngine.Vector3.one.x)) return true;
                FinishAiConversation("failed"); return false;
            }
            catch { FinishAiConversation("failed"); return false; }
        }
        internal void CancelAiConversation(string conversation, string request)
        {
            if (!_aiOwner.Matches(conversation, request)) return;
            // Clear ownership before stopping so cancellation can never report natural completion.
            FinishAiConversation("cancelled");
            StopAiDialogue("AI conversation cancelled");
        }
        private void StopAiDialogue(string reason)
        {
            _loadPending = false; Interlocked.Increment(ref _loadGeneration);
            lock (_pendingLock) { _pendingPcm = null; _pendingError = null; _pendingReady = false; }
            ClearQueuedItems(); StopInternal(reason, writeLog: false);
        }
        private bool RestoreAiLip(ChaControl actor)
        {
            if (_aiLipActor == null || _aiLipActor != actor) return false;
            if (actor.asVoice == _source) actor.SetLipSync(_aiPreviousLip);
            _aiLipActor = null; _aiPreviousLip = null; return true;
        }
        private void FinishAiConversation(string status)
        {
            if (_lipSyncBoundFemale != _aiLipActor) { _aiLipActor = null; _aiPreviousLip = null; }
            _aiOwner.Finish(status);
        }
    }
}
