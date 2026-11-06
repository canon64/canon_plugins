using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MainGameIPhoneCamera;

internal static partial class Program
{
    private static async Task OrientationTests(string ffmpeg, string ffprobe, string root)
    {
        Check(CaptureOrientation.FromGravity(0, 1) == 0 && CaptureOrientation.FromGravity(1, 0) == 1
            && CaptureOrientation.FromGravity(0, -1) == 2 && CaptureOrientation.FromGravity(-1, 0) == 3, "four physical phone orientations");
        Check(CaptureOrientation.StableFromGravity(.72f,.70f,0)==0, "display orientation does not chatter at a diagonal");
        Check(CaptureOrientation.StableFromGravity(.9f,.3f,0)==1 && CaptureOrientation.StableFromGravity(-1,0,0)==3, "display switches to either horizontal direction");
        Check(CaptureOrientation.StableFromGravity(.01f,.01f,2)==2, "face-up phone preserves its last readable direction");
        int[][] expected = { new[] { 0, 1, 2, 3, 4, 5 }, new[] { 4, 2, 0, 5, 3, 1 }, new[] { 5, 4, 3, 2, 1, 0 }, new[] { 1, 3, 5, 0, 2, 4 } };
        for (int q = 0; q < 4; q++)
            Check(CaptureOrientation.Rotate(new[] { 0, 1, 2, 3, 4, 5 }, 2, 3, q).SequenceEqual(expected[q]), "photo pixel rotation preserves all positions for quarter turn " + q);
        byte[][] colors = { new byte[] { 240, 10, 10 }, new byte[] { 10, 240, 10 }, new byte[] { 10, 10, 240 }, new byte[] { 240, 240, 10 } };
        var pattern = new int[64 * 128]; var rgb = new byte[pattern.Length * 3];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 64; x++)
        {
            int code = (x < 32 ? 0 : 1) + (y < 64 ? 0 : 2); pattern[y * 64 + x] = code;
            for (int c = 0; c < 3; c++) rgb[(y * 64 + x) * 3 + c] = colors[code][c];
        }
        for (int turns = 0; turns < 4; turns++)
        {
            string path = Path.Combine(root, "orientation-" + turns + "-" + Guid.NewGuid().ToString("N") + ".mp4");
            int currentPose = turns;
            var writer = new MovieWriter(ffmpeg, path, 64, 128, 10, 48000, currentPose);
            Check(writer.FrameBytes(rgb, 0), "first orientation frame");
            writer.AudioSamples(Enumerable.Repeat(.15f, 48000).ToArray(), 2);
            currentPose = (turns + 1) & 3; // User turns the handset after recording begins.
            Check(writer.FrameBytes(rgb, 1), "frame after handset pose change");
            await Task.Delay(250);
            await writer.FinishAsync();
            using var info = JsonDocument.Parse(await Run(ffprobe, "-v error -show_streams -of json " + MovieWriter.Quote(path)));
            var streams = info.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            var video = streams.Single(s => s.GetProperty("codec_type").GetString() == "video");
            int width = (turns & 1) == 0 ? 64 : 128, height = (turns & 1) == 0 ? 128 : 64;
            Check(video.GetProperty("width").GetInt32() == width && video.GetProperty("height").GetInt32() == height, "recorded size stays at start orientation " + turns);
            Check(streams.Any(s => s.GetProperty("codec_type").GetString() == "audio"), "rotated recording retains audio " + turns);
            string decodedPath = path + ".rgb";
            await Run(ffmpeg, "-v error -y -i " + MovieWriter.Quote(path) + " -frames:v 2 -f rawvideo -pix_fmt rgb24 " + MovieWriter.Quote(decodedPath));
            byte[] decoded = File.ReadAllBytes(decodedPath);
            var rotated = CaptureOrientation.Rotate(pattern, 64, 128, turns);
            Check(decoded.Length == width * height * 3 * 2, "two real decoded frames");
            for (int frame = 0; frame < 2; frame++)
            for (int iy = 0; iy < 2; iy++) for (int ix = 0; ix < 2; ix++)
            {
                int x = width * (ix == 0 ? 1 : 3) / 4, y = height * (iy == 0 ? 1 : 3) / 4;
                byte[] color = colors[rotated[(height - 1 - y) * width + x]];
                int offset = (frame * width * height + y * width + x) * 3;
                Check(Enumerable.Range(0, 3).All(c => Math.Abs(decoded[offset + c] - color[c]) < 45), "correct pixels, no mirror/stretch, orientation " + turns + " frame " + frame + " corner " + ix + iy);
            }
        }
    }
}
