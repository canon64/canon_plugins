using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameBlankMapAdd
{
    // Prepare geometry offline and materialize each unique mesh once while loading the island.
    internal sealed class AiIslandLodMeshes : IDisposable
    {
        private readonly BinaryReader _reader;
        private readonly Dictionary<string, long> _offsets = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        internal AiIslandLodMeshes(Stream stream)
        {
            _reader = new BinaryReader(stream, Encoding.UTF8);
            if (Encoding.ASCII.GetString(_reader.ReadBytes(8)) != "AILOD001") throw new InvalidDataException("Invalid island LOD cache.");
            int count = _reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = Encoding.UTF8.GetString(_reader.ReadBytes(_reader.ReadInt32()));
                _offsets.Add(key, stream.Position);
                int vertices = _reader.ReadInt32(), submeshes = _reader.ReadInt32();
                stream.Seek((long)vertices * 18 * sizeof(float), SeekOrigin.Current);
                for (int sub = 0; sub < submeshes; sub++)
                { int indices = _reader.ReadInt32(); stream.Seek((long)indices * sizeof(int), SeekOrigin.Current); }
            }
        }
        internal static string Key(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            Vector3 c = bounds.center, e = bounds.extents;
            var values = new[] { c.x, c.y, c.z, e.x, e.y, e.z };
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return mesh.name + "\n" + mesh.vertexCount + "\n" + mesh.subMeshCount + "\n" + BitConverter.ToString(bytes);
        }
        internal Mesh Get(Mesh source)
        {
            if (source == null) return null;
            string key = Key(source);
            if (_meshes.TryGetValue(key, out Mesh cached)) return cached;
            if (!_offsets.TryGetValue(key, out long offset)) return null;
            _reader.BaseStream.Position = offset;
            int count = _reader.ReadInt32(), submeshes = _reader.ReadInt32();
            var positions = new Vector3[count]; var normals = new Vector3[count];
            var uv = new Vector2[count]; var uv2 = new Vector2[count];
            var colors = new Color[count]; var tangents = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
                normals[i] = new Vector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle()).normalized;
                uv[i] = new Vector2(_reader.ReadSingle(), _reader.ReadSingle());
                uv2[i] = new Vector2(_reader.ReadSingle(), _reader.ReadSingle());
                colors[i] = new Color(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
                tangents[i] = new Vector4(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
            }
            var mesh = new Mesh { name = source.name + " (island distant LOD)", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.normals = normals; mesh.uv = uv; mesh.uv2 = uv2;
            mesh.colors = colors; mesh.tangents = tangents; mesh.subMeshCount = submeshes;
            for (int sub = 0; sub < submeshes; sub++)
            {
                var indices = new int[_reader.ReadInt32()];
                for (int i = 0; i < indices.Length; i++) indices[i] = _reader.ReadInt32();
                mesh.SetTriangles(indices, sub, false);
            }
            mesh.bounds = source.bounds; mesh.UploadMeshData(true);
            _meshes.Add(key, mesh);
            return mesh;
        }
        public void Dispose()
        {
            foreach (Mesh mesh in _meshes.Values) UnityEngine.Object.Destroy(mesh);
            _meshes.Clear(); _reader.Dispose();
        }
    }
}
