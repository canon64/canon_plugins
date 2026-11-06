using System;

namespace MainGameIPhoneCamera
{
    internal readonly struct PhotoSize
    {
        // Raw iPhone X sensor buffer: 1125 x 2436, even dimensions. Output is rotated at capture.
        internal readonly int Width, Height;
        private PhotoSize(int width, int height) { Width = width; Height = height; }
        internal static PhotoSize FromHeight(int requestedHeight)
        {
            int height = Math.Max(480, Math.Min(3840, requestedHeight)) & ~1;
            int width = ((int)Math.Round(height * (1125.0 / 2436.0))) & ~1;
            return new PhotoSize(width, height);
        }
    }
}
