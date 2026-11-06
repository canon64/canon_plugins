using System;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private HotelVoiceDiagnostics _voiceDiagnostics;
        internal void RefreshVoiceDiagnostics()
        {
            _voiceDiagnostics?.Dispose(); _voiceDiagnostics = null;
            if (_closing || _diagnostics?.Invoke() != true || _hSession?.Busy != true) return;
            try { _voiceDiagnostics = new HotelVoiceDiagnostics(_diagnostics, () => _hSession, _log); }
            catch (Exception ex) { if (_diagnostics()) _log("HOTEL_VOICE_POSITION setup-failed=" + ex.Message); }
        }
        private void DrawVoiceDiagnostics()
        {
            if (_diagnostics?.Invoke() != true) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("本編音声の定位診断");
            if (GUILayout.Button("音声定位を記録", GUILayout.ExpandWidth(false)))
            {
                _voiceDiagnostics?.Snapshot("manual-camera-check");
                _placementPanel?.Status("本編音声・カメラ・聞き取り位置をログへ記録しました。");
            }
            GUILayout.EndVertical();
        }
    }
}
