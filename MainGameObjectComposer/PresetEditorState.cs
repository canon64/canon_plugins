namespace MainGameObjectComposer
{
    [System.Serializable]
    internal sealed class PresetEditorSnapshot
    {
        public int editSlot = 1;
        public int fixedSlot = 1;
    }

    // The edit destination chooses a pose endpoint; it never stops the motion clock.
    internal sealed class PresetEditorState
    {
        internal int EditSlot { get; private set; } = 1;
        internal int FixedSlot { get; private set; } = 1;
        internal void Open() { EditSlot = FixedSlot = 1; }
        internal void Select(int slot)
        {
            EditSlot = slot;
            if (slot == 1 || slot == 2) FixedSlot = slot;
        }
        internal PresetEditorSnapshot Capture() => new PresetEditorSnapshot
        {
            editSlot = EditSlot, fixedSlot = FixedSlot
        };
        internal void Restore(PresetEditorSnapshot snapshot)
        {
            Open();
            if (snapshot == null) return;
            FixedSlot = snapshot.fixedSlot == 2 ? 2 : 1;
            EditSlot = snapshot.editSlot == 0 || snapshot.editSlot == 2 ? snapshot.editSlot : 1;
        }
        internal float Blend(float motion) => FixedSlot == 2 ? 1f : 0f;
    }
}
