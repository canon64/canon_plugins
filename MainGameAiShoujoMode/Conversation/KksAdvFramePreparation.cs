using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class KksAdvFramePreparation
    {
        internal static bool RequiresCapture(IReadOnlyList<KksAdvTape.Entry> entries)
        {
            foreach (var entry in entries)
                switch (entry.Row.Command)
                {
                    // Loading/jumping can expand commands not yet visible to this preview.
                    case "Open": case "Jump": case "ParameterFile":
                    case "Text": case "Motion": case "Expression": case "CharaMotion": case "CharaExpression":
                    case "CharaMotionDefault": case "CharaLookEyes": case "CharaLookNeck":
                    case "CharaLookEyesTarget": case "CharaLookNeckTarget":
                    case "CrossFade": case "EventCGSetting": case "EventCGNext": case "EventCGRelease":
                    case "DOFTarget": case "CameraLightActive": case "CameraLightAngle": return true;
                }
            return false;
        }
        internal static Func<IReadOnlyList<KksAdvTape.Entry>, Action, Action<Exception>, IDisposable> Create(
            Func<Action, Action<Exception>, IDisposable> capture)
        {
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            return (entries, completed, failed) =>
            {
                if (RequiresCapture(entries)) return capture(completed, failed);
                completed(); return null;
            };
        }
    }
}
