using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MainGameDesktopDisplay
{
    // Windows-only pixel producer. No Unity, timers, workers, files or desktop input.
    internal sealed class DesktopCapture : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Bounds { internal int Left, Top, Right, Bottom; internal int Width => Right - Left; internal int Height => Bottom - Top; }
        internal sealed class Monitor
        {
            internal string Device;
            internal Bounds Rect;
            internal bool Primary;
            public override string ToString() => Device + "  " + Rect.Width + "×" + Rect.Height + (Primary ? "（メイン）" : "");
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MonitorInfo
        {
            internal int Size;
            internal Bounds Monitor, Work;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device;
        }
        [StructLayout(LayoutKind.Sequential)] struct BitmapInfo
        {
            internal uint Size;
            internal int Width, Height;
            internal ushort Planes, BitCount;
            internal uint Compression, SizeImage;
            internal int XPels, YPels;
            internal uint ClrUsed, ClrImportant, Colors;
        }
        [StructLayout(LayoutKind.Sequential)] struct Point { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct CursorInfo { internal int Size, Flags; internal IntPtr Cursor; internal Point Position; }
        [StructLayout(LayoutKind.Sequential)] struct IconInfo { internal int Icon; internal uint HotX, HotY; internal IntPtr Mask, Color; }
        delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Bounds rect, IntPtr data);
        private IntPtr _desktop, _memory, _bitmap, _oldBitmap, _pixels;
        private readonly Bounds _source;
        internal Bounds Source => _source;
        internal int Width { get; }
        internal int Height { get; }
        internal int ByteCount => checked(Width * Height * 4);
        internal IntPtr Pixels => _pixels;

        internal static Monitor[] Enumerate()
        {
            var list = new List<Monitor>();
            Exception error = null;
            MonitorCallback callback = (IntPtr monitor, IntPtr dc, ref Bounds rect, IntPtr data) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfo(monitor, ref info)) { error = Failure("モニター情報の取得"); return false; }
                list.Add(new Monitor { Device = info.Device, Rect = info.Monitor, Primary = (info.Flags & 1) != 0 });
                return true;
            };
            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw Failure("モニター一覧の取得");
            if (error != null) throw error;
            list.Sort((a, b) => a.Primary == b.Primary ? string.CompareOrdinal(a.Device, b.Device) : (a.Primary ? -1 : 1));
            return list.ToArray();
        }

        internal DesktopCapture(Bounds source, int maximumWidth)
        {
            if (source.Width <= 0 || source.Height <= 0 || maximumWidth < 16) throw new ArgumentOutOfRangeException(nameof(source));
            _source = source;
            // Bound both axes, including portrait monitors; never upscale the desktop.
            double scale = Math.Min(1d, (double)maximumWidth / Math.Max(source.Width, source.Height));
            Width = Math.Max(1, (int)Math.Round(source.Width * scale));
            Height = Math.Max(1, (int)Math.Round(source.Height * scale));
            try
            {
                _desktop = GetDC(IntPtr.Zero);
                if (_desktop == IntPtr.Zero) throw Failure("デスクトップDCの取得");
                _memory = CreateCompatibleDC(_desktop);
                if (_memory == IntPtr.Zero) throw Failure("キャプチャDCの作成");
                // Positive height = bottom-up BGRA, matching Unity texture row order.
                var info = new BitmapInfo { Size = 40, Width = Width, Height = Height, Planes = 1, BitCount = 32 };
                _bitmap = CreateDIBSection(_desktop, ref info, 0, out _pixels, IntPtr.Zero, 0);
                if (_bitmap == IntPtr.Zero || _pixels == IntPtr.Zero) throw Failure("キャプチャ画像の作成");
                _oldBitmap = SelectObject(_memory, _bitmap);
                if (_oldBitmap == IntPtr.Zero || _oldBitmap == new IntPtr(-1)) throw Failure("キャプチャ画像の選択");
                SetStretchBltMode(_memory, 3); // COLORONCOLOR: bounded work, no PNG/JPEG encoding.
            }
            catch { Dispose(); throw; }
        }

        internal void Capture(bool includeCursor)
        {
            if (_memory == IntPtr.Zero) throw new ObjectDisposedException(nameof(DesktopCapture));
            if (!StretchBlt(_memory, 0, 0, Width, Height, _desktop, _source.Left, _source.Top, _source.Width, _source.Height, 0x40CC0020))
                throw Failure("デスクトップ画像の取得"); // SRCCOPY | CAPTUREBLT
            if (includeCursor) DrawCursor();
            if (!GdiFlush()) throw Failure("キャプチャ画像の確定");
        }

        private void DrawCursor()
        {
            var cursor = new CursorInfo { Size = Marshal.SizeOf(typeof(CursorInfo)) };
            if (!GetCursorInfo(ref cursor) || (cursor.Flags & 1) == 0 || cursor.Cursor == IntPtr.Zero) return;
            if (cursor.Position.X < _source.Left || cursor.Position.X >= _source.Right || cursor.Position.Y < _source.Top || cursor.Position.Y >= _source.Bottom) return;
            if (!GetIconInfo(cursor.Cursor, out var icon)) return;
            try
            {
                double sx = (double)Width / _source.Width, sy = (double)Height / _source.Height;
                int x = (int)Math.Round((cursor.Position.X - _source.Left - icon.HotX) * sx);
                int y = (int)Math.Round((cursor.Position.Y - _source.Top - icon.HotY) * sy);
                DrawIconEx(_memory, x, y, cursor.Cursor, Math.Max(1, (int)(GetSystemMetrics(13) * sx)), Math.Max(1, (int)(GetSystemMetrics(14) * sy)), 0, IntPtr.Zero, 3);
            }
            finally { if (icon.Mask != IntPtr.Zero) DeleteObject(icon.Mask); if (icon.Color != IntPtr.Zero) DeleteObject(icon.Color); }
        }

        public void Dispose()
        {
            if (_memory != IntPtr.Zero && _oldBitmap != IntPtr.Zero && _oldBitmap != new IntPtr(-1)) SelectObject(_memory, _oldBitmap);
            if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
            if (_memory != IntPtr.Zero) DeleteDC(_memory);
            if (_desktop != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _desktop);
            _desktop = _memory = _bitmap = _oldBitmap = _pixels = IntPtr.Zero;
        }
        private static Exception Failure(string operation) => new Win32Exception(Marshal.GetLastWin32Error(), operation + "に失敗しました");
        [DllImport("user32.dll", SetLastError = true)] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, int step, IntPtr brush, int flags);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll", SetLastError = true)] static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
        [DllImport("gdi32.dll", SetLastError = true)] static extern bool GdiFlush();
    }
}
