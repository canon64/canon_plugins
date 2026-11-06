using System;
using System.IO;
using UnityEngine;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        public void Capture(string path) => CapturePresetScreenshotWithoutUi(path);
        private static void Destroy(UnityEngine.Object value) { value.Destroyed = true; }
    }
}

internal static class Program
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void Main()
    {
        int passed = 0;
        string path = Path.Combine(AppContext.BaseDirectory, "capture.png");
        foreach (string scenario in new[] { "success", "render failure", "encode failure", "write failure" })
        {
            var target = new RenderTexture();
            var active = new RenderTexture();
            var camera = new Camera { targetTexture = target, aspect = 1.25f, cullingMask = -1 };
            Camera.main = camera;
            RenderTexture.active = active;
            RenderTexture.Last = null;
            Texture2D.Last = null;
            Texture2D.FailEncode = scenario == "encode failure";
            camera.OnRender = () =>
            {
                Check(camera.cullingMask == (-1 & ~(1 << 5) & ~(1 << 31)), "UI and gizmo layers excluded only from capture");
                Check(camera.targetTexture != target && camera.targetTexture.width == 1920, "separate render target");
                Check(camera.aspect == 1920f / 1080, "capture aspect");
                if (scenario == "render failure") throw new InvalidOperationException("render");
            };
            bool failed = false;
            try { new MainGirlHipHijack.Plugin().Capture(scenario == "write failure" ? AppContext.BaseDirectory : path); }
            catch { failed = true; }
            Check(failed == (scenario != "success"), scenario + " result");
            Check(camera.targetTexture == target && camera.aspect == 1.25f && camera.cullingMask == -1, scenario + " restores camera");
            Check(RenderTexture.active == active, scenario + " restores active target");
            Check(RenderTexture.Last.Released, scenario + " releases temporary target");
            Check(Texture2D.Last == null || Texture2D.Last.Destroyed, scenario + " destroys pixel buffer");
            if (!failed) Check(File.ReadAllBytes(path).Length == 3, "PNG bytes saved synchronously");
            Console.WriteLine("PASS " + scenario);
            passed++;
        }
        Camera.main = null;
        bool missingCamera = false;
        try { new MainGirlHipHijack.Plugin().Capture(path); }
        catch (InvalidOperationException) { missingCamera = true; }
        Check(missingCamera, "no camera fails before capture");
        File.Delete(path);
        Console.WriteLine("PASS no camera; total " + (passed + 1));
    }
}

// Deliberately no Canvas, GUI, or visibility API: the real capture source must
// compile and run without being able to hide screen UI or change input ownership.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
    }
    public class Camera : Object
    {
        public static Camera main;
        public RenderTexture targetTexture;
        public float aspect;
        public int cullingMask;
        public Action OnRender;
        public void Render() => OnRender();
    }
    public class RenderTexture : Object
    {
        public static RenderTexture active, Last;
        public int width, height;
        public bool Released;
        public static RenderTexture GetTemporary(int w, int h, int depth, RenderTextureFormat format)
            => Last = new RenderTexture { width = w, height = h };
        public static void ReleaseTemporary(RenderTexture value) { value.Released = true; }
    }
    public class Texture2D : Object
    {
        public static Texture2D Last;
        public static bool FailEncode;
        public Texture2D(int w, int h, TextureFormat format, bool mip) { Last = this; }
        public void ReadPixels(Rect rect, int x, int y) { }
        public void Apply() { }
        public byte[] EncodeToPNG()
        {
            if (FailEncode) throw new InvalidOperationException("encode");
            return new byte[] { 1, 2, 3 };
        }
    }
    public struct Rect { public Rect(int x, int y, int w, int h) { } }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
    public enum RenderTextureFormat { ARGB32 }
    public enum TextureFormat { RGB24 }
}
