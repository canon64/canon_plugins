using System;
using MainGameAiShoujoMode;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

static class Program
{
    static int checks;
    static readonly Dictionary<string, int> Fixture = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture.json")));
    static int N(string key) => Fixture[key];
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Main()
    {
        foreach (bool locked in new[] { false, true })
        foreach (bool loading in new[] { false, true })
        {
            var corridor = new GameObject(); var room = new GameObject();
            var map = new ActionGame.ActionMap(corridor, N("corridor"), locked, loading);
            var objects = map.mapObjects; var group = map.mapObjectGroup;
            var scope = new HSessionBorrowedMapScope(map, room, N("room"));
            Check(ReferenceEquals(map.mapRoot, room) && map.no == N("room"), "H receives the actual room on the existing walking Map");
            Check(map.Locked && !map.isMapLoading, "native H does not inherit an unresolved map load");
            Check(map.mapObjects.Length == default(int) && ReferenceEquals(map.mapObjectGroup, room.transform), "H map objects belong to the room");
            scope.Dispose();
            Check(ReferenceEquals(map.mapRoot, corridor) && map.no == N("corridor"), "exit restores corridor root and metadata");
            Check(map.Locked == locked && map.isMapLoading == loading, "exit restores exact original lock and load state");
            Check(ReferenceEquals(map.mapObjects, objects) && ReferenceEquals(map.mapObjectGroup, group), "exit restores object container identities");
            var other = new GameObject(); map.ReplaceRoot(other); scope.Dispose();
            Check(ReferenceEquals(map.mapRoot, other), "repeated cleanup does not overwrite later ownership");
        }
        var root = new GameObject(); var failed = new ActionGame.ActionMap(root, N("corridor"), true, false) { RejectRoomObjects = true };
        try { new HSessionBorrowedMapScope(failed, new GameObject(), N("room")); throw new Exception("expected bind failure"); }
        catch (System.Reflection.TargetInvocationException) { }
        Check(ReferenceEquals(failed.mapRoot, root) && failed.no == N("corridor") && failed.Locked && !failed.isMapLoading, "partial bind failure rolls back the original walking state");
        Console.WriteLine("PASS " + checks + " borrowed H map ownership and rollback checks (native boundary mocked)");
    }
}
namespace UnityEngine
{
    public class Transform { }
    public class GameObject { public Transform transform = new Transform(); }
}
public class BaseMap
{
    public UnityEngine.GameObject mapRoot { get; private set; }
    public int no { get; protected set; }
    public bool isMapLoading { get; protected set; }
    protected bool _isLock { get; set; }
    public bool Locked => _isLock;
    public void ReplaceRoot(UnityEngine.GameObject root) => mapRoot = root;
    protected BaseMap(UnityEngine.GameObject root, int number, bool locked, bool loading)
    { mapRoot = root; no = number; _isLock = locked; isMapLoading = loading; }
}
namespace ActionGame.MapObject { public class Kind { } }
namespace ActionGame
{
    public class ActionMap : BaseMap
    {
        MapObject.Kind[] objects = new[] { new MapObject.Kind() };
        public bool RejectRoomObjects;
        public MapObject.Kind[] mapObjects { get => objects; private set { if (RejectRoomObjects && value.Length == default(int)) throw new InvalidOperationException(); objects = value; } }
        public UnityEngine.Transform mapObjectGroup { get; private set; } = new UnityEngine.Transform();
        public ActionMap(UnityEngine.GameObject root, int number, bool locked, bool loading) : base(root, number, locked, loading) { }
    }
}
