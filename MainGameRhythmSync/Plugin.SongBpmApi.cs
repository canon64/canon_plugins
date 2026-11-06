namespace MainGameRhythmSync {
    public sealed partial class Plugin {
        public static int GetRegisteredSongBpm(string path) => MainGameBeatSyncSpeed.Plugin.Instance?.ReadRegisteredBpm(path) ?? 0;
        public static bool RegisterCurrentSongBpm(int bpm) => MainGameBeatSyncSpeed.Plugin.Instance != null && MainGameBeatSyncSpeed.Plugin.Instance.RegisterCurrentBpm(bpm);
        public static bool RequestSongBpmAnalysis(string path) => MainGameBeatSyncSpeed.Plugin.Instance?.RequestBpmAnalysis(path) ?? false;
        public static string GetSongBpmAnalysisStatus(string path) => MainGameBeatSyncSpeed.Plugin.Instance?.BpmAnalysisStatus(path) ?? "リズム同期未ロード";
    }
}
