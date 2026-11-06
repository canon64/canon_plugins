using SessionGate = CanonPlugins.Local.SessionGate;
namespace AstralMode
{
    public sealed partial class Plugin
    {
        // ksPlug時間制御との接続はここだけ。切り離す際はこの初期化を外す。
        private void InitializeKsPlugTimePatch()
        {
            KsPlugTimePatch.Install(
                () => this != null && isActiveAndEnabled
                    && _cfgEnabled != null && _cfgEnabled.Value
                    && _cfgSuppressKsPlugTimePassesInAstral != null && _cfgSuppressKsPlugTimePassesInAstral.Value
                    && _state != AstralState.Normal
                    && SessionGate.Allowed,
                message => Logger.LogWarning(message));
        }

        private void Start() => InitializeKsPlugTimePatch();
    }
}
