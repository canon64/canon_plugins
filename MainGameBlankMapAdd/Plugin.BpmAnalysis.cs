using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private static bool _bpmAnalysisApiResolved;
        private static Func<string, bool> _requestBpmAnalysis;
        private static Func<string, string> _getBpmAnalysisStatus;

        private static void ResolveBpmAnalysisApi()
        {
            if (_bpmAnalysisApiResolved) return;
            _bpmAnalysisApiResolved = true;
            if (!Chainloader.PluginInfos.TryGetValue("com.kks.maingame.rhythmsync", out var info)) return;
            var type = info.Instance.GetType();
            var request = type.GetMethod("RequestSongBpmAnalysis");
            var status = type.GetMethod("GetSongBpmAnalysisStatus");
            if (request != null && status != null)
            {
                _requestBpmAnalysis = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), request);
                _getBpmAnalysisStatus = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), status);
            }
        }

        private void DrawBpmAnalysisButton(Rect rect, Func<Rect, string, string, bool> helpButton)
        {
            ResolveBpmAnalysisApi();
            string path = CanonPlugins.Shared.VmdRhythmSource.TryRead(out string vmd, out _, out _) ? vmd : GetCurrentVideoPath();
            string status = _getBpmAnalysisStatus?.Invoke(path) ?? "RhythmSync未ロード";
            bool busy = status == "解析中";
            bool previous = GUI.enabled;
            GUI.enabled = previous && _requestBpmAnalysis != null && !string.IsNullOrWhiteSpace(path) && !busy && status != "リズム同期OFF";
            if (helpButton(rect, busy ? "解析中…" : "BPM解析",
                "現在の曲のBPMを音声から解析して保存します。登録済みの値も更新します。右Ctrlタップで修正できます。\n" + status))
                _requestBpmAnalysis(path);
            GUI.enabled = previous;
        }
    }
}
