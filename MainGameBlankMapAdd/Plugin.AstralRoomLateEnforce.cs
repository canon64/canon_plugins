namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private void FixedUpdate()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            MaintainAstralRoomOffset();
        }
    }
}
