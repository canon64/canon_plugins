using BepInEx;
using MainGamePregnancyPlusBridge;

namespace MainGameVoiceImpactBoost
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency("com.kks.main.pregnancyplusbridge", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.kks.maingame.voicefaceeventbridge", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.main.voiceimpactboost";
        public const string PluginName = "MainGameVoiceImpactBoost";
        public const string Version = "0.2.0";

        private PluginConfig _config;
        private PluginFileLogger _logger;
        private HSceneMonitor _hSceneMonitor;
        private VoiceImpactBoostService _boostService;
        private bool _subscribed;

        private void Awake()
        {
            _config = new PluginConfig(Config);
            _logger = new PluginFileLogger(base.Logger, Info.Location, PluginName + ".log");
            _boostService = new VoiceImpactBoostService(_config, _logger);
            _hSceneMonitor = new HSceneMonitor(_config, _logger, _boostService.ResetOneShotState);

            TrySubscribe();
            _logger.LogInfo("起動完了 v" + Version);
        }

        private void Update()
        {
            _hSceneMonitor.Update();
        }

        private void OnDestroy()
        {
            TryUnsubscribe();
        }

        private void TrySubscribe()
        {
            try
            {
                MainGamePregnancyPlusBridge.Plugin.OnBellyBokoPeak += _boostService.HandleBellyBokoPeak;
                _subscribed = true;
                _logger.LogInfo("subscribed OnBellyBokoPeak");
            }
            catch (System.Exception ex)
            {
                _logger.LogInfo("subscribe failed: " + ex.Message);
            }
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            try
            {
                MainGamePregnancyPlusBridge.Plugin.OnBellyBokoPeak -= _boostService.HandleBellyBokoPeak;
                _logger.LogInfo("unsubscribed OnBellyBokoPeak");
            }
            catch (System.Exception ex)
            {
                _logger.LogInfo("unsubscribe failed: " + ex.Message);
            }

            _subscribed = false;
        }
    }
}
