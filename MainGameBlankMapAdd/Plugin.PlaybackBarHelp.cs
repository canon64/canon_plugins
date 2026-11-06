using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private Action _beginPlaybackHelp;
        private Func<string> _takePlaybackHelp;
        private Action _endPlaybackHelp;
        private float _resolvePlaybackHelpAfter;
        private bool _playbackHelpCapturing;
        private bool _playbackHelpErrorReported;
        private GUIStyle _playbackHelpStyle;
        private readonly UI.PlaybackHelpWindow _playbackHelpWindow = new UI.PlaybackHelpWindow();
        private bool _playbackHelpSubmitted;
        private string _embeddedHoveredHelp;

        private void ClearPlaybackHelpWindow()
        {
            _playbackHelpWindow.Clear();
            _playbackHelpSubmitted = false;
        }

        private void ReportPlaybackHelpError(Exception ex)
        {
            if (_playbackHelpErrorReported) return;
            _playbackHelpErrorReported = true;
            Logger.LogWarning("Playback help integration failed; using original help: " + ex);
        }

        private void BeginPlaybackHelpCapture()
        {
            try
            {
                if (_beginPlaybackHelp == null && Time.unscaledTime >= _resolvePlaybackHelpAfter)
                {
                    _resolvePlaybackHelpAfter = Time.unscaledTime + 2f;
                    if (!Chainloader.PluginInfos.TryGetValue("com.kks.maingame.uihelp", out var info)) return;
                    Type api = info.Instance.GetType();
                    var begin = api.GetMethod("BeginHostCapture", Type.EmptyTypes);
                    var take = api.GetMethod("TakeHostHelp", Type.EmptyTypes);
                    var end = api.GetMethod("EndHostCapture", Type.EmptyTypes);
                    if (begin == null || take == null || end == null)
                        throw new MissingMethodException(api.FullName, "BeginHostCapture/TakeHostHelp/EndHostCapture");
                    // Publish only after all three signatures have been validated.
                    var beginCall = (Action)Delegate.CreateDelegate(typeof(Action), begin);
                    var takeCall = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), take);
                    var endCall = (Action)Delegate.CreateDelegate(typeof(Action), end);
                    _beginPlaybackHelp = beginCall;
                    _takePlaybackHelp = takeCall;
                    _endPlaybackHelp = endCall;
                }
                if (_beginPlaybackHelp == null) return;
                _playbackHelpCapturing = true;
                _beginPlaybackHelp();
            }
            catch (Exception ex)
            {
                ReportPlaybackHelpError(ex);
                EndPlaybackHelpCapture();
            }
        }

        private string TakePlaybackHelp()
        {
            if (!_playbackHelpCapturing) return null;
            try { return _takePlaybackHelp(); }
            catch (Exception ex) { ReportPlaybackHelpError(ex); return null; }
        }

        private void EndPlaybackHelpCapture()
        {
            if (!_playbackHelpCapturing) return;
            try { _endPlaybackHelp?.Invoke(); }
            catch (Exception ex) { ReportPlaybackHelpError(ex); }
            finally { _playbackHelpCapturing = false; }
        }
    }
}
