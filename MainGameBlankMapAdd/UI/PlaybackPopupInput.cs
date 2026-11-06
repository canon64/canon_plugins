using System;
using UnityEngine;

namespace MainGameBlankMapAdd.UI
{
    // Keep control allocation/drawing order unchanged; only the foreground list
    // receives pointer events. GUI.enabled alone is insufficient for custom controls.
    internal sealed class PlaybackPopupInput : IDisposable
    {
        private readonly Event _event;
        private readonly EventType _type;
        private readonly int _target;

        internal PlaybackPopupInput(Event current, int target)
        {
            _event = current;
            _type = current.type;
            _target = IsPointer(_type) ? target : -1;
            if (_target >= 0) _event.type = EventType.Ignore;
        }

        internal static bool IsPointer(EventType type) => type == EventType.MouseDown
            || type == EventType.MouseUp || type == EventType.MouseDrag || type == EventType.ScrollWheel;

        internal IDisposable Enter(int popup)
        {
            if (_target == popup) _event.type = _type;
            return new PopupScope(this, popup);
        }

        public void Dispose()
        {
            if (_target < 0) return;
            // Even blank rows, padding and scrollbars belong to the list.
            _event.type = _type;
            _event.Use();
        }

        private sealed class PopupScope : IDisposable
        {
            private readonly PlaybackPopupInput _owner;
            private readonly int _popup;
            internal PopupScope(PlaybackPopupInput owner, int popup) { _owner = owner; _popup = popup; }
            public void Dispose()
            {
                if (_owner._target == _popup) _owner._event.type = EventType.Ignore;
            }
        }
    }
}
