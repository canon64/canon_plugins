using System;
using UnityEngine;
using MainGameBlankMapAdd.UI;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private const int FolderPopup = 0, VideoPopup = 1, ReverbPopup = 2,
            ActorPopup = 3, RoomPopup = 4, SlideshowPopup = 5;
        private readonly Rect[] _popupScreenRects = new Rect[6];
        private PlaybackPopupInput _popupInput;
        private Vector2 _popupScreenOrigin;
        private static readonly int[] PopupDrawOrder = { FolderPopup, VideoPopup, ReverbPopup, SlideshowPopup, ActorPopup, RoomPopup };

        private bool IsPlaybackPopupOpen(int id)
        {
            switch (id)
            {
                case FolderPopup: return _vmdBarMode ? _vmdGroupOpen : _settings.FolderPlayEnabled && _folderDropdownOpen;
                case VideoPopup: return _vmdBarMode ? _vmdEntryOpen : _settings.FolderPlayEnabled && _videoDropdownOpen;
                case ReverbPopup: return _reverbDropdownOpen;
                case ActorPopup: return _actorCoordinatesOpen;
                case RoomPopup: return _roomOverlayDropdownOpen;
                case SlideshowPopup: return _imageSlideshowFolderDropdownOpen;
                default: return false;
            }
        }

        private void BeginPlaybackPopupInput()
        {
            _popupScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            var mouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            int target = -1;
            // Match foreground drawing order, including the embedded room window.
            foreach (int id in PopupDrawOrder)
                if (IsPlaybackPopupOpen(id) && _popupScreenRects[id].Contains(mouse)) target = id;
            _popupInput = new PlaybackPopupInput(Event.current, target);
            // Only lists actually drawn this repaint may retain a blocking rect.
            if (Event.current.type == EventType.Repaint)
                Array.Clear(_popupScreenRects, 0, _popupScreenRects.Length);
        }

        private IDisposable EnterPlaybackPopup(int id, Rect rect)
        {
            // Convert out of any embedded window/scroll group before caching.
            var position = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            _popupScreenRects[id] = new Rect(position.x, position.y, rect.width, rect.height);
            return _popupInput.Enter(id);
        }

        private Rect GetPlaybackPopupInputRect(int id)
        {
            if (!IsPlaybackPopupOpen(id)) return Rect.zero;
            var rect = _popupScreenRects[id];
            return new Rect(rect.x - _popupScreenOrigin.x, rect.y - _popupScreenOrigin.y, rect.width, rect.height);
        }
    }
}
