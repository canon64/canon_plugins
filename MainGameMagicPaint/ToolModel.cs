using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameMagicPaint
{
    // Dimensioned rotational profiles, recessed curved grips, separate metal collars and felt/rubber ends.
    internal sealed class ToolModel : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly Transform Visual, Tip, Eraser;
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly Material _color, _felt, _white, _rubber, _metal, _dark;
        internal ToolModel(Transform parent, PaintTool tool)
        {
            Root = new GameObject("MagicPaint handheld anchor"); Root.transform.SetParent(parent, false);
            Visual = new GameObject("Appearance").transform; Visual.SetParent(Root.transform, false);
            try
            {
                _white = Mat("Warm white enamel", new Color(.83f, .84f, .85f), .12f, .48f);
                _rubber = Mat("Recessed rubber grip", new Color(.045f, .049f, .055f), 0, .12f, true);
                _metal = Mat("Brushed metal collars", new Color(.62f, .65f, .68f), .86f, .65f, true);
                _dark = Mat("Dark nozzle housing", new Color(.055f, .061f, .069f), .3f, .42f);
                _color = Mat("Selected ink indicator", Color.blue, .15f, .55f);
                _felt = Mat("Selected felt nib", Color.blue, 0, .08f, true);
                if (tool == PaintTool.Marker) BuildMarker(); else BuildSpray();
                Tip = new GameObject("Working tip").transform; Tip.SetParent(Visual, false);
                Tip.localPosition = tool == PaintTool.Marker ? new Vector3(0, 0, .079f) : new Vector3(0, .063f, .012f);
                Eraser = new GameObject("Rear eraser contact").transform; Eraser.SetParent(Visual, false); Eraser.localPosition = new Vector3(0, 0, -.078f);
            }
            catch { Dispose(); throw; }
        }
        Material Mat(string name, Color color, float metallic, float gloss, bool grain = false)
        {
            Shader shader = Shader.Find("Standard"); if (shader == null) throw new InvalidOperationException("Standard shaderがありません");
            var m = new Material(shader) { name = name, color = color }; _owned.Add(m);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", gloss);
            if (grain)
            {
                var tex = new Texture2D(128, 128, TextureFormat.RGB24, false) { name = name + " grain", wrapMode = TextureWrapMode.Repeat }; _owned.Add(tex);
                var rng = new System.Random(47); var pixels = new Color32[128 * 128];
                for (int i = 0; i < pixels.Length; i++) { byte value = (byte)(210 + rng.Next(46)); pixels[i] = new Color32(value, value, value, 255); }
                tex.SetPixels32(pixels); tex.Apply(); m.mainTexture = tex; m.mainTextureScale = new Vector2(4, 12);
            }
            return m;
        }
        void BuildMarker()
        {
            // Rotational mesh axes are Y; rotate the complete marker assembly to Z.
            var assembly = new GameObject("Marker 15.8cm").transform; assembly.SetParent(Visual, false); assembly.localRotation = Quaternion.Euler(90, 0, 0);
            Lathe(assembly, "Rounded white barrel", _white, new[] { P(0, -.056f), P(.0098f, -.056f), P(.0114f, -.053f), P(.0116f, -.049f), P(.0116f, .041f), P(.0108f, .045f), P(0, .045f) });
            Lathe(assembly, "Rear rubber collar", _dark, new[] { P(0, -.064f), P(.0116f, -.064f), P(.0124f, -.062f), P(.0124f, -.054f), P(.0117f, -.051f), P(0, -.051f) });
            Lathe(assembly, "White rear eraser", Mat("White eraser", new Color(.88f, .87f, .83f), 0, .05f, true), new[] { P(0, -.078f), P(.0097f, -.078f), P(.0117f, -.075f), P(.0117f, -.064f), P(.011f, -.062f), P(0, -.062f) });
            Lathe(assembly, "Front metal ring", _metal, new[] { P(0, .040f), P(.0118f, .040f), P(.0118f, .043f), P(.0109f, .044f), P(0, .044f) });
            Lathe(assembly, "Blue ink band", _color, new[] { P(0, .044f), P(.011f, .044f), P(.011f, .047f), P(0, .047f) });
            Lathe(assembly, "Tapered black nib housing", _dark, new[] { P(0, .047f), P(.0108f, .047f), P(.0107f, .051f), P(.0094f, .056f), P(.0064f, .063f), P(.006f, .067f), P(0, .067f) });
            Chisel(assembly);
            for (int i = 0; i < 4; i++)
            {
                Grip(assembly, "Recess silver border " + i, _metal, .01178f, -.002f, .082f, .74f, i * Mathf.PI / 2);
                Grip(assembly, "Curved black grip " + i, _rubber, .01188f, -.002f, .078f, .63f, i * Mathf.PI / 2);
            }
            for (int i = 0; i < 3; i++) Lathe(assembly, "Nozzle seam " + i, _rubber, new[] { P(.0102f - .0005f * i, .050f + .002f * i), P(.0104f - .0005f * i, .0502f + .002f * i) });
        }
        void BuildSpray()
        {
            Lathe(Visual, "White spray can 14cm", _white, new[] { P(0, -.071f), P(.0205f, -.071f), P(.023f, -.068f), P(.023f, -.064f), P(.023f, .030f), P(.022f, .036f), P(0, .036f) });
            Lathe(Visual, "Rolled metal base", _metal, new[] { P(0, -.0715f), P(.022f, -.0715f), P(.024f, -.070f), P(.024f, -.065f), P(.0233f, -.063f), P(0, -.063f) });
            Lathe(Visual, "Metal shoulder", _metal, new[] { P(0, .032f), P(.0238f, .032f), P(.0245f, .034f), P(.0245f, .036f), P(.022f, .037f), P(.021f, .043f), P(.018f, .050f), P(.012f, .054f), P(.010f, .054f), P(0, .054f) });
            Lathe(Visual, "Nozzle collar", _metal, new[] { P(0, .052f), P(.0115f, .052f), P(.0125f, .054f), P(.0125f, .056f), P(0, .056f) });
            Lathe(Visual, "Selected-color nozzle", _color, new[] { P(0, .055f), P(.009f, .055f), P(.0098f, .057f), P(.0098f, .069f), P(.0088f, .070f), P(0, .070f) });
            var nozzle = new GameObject("Front-facing nozzle bore").transform; nozzle.SetParent(Visual, false); nozzle.localPosition = new Vector3(0, .063f, .009f); nozzle.localRotation = Quaternion.Euler(90, 0, 0);
            Lathe(nozzle, "Metal nozzle lip", _metal, new[] { P(.0023f, 0), P(.0034f, 0), P(.0034f, .0028f), P(.0023f, .0028f), P(.0023f, 0) });
            Lathe(nozzle, "Black spray aperture", _dark, new[] { P(0, .0024f), P(.00225f, .0024f), P(.00225f, .0027f), P(0, .0027f) });
            for (int i = 0; i < 3; i++)
            {
                Grip(Visual, "Metal grip frame " + i, _metal, .02315f, -.014f, .079f, 1.23f, i * Mathf.PI * 2 / 3);
                Grip(Visual, "Recessed can grip " + i, _rubber, .02328f, -.014f, .075f, 1.11f, i * Mathf.PI * 2 / 3);
            }
        }
        static Vector2 P(float radius, float height) => new Vector2(radius, height);
        void Chisel(Transform parent)
        {
            var ring = new[] { new Vector2(-.0034f,-.0022f), new Vector2(.0034f,-.0022f), new Vector2(.004f,-.0016f), new Vector2(.004f,.0016f), new Vector2(.0034f,.0022f), new Vector2(-.0034f,.0022f), new Vector2(-.004f,.0016f), new Vector2(-.004f,-.0016f) };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int row = 0; row < 2; row++) for (int i = 0; i < ring.Length; i++)
            { var p = ring[i]; v.Add(new Vector3(p.x, row == 0 ? .066f : .078f + p.x * .25f, p.y)); uv.Add(new Vector2((float)i / ring.Length, row)); }
            for (int i = 0; i < 8; i++) { int n=(i+1)%8; t.AddRange(new[]{i,i+8,n,n,i+8,n+8}); }
            // End cap points toward +Y; the slanted broad end is the actual felt contact tip.
            for (int i=1;i<7;i++) t.AddRange(new[]{8,8+i+1,8+i});
            MeshPart(parent,"Broad chisel felt tip",_felt,v,uv,t);
        }
        void Lathe(Transform parent, string name, Material material, Vector2[] profile, float zScale = 1)
        {
            const int steps = 64; var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++) for (int j = 0; j <= steps; j++)
            { float angle = j * Mathf.PI * 2 / steps; vertices.Add(new Vector3(Mathf.Sin(angle) * profile[row].x, profile[row].y, Mathf.Cos(angle) * profile[row].x * zScale)); uv.Add(new Vector2((float)j / steps, (float)row / (profile.Length - 1))); }
            for (int row = 0; row < profile.Length - 1; row++) for (int j = 0; j < steps; j++)
            { int a = row * (steps + 1) + j, b = a + steps + 1; triangles.Add(a); triangles.Add(a + 1); triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b); }
            MeshPart(parent, name, material, vertices, uv, triangles);
        }
        void Grip(Transform parent, string name, Material material, float radius, float yCenter, float length, float angularWidth, float angle)
        {
            const int rows = 30, cols = 18; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int row = 0; row <= rows; row++)
            {
                float y = (float)row / rows; float cap = Mathf.Clamp01(Mathf.Min(y, 1 - y) * 8); float width = Mathf.Sqrt(Mathf.Max(.0001f, 1 - (1 - cap) * (1 - cap))) * angularWidth;
                for (int j = 0; j <= cols; j++) { float u = (float)j / cols, a = angle + (u - .5f) * width; v.Add(new Vector3(Mathf.Sin(a) * radius, yCenter + (y - .5f) * length, Mathf.Cos(a) * radius)); uv.Add(new Vector2(u, y)); }
            }
            for (int r = 0; r < rows; r++) for (int j = 0; j < cols; j++) { int a = r * (cols + 1) + j, b = a + cols + 1; t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b); }
            MeshPart(parent, name, material, v, uv, t);
        }
        void MeshPart(Transform parent, string name, Material material, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var mesh = new Mesh { name = name }; _owned.Add(mesh); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; var r = part.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        }
        internal void SetColor(Color color) { _color.color = color; _felt.color = color; }
        public void Dispose()
        {
            if (Root != null) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); }
            foreach (var resource in _owned) if (resource != null) UnityEngine.Object.Destroy(resource); _owned.Clear();
        }
    }
}
