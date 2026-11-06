using System;
using System.Reflection;
using MainGameCharacterMorphBridge;
using UnityEngine;

public class Makeup
{
    public Color eyeshadowColor, cheekColor, lipColor;
    public int eyeshadowId, cheekId, lipId;
    public Color[] paintColor = new Color[2];
    public int[] paintId = new int[2];
    public Vector4[] paintLayout = new Vector4[2];
}
public class Coordinate { public bool enableMakeup; public Makeup makeup = new Makeup(); }
public class Face { public Makeup baseMakeup = new Makeup(); public Color lipLineColor, moleColor; public int lipLineId, moleId; }
public class Body
{
    public Color[] paintColor = new Color[2];
    public int[] paintId = new int[2], paintLayoutId = new int[2];
    public Vector4[] paintLayout = new Vector4[2];
    public int sunburnId;
}
public class Custom { public Face face = new Face(); public Body body = new Body(); }
public class Status { public int coordinateType; }
public class ChaFileControl { public Custom custom = new Custom(); public Coordinate[] coordinate = { new Coordinate() }; public Status status = new Status(); }
public class ChaControl { public ChaFileControl chaFile = new ChaFileControl(); public Coordinate nowCoordinate = new Coordinate(); }

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float v) { r = g = b = a = v; }
        public static Color Lerp(Color f, Color t, float x) => new Color(f.r + (t.r - f.r) * x);
        public static bool operator ==(Color a, Color b) => a.Equals(b);
        public static bool operator !=(Color a, Color b) => !a.Equals(b);
        public override bool Equals(object o) => o is Color c && r == c.r && g == c.g && b == c.b && a == c.a;
        public override int GetHashCode() => r.GetHashCode();
    }
    public struct Vector4
    {
        public float x;
        public Vector4(float v) { x = v; }
        public static Vector4 Lerp(Vector4 f, Vector4 t, float x) => new Vector4(f.x + (t.x - f.x) * x);
        public static bool operator ==(Vector4 a, Vector4 b) => a.x == b.x;
        public static bool operator !=(Vector4 a, Vector4 b) => a.x != b.x;
        public override bool Equals(object o) => o is Vector4 v && x == v.x;
        public override int GetHashCode() => x.GetHashCode();
    }
    public static class Mathf { public static int Min(int a, int b) => Math.Min(a, b); }
}

namespace MainGameCharacterMorphBridge
{
    public sealed partial class Plugin
    {
        private float _transitionTimeRatio = -1;
        private static object GetMemberValue(object o, string n) => o?.GetType().GetField(n)?.GetValue(o);
        private static bool TryGetInt(object o, string n, out int v) { object x = GetMemberValue(o, n); v = x is int i ? i : 0; return x is int; }
        private static bool TrySetInt(object o, string n, int v) { var f = o?.GetType().GetField(n); f?.SetValue(o, v); return f != null; }
        private static Color[] ReadColors(object o, string[] names) => Array.ConvertAll(names, n => GetMemberValue(o, n) is Color c ? c : default);
        private static bool ApplyContainerColors(object o, string[] names, Color[] f, Color[] t, float x, bool force)
        {
            if (o == null || f == null || t == null) return false;
            foreach (var n in names) { int i = Array.IndexOf(names, n); o.GetType().GetField(n).SetValue(o, Color.Lerp(f[i], t[i], x)); }
            return true;
        }
        private void LogExecution(string s) { }
        public static void Main()
        {
            int checks = 0;
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
            var actor = new ChaControl();
            actor.chaFile.coordinate[0].enableMakeup = true;
            actor.chaFile.coordinate[0].makeup.lipId = 11;
            actor.nowCoordinate.enableMakeup = true;
            actor.nowCoordinate.makeup.lipId = 22;
            Check(MakeupSnapshotFrom(actor).Ids[2] == 22, "capture must use runtime coordinate copy");
            Check(MakeupSnapshotFrom(actor.chaFile, 0).Ids[2] == 11, "card capture must use stored coordinate");
            actor.nowCoordinate.enableMakeup = false;
            actor.chaFile.custom.face.baseMakeup.lipId = 33;
            Check(MakeupSnapshotFrom(actor).Ids[2] == 33, "base makeup when runtime override disabled");
            actor.nowCoordinate.enableMakeup = true;
            var card = new ChaFileControl();
            var makeup = card.custom.face.baseMakeup;
            makeup.eyeshadowId = 41; makeup.cheekId = 42; makeup.lipId = 43;
            makeup.lipColor = new Color(0.8f);
            makeup.paintId[0] = 44; makeup.paintLayout[0] = new Vector4(0.7f); makeup.paintColor[0] = new Color(0.6f);
            card.custom.body.paintId[1] = 51; card.custom.body.paintLayoutId[1] = 52;
            card.custom.body.paintLayout[1] = new Vector4(0.9f); card.custom.body.paintColor[1] = new Color(0.4f);
            var original = MakeupSnapshotFrom(actor);
            var destination = MakeupSnapshotFrom(card, 0);
            var plugin = new Plugin();
            void Apply(MakeupSnapshot f, MakeupSnapshot t, float blend) => plugin.ApplyMakeupBlend(actor, f, t, blend, true, out _, out _, out _, out _, out _, out _);
            Apply(original, destination, 1);
            Check(actor.nowCoordinate.makeup.lipId == 43 && actor.nowCoordinate.makeup.eyeshadowId == 41 && actor.nowCoordinate.makeup.cheekId == 42, "all makeup IDs applied to renderer input");
            Check(actor.chaFile.coordinate[0].makeup.lipId == 11, "stored wardrobe is preserved");
            Check(actor.nowCoordinate.makeup.lipColor == makeup.lipColor, "lip color");
            Check(actor.nowCoordinate.makeup.paintId[0] == 44 && actor.nowCoordinate.makeup.paintLayout[0].x == 0.7f && actor.nowCoordinate.makeup.paintColor[0] == makeup.paintColor[0], "face paint type color layout");
            Check(actor.chaFile.custom.body.paintId[1] == 51 && actor.chaFile.custom.body.paintLayoutId[1] == 52 && actor.chaFile.custom.body.paintLayout[1].x == 0.9f && actor.chaFile.custom.body.paintColor[1] == new Color(0.4f), "body paint type placement color layout");
            // Session restoration intentionally passes the same snapshot as both endpoints.
            Apply(original, original, 0);
            Check(actor.nowCoordinate.makeup.lipId == 22, "equal endpoint ID restoration");
            Check(actor.nowCoordinate.makeup.paintId[0] == 0 && actor.nowCoordinate.makeup.paintLayout[0].x == 0 && actor.nowCoordinate.makeup.paintColor[0] == default, "equal endpoint face paint restoration including removal");
            Check(actor.chaFile.custom.body.paintId[1] == 0 && actor.chaFile.custom.body.paintLayoutId[1] == 0 && actor.chaFile.custom.body.paintLayout[1].x == 0 && actor.chaFile.custom.body.paintColor[1] == default, "equal endpoint body paint restoration");
            Check(destination.BodyPaintIds[1] == 51, "snapshots do not alias live arrays");
            Console.WriteLine("PASS: " + checks + " makeup and paint checks");
        }
    }
}
