namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private bool DildoVoiceSuppressed => _settings == null || !_settings.VoiceEnabled || SyringeVoiceSuppressed;

        // Apply only on setting/session/lease changes; cleanup also works after gate loss.
        private void ApplyVoiceSetting()
        {
            bool enabled = CanonPlugins.Shared.PluginSessionGate.Allowed && !DildoVoiceSuppressed;
            MainGameVoiceFaceEventBridge.PublicApi.SetImpactCueEnabled(Guid, enabled, !enabled);
            if (enabled) return;
            StopOralAudio();
            ResetCloneVoiceCycle();
        }
    }
}
