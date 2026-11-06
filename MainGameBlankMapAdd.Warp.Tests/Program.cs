using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private static int checks;
        private static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
        private static bool Near(Vector3 a,Vector3 b)=>(a.N-b.N).Length()<.001f;
        public static void Main()
        {
            TerrainGridTests();
            string directory=Path.Combine(@"J:\temp\map-warp-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var p=new Plugin{_pluginDir=directory};p.LoadMapWarpPoints();
            p._femaleChara.transform.position=new Vector3(10,2,-5);
            p._roomOverlayBasePosition=new Vector3(4,-2,7);p._roomOverlayActorAnchor=new Vector3(9,2,5);
            p._roomOverlayBaseRotation=Quaternion.Euler(10,20,0);p._roomOverlayPositionRoot.transform.localScale=new Vector3(.1f,.2f,.15f);
            p.settings.RotationY.Value=65;p.settings.RotateAroundActor.Value=true;p.ApplyRoomOverlayPosition();
            p.TryMapWarpContext(out string key,out var root,out _);var actor=p._femaleChara.transform.position;
            p._mapWarpName="海岸";p.RegisterMapWarpPoint(key,false,null);
            Check(p._mapWarpDocument.Points.Count==1&&File.Exists(p.MapWarpPath),"register actor location and save file");
            var point=p._mapWarpDocument.Points[0];var id=point.Id;
            Check(Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),actor),"current location stored in map-local coordinates");
            p.settings.RotationY.Value=-90;p.settings.X.Value=500;p.ApplyRoomOverlayPosition();
            var camera=CanonPlugins.Shared.PluginSessionGate.HScene.flags.ctrlCamera;camera.State=new CameraControl.Data{Pos=new Vector3(1,2,3),Fov=47};
            p.WarpToMapPoint(key,id);
            Check(p.settings.RotationY.Value==-90&&p.settings.RotateAroundActor.Value&&Near(root.lossyScale,new Vector3(.1f,.2f,.15f)),"warp preserves current rotation, scale and pivot instead of registered orientation");
            Check(Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),actor),"warp aligns landmark through rotation, nonuniform scale and actor-centered pivot");
            Check(camera.Writes==0&&camera.State.Fov==47&&Near(camera.State.Pos,new Vector3(1,2,3))&&Near(p._femaleChara.transform.position,actor),"warp never writes camera or actor state");
            string registeredBeforeRotation=File.ReadAllText(p.MapWarpPath);
            var registeredLocal=new Vector3(point.X,point.Y,point.Z);
            p.settings.RotationY.Value=-60;p.ApplyMapTabRotation(false);
            Check(Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),actor),"post-warp Y rotation keeps warped place under actor with offset loading pivot");
            p.settings.RotateAroundActor.Value=false;p.WarpToMapPoint(key,id);
            Check(Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),actor),"same landmark works when rotation pivot changes to map origin");
            p.settings.RotationY.Value=15;p.ApplyMapTabRotation(false);
            Check(Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),actor),"post-warp rotation fixes actor point even with map-origin placement settings");
            Check(File.ReadAllText(p.MapWarpPath)==registeredBeforeRotation&&Near(new Vector3(point.X,point.Y,point.Z),registeredLocal),"changing rotation center and direction leaves registered map coordinates and file unchanged");
            p.settings.X.Value+=100;p.ApplyRoomOverlayPosition();p.WarpToMapPoint(key,id);
            Check(p.settings.RotationY.Value==15&&Near(root.TransformPoint(registeredLocal),actor),"old registered place remains usable after pivot and angle changes without re-registration");
            p._mapWarpName="展望台";camera.transBase=new Transform{position=new Vector3(10,3,4),rotation=Quaternion.Euler(0,40,0)};
            Vector3 focus=camera.transBase.TransformPoint(camera.State.Pos);p.RegisterMapWarpPoint(key,true,null);var viewed=p._mapWarpDocument.Points.Last();
            Check(Near(root.TransformPoint(new Vector3(viewed.X,viewed.Y,viewed.Z)),focus)&&camera.Writes==0,"register camera focus in world-base coordinates without moving camera");
            p._mapWarpName="新しい名前";p.ChangeMapWarpPoint(key,id,false);Check(p._mapWarpDocument.Points[0].Name=="新しい名前","rename selected landmark");
            p.RegisterMapWarpPoint(key,true,id);Check(p._mapWarpDocument.Points.Count==2&&p._mapWarpDocument.Points[0].Id==id,"explicit overwrite retains landmark ID");
            p.LoadMapWarpPoints();Check(p._mapWarpDocument.Points.Count==2,"landmarks survive session reload");
            string saved=File.ReadAllText(p.MapWarpPath);string savedName=p._mapWarpDocument.Points[0].Name;
            using(var locked=new FileStream(p.MapWarpPath,FileMode.Open,FileAccess.Read,FileShare.None))
            {p._mapWarpName="保存失敗";p.ChangeMapWarpPoint(key,id,false);}
            Check(p._mapWarpDocument.Points[0].Name==savedName&&File.ReadAllText(p.MapWarpPath)==saved,"failed atomic replacement preserves in-memory document and saved file");
            var before=root.position;p._roomOverlayBundleName="other";p.WarpToMapPoint(key,id);
            Check(Near(root.position,before),"stale UI action for another map is rejected");p._roomOverlayBundleName="bundle";
            p._roomOverlayLoading=true;p.RegisterMapWarpPoint(key,false,null);p.WarpToMapPoint(key,id);Check(p._mapWarpDocument.Points.Count==2&&Near(root.position,before),"loading rejects register and warp");p._roomOverlayLoading=false;
            p._sessionStarted=false;p.WarpToMapPoint(key,id);Check(Near(root.position,before),"ended session rejects warp");p._sessionStarted=true;
            p.allowed=false;p.ChangeMapWarpPoint(key,id,true);Check(p._mapWarpDocument.Points.Count==2,"gate denial rejects mutation");p.allowed=true;
            p.Config.Fail=true;p.settings.X.Value=44;p.ApplyRoomOverlayPosition();before=root.position;p.WarpToMapPoint(key,id);
            Check(Near(root.position,before)&&p.settings.X.Value==44&&p.Config.SaveOnConfigSet,"failed config save rolls back transform and restores autosave");p.Config.Fail=false;
            p.ChangeMapWarpPoint(key,id,true);Check(p._mapWarpDocument.Points.Count==1&&File.Exists(p.MapWarpPath+".bak"),"delete and atomic replacement preserve a backup");
            p._roomOverlaySceneName="hotel";p._vamHotelActorAnchor=new Vector3(10,2,-5);p._vamHotelScale.Value=2.5f;p._vamHotelRotationY.Value=33;p.ApplyVamHotelTransform();
            p.TryMapWarpContext(out key,out root,out _);p._mapWarpName="ホテル";p.RegisterMapWarpPoint(key,false,null);var hotel=p._mapWarpDocument.Points.Last();
            p._vamHotelScale.Value=.3f;p._vamHotelRotationY.Value=-15;p.ApplyVamHotelTransform();p.WarpToMapPoint(key,hotel.Id);
            Check(p._vamHotelScale.Value==.3f&&p._vamHotelRotationY.Value==-15&&Near(root.TransformPoint(new Vector3(hotel.X,hotel.Y,hotel.Z)),actor),"hotel warp aligns landmark while preserving current scale and rotation");
            p._vamHotelRotationY.Value=75;p.ApplyMapTabRotation(true);
            Check(Near(root.TransformPoint(new Vector3(hotel.X,hotel.Y,hotel.Z)),actor),"hotel post-warp rotation keeps landmark at actor despite displaced hotel root");
            before=root.position;
            var customCenterWorld=camera.transBase.TransformPoint(camera.State.Pos);
            p.SetMapPointRotationCenter(key,hotel.Id,true);
            var custom=p._mapWarpDocument.Points.Single(v=>v.Id==hotel.Id);
            var customCenterLocal=new Vector3(custom.CenterX,custom.CenterY,custom.CenterZ);
            Check(custom.HasRotationCenter&&Near(root.TransformPoint(customCenterLocal),customCenterWorld)&&Near(root.position,before),"set per-landmark camera-focus center without moving map or landmark");
            p._vamHotelRotationY.Value=105;p.ApplyMapTabRotation(true);
            Check(Near(root.TransformPoint(customCenterLocal),customCenterWorld)&&Near(p._femaleChara.transform.position,actor),"rotation keeps custom map-local center fixed without moving actor");
            Check(!p._mapWarpDocument.Points.First(v=>v.Id!=hotel.Id).HasRotationCenter,"custom center changes only selected landmark");
            var persisted=MapWarpPointStore.Load(p.MapWarpPath).Points.Single(v=>v.Id==hotel.Id);
            Check(persisted.HasRotationCenter&&Near(new Vector3(persisted.CenterX,persisted.CenterY,persisted.CenterZ),customCenterLocal),"custom rotation center survives disk reload");
            p._mapWarpName="ホテル";p.RegisterMapWarpPoint(key,false,hotel.Id);
            custom=p._mapWarpDocument.Points.Single(v=>v.Id==hotel.Id);
            Check(custom.HasRotationCenter&&Near(new Vector3(custom.CenterX,custom.CenterY,custom.CenterZ),customCenterLocal),"explicit location overwrite preserves individually saved rotation center");
            p.SetMapPointRotationCenter(key,hotel.Id,null);
            Check(!p._mapWarpDocument.Points.Single(v=>v.Id==hotel.Id).HasRotationCenter,"reset center uses landmark location again");
            p._mapWarpName="ホテル展望";p.RegisterMapWarpPoint(key,true,null);var second=p._mapWarpDocument.Points.Last();
            p.ReturnMapWarpOrigin();
            Check(p._vamHotelScale.Value==.3f&&p._vamHotelRotationY.Value==-15&&p._mapWarpActiveId==null,"square restores pre-first-warp placement including scale and rotation");
            GUI.Click="▷";p.DrawMapWarpBar(new Rect(0,0,230,23));Check(p._mapWarpActiveId==hotel.Id,"bar next starts at first landmark in current map only");
            GUI.Click="▷";p.DrawMapWarpBar(new Rect(0,0,230,23));Check(p._mapWarpActiveId==second.Id,"bar next advances to next registered landmark");
            p.StepMapWarpPoint(1);Check(p._mapWarpActiveId==hotel.Id,"next wraps from final landmark to first");
            p.StepMapWarpPoint(-1);Check(p._mapWarpActiveId==second.Id,"previous wraps from first landmark to final");
            GUI.Click="□";p.DrawMapWarpBar(new Rect(0,0,230,23));Check(p._vamHotelScale.Value==.3f&&p._mapWarpActiveId==null,"bar square preserves original return target across repeated warps");
            GUI.Click="◁";p.DrawMapWarpBar(new Rect(0,0,230,23));Check(p._mapWarpActiveId==second.Id,"bar previous from origin selects final landmark");
            p.Config.Fail=true;var failedActive=p._mapWarpActiveId;before=root.position;p.ReturnMapWarpOrigin();
            Check(p._mapWarpActiveId==failedActive&&Near(root.position,before),"failed return rolls back placement and retains active landmark");p.Config.Fail=false;
            p._roomOverlayLoading=true;p.StepMapWarpPoint(1);p.ReturnMapWarpOrigin();Check(p._mapWarpActiveId==failedActive,"loading rejects navigation and return commands");p._roomOverlayLoading=false;
            p._vamHotelPivot=new GameObject();p.ReturnMapWarpOrigin();Check(p._mapWarpReturnValues==null&&p._mapWarpActiveId==null,"reloaded map root never inherits old return placement");
            foreach(string scene in new[]{"island","road","sandbox"}){p._roomOverlaySceneName=scene;p.TryMapWarpContext(out key,out root,out _);Check(key.StartsWith("special:"),scene+" has an independent stable map identity");}
            SourceMapTests(directory);
            string damaged="{broken file";File.WriteAllText(p.MapWarpPath,damaged);p.LoadMapWarpPoints();p.RegisterMapWarpPoint(key,false,null);
            Check(p._mapWarpDocument==null&&File.ReadAllText(p.MapWarpPath)==damaged,"corrupt file disables writes and is preserved byte-for-byte");
            var invalid=new MapWarpDocument();invalid.Points.Add(new MapWarpPoint{Id="a",MapKey="b",Name="c",X=float.NaN});
            try{MapWarpPointStore.Save(Path.Combine(directory,"invalid.json"),invalid);throw new Exception("accepted invalid coordinates");}catch(InvalidDataException){}
            Check(!File.Exists(Path.Combine(directory,"invalid.json")),"nonfinite coordinates fail before any file write");
            Check(camera.Writes==0,"all registration, warp and error paths leave camera untouched");
            string legacy=Path.Combine(directory,"legacy.json");
            File.WriteAllText(legacy,"{\"Version\":1,\"Points\":[{\"Id\":\"old\",\"MapKey\":\"old-map\",\"Name\":\"old\",\"X\":3,\"Y\":2,\"Z\":1,\"Scale\":1}]}");
            var legacyPoint=MapWarpPointStore.Load(legacy).Points[0];
            Check(!legacyPoint.HasRotationCenter&&legacyPoint.X==3,"existing files without center fields retain landmarks and default center to location");
            Console.WriteLine("TOTAL "+checks+" PASSED; live KKS remains unverified");
        }

        private static void SourceMapTests(string directory)
        {
            var p = new Plugin { _pluginDir = directory, _roomOverlaySceneName = null };
            p.LoadMapWarpPoints();
            p.LastReservedMap.infoDic[42] = new MapInfo { AssetBundleName = "map\\room", AssetName = "bedroom" };
            var original = new Vector3(3, 7, -9);
            p.LastReservedMap.mapRoot.transform.position = original;
            p.settings.X.Value = 12;
            Check(p.TryMapWarpContext(out string key, out var root, out bool hotel) && !hotel,
                "native source map supports placement and H-location context without an overlay");
            Check(key == "bundle:map/room\nscene:bedroom" && Near(root.position, original + new Vector3(12,0,0)),
                "source map has stable native identity and applies its saved placement once");
            p.TryMapWarpContext(out _, out _, out _);
            Check(Near(root.position, original + new Vector3(12,0,0)), "repeated context reads never accumulate source offsets");
            p._mapWarpName = "通常マップの地点";
            p.RegisterMapWarpPoint(key, false, null);
            var point = p._mapWarpDocument.Points.Last();
            p.settings.X.Value = 100; p.ApplyRoomOverlayPosition();
            var before = root.position;
            GUI.Click = "▷"; p.DrawMapWarpBar(new Rect(0,0,230,23));
            Check(p._mapWarpActiveId == point.Id && Near(root.TransformPoint(new Vector3(point.X,point.Y,point.Z)),p._femaleChara.transform.position),
                "source-map registered point is reachable from restored triangle control");
            GUI.Click = "□"; p.DrawMapWarpBar(new Rect(0,0,230,23));
            Check(Near(root.position,before), "source-map square restores placement before navigation");
            p.RestoreSourceMapPlacement();
            Check(Near(root.position, original), "session cleanup restores native source transform without changing saved settings");
            p.allowed = false;
            Check(!p.TryMapWarpContext(out _,out _,out _), "gate denial also blocks native source placement");
        }

        private static void TerrainGridTests()
        {
            var heights = new float[257,257];
            for(int z=0;z<257;z++)for(int x=0;x<257;x++)heights[z,x]=(x+z)/512f;
            var size=new Vector3(2252.8f,370f,2252.8f);
            var high=AiIslandTerrainGrid.Build(heights,size,0,0,64,1);
            var low=AiIslandTerrainGrid.Build(heights,size,0,0,64,4);
            var adjacent=AiIslandTerrainGrid.Build(heights,size,64,0,64,1);
            Check(high.Triangles.All(i=>i>=0&&i<high.Vertices.Length)&&low.Triangles.All(i=>i>=0&&i<low.Vertices.Length),"terrain detailed and distant tiles contain valid triangles");
            Check(low.Triangles.Length<high.Triangles.Length/4,"terrain distant LOD reduces geometry");
            bool seams=true;
            for(int z=0;z<65;z++)seams &= Near(high.Vertices[z*65+64],adjacent.Vertices[z*65]);
            Check(seams,"terrain tile borders match exactly");
            Check(Near(high.Vertices[64*65+64],new Vector3(size.x/4,size.y/4,size.z/4)),"full heightfield shape is preserved before one root unit conversion");
            var rotation=Quaternion.Euler(0,54.85185f,0);var actor=new Vector3(10,2,-5);var scale=Vector3.one*.1f;
            var local=high.Vertices[32*65+32];var target=MapWarpGeometry.RootForPoint(local,actor,rotation,scale);
            Check(Near(target+rotation*Vector3.Scale(local,scale),actor),"rotated terrain landmark and collider mesh use the same map transform");
            Check(high.UV.Length==high.Vertices.Length&&high.Normals.Length==high.Vertices.Length&&high.Tangents.Length==high.Vertices.Length,"terrain mesh retains complete CTS texture and normal channels");
            Check(high.Vertices.Skip(65*65).All(v=>v.y==-size.y),"terrain skirts cover mismatched detailed/distant tile edges");
        }
    }
}
