using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string description) { if (!ok) throw new Exception(description); Checks.Add(description); }
    private static GameObject Floor() { var root = new GameObject(); root.Colliders.Add(new Collider { gameObject = root, enabled = true }); return root; }
    private static void Drain(IEnumerator steps) { while (steps.MoveNext()) { } }
    private static void Main()
    {
        var settings = new Parameters(JsonSerializer.Deserialize<Dictionary<string,float>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixture.json"))));
        var imported = Floor(); var oldMapRoot = new GameObject(); oldMapRoot.transform.parent = imported.transform;
        var action = new ActionScene(oldMapRoot); var door = new Transform();
        using (var map = new NativeActionMapScope(action))
        {
            Drain(map.Attach(imported,settings,default,default,new[] {door,door}));
            Check(oldMapRoot.activeSelf && imported.activeSelf, "hotel render root inside incoming wrapper remains active");
            Check(ReferenceEquals(action.Map.mapRoot,imported) && action.Map.Locked, "owned map survives native active-scene changes");
            Check(NavMeshBuilder.Markups.Single().ignoreFromBuild && ReferenceEquals(NavMeshBuilder.Markups.Single().root,door), "moving door branches are excluded once from the static bake");
            var built = NavMeshBuilder.Settings;
            Check(built.agentRadius == settings.N("movement.bodyRadius") && built.agentHeight == settings.N("movement.bodyHeight"), "bake uses actual configured body clearance");
            Check(built.agentClimb == settings.N("movement.step") && built.agentSlope == settings.N("movement.slopeLimit"), "bridge steps and slopes use walking settings");
            Check(built.overrideVoxelSize && built.voxelSize == settings.N("navigation.voxelSize") && built.minRegionArea == settings.N("navigation.minRegionArea"), "narrow floor baking uses configured resolution and region size");
            Check(action.Player.agent.radius == built.agentRadius && action.Player.agent.height == built.agentHeight, "native agent agrees with its baked clearance");
        }
        Check(ReferenceEquals(action.Map.mapRoot,oldMapRoot) && !action.Map.Locked && oldMapRoot.activeSelf, "exit restores exact previous root and map lock");
        var unrelated = new GameObject(); action = new ActionScene(unrelated);
        using (var map = new NativeActionMapScope(action))
        { Drain(map.Attach(Floor(),settings,default,default)); Check(!unrelated.activeSelf, "unrelated outgoing map still hides"); }
        Check(unrelated.activeSelf, "unrelated outgoing map restores on cleanup");
        var minimap = action.miniMap;
        using (var ui = new NativeActionUiScope(action))
            Check(!minimap.gameObject.activeSelf && minimap.isVisible, "story minimap rendering hides without disabling native input state");
        Check(minimap.gameObject.activeSelf && minimap.isVisible, "minimap presentation restores on disposal");
        minimap.gameObject.SetActive(false);
        using (var ui = new NativeActionUiScope(action)) { }
        Check(!minimap.gameObject.activeSelf, "previously hidden minimap remains hidden");
        NavMeshBuilder.Pending = true; action = new ActionScene(null);
        var pending = new NativeActionMapScope(action); var loading = pending.Attach(Floor(),settings,default,default); loading.MoveNext();
        var data = NavMeshBuilder.Data; pending.Dispose();
        Check(!data.Destroyed && action.Map.mapRoot == null && !action.Map.Locked, "cancelled floor remains unbound while native bake still owns its data");
        NavMeshBuilder.Operation.Complete(); Drain(loading);
        Check(data.Destroyed && action.Map.mapRoot == null, "late native bake completion releases data without attaching a cancelled floor");
        Console.WriteLine("PASS " + Checks.Count + " native map ownership, door bake, clearance and minimap checks (Unity boundaries mocked)");
    }
}
namespace MapEnvironmentRuntime
{
    internal class Parameters
    {
        private readonly Dictionary<string,float> _values; public Parameters(Dictionary<string,float> values) { _values=values; }
        public float N(string key) => _values[key]; public int I(string key) => checked((int)N(key));
    }
}
namespace HarmonyLib
{
    internal static class AccessTools
    {
        private static PropertyInfo Property(Type type,string name) => type.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        public static MethodInfo PropertySetter(Type type,string name) => Property(type,name)?.GetSetMethod(true);
        public static MethodInfo PropertyGetter(Type type,string name) => Property(type,name)?.GetGetMethod(true);
    }
}
namespace UnityEngine
{
    internal class Object { public bool Destroyed; public static void Destroy(Object value) { value.Destroyed=true; } }
    internal class GameObject : Object
    {
        public readonly Transform transform; public bool activeSelf=true; public bool activeInHierarchy=>activeSelf;
        public readonly List<Collider> Colliders=new(); public GameObject() { transform=new Transform {gameObject=this}; }
        public void SetActive(bool value) { activeSelf=value; }
        public T[] GetComponentsInChildren<T>(bool inactive) => Colliders.Cast<T>().ToArray();
    }
    internal class Transform
    {
        public GameObject gameObject; public Transform parent; public Quaternion rotation {get;set;}
        public bool IsChildOf(Transform root) { for(var node=this;node!=null;node=node.parent) if(ReferenceEquals(node,root)) return true; return false; }
    }
    internal class Collider { public bool enabled; public GameObject gameObject; public Bounds bounds=>default; }
    internal struct Bounds { public void Encapsulate(Bounds bounds) {} public void Expand(float value) {} }
    internal struct Vector3 { }
    internal struct Quaternion { }
    internal static class LayerMask { public static int NameToLayer(string name)=>default; }
    internal class AsyncOperation
    {
        public bool isDone; public event Action<AsyncOperation> completed;
        public void Complete() { isDone=true;completed?.Invoke(this); }
    }
}
namespace UnityEngine.AI
{
    internal class NavMeshAgent { public int agentTypeID {get;set;} public float radius,height; public bool Warp(Vector3 point)=>true; }
    internal class NavMeshData : UnityEngine.Object { public string name; public NavMeshData(int type) {} }
    internal struct NavMeshDataInstance { public bool valid; public void Remove() {} }
    internal struct NavMeshBuildSource { }
    internal struct NavMeshBuildMarkup { public Transform root; public bool ignoreFromBuild; }
    internal enum NavMeshCollectGeometry { PhysicsColliders }
    internal struct NavMeshBuildSettings
    { public int agentTypeID {get;set;} public float agentHeight,agentRadius,agentClimb,agentSlope,voxelSize,minRegionArea; public bool overrideVoxelSize; }
    internal struct NavMeshHit { public Vector3 position {get;set;} }
    internal static class NavMesh
    {
        public static int AllAreas=>default; public static int GetAreaFromName(string name)=>default;
        public static NavMeshBuildSettings GetSettingsByID(int id)=>new() {agentTypeID=id};
        public static NavMeshDataInstance AddNavMeshData(NavMeshData data)=>new() {valid=true};
        public static bool SamplePosition(Vector3 feet,out NavMeshHit start,float radius,int mask) { start=default;return true; }
    }
    internal static class NavMeshBuilder
    {
        public static List<NavMeshBuildMarkup> Markups; public static NavMeshBuildSettings Settings;
        public static bool Pending; public static NavMeshData Data; public static AsyncOperation Operation;
        public static void CollectSources(Transform root,int mask,NavMeshCollectGeometry kind,int area,List<NavMeshBuildMarkup> markups,List<NavMeshBuildSource> sources)
        { Markups=markups;sources.Add(default); }
        public static AsyncOperation UpdateNavMeshDataAsync(NavMeshData data,NavMeshBuildSettings settings,List<NavMeshBuildSource> sources,Bounds bounds)
        { Data=data;Settings=settings;Operation=new AsyncOperation {isDone=!Pending};return Operation; }
    }
}
internal class BaseMap
{
    protected bool _isLock {get;set;} public bool Locked=>_isLock;
    public GameObject mapRoot {get;private set;} public bool isMapLoading {get;private set;} public int no {get;private set;}
    public readonly Dictionary<int,MapParam> infoDic=new() {{default,new MapParam()}};
    public BaseMap(GameObject root) { mapRoot=root; }
    public MapParam GetParam(int id)=>infoDic.TryGetValue(id,out var value)?value:null;
    internal class MapParam {public int No {get;set;}}
}
internal class ActionScene
{
    public readonly BaseMap Map; public readonly NativePlayer Player=new(); public readonly Minimap miniMap=new();
    public ActionScene(GameObject root) {Map=new BaseMap(root);}
}
internal class NativePlayer
{
    public bool initialized=true; public int mapNo; public readonly NavMeshAgent agent=new();public readonly Transform transform=new();public void SetActive(bool value) {}
}
internal class Minimap {public readonly GameObject gameObject=new();public bool isVisible=true;}
namespace Manager {internal static class Game { public static object saveData=>null; }}
namespace MainGameAiShoujoMode {internal static class NativeActionWorldScope { public static bool Owns(object value)=>true; }}
