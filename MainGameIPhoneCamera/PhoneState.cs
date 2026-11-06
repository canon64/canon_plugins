using System;

namespace MainGameIPhoneCamera
{
    internal enum PhonePage { Locked, Home, Camera, Gallery, Viewer, Wallpaper, Settings, Extension, Off }
    internal static class PhoneGrid
    {
        internal static int Move(int current, int count, int x, int y, int columns)
        {
            if (count <= 0) return 0;
            if (columns == 5)
            {
                // Nine entries per page: the next page starts a fresh five-column row.
                int page = current / 9, local = current % 9;
                int row = local / columns, col = local % columns;
                if (x != 0)
                {
                    int next = local + x;
                    return next >= 0 && next < 9 && next / columns == row && page * 9 + next < count ? page * 9 + next : current;
                }
                int nextRow = row - y;
                if (nextRow < 0) { page--; nextRow = 1; }
                if (nextRow > 1) { page++; nextRow = 0; }
                int nextIndex = page * 9 + nextRow * columns + col;
                return page >= 0 && nextRow * columns + col < 9 && nextIndex < count ? nextIndex : current;
            }
            int target = x != 0 ? current + x : current - y * columns;
            if (target < 0 || target >= count) return current;
            if (x != 0 && target / columns != current / columns) return current;
            return target;
        }
    }
    internal sealed class PhoneState
    {
        internal PhonePage Page { get; private set; } = PhonePage.Locked;
        internal bool Held { get; private set; }
        internal bool MediaActive { get; private set; }
        internal DateTime? Deadline { get; private set; }
        private DateTime? _released;
        internal void Show(PhonePage page, DateTime now)
        {
            Page = page;
            if (!Held && !MediaActive)
            {
                if (page == PhonePage.Off) Deadline = null;
                else if (page == PhonePage.Locked) { _released = now.AddSeconds(-60); Deadline = now.AddSeconds(30); }
                else { _released = now; Deadline = now.AddSeconds(60); }
            }
        }
        internal void Grab(DateTime now)
        {
            if (!MediaActive) Expire(now);
            Held = true; Deadline = null; _released = null;
            if (Page == PhonePage.Off) Page = PhonePage.Locked;
        }
        internal void Release(DateTime now)
        {
            if (!Held && Deadline.HasValue) return;
            Held = false;
            if (MediaActive || Page == PhonePage.Off) { Deadline = null; return; }
            _released = Page == PhonePage.Locked ? now.AddSeconds(-60) : now;
            Deadline = now.AddSeconds(Page == PhonePage.Locked ? 30 : 60);
        }
        internal void SetMediaActive(bool active, DateTime now)
        {
            if (MediaActive == active) return;
            MediaActive = active;
            if (active) { Deadline = null; _released = null; }
            else if (!Held) { Deadline = null; _released = null; Release(now); }
        }
        internal bool Expire(DateTime now)
        {
            if (Held || MediaActive || !_released.HasValue || !Deadline.HasValue || now < Deadline.Value) return false;
            if (now >= _released.Value.AddSeconds(90)) { Page = PhonePage.Off; Deadline = null; }
            else { Page = PhonePage.Locked; Deadline = _released.Value.AddSeconds(90); }
            return true;
        }
    }

    internal readonly struct CropRect
    {
        internal readonly float X, Y, Width, Height;
        internal CropRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
    }
    internal sealed class WallpaperDraft
    {
        internal float X = .5f, Y = .5f, Zoom = 1f;
        internal int Edit; // 0=buttons, 1=position, 2=zoom
        internal bool ForLock;
        internal CropRect Crop(int width, int height)
        {
            double aspect = (double)Math.Max(1, width) / Math.Max(1, height), target = 1125.0 / 2436;
            float w = (float)Math.Min(1, target / aspect) / Zoom;
            float h = (float)Math.Min(1, aspect / target) / Zoom;
            return new CropRect((1 - w) * X, (1 - h) * Y, w, h);
        }
        internal void Move(int dx, int dy)
        {
            if (Edit == 1) { X = Clamp(X + dx * .025f, 0, 1); Y = Clamp(Y + dy * .025f, 0, 1); }
            if (Edit == 2) Zoom = Clamp(Zoom + dy * .25f, 1, 8);
        }
        internal void Reset() { X = Y = .5f; Zoom = 1; Edit = 0; }
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
