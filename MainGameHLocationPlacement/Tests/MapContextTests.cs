using System;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class MapContextTests
    {
        private class MapFixture
        {
            public string _roomOverlaySceneName, _roomOverlayBundleName, _roomOverlayDisplayName, _hPlaceKey;
            public bool _roomOverlayLoading, _sessionStarted;
            public GameObject _roomOverlayPositionRoot, _vamHotelPivot;
            public Transform _hPlaceRoot;
            public ChaControl _femaleChara;
            public Place _hPlaceActive, _hPlaceDraft;
            public object LastReservedMap => null;
            public static event Action<ChaControl,long,string,string> HLocationMoving { add {} remove {} }
            public static event Action<ChaControl,long,string,string> HLocationMoved { add {} remove {} }
        }
        private sealed class Place { public string Id, Name, MapKey; }
        private sealed class UpdatedMap : MapFixture { public int UnrelatedNewSetting; }
        private sealed class WrongFieldMap : MapFixture { public new int _roomOverlayLoading; }
        private sealed class WrongEventMap : MapFixture { public new static event Action HLocationMoved { add {} remove {} } }
        internal static void Run()
        {
            MapContract.Check(typeof(MapFixture),typeof(ChaControl),typeof(object));
            MapContract.Check(typeof(UpdatedMap),typeof(ChaControl),typeof(object));
            Program.Throws(()=>MapContract.Check(typeof(WrongFieldMap),typeof(ChaControl),typeof(object)),"reject changed map field type");
            Program.Throws(()=>MapContract.Check(typeof(WrongEventMap),typeof(ChaControl),typeof(object)),"reject changed map event signature");
            Program.Throws(()=>MapContract.Check(typeof(MapFixture),typeof(ChaControl),typeof(string)),"reject changed source type");
            var overlay = new GameObject("AllPoseRoom201").transform;
            var room = new GameObject("Hotel201").transform;
            var island = new GameObject("AIIsland").transform;
            var allPose = MapContextResolver.Resolve(new MapContextInput { MapActive=true, Scene="HotelMyroom", Bundle="map/scene/hotelmyroom.unity3d", OverlayRoot=overlay, SourceRoot=room, SourceKey="source:910" });
            var hotel = MapContextResolver.Resolve(new MapContextInput { Hotel=true,RoomId="201",SourceRoot=room,MapActive=false,Scene="HotelMyroom",OverlayRoot=overlay,BoundRoot=room,BoundKey="stale" });
            var ai = MapContextResolver.Resolve(new MapContextInput { Ai=true,SourceRoot=island,MapActive=false,Scene="HotelMyroom",OverlayRoot=overlay,BoundRoot=island,BoundKey="stale" });
            Program.Assert(allPose.Root==overlay&&allPose.Key=="bundle:map/scene/hotelmyroom.unity3d\nscene:HotelMyroom","AllPoseRoom 201 uses current overlay");
            Program.Assert(hotel.Root==room&&hotel.Key=="source:hotel-room-201","hotel 201 uses owned room and ignores stale overlay");
            Program.Assert(ai.Root==island&&ai.Key=="special:ai_shoujo_main","AI H uses owned island while map plugin inactive");
            var bound=MapContextResolver.Resolve(new MapContextInput {MapActive=true,Hotel=true,RoomId="201",SourceRoot=room,BoundRoot=room,BoundKey="source:hotel-room-201"});
            Program.Assert(bound.Key==hotel.Key,"hotel identity stable before and after H-place binding");
            var islandOverlay=MapContextResolver.Resolve(new MapContextInput {MapActive=true,Scene="map_00_data",OverlayRoot=island});
            Program.Assert(islandOverlay.Key==ai.Key,"AI island identity agrees with map tab");
            var pivot=new GameObject("VamHotel").transform;
            Program.Assert(MapContextResolver.Resolve(new MapContextInput {MapActive=true,Scene="thehotelroom_lit",HotelPivot=pivot,OverlayRoot=overlay}).Root==pivot,"hotel asset uses pivot");
            Program.Throws(()=>MapContextResolver.Resolve(new MapContextInput {Hotel=true,SourceRoot=room}),"reject missing hotel room id");
            Program.Throws(()=>MapContextResolver.Resolve(new MapContextInput {Ai=true,OverlayRoot=overlay,Scene="stale"}),"never use stale map instead of missing owned island");
            var frames=new[]{Program.Pose(3,0,-7,0),Program.Pose(-4,2,9,90),Program.Pose(8,1,-2,180)};
            for(int a=0;a<3;a++)for(int b=0;b<3;b++)
            {
                var space=new MainGameHLocationCoordinates.CoordinateSpace(frames[a],()=>{});space.UpdateDestination(frames[b]);
                var local=Program.Pose(.2f,.4f,-.6f,25);
                var source=new MainGameHLocationCoordinates.RigidPose(frames[a].position+frames[a].rotation*local.position,frames[a].rotation*local.rotation);
                var expected=new MainGameHLocationCoordinates.RigidPose(frames[b].position+frames[b].rotation*local.position,frames[b].rotation*local.rotation);
                Program.Near(space.Transform(source),expected,"cross-mode object / independent IK "+a+" -> "+b);
            }
        }
    }
}
