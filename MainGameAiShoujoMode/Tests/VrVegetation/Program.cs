using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;
using NMatrix = System.Numerics.Matrix4x4;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static implicit operator NVector(Vector3 v) => new NVector(v.x, v.y, v.z);
        public static implicit operator Vector3(NVector v) => new Vector3(v.X, v.Y, v.Z);
        public static Vector3 Scale(Vector3 a, Vector3 b) => (NVector)a * (NVector)b;
        public static Vector3 operator +(Vector3 a, Vector3 b) => (NVector)a + (NVector)b;
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; }
        public static implicit operator NQuaternion(Quaternion q) => new NQuaternion(q.x,q.y,q.z,q.w);
        public static implicit operator Quaternion(NQuaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion operator *(Quaternion a, Quaternion b) => (NQuaternion)a * (NQuaternion)b;
    }
    public struct Color { public float value; }
    public class Component { public GameObject gameObject; public Transform transform => gameObject.transform; }
    public class Transform
    {
        public GameObject gameObject;
        public Transform parent;
        public Vector3 localPosition;
        public Vector3 localScale = NVector.One;
        public Quaternion localRotation = NQuaternion.Identity;
        private NMatrix World => NMatrix.CreateScale(localScale) * NMatrix.CreateFromQuaternion(localRotation) * NMatrix.CreateTranslation(localPosition) * (parent?.World ?? NMatrix.Identity);
        public Vector3 position
        {
            get => World.Translation;
            set { NMatrix.Invert(parent?.World ?? NMatrix.Identity, out var inverse); localPosition = NVector.Transform(value, inverse); }
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = (parent == null ? NQuaternion.Identity : NQuaternion.Inverse(parent.rotation)) * (NQuaternion)value;
        }
        public Vector3 lossyScale { get { NMatrix.Decompose(World, out var scale, out _, out _); return scale; } }
        public void SetParent(Transform value, bool worldPositionStays)
        {
            parent?.gameObject.Children.Remove(gameObject); parent = value; parent?.gameObject.Children.Add(gameObject);
        }
    }
    public class GameObject
    {
        public string name;
        public Transform transform;
        public bool activeSelf = true;
        public List<GameObject> Children = new();
        public List<Component> Components = new();
        public GameObject(string value) { name = value; transform = new Transform { gameObject = this }; }
        public void SetActive(bool value) { activeSelf = value; }
        public T Add<T>() where T : Component, new() { var value = new T { gameObject = this }; Components.Add(value); return value; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Components.OfType<T>().Concat(Children.SelectMany(child => child.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public class Collider : Component { public bool enabled = true; }
    public class MaterialPropertyBlock
    {
        public Dictionary<string, Color> Colors = new();
        public void SetColor(string key, Color value) { Colors[key] = value; }
    }
    public class Renderer : Component
    {
        public object sharedMaterial;
        public Dictionary<string, Color> Colors = new();
        public void GetPropertyBlock(MaterialPropertyBlock block) { block.Colors = new Dictionary<string, Color>(Colors); }
        public void SetPropertyBlock(MaterialPropertyBlock block) { Colors = new Dictionary<string, Color>(block.Colors); }
    }
    public static class Object
    {
        public static bool Fail;
        public static GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (Fail) throw new InvalidOperationException("Clone failed");
            var clone = new GameObject(prefab.name); clone.transform.SetParent(parent, false);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) clone.Add<Renderer>().sharedMaterial = renderer.sharedMaterial;
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true)) clone.Add<Collider>();
            return clone;
        }
        public static void DestroyImmediate(GameObject root) { root.transform.SetParent(null, false); root.SetActive(false); }
    }
    public class TreePrototype { public GameObject prefab; }
    public struct TreeInstance { public int prototypeIndex; public Vector3 position; public float widthScale, heightScale, rotation; public Color color, lightmapColor; }
    public class DetailPrototype { public GameObject prototype; public object prototypeTexture; }
    public class TerrainData
    {
        public Vector3 size;
        public TreePrototype[] treePrototypes = Array.Empty<TreePrototype>();
        public TreeInstance[] treeInstances = Array.Empty<TreeInstance>();
        public DetailPrototype[] detailPrototypes = Array.Empty<DetailPrototype>();
    }
    public class Terrain : Component { public TerrainData terrainData; public void Flush() { } }
}
namespace MapEnvironmentRuntime
{
    public class Parameters { public float N(string key) => Program.N(key); }
}
internal static class Program
{
    private static readonly System.Text.Json.JsonElement Values = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"settings.json"))).RootElement;
    internal static float N(string key) => Values.GetProperty(key).GetSingle();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static bool Near(float a, float b) => Math.Abs(a-b) < N("epsilon");
    private static UnityEngine.TerrainData Copy(UnityEngine.TerrainData source) => new UnityEngine.TerrainData {
        size = source.size, treePrototypes = source.treePrototypes.ToArray(), treeInstances = source.treeInstances.ToArray(),
        detailPrototypes = source.detailPrototypes.ToArray()
    };
    private static void Main()
    {
        var settings = new Parameters();
        var map = new UnityEngine.GameObject("Map");
        map.transform.localScale = new UnityEngine.Vector3(N("mapScale"),N("mapScale"),N("mapScale"));
        var terrain = new UnityEngine.GameObject("Terrain").Add<UnityEngine.Terrain>();
        terrain.transform.position = new UnityEngine.Vector3(N("terrainX"),N("terrainY"),N("terrainZ"));
        var prefab = new UnityEngine.GameObject("Original tree");
        prefab.transform.localScale = new UnityEngine.Vector3(N("prefabScale"),N("prefabScale"),N("prefabScale"));
        var material = new object(); prefab.Add<UnityEngine.Renderer>().sharedMaterial = material;
        var sourceCollider = prefab.Add<UnityEngine.Collider>();
        var tree = new UnityEngine.TreeInstance {
            prototypeIndex = (int)N("zero"), position = new UnityEngine.Vector3(N("treeX"),N("treeY"),N("treeZ")),
            widthScale = N("treeWidth"), heightScale = N("treeHeight"), rotation = N("treeRotation"),
            color = new UnityEngine.Color { value = N("tint") }, lightmapColor = new UnityEngine.Color { value = N("lightmapTint") }
        };
        terrain.terrainData = new UnityEngine.TerrainData {
            size = new UnityEngine.Vector3(N("sizeX"),N("sizeY"),N("sizeZ")),
            treePrototypes = new[] { new UnityEngine.TreePrototype { prefab = prefab } },
            treeInstances = new[] { tree, tree }, detailPrototypes = new[] { new UnityEngine.DetailPrototype() }
        };
        var sourceData = terrain.terrainData;
        terrain.terrainData = Copy(sourceData);
        var root = SandboxMeshVegetation.Build(terrain, map.transform, settings);
        Require(root.activeSelf && root.transform.parent == map.transform && root.Children.Count == N("two"), "Placement count/ownership changed");
        foreach (var clone in root.Children)
        {
            Require(Near(clone.transform.position.x, N("terrainX")+N("treeX")*N("sizeX")) && Near(clone.transform.position.y,N("terrainY")+N("treeY")*N("sizeY")) && Near(clone.transform.position.z,N("terrainZ")+N("treeZ")*N("sizeZ")), "Native terrain position changed");
            Require(Near(clone.transform.lossyScale.x,N("prefabScale")*N("treeWidth")) && Near(clone.transform.lossyScale.y,N("prefabScale")*N("treeHeight")), "Terrain scale was applied twice");
            Require(Near(clone.transform.rotation.y,(float)Math.Sin(N("treeRotation")*N("math.half"))), "Tree radians were treated as degrees");
            var renderer = clone.GetComponentsInChildren<UnityEngine.Renderer>(true).Single();
            Require(ReferenceEquals(renderer.sharedMaterial,material) && renderer.Colors["_TreeInstanceColor"].value == N("tint") && renderer.Colors["_TreeInstanceLightmapColor"].value == N("lightmapTint"), "Source material/tints changed");
            Require(clone.GetComponentsInChildren<UnityEngine.Collider>(true).Single().enabled, "Replacement tree collider was disabled");
        }
        Require(sourceCollider.enabled && sourceData.treeInstances.Length == N("two") && sourceData.treePrototypes.Length == N("one"), "Source collision/tree data mutated");
        Require(!terrain.terrainData.treeInstances.Any() && !terrain.terrainData.treePrototypes.Any() && !terrain.terrainData.detailPrototypes.Any(), "Native vegetation database remained attached");
        var old = root.Children.First().transform.position;
        map.transform.localPosition = new UnityEngine.Vector3(N("one"),N("zero"),N("zero"));
        Require(Near(root.Children.First().transform.position.x,old.x+N("one")), "Vegetation did not follow map placement");
        var before = map.Children.Count;
        terrain.terrainData = Copy(sourceData);
        UnityEngine.Object.Fail = true;
        try { SandboxMeshVegetation.Build(terrain,map.transform,settings); throw new Exception("Clone failure swallowed"); }
        catch (InvalidOperationException) { }
        Require(map.Children.Count == before, "Partial vegetation leaked after failure");
        UnityEngine.Object.Fail = false;
        terrain.terrainData.detailPrototypes.Single().prototypeTexture = new object();
        try { SandboxMeshVegetation.Build(terrain,map.transform,settings); throw new Exception("Valid grass silently hidden"); }
        catch (InvalidDataException) { }
        Require(map.Children.Count == before, "Unsupported detail failure leaked a root");
        terrain.terrainData.detailPrototypes.Single().prototypeTexture = null;
        terrain.terrainData.treePrototypes.Single().prefab = null;
        try { SandboxMeshVegetation.Build(terrain,map.transform,settings); throw new Exception("Broken prefab accepted"); }
        catch (InvalidDataException) { }
        Console.WriteLine("VR_VEGETATION_PASS count=true positions=true radians=true scale=true materials=true colors=true nativeDatabaseDetached=true prefabCollisionPreserved=true sourceDataUnchanged=true followsMap=true partialFailure=true invalidPrefab=true validGrassRejected=true unity=mocked");
    }
}
