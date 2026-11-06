using UnityEngine;
using UnityEngine.UI;

namespace MainGameIPhoneCamera
{
    // Use the installed screen silhouette, including its rounded corners/notch, as UI stencil.
    internal sealed class PhoneScreenMask : MaskableGraphic
    {
        private Vector3[] _vertices;
        private int[] _triangles;
        internal void Initialize(Mesh screen)
        {
            _vertices = screen.vertices; _triangles = screen.triangles;
            raycastTarget = false; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_vertices == null) return;
            foreach (var p in _vertices)
                helper.AddVert(new Vector3(-p.x * 10000, p.z * 10000, 0), Color.white, Vector2.zero);
            for (int i = 0; i < _triangles.Length; i += 3)
                helper.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
        }
    }
}
