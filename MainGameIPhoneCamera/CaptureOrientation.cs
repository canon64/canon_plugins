using System;

namespace MainGameIPhoneCamera
{
    // Counter-clockwise quarter turns, applied to pixels, not metadata.
    internal static class CaptureOrientation
    {
        internal static int FromGravity(float rightUp, float imageUp)
            => Math.Abs(rightUp) > Math.Abs(imageUp) ? (rightUp > 0 ? 1 : 3) : (imageUp >= 0 ? 0 : 2);
        internal static int StableFromGravity(float rightUp, float imageUp, int current)
        {
            float magnitude = (float)Math.Sqrt(rightUp * rightUp + imageUp * imageUp);
            if (magnitude < .1f) return current;
            int next = FromGravity(rightUp, imageUp);
            float score = current == 0 ? imageUp : current == 1 ? rightUp : current == 2 ? -imageUp : -rightUp;
            return Math.Max(Math.Abs(rightUp), Math.Abs(imageUp)) - score < magnitude * .12f ? current : next;
        }
        internal static string Filter(int turns)
        {
            switch (turns & 3)
            {
                case 1: return "vflip,transpose=cclock";
                case 2: return "vflip,hflip,vflip";
                case 3: return "vflip,transpose=clock";
                default: return "vflip";
            }
        }
        internal static T[] Rotate<T>(T[] source, int width, int height, int turns)
        {
            if (source == null || source.Length != width * height) throw new ArgumentException("Invalid pixel dimensions");
            turns &= 3;
            if (turns == 0) return source;
            int outputWidth = (turns & 1) != 0 ? height : width;
            var result = new T[source.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int dx = turns == 1 ? height - 1 - y : turns == 2 ? width - 1 - x : y;
                int dy = turns == 1 ? x : turns == 2 ? height - 1 - y : width - 1 - x;
                result[dy * outputWidth + dx] = source[y * width + x];
            }
            return result;
        }
    }
}
