using System;
using System.Linq;
using System.IO;

namespace MainGameBlankMapAdd
{
    internal static class MapWarpOrigin
    {
        internal static string PhotoPath(string directory, MapWarpPoint origin)
        {
            if (origin == null || !origin.IsOrigin || !Guid.TryParseExact(origin.Id, "N", out _))
                throw new InvalidDataException("元の場所の画像IDが不正です。");
            return Path.Combine(directory, origin.Id + ".png");
        }

        internal static void SavePhoto(string directory, MapWarpPoint origin, byte[] png)
        {
            string path = PhotoPath(directory, origin), temp = path + ".tmp";
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllBytes(temp, png);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static MapWarpPoint Find(MapWarpDocument document, string key) =>
            document?.Points.SingleOrDefault(p => p.MapKey == key && p.IsOrigin);

        internal static MapWarpDocument CreateIfMissing(MapWarpDocument source, string key, Func<MapWarpPoint> capture)
        {
            if (Find(source, key) != null) return null;
            MapWarpPoint point = capture();
            point.Id = Guid.NewGuid().ToString("N"); point.MapKey = key;
            point.Name = "元の場所"; point.IsOrigin = true;
            point.Validate();
            var document = source.Clone();
            document.Points.Insert(0, point.Clone());
            document.Validate();
            return document;
        }
    }
}
