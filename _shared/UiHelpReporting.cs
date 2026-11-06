using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace CanonPlugins.Shared
{
    internal static class UiHelpReporting
    {
        private static bool _resolved;
        private static Action<Rect, string, string> _report;

        internal static void ReportLastControl(string label, string help)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (!_resolved)
            {
                _resolved = true;
                if (Chainloader.PluginInfos.TryGetValue("com.kks.maingame.uihelp", out var info))
                {
                    try
                    {
                        var method = info.Instance.GetType().GetMethod("ReportControl", new[] { typeof(Rect), typeof(string), typeof(string) });
                        if (method != null) _report = (Action<Rect, string, string>)Delegate.CreateDelegate(typeof(Action<Rect, string, string>), method);
                    }
                    catch (Exception ex)
                    {
                        BepInEx.Logging.Logger.CreateLogSource("UiHelpReporting").LogWarning("Help reporting connection failed: " + ex);
                    }
                }
            }
            try { _report?.Invoke(GUILayoutUtility.GetLastRect(), label, help); }
            catch (Exception ex)
            {
                _report = null;
                BepInEx.Logging.Logger.CreateLogSource("UiHelpReporting").LogWarning("Help reporting disabled after an observation error: " + ex);
            }
        }
    }
}
