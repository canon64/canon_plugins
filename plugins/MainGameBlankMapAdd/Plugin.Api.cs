using System;

namespace MainGameBlankMapAdd
{
    /// <summary>
    /// 外部プラグインが動画マップに接続するための公開API。
    /// イベントを購読するか、staticメソッドを呼ぶ。
    /// </summary>
    public sealed partial class Plugin
    {
        // ── イベント ─────────────────────────────────────────────────────────

        /// <summary>動画の準備が完了したとき（再生開始直前）に発火。引数は動画ファイルパス。</summary>
        public static event Action<string> OnVideoLoaded;

        /// <summary>動画の再生が開始したとき発火。引数は動画ファイルパス。</summary>
        public static event Action<string> OnVideoStarted;

        /// <summary>動画が終端に達したとき発火（ループ設定に関わらず）。引数は動画ファイルパス。</summary>
        public static event Action<string> OnVideoEnded;

        /// <summary>設定が保存・適用されたとき発火。</summary>
        public static event Action OnSettingsApplied;

        // ── 公開メソッド ─────────────────────────────────────────────────────

        /// <summary>
        /// 外部プラグインから動画パスまたはストリームURLを指定して動画ルームを切り替える。
        /// ファイルパス・rtsp://・http:// などを受け付ける。
        /// </summary>
        public static bool LoadVideo(string urlOrPath)
        {
            var inst = Instance;
            if (inst == null) return false;
            inst.LogWarn("[api] LoadVideo is disabled. Individual playback has been removed; use folder play controls.");
            return false;
        }

        /// <summary>
        /// 現在 _mainVideoPlayer に設定されている動画ファイルパスを返す。
        /// 未再生または不明なら null。
        /// </summary>
        public static string GetCurrentVideoPath()
        {
            var inst = Instance;
            if (inst?._mainVideoPlayer == null) return null;

            string url = inst._mainVideoPlayer.url;
            if (string.IsNullOrEmpty(url)) return null;

            // Unity VideoPlayer は "file:///..." 形式で返すことがある
            if (url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                url = Uri.UnescapeDataString(url.Substring(8));

            return url;
        }

        // ── 内部からイベントを安全に発火するヘルパー ────────────────────────

        private static void FireOnVideoLoaded(string videoPath)
        {
            try { OnVideoLoaded?.Invoke(videoPath); }
            catch (Exception ex)
            {
                Instance?.LogWarn($"[api] OnVideoLoaded handler threw: {ex.Message}");
            }
        }

        private static void FireOnVideoStarted(string videoPath)
        {
            try { OnVideoStarted?.Invoke(videoPath); }
            catch (Exception ex)
            {
                Instance?.LogWarn($"[api] OnVideoStarted handler threw: {ex.Message}");
            }
        }

        private static void FireOnVideoEnded(string videoPath)
        {
            try { OnVideoEnded?.Invoke(videoPath); }
            catch (Exception ex)
            {
                Instance?.LogWarn($"[api] OnVideoEnded handler threw: {ex.Message}");
            }
        }

        private static void FireOnSettingsApplied()
        {
            try { OnSettingsApplied?.Invoke(); }
            catch (Exception ex)
            {
                Instance?.LogWarn($"[api] OnSettingsApplied handler threw: {ex.Message}");
            }
        }
    }
}
