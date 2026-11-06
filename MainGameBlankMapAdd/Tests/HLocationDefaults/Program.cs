using System;
using System.IO;
using System.Linq;
using System.Text;
using MainGameBlankMapAdd;
using UnityEngine;

class Program
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    static void Reject(Action action,string name){try{action();}catch{checks++;return;}throw new Exception(name);}
    static HLocationPreset Place(string id,string map="map-a")=>new HLocationPreset{Id=id,MapKey=map,Name=id,Position=new[]{1f,2f,3f},Rotation=new[]{0f,90f,0f},Center=new float[3],CameraPosition=new float[3],CameraRotation=new float[3],CameraFocus=new float[3],Fov=45};
    static void Main()
    {
        Console.WriteLine("Testing H-location defaults, shared hotel identity, and one-shot lifecycle");
        CheckOriginNavigation();
        int collected=0,written=0;
        MainGameBlankMapAdd.Diagnostics.HLocationTrace.Write(false,()=>{collected++;return "off";},_=>written++);
        Check(collected==0&&written==0,"diagnostics OFF skips state collection and output");
        MainGameBlankMapAdd.Diagnostics.HLocationTrace.Write(true,()=>{collected++;return "on";},_=>written++);
        Check(collected==1&&written==1,"diagnostics ON collects and writes");
        MainGameBlankMapAdd.Diagnostics.HLocationTrace.Write(false,()=>{collected++;return "off again";},_=>written++);
        Check(collected==1&&written==1,"diagnostics disabling stops subsequent work");
        MainGameBlankMapAdd.Diagnostics.HLocationTrace.Write(true,()=>throw new IOException("collect failure"),_=>written++);
        MainGameBlankMapAdd.Diagnostics.HLocationTrace.Write(true,()=>"sink failure",_=>throw new IOException("disk failure"));
        Check(written==1,"diagnostic failures do not escape into save/restore");
        string repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
        string temp=Path.Combine(repo,".build","h-location-defaults",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        string path=Path.Combine(temp,"places.json");
        var doc=new HLocationDocument();doc.Presets.Add(Place("one"));doc.Presets.Add(Place("two"));doc.Presets.Add(Place("other","map-b"));
        HLocationPresetStore.Save(path,doc);
        Check(!File.ReadAllText(path).Contains("IsDefault"),"old shape omits unselected default");
        var loaded=HLocationPresetStore.Load(path,"missing");Check(loaded.DefaultFor("map-a")==null,"old data has no default");
        var first=loaded.WithDefault("map-a","one",true);Check(loaded.DefaultFor("map-a")==null&&first.DefaultFor("map-a").Id=="one","copy before commit");
        first=first.WithDefault("map-b","other",true).WithDefault("map-a","two",true);
        Check(first.DefaultFor("map-a").Id=="two"&&first.DefaultFor("map-b").Id=="other","one default per map");
        HLocationPresetStore.Save(path,first);loaded=HLocationPresetStore.Load(path,"missing");
        Check(loaded.DefaultFor("map-a").Id=="two","restart persists default");
        loaded.Presets.Single(p=>p.Id=="two").Name="renamed";loaded.Presets.Single(p=>p.Id=="two").Position[0]=8;
        HLocationPresetStore.Save(path,loaded);Check(HLocationPresetStore.Load(path,"missing").DefaultFor("map-a").Name=="renamed","edit preserves default");
        var cleared=loaded.WithDefault("map-a","two",false);Check(cleared.DefaultFor("map-a")==null,"clear default");
        cleared.Presets.RemoveAll(p=>p.Id=="other");Check(cleared.DefaultFor("map-b")==null,"deletion clears reference");
        var invalid=loaded.Clone();invalid.Presets[0].IsDefault=true;Reject(()=>invalid.Validate(),"duplicate default rejected");
        invalid=loaded.Clone();invalid.Presets[0].Legacy=true;Reject(()=>invalid.WithDefault("map-a","one",true),"legacy cannot become default before capture");
        string overlay="bundle:map/scene/canon_hotel_201.unity3d\nscene:CanonPinkRoom201Map";
        var hotel=new HLocationDocument();hotel.Presets.Add(Place("overlay",overlay));hotel.Presets.Add(Place("native","source:hotel-room-201"));
        hotel=hotel.WithDefault("source:hotel-room-201","overlay",true);
        Check(hotel.DefaultFor(overlay).Id=="overlay"&&hotel.Presets[0].MapKey==overlay&&hotel.Presets[1].MapKey=="source:hotel-room-201","hotel modes share default without rewriting keys");
        Check(MapLocationNavigation.Ids(new MapWarpDocument(),hotel,"source:hotel-room-201").Count==2,"hotel navigation sees both sets");
        var badHotel=hotel.Clone();badHotel.Presets[1].IsDefault=true;Reject(()=>badHotel.Validate(),"alias rooms cannot have conflicting defaults");
        Check(!HLocationMapIdentity.Same(overlay,"source:hotel-room-reception-suite"),"different rooms remain separate");
        Check(HLocationMapIdentity.Same("source:hotel-room-reception-suite","bundle:map/scene/canon_reception_suite.unity3d\nscene:CanonReceptionSuiteMap"),"reception alias exact");
        var p=new Plugin{_hLocations=loaded,HPlacePath=path};
        var noDefault=new Plugin{_hLocations=doc,HPlacePath=path};noDefault.Ready();
        p.Ready();
        Check(p.PoseRestoreRequests==1&&noDefault.PoseRestoreRequests==1,"all maps use the same pose/default selector scheduling");
        p.SetDefault("one",true);
        Check(p.Moves.Count==0,"changing map default does not move immediately");
        p=new Plugin{_hLocations=loaded,HPlacePath=Path.Combine(temp,"blocked"),OriginFailure=true};Directory.CreateDirectory(p.HPlacePath);
        p.SetDefault("one",true);Check(p._hLocations.DefaultFor("map-a").Id=="two","failed default write preserves previous in-memory choice");
        Check(HLocationPresetStore.Load(path,"missing").DefaultFor("map-a").Id=="one","failed independent write leaves last successful file unchanged");
        string runtime=File.ReadAllText(Path.Combine(repo,"MainGameBlankMapAdd","Plugin.HLocationRuntime.cs"));
        Check(runtime.Contains("automatic?\"default\":\"recall\""),"auto recall uses existing ApplyHPlace and notifications");
        foreach(string file in new[]{"Plugin.Session.cs","Plugin.RoomOverlay.cs","Plugin.VamHotelOverlay.cs","Plugin.MapSandboxOverlay.cs","Plugin.AiIsland.cs","Plugin.StreetlightRoad.cs"})
            Check(File.ReadAllText(Path.Combine(repo,"MainGameBlankMapAdd",file)).Contains("QueueDefaultHPlace();"),"ready hook "+file);
        string live="F:/kks/BepInEx/plugins/canon_plugins/MainGameBlankMapAdd/HLocationPresets.json";
        if(File.Exists(live)){
            var existing=HLocationPresetStore.Load(live,"missing");
            int aliases=existing.Presets.Count(x=>HLocationMapIdentity.Same(x.MapKey,overlay));
            Check(MapLocationNavigation.Ids(new MapWarpDocument(),existing,"source:hotel-room-201").Count==aliases,"actual installed hotel records visible across modes without modifying them");
        }
        Console.WriteLine("PASS "+checks+" default H-location checks");
    }

    static void CheckOriginNavigation()
    {
        var origin=new MapWarpPoint{Id="origin",MapKey="map-a",Name="元の場所",IsOrigin=true,Placement=new float[6]};
        var oldPlaces=new MapWarpDocument();oldPlaces.Points.Add(origin);
        var places=new HLocationDocument();var only=Place("only");only.IsDefault=true;places.Presets.Add(only);
        Check(MapLocationNavigation.Ids(oldPlaces,places,"map-a").SequenceEqual(new[]{"only"}),"one H preset plus automatic origin has one arrow candidate");
        foreach(int direction in new[]{-1,1}){
            Check(MapLocationNavigation.Next(oldPlaces,places,"map-a","only",direction)=="only","one preset never alternates with origin direction "+direction);
            Check(MapLocationNavigation.Next(oldPlaces,places,"map-a","origin",direction)=="only","arrow from origin selects the only preset direction "+direction);
        }
        Check(MapWarpOrigin.Find(oldPlaces,"map-a")==origin&&oldPlaces.Points.Count==1,"origin remains saved and available for square button");
        Check(MapLocationNavigation.Next(oldPlaces,new HLocationDocument(),"map-a",null,1)==null,"origin alone is not an arrow destination");
        Check(MapLocationNavigation.Next(null,null,"map-a",null,1)==null,"missing documents have no destinations");
        places.Presets.Add(Place("second"));places.Presets.Add(Place("other","map-b"));
        Check(MapLocationNavigation.Next(oldPlaces,places,"map-a","only",1)=="second","forward selects next H preset");
        Check(MapLocationNavigation.Next(oldPlaces,places,"map-a","second",1)=="only","forward wraps without origin");
        Check(MapLocationNavigation.Next(oldPlaces,places,"map-a","only",-1)=="second","backward wraps without origin");
        oldPlaces.Points.Add(new MapWarpPoint{Id="registered",MapKey="map-a",Name="手動登録地点"});
        Check(MapLocationNavigation.Ids(oldPlaces,places,"map-a").SequenceEqual(new[]{"registered","only","second"}),"manual locations retained and other maps excluded");
        string directory="F:/kks/BepInEx/plugins/canon_plugins/MainGameBlankMapAdd";
        string warp=Path.Combine(directory,"MapWarpPoints.json"),presets=Path.Combine(directory,"HLocationPresets.json");
        if(File.Exists(warp)&&File.Exists(presets)){
            var installedWarp=MapWarpPointStore.Load(warp);var installedPlaces=HLocationPresetStore.Load(presets,"missing");
            foreach(var installedOrigin in installedWarp.Points.Where(x=>x.IsOrigin)){
                var ids=MapLocationNavigation.Ids(installedWarp,installedPlaces,installedOrigin.MapKey);
                Check(!ids.Contains(installedOrigin.Id),"installed origin excluded: "+installedOrigin.MapKey);
                if(ids.Count==1){
                    Check(MapLocationNavigation.Next(installedWarp,installedPlaces,installedOrigin.MapKey,ids[0],1)==ids[0],"installed single preset remains selected");
                    Check(MapLocationNavigation.Next(installedWarp,installedPlaces,installedOrigin.MapKey,ids[0],-1)==ids[0],"installed single preset remains selected backwards");
                }
            }
        }
    }
}
