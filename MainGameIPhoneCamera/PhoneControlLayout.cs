namespace MainGameIPhoneCamera
{
    internal static class PhoneControlLayout
    {
        internal const float ButtonWidth = 160, ButtonHeight = 60, HintHeight = 48, MessageHeight = 44;
        private static int Columns(bool landscape, int count) => landscape ? count : 3;
        private static int Rows(bool landscape, int count) => (count + Columns(landscape, count) - 1) / Columns(landscape, count);
        internal static float Height(bool landscape) => landscape ? 560 : 1166;
        internal static float HintY(bool landscape) => -Height(landscape) / 2 + 32;
        internal static float ButtonX(bool landscape, int count, int index)
            => (index % Columns(landscape, count) - (Columns(landscape, count) - 1) * .5f) * 174;
        internal static float ButtonY(bool landscape, int count, int index)
            => -Height(landscape) / 2 + 100 + (Rows(landscape, count) - 1 - index / Columns(landscape, count)) * 69;
        internal static float MessageY(bool landscape, int count)
            => -Height(landscape) / 2 + 170 + (Rows(landscape, count) - 1) * 69;
    }
}
