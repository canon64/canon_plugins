namespace MainGameHandySync
{
    internal sealed partial class Plugin
    {
        private static HSceneProc _hSceneProc;
        private static HFlag _hFlag;
        private static bool _inHScene;

        internal static HSceneProc HSceneProc => _hSceneProc;
        internal static HFlag HFlag => _hFlag;
        internal static bool InHScene => _inHScene;

    }
}
