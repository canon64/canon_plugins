using System;

namespace MainGameBlankMapAdd
{
    /// <summary>One editor registers its button during bootstrap; availability reads its held session state.</summary>
    public static class CharacterMakerBarApi
    {
        private static Registration _editor;
        public static bool IsAvailable => _editor != null && _editor.CanOpen();
        public static void Open() { if (IsAvailable) _editor.Open(); }

        public static IDisposable Register(Func<bool> canOpen, Action open)
        {
            if (canOpen == null || open == null) throw new ArgumentNullException();
            if (_editor != null) throw new InvalidOperationException("A character editor is already registered.");
            return _editor = new Registration(canOpen, open);
        }

        private sealed class Registration : IDisposable
        {
            internal readonly Func<bool> CanOpen;
            internal readonly Action Open;
            internal Registration(Func<bool> canOpen, Action open) { CanOpen = canOpen; Open = open; }
            public void Dispose() { if (ReferenceEquals(_editor, this)) _editor = null; }
        }
    }
}
