using System;
using System.Threading;
using CanonGate;
namespace MainGameHandySync
{
    internal sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); ClearHandyInput(); }
        private void BeginGateSession()
        {
            _hSceneProc = SessionGate.HScene; _hFlag = _hSceneProc.flags; _inHScene = true;
            _externalMotionStopped = !_cfgLinked.Value;
            if (_cfgEnabled.Value && _cfgLinked.Value) _handyClient.BeginSession();
            PrepareHampRestartInternal(); ConnectRhythmStroke();
            RefreshHandyRecording();
        }
        private void EndGateSession()
        {
            _handyEventRecording.Stop();
            DisconnectRhythmStroke(); ClearHandyInput();
            StopAllCoroutines(); _strokeCoroutine = null; _handyClient.EndSession();
            _lmAnalyzer?.Clear(); _animationReader.Reset(); _hSceneProc = null; _hFlag = null;
            _inHScene = false; _hampRunning = false; _currentPoseKey = "";
        }
        private void OnDestroy() { SessionGate.Close(); _rhythmHooks?.UnpatchSelf(); ClearHandyInput(); }
    }
}
