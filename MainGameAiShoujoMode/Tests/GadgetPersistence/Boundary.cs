using System;
using System.Collections.Generic;

namespace BepInEx { public class BaseUnityPlugin { } }
namespace BepInEx.Bootstrap
{
    public sealed class PluginInfo { public BepInEx.BaseUnityPlugin Instance; }
    public static class Chainloader { public static readonly Dictionary<string, PluginInfo> PluginInfos = new Dictionary<string, PluginInfo>(); }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float a,float b,float c) { x=a;y=b;z=c; }
        public static bool operator ==(Vector3 a,Vector3 b) => a.x == b.x;
        public static bool operator !=(Vector3 a,Vector3 b) => !(a==b);
        public override bool Equals(object o) => o is Vector3 v && this==v;
        public override int GetHashCode() => x.GetHashCode();
    }
    public class Transform { }
    public struct Quaternion { public float x,y,z,w; }
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
        public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), Options);
        public static T FromJson<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, Options);
    }
    public static class GUI { public static bool enabled = true; public static Skin skin = new Skin(); }
    public sealed class Skin { public object box; }
    public static class GUILayout
    {
        public static void BeginVertical(object style) { }
        public static void EndVertical() { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void Label(string text, params object[] options) { }
        public static bool Button(string text, params object[] options) => false;
        public static object ExpandWidth(bool expand) => null;
    }
}
namespace CanonPlugins.Shared
{
    public sealed class GadgetRoomPose
    {
        public UnityEngine.Vector3 position,scale;
        public UnityEngine.Quaternion rotation;
        public bool worldScale=true;
        public void Validate() { if (scale.x <= default(float)) throw new ArgumentException("invalid scale"); }
    }
    // The boundary supplies a known valid protocol record; the production capture/save path is linked unchanged.
    public sealed class GadgetRoomSnapshot
    {
        public bool visible;
        public object[] poses = Array.Empty<object>();
        private string _json;
        public static GadgetRoomSnapshot Decode(string json) => new GadgetRoomSnapshot { visible = true, _json = json };
        public string Encode() => _json;
    }
}
namespace MainGameAiShoujoMode { public sealed class HotelRoomGadget { public string guid, payload; } }
