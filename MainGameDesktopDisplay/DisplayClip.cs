using System;

namespace MainGameDesktopDisplay
{
    // Pixel-exact source bounds also define mesh fractions and mouse coordinates.
    internal readonly struct DisplayClip
    {
        internal readonly DesktopCapture.Bounds Bounds;
        internal readonly float Width, Height, CenterX, CenterY;
        internal DisplayClip(DesktopCapture.Bounds source, float left, float right, float top, float bottom)
        {
            int w = source.Width, h = source.Height;
            int l = Pixels(left, w, w - 1), r = Pixels(right, w, w - l - 1);
            int t = Pixels(top, h, h - 1), b = Pixels(bottom, h, h - t - 1);
            Bounds = new DesktopCapture.Bounds { Left = source.Left + l, Right = source.Right - r, Top = source.Top + t, Bottom = source.Bottom - b };
            Width = (float)Bounds.Width / w; Height = (float)Bounds.Height / h;
            CenterX = (l - r) / (2f * w); CenterY = (b - t) / (2f * h);
        }
        private static int Pixels(float percent, int size, int maximum)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent)) return 0;
            return (int)Math.Max(0, Math.Min(maximum, Math.Round(size * (double)percent / 100)));
        }
    }
}
