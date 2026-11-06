using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MainGameDesktopDisplay
{
    internal sealed class DisplayPreset
    {
        internal string Name, Monitor;
        internal readonly float[] Edges = new float[4];
        internal float Width;
        internal static string Encode(List<DisplayPreset> presets)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(presets.Count);
                    foreach (var preset in presets)
                    {
                        writer.Write(preset.Name); writer.Write(preset.Monitor);
                        for (int i = 0; i < 4; i++) writer.Write(preset.Edges[i]);
                        writer.Write(preset.Width);
                    }
                }
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        internal static List<DisplayPreset> Decode(string data)
        {
            var result = new List<DisplayPreset>();
            if (string.IsNullOrEmpty(data)) return result;
            using (var stream = new MemoryStream(Convert.FromBase64String(data)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != 1) throw new FormatException("プリセット形式が未対応です。");
                int count = reader.ReadInt32();
                if (count < 0 || count > stream.Length / 24) throw new FormatException("プリセット件数が不正です。");
                for (int n = 0; n < count; n++)
                {
                    var preset = new DisplayPreset { Name = reader.ReadString(), Monitor = reader.ReadString() };
                    if (string.IsNullOrWhiteSpace(preset.Name) || string.IsNullOrEmpty(preset.Monitor)) throw new FormatException("プリセット名が不正です。");
                    for (int i = 0; i < 4; i++)
                    {
                        float value = reader.ReadSingle();
                        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 100) throw new FormatException("切り取り値が不正です。");
                        preset.Edges[i] = value;
                    }
                    preset.Width = reader.ReadSingle();
                    if (float.IsNaN(preset.Width) || preset.Width < .1f || preset.Width > 20) throw new FormatException("画面幅が不正です。");
                    result.Add(preset);
                }
                if (stream.Position != stream.Length) throw new FormatException("プリセットの末尾が不正です。");
            }
            return result;
        }
    }
}
