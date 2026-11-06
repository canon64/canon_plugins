namespace MainGameMagicPaint
{
    internal static class PaintVisibility
    {
        internal static bool Participates(bool enabled, bool active, bool forceOff, bool shadowsOnly, int layer, string name)
        {
            if (!enabled || !active || forceOff || shadowsOnly) return false;
            // Native KKS skeleton assets put these invisible shadow proxies on layer 11.
            // Keep other layer-11 renderers and visible clothes/hair in the occlusion test.
            return layer != 11 || (name != "o_shadowcaster" && name != "o_shadowcaster_cm");
        }
    }
}
