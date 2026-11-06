using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MainGameDesktopDisplay;

static class NativeProgram
{
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static int Main()
    {
        try
        {
            Console.WriteLine("Native desktop capture smoke test: no image files, no desktop interaction.");
            var monitors = DesktopCapture.Enumerate();
            Check(monitors.Length > 0, "monitor enumeration");
            foreach (var monitor in monitors)
            {
                using (var capture = new DesktopCapture(monitor.Rect, 1280))
                {
                    Check(capture.Width <= 1280 && capture.Height <= 1280, "bounded capture: " + monitor);
                    capture.Capture(false);
                    byte[] pixels = new byte[capture.ByteCount];
                    Marshal.Copy(capture.Pixels, pixels, 0, pixels.Length);
                    bool different = false;
                    for (int i = 4; i < pixels.Length; i += 4)
                        if (pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] || pixels[i + 2] != pixels[2]) { different = true; break; }
                    Check(different, "real desktop has varying RGB pixels: " + monitor.Device);
                    capture.Capture(true);
                    Check(capture.Pixels != IntPtr.Zero, "cursor-inclusive capture");
                }
            }
            var rect = monitors[0].Rect;
            var clipped = new DisplayClip(rect, 15, 25, 20, 30);
            using (var capture = new DesktopCapture(clipped.Bounds, Math.Max(rect.Width, rect.Height)))
            {
                capture.Capture(false);
                Check(capture.Width == clipped.Bounds.Width && capture.Height == clipped.Bounds.Height && capture.ByteCount == capture.Width * capture.Height * 4,
                    "native four-edge crop allocates and transfers only retained pixels");
            }
            var minimum = new DisplayClip(rect, 100, 100, 100, 100);
            using (var capture = new DesktopCapture(minimum.Bounds, 1280))
            {
                capture.Capture(false);
                Check(capture.Width == 1 && capture.Height == 1 && capture.ByteCount == 4, "native minimum crop captures one pixel");
            }
            // Small 1:1 crop permits independent GetPixel comparison of all color channels and row order.
            rect.Right = rect.Left + 256; rect.Bottom = rect.Top + 256;
            using (var capture = new DesktopCapture(rect, 256))
            {
                capture.Capture(false);
                IntPtr dc = GetDC(IntPtr.Zero);
                int matches = 0, count = 0;
                try
                {
                    for (int y = 8; y < 256; y += 31)
                        for (int x = 8; x < 256; x += 31)
                        {
                            uint color = GetPixel(dc, rect.Left + x, rect.Top + y);
                            int offset = ((capture.Height - 1 - y) * capture.Width + x) * 4;
                            bool equal = Marshal.ReadByte(capture.Pixels, offset) == ((color >> 16) & 255) &&
                                Marshal.ReadByte(capture.Pixels, offset + 1) == ((color >> 8) & 255) &&
                                Marshal.ReadByte(capture.Pixels, offset + 2) == (color & 255);
                            if (equal) matches++; count++;
                        }
                }
                finally { ReleaseDC(IntPtr.Zero, dc); }
                Check(matches >= count * .85, "BGRA channel order and bottom-up rows agree with independent GetPixel: " + matches + "/" + count);
            }
            // Warm up P/Invoke entry points before measuring native handle ownership.
            using (var warm = new DesktopCapture(rect, 256)) warm.Capture(true);
            uint before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                var capture = new DesktopCapture(rect, 256);
                capture.Capture(true); capture.Dispose(); capture.Dispose();
                try { capture.Capture(false); throw new Exception("disposed capture was accepted"); }
                catch (ObjectDisposedException) { }
            }
            uint after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            Check(after <= before + 1, "100 captures/disposals: GDI handles " + before + " -> " + after + ", " + watch.ElapsedMilliseconds + " ms");
            using (var portrait = new DesktopCapture(new DesktopCapture.Bounds { Right = 100, Bottom = 300 }, 150))
                Check(portrait.Width == 50 && portrait.Height == 150, "portrait aspect ratio and long-edge limit");
            Console.WriteLine("All native tests passed. Unity/VR rendering remains unverified.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint flags);
}
