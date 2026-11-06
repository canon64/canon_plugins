namespace MainGameVoiceFaceEventBridge.Audio
{
    internal static class ImpactCueTarget
    {
        internal static ChaControl Choose(bool explicitActor, bool speaking, ChaControl dialogue, ChaControl contact)
            => !explicitActor && speaking && dialogue != null ? dialogue : contact;
    }
}
