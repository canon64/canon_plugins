using SessionGate = CanonPlugins.Local.SessionGate;
using BepInEx;
using BepInEx.Logging;
using KKAPI.Chara;

namespace MainGameClothColliderCards
{
    [BepInDependency("canon.gate", "2.2.0")]
    [BepInPlugin(Guid, "Cloth Collider Cards", "0.1.0")]
    [BepInDependency("marco.kkapi")]
    [BepInProcess("KoikatsuSunshine")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "canon.kks.clothcollidercards";
        internal static ManualLogSource LogSource;
        public static bool IsRunning { get; private set; }
        private static BepInEx.Configuration.ConfigEntry<bool> _logEnabled;
        internal static void LogInfo(string message)
        {
            if (_logEnabled != null && _logEnabled.Value) LogSource.LogInfo(message);
        }

        private void Awake()
        {
            LogSource = Logger;
            _logEnabled = Config.Bind("Logging", "LogEnabled", false,
                "カード読込・コライダー生成の通常ログ。変更は即時反映。");
            // Startup-only: changing this setting takes effect after restarting the game.
            IsRunning = Config.Bind("General", "Enabled", false,
                "布・コライダーのテスト機能を有効にする。変更はゲーム再起動後に反映。").Value;

            CharacterApi.RegisterExtraBehaviour<ColliderCardController>(Guid);
            LogInfo("Card collider data registered.");

SessionGate.Bind(this, Guid, BeginGateSession, EndGateSession, true);
}
    }
}
