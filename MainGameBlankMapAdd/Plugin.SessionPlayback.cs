using CanonPlugins.Shared;
using MainGameBlankMapAdd.Room;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private MediaRoomResourceScope _playbackResourceScope;

        private void EnsureSessionPlayback()
        {
            if (!_sessionStarted || !PluginSessionGate.Allowed) return;
            if (_playbackResourceScope == null)
            {
                _playbackResourceScope = new MediaRoomResourceScope(0);
                var root = new GameObject("__BlankMapSessionPlayback");
                root.transform.SetParent(transform, false);
                _playbackResourceScope.OwnRoot(root);
            }
            if (_mainVideoPlayer != null) return;
            if (_settings.FolderPlayEnabled) ApplyFolderVideoPath();
            string path = ResolveVideoPath(_settings.VideoPath);
            if (!string.IsNullOrWhiteSpace(path) && !IsWebCamUrl(path))
                TryAttachVideo(null, path, "SessionPlayback", true);
        }

        private void DestroySessionPlayback()
        {
            // Invalidate callbacks before stopping native decoders.
            MediaRoomResourceScope scope = _playbackResourceScope;
            _playbackResourceScope = null;
            ClearPendingVideoStarts();
            ClearDeferredPlayFromBar();
            _fadePhase = FadePhase.None;
            _fadePendingPath = null;
            DisposeVideoBindings();
            _videoBindings.Clear();
            _mainVideoPlayer = null;
            _videoRoomAudioSource = null;
            scope?.Dispose();
        }
    }
}
