// This fake records native-window API calls; it does NOT emulate Unity rendering.
namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x=x; this.y=y; this.width=width; this.height=height; }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r=r; this.g=g; this.b=b; this.a=a; }
    }
    public sealed class GUIContent
    {
        public string text;
        public GUIContent(string text) { this.text=text; }
        public static readonly GUIContent none = new GUIContent("");
    }
    public sealed class GUIStyle { public static readonly GUIStyle none = new GUIStyle(); }
    public static class GUI
    {
        public delegate void WindowFunction(int id);
        public static bool enabled = true;
        public static Color color = new Color(1,1,1,1);
        public static readonly List<int> Windows = new List<int>();
        public static readonly List<bool> RegistrationEnabled = new List<bool>();
        public static Rect LastBox;
        public static GUIContent LastContent;
        public static bool ThrowOnWindow, ThrowOnBox;
        public static WindowFunction LastCallback;
        public static Rect Window(int id, Rect rect, WindowFunction draw, GUIContent content, GUIStyle style)
        {
            if (ThrowOnWindow) throw new InvalidOperationException("window failure");
            Windows.Add(id);
            RegistrationEnabled.Add(enabled);
            LastCallback=draw; draw(id);
            return rect;
        }
        public static void BringWindowToFront(int id) { Windows.Remove(id); Windows.Add(id); }
        public static void Box(Rect rect, GUIContent content, GUIStyle style)
        {
            if(content==null||style==null)throw new NullReferenceException("native GUI.Box null argument");
            if (ThrowOnBox) throw new InvalidOperationException("paint failure");
            LastBox=rect; LastContent=content;
        }
    }
}
