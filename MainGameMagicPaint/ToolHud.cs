using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MainGameMagicPaint
{
    internal sealed class ToolHud : IDisposable
    {
        readonly GameObject _root, _panel, _gauge, _menu, _guide;
        readonly Font _font;
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly TextMesh _status, _label, _left, _right;
        readonly Transform _fill;
        readonly Material _ink, _fillMaterial;
        readonly Mesh _guideMesh;
        readonly Vector3[] _projectedVertices = new Vector3[54];
        readonly bool[] _projectedValid = new bool[25];
        readonly List<int> _projectedTriangles = new List<int>(300);
        internal ToolHud(Transform parent)
        {
            _root = new GameObject("MagicPaint handheld HUD"); _root.transform.SetParent(parent, false);
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 36); _owned.Add(_font);
            _panel = new GameObject("Status panel"); _panel.transform.SetParent(_root.transform, false);
            var bg = Mat(new Color(.025f, .035f, .05f));
            Quad(_panel.transform, "Status background", bg, new Vector3(0, 0, .001f), new Vector2(.20f, .039f));
            _panel.transform.localPosition = new Vector3(0, -.085f, 0);
            _status = Text(_panel.transform, new Vector3(-.079f, 0, 0), .0024f, "");
            _gauge = new GameObject("Adjustment gauge"); _gauge.transform.SetParent(_root.transform, false); _gauge.transform.localPosition = new Vector3(0, .034f, 0);
            Quad(_gauge.transform, "Gauge background", bg, new Vector3(0, 0, .001f), new Vector2(.20f, .18f));
            _label = Text(_gauge.transform, new Vector3(-.093f, .079f, 0), .0028f, ""); _label.anchor = TextAnchor.UpperLeft;
            Quad(_gauge.transform, "Gauge track", Mat(new Color(.16f, .20f, .26f)), new Vector3(0, -.078f, 0), new Vector2(.15f, .006f));
            _fillMaterial = Mat(new Color(.25f, .9f, .75f));
            _fill = Quad(_gauge.transform, "Gauge fill", _fillMaterial, new Vector3(-.075f, -.078f, -.0001f), new Vector2(.075f, .006f));
            _menu = new GameObject("Tool selection"); _menu.transform.SetParent(_root.transform, false); _menu.transform.localPosition = new Vector3(0, .15f, 0);
            Quad(_menu.transform, "Selection background", bg, new Vector3(0, 0, .001f), new Vector2(.25f, .05f));
            _left = Text(_menu.transform, new Vector3(-.115f, 0, 0), .0033f, "← マジック");
            _right = Text(_menu.transform, new Vector3(.012f, 0, 0), .0033f, "スプレー →");
            _ink = Mat(Color.cyan); _guide = new GameObject("Spray surface guide", typeof(MeshFilter), typeof(MeshRenderer)); _guide.transform.SetParent(parent, false);
            _guideMesh = new Mesh { name = "Projected spray guide" }; _owned.Add(_guideMesh); _guide.GetComponent<MeshFilter>().sharedMesh = _guideMesh;
            var renderer = _guide.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _ink; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            Hide();
        }
        Material Mat(Color color)
        { var shader = Shader.Find("Unlit/Color"); if (shader == null) throw new InvalidOperationException("HUD shaderがありません"); var mat = new Material(shader) { color = color }; _owned.Add(mat); return mat; }
        TextMesh Text(Transform parent, Vector3 position, float size, string text)
        {
            var obj = new GameObject("Label", typeof(TextMesh)); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            var mesh = obj.GetComponent<TextMesh>(); mesh.font = _font; mesh.fontSize = 36; mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleLeft; mesh.color = Color.white; mesh.text = text;
            obj.GetComponent<MeshRenderer>().sharedMaterial = _font.material; return mesh;
        }
        Transform Quad(Transform parent, string name, Material mat, Vector3 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            var mesh = new Mesh { name = name }; _owned.Add(mesh);
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds(); obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            obj.transform.localScale = new Vector3(size.x, size.y, 1); return obj.transform;
        }
        internal void Show(Vector3 position, Transform view, string status, string label, float value, Color color, bool gauge, bool menu, int selection)
        {
            _root.SetActive(true); _root.transform.position = position;
            if (view != null) _root.transform.rotation = view.rotation;
            if (_status.text != status) _status.text = status;
            _gauge.SetActive(gauge && !menu); _menu.SetActive(menu);
            if (_label.text != label) _label.text = label;
            value = Mathf.Clamp01(value); _fill.localScale = new Vector3(.15f * value, .006f, 1); _fill.localPosition = new Vector3(-.075f + .075f * value, -.078f, -.0001f); _fillMaterial.color = color;
            _left.color = selection == 0 ? new Color(.3f,1,.75f) : Color.white; _right.color = selection == 1 ? new Color(.3f,1,.75f) : Color.white;
        }
        internal void Guide(bool visible, Vector3 point, Vector3 normal, float radius, Color color)
        { _guide.SetActive(false); }
        static void Triangle(List<int> triangles,int a,int b,int c) { triangles.Add(a);triangles.Add(b);triangles.Add(c); }
        internal bool ProjectGuide(Ray axis, SurfaceHit center, float radius, Color color, Func<Ray,Vector3?> project)
        {
            _guide.SetActive(false);
            if (radius <= 0 || !_root.activeInHierarchy) return false;
            _ink.color = color;
            Vector3 right = Vector3.Cross(axis.direction, Mathf.Abs(axis.direction.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, axis.direction).normalized;
            const int segments = 24; var vertices = _projectedVertices; var valid = _projectedValid; var triangles = _projectedTriangles; triangles.Clear();
            float line = Mathf.Clamp(radius*.025f,.0003f,.001f);
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments; Vector3 spoke=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                Vector3? point = project(new Ray(axis.origin, center.Point + spoke*radius - axis.origin));
                valid[i]=point.HasValue;
                if(valid[i]) { vertices[i*2]=_guide.transform.InverseTransformPoint(point.Value-spoke*line); vertices[i*2+1]=_guide.transform.InverseTransformPoint(point.Value+spoke*line); }
            }
            for(int i=0;i<segments;i++) if(valid[i]&&valid[i+1])
            { int j=i*2; Triangle(triangles,j,j+2,j+1);Triangle(triangles,j+1,j+2,j+3);Triangle(triangles,j+1,j+2,j);Triangle(triangles,j+3,j+2,j+1); }
            int k=(segments+1)*2;float dot=.0007f;
            Vector3 offset=center.Point+center.Normal*.0008f;
            vertices[k]=_guide.transform.InverseTransformPoint(offset-right*dot-up*dot);vertices[k+1]=_guide.transform.InverseTransformPoint(offset-right*dot+up*dot);
            vertices[k+2]=_guide.transform.InverseTransformPoint(offset+right*dot+up*dot);vertices[k+3]=_guide.transform.InverseTransformPoint(offset+right*dot-up*dot);
            Triangle(triangles,k,k+1,k+2);Triangle(triangles,k,k+2,k+3);Triangle(triangles,k+2,k+1,k);Triangle(triangles,k+3,k+2,k);
            _guideMesh.Clear();_guideMesh.vertices=vertices;_guideMesh.SetTriangles(triangles,0);_guideMesh.RecalculateBounds();
            _guide.SetActive(true); return _guide.activeInHierarchy;
        }
        internal void Hide() { _root.SetActive(false); _guide.SetActive(false); }
        public void Dispose()
        { UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_guide); foreach (var obj in _owned) if (obj != null) UnityEngine.Object.Destroy(obj); _owned.Clear(); }
    }
}
