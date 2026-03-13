using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MainGameBeatSyncSpeed
{
    public partial class Plugin
    {
        private static readonly object[] _snapshotArgs = new object[4];
        private static Type         _blankMapPluginType;
        private static MethodInfo   _blankMapSnapshotMethod;
        private static bool         _blankMapLookupDone;
        private static PropertyInfo _blankMapInstanceProp;
        private static FieldInfo    _mainVideoPlayerField;

        // ── 毎フレーム呼び出し ────────────────────────────────────────
        private void PollVideoRoom()
        {
            if (Time.unscaledTime < _nextVideoRoomPollTime) return;
            _nextVideoRoomPollTime = Time.unscaledTime + 0.05f; // 20fps

            if (TryGetSnapshot(out double timeSec, out bool isPlaying))
            {
                bool wasPlaying = _videoRoomPlaying;
                _videoRoomTimeSec   = timeSec;
                _videoRoomPlaying   = isPlaying;

                if (!_videoRoomFetchWasAvailable ||
                    (!wasPlaying && isPlaying))
                {
                    LogInfo($"[video-bridge] ok playing={isPlaying} time={timeSec:0.###}");
                }
                _videoRoomFetchWasAvailable = true;
            }
            else
            {
                bool hadAvailable = _videoRoomFetchWasAvailable;
                ResetVideoRoom();
                if (hadAvailable &&
                    Time.unscaledTime >= _nextVideoRoomErrorLogTime)
                {
                    _nextVideoRoomErrorLogTime = Time.unscaledTime + 2f;
                    LogInfo("[video-bridge] snapshot unavailable");
                }
            }
        }

        private void ResetVideoRoom()
        {
            _videoRoomTimeSec           = 0d;
            _videoRoomPlaying           = false;
            _videoRoomFetchWasAvailable = false;
        }

        // ── リフレクション経由でスナップショット取得 ──────────────────
        private bool TryGetSnapshot(out double timeSec, out bool isPlaying)
        {
            timeSec   = 0d;
            isPlaying = false;

            if (!ResolveSnapshotMethod()) return false;

            try
            {
                _snapshotArgs[0] = 0d;
                _snapshotArgs[1] = 0d;
                _snapshotArgs[2] = false;
                _snapshotArgs[3] = false;

                bool ok = (bool)_blankMapSnapshotMethod.Invoke(null, _snapshotArgs);
                if (!ok) return false;

                timeSec   = _snapshotArgs[0] is double t ? t : 0d;
                isPlaying = _snapshotArgs[3] is bool p && p;
                return true;
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextVideoRoomErrorLogTime)
                {
                    _nextVideoRoomErrorLogTime = Time.unscaledTime + 2f;
                    LogWarn("video-bridge invoke failed: " + ex.Message);
                }
                return false;
            }
        }

        // ── 動画ファイルパス取得 ───────────────────────────────────────
        internal bool TryGetVideoFilePath(out string videoPath)
        {
            videoPath = null;
            if (!ResolveSnapshotMethod()) return false; // ensures _blankMapPluginType is resolved

            // Plugin.Instance プロパティ
            if (_blankMapInstanceProp == null)
            {
                _blankMapInstanceProp = _blankMapPluginType.GetProperty(
                    "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (_blankMapInstanceProp == null) return false;
            }

            object inst = _blankMapInstanceProp.GetValue(null, null);
            if (inst == null) return false;

            // _mainVideoPlayer フィールド（UnityEngine.Video.VideoPlayer）
            if (_mainVideoPlayerField == null)
            {
                _mainVideoPlayerField = _blankMapPluginType.GetField(
                    "_mainVideoPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_mainVideoPlayerField == null) return false;
            }

            object player = _mainVideoPlayerField.GetValue(inst);
            if (player == null) return false;

            // VideoPlayer.url は string プロパティ（型参照不要）
            PropertyInfo urlProp = player.GetType().GetProperty("url");
            string url = urlProp?.GetValue(player, null) as string;
            if (string.IsNullOrEmpty(url)) return false;

            // "file:///..." → ローカルファイルパス
            if (url.StartsWith("file:///"))
                url = Uri.UnescapeDataString(url.Substring(8));

            videoPath = url.Replace('/', Path.DirectorySeparatorChar);
            return File.Exists(videoPath);
        }

        private bool ResolveSnapshotMethod()
        {
            if (_blankMapSnapshotMethod != null) return true;
            if (_blankMapLookupDone)             return false;

            _blankMapLookupDone = true;

            _blankMapPluginType =
                AccessTools.TypeByName("MainGameBlankMapAdd.Plugin") ??
                Type.GetType("MainGameBlankMapAdd.Plugin, MainGameBlankMapAdd");

            if (_blankMapPluginType == null)
            {
                LogWarn("MainGameBlankMapAdd.Plugin not found — video-bridge disabled");
                return false;
            }

            _blankMapSnapshotMethod = _blankMapPluginType.GetMethod(
                "TryGetMainVideoPlaybackSnapshot",
                BindingFlags.Public | BindingFlags.Static);

            if (_blankMapSnapshotMethod == null)
            {
                LogWarn("TryGetMainVideoPlaybackSnapshot not found");
                return false;
            }

            LogInfo("video-bridge connected: MainGameBlankMapAdd.Plugin.TryGetMainVideoPlaybackSnapshot");
            return true;
        }
    }
}
