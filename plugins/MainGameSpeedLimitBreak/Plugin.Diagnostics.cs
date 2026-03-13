using System;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private bool IsDiagnosticTraceEnabled()
        {
            var s = Settings;
            return s != null && s.EnablePerFrameTrace;
        }

        internal void TracePatchCall(string patchName, HFlag flags)
        {
            if (!IsDiagnosticTraceEnabled())
            {
                return;
            }

            if (flags == null)
            {
                LogDiag($"patch:{patchName}:null", $"[patch] {patchName} flags=null", 1f);
                return;
            }

            LogDiag(
                $"patch:{patchName}:mode:{(int)flags.mode}",
                $"[patch] {patchName} mode={flags.mode} speedCalc={flags.speedCalc:0.###} speed={flags.speed:0.###}",
                0.5f);
        }

        internal void TracePatchCall(string patchName)
        {
            if (!IsDiagnosticTraceEnabled())
            {
                return;
            }

            LogDiag($"patch:{patchName}", $"[patch] {patchName}", 0.5f);
        }

        internal void TraceTimelineSkip(string sourceTag, string reason)
        {
            if (!IsDiagnosticTraceEnabled())
            {
                return;
            }

            LogDiag($"timeline-skip:{sourceTag}:{reason}", $"[timeline] skip src={sourceTag} reason={reason}", 1f);
        }

        internal void TraceTimelineApply(string sourceTag, HFlag flags, float gauge01)
        {
            if (!IsDiagnosticTraceEnabled())
            {
                return;
            }

            if (flags == null)
            {
                LogDiag($"timeline-apply:{sourceTag}:null", $"[timeline] apply src={sourceTag} flags=null", 1f);
                return;
            }

            LogDiag(
                $"timeline-apply:{sourceTag}:mode:{(int)flags.mode}",
                $"[timeline] apply src={sourceTag} mode={flags.mode} gauge={gauge01:0.###} speedCalc={flags.speedCalc:0.###} speed={flags.speed:0.###}",
                0.5f);
        }

        private void LogDiag(string key, string message, float intervalSec)
        {
            float now = Time.unscaledTime;
            float interval = Mathf.Max(0.1f, intervalSec);
            if (_diagNextLogTime.TryGetValue(key, out float next) && now < next)
            {
                return;
            }

            _diagNextLogTime[key] = now + interval;
            LogInfo(message);
        }
    }
}
