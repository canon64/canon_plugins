using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Checks.Add(text); }
    private static void Main()
    {
        var f = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture.json")), new JsonSerializerOptions { IncludeFields = true });
        var p = new Parameters(f.numbers);
        var data = new TerrainData(f.heights, f.surface, new Vector3(f.size));
        var terrainObject = new GameObject("terrain") { layer = f.layer };
        var terrain = terrainObject.AddComponent<Terrain>(); terrain.terrainData = data;
        var collision = terrainObject.AddComponent<TerrainCollider>();
        var original = new Material(new Shader()); terrain.materialTemplate = original;
        terrain.transform.position = new Vector3(f.position);
        var root = new GameObject("island"); terrain.transform.SetParent(root.transform, true);
        var preparation = new SandboxTerrainRenderPreparation("island");
        SceneManager.Activate(new Scene { name = "native Action", roots = Array.Empty<GameObject>() });
        Check(terrain.enabled, "native scene activation is untouched");
        SceneManager.Activate(new Scene { name = "island", roots = new[] { root } });
        Check(!terrain.enabled && !terrain.drawHeightmap && !terrain.drawTreesAndFoliage, "terrain is disabled before any render");
        Check(collision.enabled && SceneManager.Subscribers == default, "source collision remains enabled and activation hook is removed");
        var ground = root.AddComponent<SandboxTerrainMesh>(); var meshShader = new Shader();
        var build = ground.Build(terrain, root.transform, p, meshShader);
        while (build.MoveNext()) { }
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        Check(filters.Length == f.tileCount, "finite tile construction completed");
        var mesh = filters.Single().sharedMesh;
        Check(mesh.vertices.Length == f.vertices && mesh.triangles.Length == f.indices, "source hole is retained in the ground mesh");
        Check(mesh.vertices.All(v => v.y == f.heights.First().First() * data.size.y), "source height is preserved");
        Check(mesh.normals.All(v => v.y == p.N("math.one") && v.x == p.N("math.zero") && v.z == p.N("math.zero")), "flat ground normals point up");
        for (int index = default; index < mesh.triangles.Length; index += p.I("math.two") + p.I("math.one"))
        {
            var a = mesh.vertices[mesh.triangles[index]];
            var b = mesh.vertices[mesh.triangles[index + p.I("math.one")]];
            var c = mesh.vertices[mesh.triangles[index + p.I("math.two")]];
            Check((b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z) > p.N("math.zero"), "visible triangle winding faces up");
        }
        var material = root.GetComponentsInChildren<MeshRenderer>(true).Single().sharedMaterial;
        Check(ReferenceEquals(material.shader, meshShader) && ReferenceEquals(material.CopiedFrom, original), "mesh shader retains original terrain material properties");
        var holder = filters.Single().transform.parent;
        Check(holder.KeptWorld && holder.position.Equals(terrain.transform.position), "mesh holder preserves world scale and source origin under scaled map");
        typeof(SandboxTerrainMesh).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ground, null);
        Check(mesh.Destroyed && material.Destroyed, "owned ground resources are released");
        bool failed = false;
        try { var unavailable = ground.Build(terrain, root.transform, p, new Shader { isSupported = false }); unavailable.MoveNext(); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "unsupported shader rejects loading instead of hiding the missing floor");
        preparation.Dispose();
        Console.WriteLine("PASS " + Checks.Count + " native ground geometry and activation checks");
    }
    public sealed class Fixture
    {
        public Dictionary<string, float> numbers { get; set; }
        public float[][] heights { get; set; } public bool[][] surface { get; set; }
        public float[] size { get; set; } public float[] position { get; set; }
        public int vertices { get; set; } public int indices { get; set; } public int tileCount { get; set; } public int layer { get; set; }
    }
}
namespace MapEnvironmentRuntime
{
    internal sealed class Parameters
    {
        private readonly Dictionary<string, float> _values;
        internal Parameters(Dictionary<string, float> values) { _values = values; }
        public float N(string key) => _values[key];
        public int I(string key) => checked((int)N(key));
    }
}
namespace UnityEngine
{
    internal class Object { public bool Destroyed; public static void Destroy(Object value) { value.Destroyed = true; } }
    internal class Component : Object
    {
        public GameObject gameObject; public Transform transform => gameObject.transform;
        public string name { get; set; }
    }
    internal class MonoBehaviour : Component { }
    internal class GameObject : Object
    {
        public string name; public int layer; public Transform transform;
        internal readonly List<Component> Components = new();
        internal readonly List<GameObject> Children = new();
        public GameObject(string name) { this.name = name; transform = new Transform { gameObject = this }; }
        public T AddComponent<T>() where T : Component, new() { var value = new T { gameObject = this }; Components.Add(value); return value; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Components.OfType<T>().Concat(Children.SelectMany(child => child.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    internal class Transform
    {
        public GameObject gameObject; public Transform parent; public Vector3 position; public Quaternion rotation;
        public bool KeptWorld;
        public void SetParent(Transform value, bool keepWorld) { parent = value; value.gameObject.Children.Add(gameObject); KeptWorld = keepWorld; }
    }
    internal struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float[] value) { using var items = ((IEnumerable<float>)value).GetEnumerator(); items.MoveNext(); x = items.Current; items.MoveNext(); y = items.Current; items.MoveNext(); z = items.Current; }
        public Vector3 normalized { get { var length = MathF.Sqrt(x*x+y*y+z*z); return new Vector3(x/length,y/length,z/length); } }
    }
    internal struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x; this.y=y; } }
    internal struct Vector4 { public float x,y,z,w; public Vector4(float x,float y,float z,float w) { this.x=x; this.y=y; this.z=z; this.w=w; } }
    internal struct Quaternion { public static Quaternion identity => default; }
    internal class Shader : Object { public bool isSupported = true; }
    internal class Material : Object
    {
        public string name; public Shader shader; public bool enableInstancing; public Material CopiedFrom;
        public Material(Shader shader) { this.shader = shader; }
        public void CopyPropertiesFromMaterial(Material source) { CopiedFrom = source; }
    }
    internal class Mesh : Object
    {
        public string name; public Rendering.IndexFormat indexFormat;
        public Vector3[] vertices, normals; public Vector4[] tangents; public Vector2[] uv,uv2; public int[] triangles;
        public void RecalculateBounds() { }
    }
    internal class MeshFilter : Component { public Mesh sharedMesh; }
    internal class MeshRenderer : Component { public Material sharedMaterial; public Rendering.ShadowCastingMode shadowCastingMode; }
    internal class Terrain : Component { public TerrainData terrainData; public Material materialTemplate; public bool enabled = true, drawHeightmap = true, drawTreesAndFoliage = true; }
    internal class TerrainCollider : Component { public bool enabled = true; }
    internal class TerrainData
    {
        private readonly float[,] _heights; private readonly bool[,] _surface;
        public Vector3 size; public int heightmapResolution => _heights.GetLength(default); public int holesResolution => _surface.GetLength(default);
        public TerrainData(float[][] heights, bool[][] surface, Vector3 size)
        {
            this.size = size; _heights = new float[heights.Length,heights.First().Length]; _surface = new bool[surface.Length,surface.First().Length];
            for (int z = default; z < heights.Length; z++) for (int x = default; x < heights[z].Length; x++) _heights[z,x] = heights[z][x];
            for (int z = default; z < surface.Length; z++) for (int x = default; x < surface[z].Length; x++) _surface[z,x] = surface[z][x];
        }
        public float[,] GetHeights(int x,int z,int width,int height) => _heights;
        public bool[,] GetHoles(int x,int z,int width,int height) => _surface;
    }
}
namespace UnityEngine.Rendering
{
    internal enum IndexFormat { UInt32 }
    internal enum ShadowCastingMode { Off }
}
namespace UnityEngine.SceneManagement
{
    internal enum LoadSceneMode { Additive }
    internal struct Scene { public string name; public GameObject[] roots; public GameObject[] GetRootGameObjects() => roots; }
    internal static class SceneManager
    {
        public static event Action<Scene,LoadSceneMode> sceneLoaded;
        public static int Subscribers => sceneLoaded?.GetInvocationList().Length ?? default;
        public static void Activate(Scene scene) => sceneLoaded?.Invoke(scene,LoadSceneMode.Additive);
    }
}
