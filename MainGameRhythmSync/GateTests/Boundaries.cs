using System;
using CanonGate;
using UnityEngine;
public sealed class VoiceState { public bool speedMotion; }
public sealed class HFlag
{
    public float speed=1.3f,speedCalc=.4f,speedItem=.7f,timeNoClick=2f;
    public Vector2 speedUpClac=new Vector2(.2f,.6f);
    public VoiceState voice=new VoiceState();
}
public class FaceListCtrl { }
public partial class HSceneProc { public HFlag flags = new HFlag(); }
public sealed class VrScene : MonoBehaviour { public HFlag flags = new HFlag(); }
namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static int Held;
        public static void SetInputBlockRects(string o,Rect[] r) { Held=1; }
        public static void SetIdleCursorUnlock(string o,bool v) { if(!v) Held=0; }
        public static void ClearInputBlockRects(string o) { Held=0; }
        public static void Sync(string o,string s,bool v) { }
    }
}
namespace MainGameRhythmSync
{
    public sealed partial class Plugin : MonoBehaviour
    {
        public const string Guid="com.kks.maingame.rhythmsync";
        internal bool LoggingEnabled=true;
        internal BepInEx.Log Logger = new BepInEx.Log();
        private object _effectSource;
        private void PrepareEffectSource() { }
        private void ReleaseGroup() { }
        internal void Register() { _gateRegistration=GateApi.Register(Guid,BeginRhythmSession,EndRhythmSession); }
        internal void Unregister() => _gateRegistration.Dispose();
    }
}
namespace MainGameBeatSyncSpeed
{
    public partial class Plugin : MonoBehaviour
    {
        public const string Guid="com.kks.maingame.beatsyncseed";
        internal static Plugin Instance;
        internal static MainGameRhythmSync.Plugin Host;
        private MainGameRhythmSync.SessionScene _hSceneProc;
        private bool _insideHScene,_wavAnalysisRunning,_wavExtractionRunning,_analysisReady;
        private int _wavAnalysisGeneration;
        private object _wavAnalysisResult;
        private string _wavExtractionResult,_lastAnalyzedPath,_lastVideoPath,_effectSong,_wavExtractionSource,_currentSongPathKey;
        private float[] _beatIntensities;
        internal static float CurrentIntensity01;
        private float _currentZoneRawTarget,_highSustainStart,_lowSustainStart,_nextVideoRoomPollTime,_nextSongPathPollTime,_nextVideoPathChangeCheckTime,_smoothedIntensity01;
        private Type _blankMapPluginType;
        private void ResolveSnapshotMethod() { }
        private void TryGetVideoUrlFromApi() { }
        private void ResetVideoRoom() { }
        private void LogInfo(string s) { }
        private void BeginAutoBpmSession() { }
        private void EndAutoBpmSession() { }
        internal int Epoch => _workEpoch;
        internal bool PendingCleared => _wavAnalysisResult==null && _wavExtractionResult==null;
        internal void SeedPending() { _wavAnalysisResult=new object(); _wavExtractionResult="old"; }
    }
}
namespace MainGameSpeedLimitBreak
{
    internal static class SettingsStore
    {
        internal static int Loads;
        internal static object LoadOrCreate(string p,Action<string> a,Action<string>b,Action<string>c) { ++Loads; return new object(); }
    }
    public partial class Plugin : MonoBehaviour
    {
        public const string Guid="com.kks.maingame.speedlimitbreak";
        internal static Plugin Instance;
        internal static MainGameRhythmSync.Plugin Host;
        private MainGameRhythmSync.SessionScene _hSceneProc;
        private bool _insideHScene,_showBpmUi;
        private string _pluginDir,_uiNotice;
        private float _nextVideoRoomPollTime,_nextBpmReferenceModeScanTime;
        private Rect _windowRect;
        private object Settings;
        internal void OpenUi() { _showBpmUi=true; RefreshInputCapture(); }
        private void ApplyConfigManagerOverrides(bool b,string s) { }
        private void SyncUiFromSettings() { }
        private void ResolveBlankMapSnapshotMethod() { }
        private void StopBpmMeasure(string s,bool b) { }
        private void ResetVideoCueRuntime(bool b) { }
        private void ResetVideoRoomPlaybackSnapshot() { }
        private void LogInfo(string s) { }
        private void LogWarn(string s) { }
        private void LogError(string s) { }
    }
}
