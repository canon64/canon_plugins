using System;
using System.Collections.Generic;
namespace MainGameBlankMapAdd
{
    // Width-based placement shared by measurement and drawing; no scene or input polling.
    internal sealed class PlaybackButtonFlow
    {
        internal const float PhoneMagicCandleWidth = 58f + 4f + 74f + 4f + 86f;
        internal struct Slot { internal float X, Y, Width; }
        readonly float _right;
        float _x = 110f;
        internal int Row { get; private set; }
        internal PlaybackButtonFlow(float width) { _right = Math.Max(114f, width - 6f); }
        internal Slot Take(float width, float gap = 4f)
        {
            if (_x + width > _right && _x > 6f) { _x = 6f; Row++; }
            var slot = new Slot { X = _x, Y = Row * 26f, Width = width }; _x += width + gap; return slot;
        }
        internal static float ListOffset(float width, float buttonWidth, bool folder, bool handy, bool vmd)
        {
            var flow = new PlaybackButtonFlow(width);
            if (vmd) { for (int i=0;i<5;i++) flow.Take(56); if (width >= 650) flow.Take(130); }
            else
            {
                for (int i=0;i<(folder?5:3);i++) flow.Take(buttonWidth,6);
                if (folder) { flow.Take(Math.Max(buttonWidth,92),6); flow.Take(Math.Max(buttonWidth,108),6); }
                flow.Take(Math.Max(buttonWidth,108),6);
                if (folder) flow.Take(Math.Max(buttonWidth,98),6);
            }
            if (handy) flow.Take(110);
            flow.Take(58); flow.Take(58); flow.Take(58); flow.Take(58);
            // Keep the three adjacent item buttons together, including when wrapping.
            flow.Take(PhoneMagicCandleWidth); flow.Take(110);
            return (flow.Row+1)*26f + (vmd && width<980 ? 26f : 0f);
        }
    }
}
