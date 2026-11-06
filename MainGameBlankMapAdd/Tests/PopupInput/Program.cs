using MainGameBlankMapAdd;
using UnityEngine;

Console.WriteLine("Playback popup routing regression: no Unity/game launch");
Plugin.VerifyPopupInput();

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class Settings { internal bool FolderPlayEnabled = true; }
        private Settings _settings = new Settings();
        private bool _vmdBarMode, _vmdGroupOpen, _vmdEntryOpen, _folderDropdownOpen, _videoDropdownOpen,
            _reverbDropdownOpen, _actorCoordinatesOpen, _roomOverlayDropdownOpen, _imageSlideshowFolderDropdownOpen;

        public static void VerifyPopupInput()
        {
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            foreach (int popup in new[] { FolderPopup, VideoPopup, ReverbPopup, ActorPopup, RoomPopup, SlideshowPopup })
            foreach (var type in new[] { EventType.MouseDown, EventType.MouseUp, EventType.MouseDrag, EventType.ScrollWheel })
            {
                var p = new Plugin { _folderDropdownOpen = true, _videoDropdownOpen = true,
                    _reverbDropdownOpen = true, _actorCoordinatesOpen = true, _roomOverlayDropdownOpen = true,
                    _imageSlideshowFolderDropdownOpen = true };
                p._popupScreenRects[popup] = new Rect(100, 200, 80, 60);
                Event.current = new Event { type = type, mousePosition = new Vector2(110, 210) };
                p.BeginPlaybackPopupInput();
                Check(Event.current.type == EventType.Ignore, "Underlying controls before list cannot receive " + type);
                using (p.EnterPlaybackPopup((popup + 1) % 6, new Rect(0, 0, 1, 1)))
                    Check(Event.current.type == EventType.Ignore, "Other lists cannot receive the target event");
                using (p.EnterPlaybackPopup(popup, new Rect(100, 200, 80, 60)))
                {
                    Check(Event.current.type == type, "Target list receives " + type);
                    // Covers selection closing the list and padding not consuming input.
                    if (type == EventType.MouseUp) { p._folderDropdownOpen = false; Event.current.Use(); }
                }
                Check(Event.current.type == EventType.Ignore, "Later controls/embedded window cannot receive the event");
                p._popupInput.Dispose();
                Check(Event.current.type == EventType.Used, "List padding and selection do not leak to another OnGUI");
            }

            var closed = new Plugin();
            closed._popupScreenRects[ReverbPopup] = new Rect(100, 200, 80, 60);
            Event.current = new Event { type = EventType.MouseDown, mousePosition = new Vector2(110, 210) };
            closed.BeginPlaybackPopupInput(); closed._popupInput.Dispose();
            Check(Event.current.type == EventType.MouseDown, "Closed list cached rect cannot intercept clicks");
            Check(closed.GetPlaybackPopupInputRect(ReverbPopup).width == 0, "Closed reverb list has no native input blocker");

            foreach (bool group in new[] { true, false })
            {
                var vmd = new Plugin { _vmdBarMode = true, _vmdGroupOpen = group, _vmdEntryOpen = !group };
                int id = group ? FolderPopup : VideoPopup;
                vmd._popupScreenRects[id] = new Rect(100, 200, 80, 60);
                Event.current = new Event { type = EventType.MouseDown, mousePosition = new Vector2(110, 210) };
                vmd.BeginPlaybackPopupInput();
                Check(Event.current.type == EventType.Ignore, "VMD group/entry input protected");
                using (vmd.EnterPlaybackPopup(id, new Rect(100, 200, 80, 60)))
                    Check(Event.current.type == EventType.MouseDown, "VMD group/entry receives selection");
                vmd._popupInput.Dispose();
            }

            closed._folderDropdownOpen = true;
            closed._settings.FolderPlayEnabled = false;
            closed._popupScreenRects[FolderPopup] = new Rect(100, 200, 80, 60);
            Check(closed.GetPlaybackPopupInputRect(FolderPopup).width == 0, "Disabled folder playback leaves no hidden list blocker");

            var overlap = new Plugin { _roomOverlayDropdownOpen = true, _actorCoordinatesOpen = true };
            overlap._popupScreenRects[ActorPopup] = overlap._popupScreenRects[RoomPopup] = new Rect(100, 200, 80, 60);
            Event.current = new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(110, 210) };
            overlap.BeginPlaybackPopupInput();
            using (overlap.EnterPlaybackPopup(ActorPopup, new Rect(100, 200, 80, 60)))
                Check(Event.current.type == EventType.Ignore, "Covered list cannot scroll");
            using (overlap.EnterPlaybackPopup(RoomPopup, new Rect(100, 200, 80, 60)))
                Check(Event.current.type == EventType.ScrollWheel, "Foreground list owns scroll");
            overlap._popupInput.Dispose();

            foreach (var type in new[] { EventType.Layout, EventType.Repaint, EventType.KeyDown })
            {
                Event.current = new Event { type = type, mousePosition = new Vector2(110, 210) };
                overlap.BeginPlaybackPopupInput();
                using (overlap.EnterPlaybackPopup(RoomPopup, new Rect(100, 200, 80, 60)))
                    Check(Event.current.type == type, "Layout/repaint/keyboard order remains intact");
                overlap._popupInput.Dispose();
                Check(Event.current.type == type, "Non-pointer event preserved on exit");
            }

            GUIUtility.Offset = new Vector2(300, 400);
            Event.current = new Event { type = EventType.Repaint };
            overlap.BeginPlaybackPopupInput();
            Check(overlap.GetPlaybackPopupInputRect(ActorPopup).width == 0, "Undrawn list removed on repaint");
            GUIUtility.Offset = new Vector2(320, 450);
            using (overlap.EnterPlaybackPopup(RoomPopup, new Rect(10, 20, 80, 60))) { }
            var rootRect = overlap.GetPlaybackPopupInputRect(RoomPopup);
            Check(rootRect.x == 30 && rootRect.y == 70, "Embedded coordinates converted to root blocker coordinates");
            overlap._popupInput.Dispose();
            GUIUtility.Offset = new Vector2(300, 400);
            Event.current = new Event { type = EventType.MouseDown, mousePosition = new Vector2(40, 80) };
            overlap.BeginPlaybackPopupInput();
            Check(Event.current.type == EventType.Ignore, "Embedded list hit from screen/root coordinates");
            overlap._popupInput.Dispose();

            Event.current = new Event { type = EventType.MouseDown };
            try
            {
                using (var input = new MainGameBlankMapAdd.UI.PlaybackPopupInput(Event.current, RoomPopup))
                using (input.Enter(RoomPopup)) throw new InvalidOperationException();
            }
            catch (InvalidOperationException) { }
            Check(Event.current.type == EventType.Used, "Exception cannot leak the captured pointer event");
            Console.WriteLine($"PASS {checks} popup routing checks");
        }
    }
}

namespace UnityEngine
{
    public enum EventType { MouseDown, MouseUp, MouseDrag, ScrollWheel, Layout, Repaint, KeyDown, Ignore, Used }
    public sealed class Event
    {
        public static Event current;
        public EventType type;
        public Vector2 mousePosition;
        public void Use() { type = EventType.Used; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public static Rect zero => new Rect(0, 0, 0, 0);
        public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }
    public static class GUIUtility
    {
        public static Vector2 Offset;
        public static Vector2 GUIToScreenPoint(Vector2 p) => new Vector2(p.x + Offset.x, p.y + Offset.y);
    }
}
