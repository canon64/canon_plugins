using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        bool _hotelPlaybackSession, allowed = true;
        readonly MapSession _mapSession = new MapSession();
        readonly List<MapInfo.Param> _roomOverlayChoices = new List<MapInfo.Param>();
        readonly Settings _settings = new Settings { AddedMapNo = 910 };
        readonly Actor _femaleChara = new Actor();
        GameObject _roomOverlayPositionRoot;
        string _roomOverlayPositionKey, _mapTabInputKey;
        Vector3 _roomOverlayBasePosition, _roomOverlayActorAnchor;
        Quaternion _roomOverlayBaseRotation;
        bool CanUseRoomOverlay() => allowed;
        void EnsureRoomOverlayPositionConfig(string key) { }
        void ApplyRoomOverlayPosition() { }
        static int checks;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        public static void Main()
        {
            var p = new Plugin();
            p.RefreshRoomOverlayChoices();
            Check(p._roomOverlayChoices.Count == 0, "missing reservation must not throw during Layout");
            var stale = new BaseMap { no = 910, mapRoot = new GameObject() };
            p._mapSession.LastReservedMap = stale;
            var room = new BaseMap { no = 42, mapRoot = new GameObject(), infoDic = new Dictionary<int, MapInfo.Param> {
                {2, new MapInfo.Param {No=2, AssetBundleName="map2", AssetName="room2"}},
                {1, new MapInfo.Param {No=1, AssetBundleName="map1", AssetName="room1"}},
                {910, new MapInfo.Param {No=910, AssetBundleName="added", AssetName="added"}},
                {3, null}
            }};
            p._hotelPlaybackSession = true;
            CanonPlugins.Shared.PluginSessionGate.HScene = new HSceneProc { Map = room };
            CanonGate.GateApi.Current = new CanonGate.Session { HotelRoomId = "201" };
            p.BindOwnedMapContext(); p.RefreshRoomOverlayChoices();
            Check(ReferenceEquals(p.LastReservedMap,room), "hotel binds actual room instead of stale reservation");
            Check(p._roomOverlayChoices.Count==2 && p._roomOverlayChoices[0].No==1, "hotel catalog filters and sorts native maps");
            int reads=CanonPlugins.Shared.PluginSessionGate.HScene.Reads;
            p.RefreshRoomOverlayChoices(); p.RefreshRoomOverlayChoices();
            Check(CanonPlugins.Shared.PluginSessionGate.HScene.Reads==reads, "Layout does not rediscover room component");
            Check(p.TrySourceMapWarpContext(out var key,out var root) && key=="source:hotel-room-201" && ReferenceEquals(root,room.mapRoot.transform), "H location uses actual 201 room coordinates");
            Check(p._roomOverlayPositionKey=="source-hotel-room-201", "placement config isolates hotel room");
            CanonPlugins.Shared.PluginSessionGate.HScene = new HSceneProc();
            p.BindOwnedMapContext();
            Check(p.LastReservedMap == null, "gate start before native borrowed map assignment remains unresolved");
            CanonPlugins.Shared.PluginSessionGate.HScene.Borrow(room);
            p.BindOwnedMapContext();
            Check(ReferenceEquals(p.LastReservedMap,room), "native VR H borrows its existing ActionMap without an HProc component");
            Check(p.TrySourceMapWarpContext(out key,out root) && key=="source:hotel-room-201" && ReferenceEquals(root,room.mapRoot.transform), "ready-bound borrowed map enables hotel H locations");
            p.RestoreSourceMapPlacement(); p.ClearOwnedMapContext();
            Check(p.LastReservedMap==null && p._roomOverlayChoices.Count==0, "cleanup cannot fall back to previous reservation");
            CanonGate.GateApi.Current.HotelRoomId="202";
            p.BindOwnedMapContext();
            Check(p.TrySourceMapWarpContext(out key,out root) && key=="source:hotel-room-202", "202 cannot share 201 presets");
            p._hotelPlaybackSession=false; p.BindOwnedMapContext();
            Check(ReferenceEquals(p.LastReservedMap,stale) && p.TrySourceMapWarpContext(out key,out root) && key=="source:910", "ordinary source behavior retained");
            p.allowed=false; p.RefreshRoomOverlayChoices();
            Check(p._roomOverlayChoices.Count==0,"denied session clears choices");
            Console.WriteLine("PASS "+checks+" real map-context and source-placement checks");
        }
    }
    sealed class MapSession { public BaseMap LastReservedMap; }
    sealed class Settings { public int AddedMapNo; }
    sealed class Actor { public Transform transform = new GameObject().transform; }
}
class BaseMap { public int no; public GameObject mapRoot; public IReadOnlyDictionary<int,MapInfo.Param> infoDic; }
class MapInfo { public class Param { public int No; public string AssetBundleName,AssetName; } }
class HSceneProc { public BaseMap Map; private BaseMap map; public int Reads; public void Borrow(BaseMap value) { map=value; } public T GetComponent<T>() where T:class { Reads++;return Map as T; } }
namespace CanonPlugins.Shared { static class PluginSessionGate { public static HSceneProc HScene; } }
namespace CanonGate { class Session { public string HotelRoomId; public bool IsAiShoujoMode; } static class GateApi { public static Session Current; } }
namespace UnityEngine
{
    class GameObject { public readonly Transform transform; public GameObject() { transform=new Transform {gameObject=this}; } }
    class Transform { public GameObject gameObject; public Vector3 position; public Quaternion rotation; public void SetPositionAndRotation(Vector3 p,Quaternion r) {position=p;rotation=r;} }
    struct Vector3 { }
    struct Quaternion { }
}
