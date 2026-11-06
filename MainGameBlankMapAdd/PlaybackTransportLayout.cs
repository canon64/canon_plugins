using System;
namespace MainGameBlankMapAdd
{
    internal static class PlaybackTransportLayout
    {
        internal static bool WrapTransport(float width) => width < 980;
        internal static float ListOffset(float width) => WrapTransport(width) ? 52 : 26;
        internal static bool SeparateAudio(float width) => width < 1400f;
        internal static bool InlinePresetControls(float width) => width >= 1270f;
        internal static bool InlineMapWarp(float width) => width >= 1560f;
        internal static float MapWarpRowHeight(float width) => InlineMapWarp(width) ? 0f : 24f;
        internal static float TopControlsHeight(float width) => InlinePresetControls(width) ? 24f : (width >= 840f ? 48f : 72f);
        internal static bool WrapPresetControls(float width) => width < 840f;
#if SHOW_EXPERIMENTAL_VRFX_BAR
        internal const float ToggleContentWidth = 1064f;
#else
        internal const float ToggleContentWidth = 932f;
#endif
        internal static float ToggleRowHeight(float width) => ToggleContentWidth > width - 12f ? 38f : 20f;
        internal static float ExtraHeight(float width, bool expanded) => ListOffset(width) + 22 + (SeparateAudio(width) ? 26 : 0) + (expanded ? 26 : 0);
        internal static float MinimumHeight(float width, bool expanded) =>
            (expanded ? 522 : 8) + TopControlsHeight(width) + MapWarpRowHeight(width) + ListOffset(width) + (SeparateAudio(width) ? 26 : 0) + 26 + ToggleRowHeight(width) + 4;
        internal static float MinimumHeight(float width, bool expanded, float buttonWidth, bool folder, bool handy, bool vmd) =>
            MinimumHeight(width, expanded) - ListOffset(width) + PlaybackButtonFlow.ListOffset(width, buttonWidth, folder, handy, vmd);
        internal static float ChoiceWidth(float width) => Math.Max(1, (width - 18) / 4);
        internal static float SeekWidth(float start, float end, bool handy) => Math.Max(40, end - start - (handy ? 114 : 0));
    }
}
