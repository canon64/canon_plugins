using System;

namespace MainGameBlankMapAdd
{
    internal struct MapTabLayout
    {
        internal readonly float ContentWidth, AdjustmentWidth, ListWidth, PaneHeight;
        internal MapTabLayout(float availableWidth, float availableHeight)
        {
            ContentWidth = Math.Max(650f, availableWidth);
            AdjustmentWidth = Math.Min(620f, Math.Max(420f, ContentWidth * .55f));
            ListWidth = Math.Max(210f, ContentWidth - AdjustmentWidth - 12f);
            PaneHeight = Math.Max(120f, availableHeight - 52f);
        }
        internal static int Columns(float availableWidth)
        {
            return Math.Max(1, (int)Math.Floor((availableWidth - 16f + 4f) / 164f));
        }
    }
}
