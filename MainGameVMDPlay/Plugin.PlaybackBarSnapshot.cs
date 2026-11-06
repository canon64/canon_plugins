using System;

namespace MainGameVMDPlay
{
    public sealed class PlaybackBarSnapshot
    {
        public bool Ready, Owns, Loop;
        public string Title, Status;
        public float Position, Duration, Volume;
        public string[] Groups, Entries;
        public int GroupIndex, EntryIndex;
    }

    public sealed partial class Plugin
    {
        // The host reads this only when drawing its UI. Catalog rebuilding uses the existing dirty revision.
        public static PlaybackBarSnapshot ReadPlaybackBarSnapshot()
        {
            var p = Instance;
            if (!RoomAllowed || p?._settings == null) return null;
            p.UpdateBarCatalog();
            return new PlaybackBarSnapshot {
                Ready = p._available && p._cfgEnabled.Value && p._insideHScene && p._trialChara != null,
                Owns = p._vmdOwnsCharacter, Loop = p._settings.MyListLoop,
                Title = p._presetName, Status = p._presetMessage,
                Position = p._seekDragging || p._seekRequested ? p._seekDragValue : p._currentAnimTime,
                Duration = p._animLength, Volume = p._settings.AudioVolume,
                Groups = p._barGroups, Entries = p._barEntries,
                GroupIndex = p._barGroup.Length == default(int) ? default(int) : Array.IndexOf(p._barGroups, p._barGroup),
                EntryIndex = p.BarIndex
            };
        }
    }
}
