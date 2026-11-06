using System;
using System.IO;
using System.Linq;
using UnityEngine;
using CanonPlugins.Shared;
using Object=UnityEngine.Object;
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private static int checks;
        private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
        private static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<.00001f;
        private static void Finish(Plugin p){while(p.pending.MoveNext()){}p.pending=null;}
        private static HLocationPreset Sample(string id="sample",string map="island")=>new HLocationPreset{Id=id,MapKey=map,Name="場所",Position=new[]{1f,2f,3f},Rotation=new float[3],Center=new[]{1f,2f,3f},CameraPosition=new[]{1f,3f,-4f},CameraFocus=new[]{1f,2f,3f},CameraRotation=new float[3],Fov=47};
        public static void Main()
        {
            string dir=Path.Combine(@"J:\temp\h-location-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var document=new HLocationDocument();string name=document.AllocateName("a");document.Presets.Add(Sample("one","a"));document.Presets[0].Name=name;
            Check(name=="H場所001"&&document.AllocateName("a")=="H場所002"&&document.AllocateName("b")=="H場所001","map-specific automatic numbering");
            document.Presets.Clear();string file=Path.Combine(dir,"HLocationPresets.json");HLocationPresetStore.Save(file,document);
            document=HLocationPresetStore.Load(file,"");Check(document.AllocateName("a")=="H場所003","deleted numbers remain reserved across reload");
            var preset=Sample();document.Presets.Add(preset);HLocationPresetStore.SaveImage(file,Path.Combine(dir,"images"),document,preset,new byte[]{1,2,3});
            string saved=File.ReadAllText(file),image=preset.Image;Check(File.Exists(Path.Combine(dir,"images",image)),"JSON commits with its image");
            preset.Name="改名";HLocationPresetStore.Save(file,document);Check(preset.Image==image,"rename leaves image identity unchanged");
            int images=Directory.GetFiles(Path.Combine(dir,"images")).Length;
            using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){
                try{HLocationPresetStore.SaveImage(file,Path.Combine(dir,"images"),document,preset,new byte[]{4});throw new Exception("accepted locked file");}catch(IOException){}
            }
            Check(preset.Image==image&&Directory.GetFiles(Path.Combine(dir,"images")).Length==images,"failed JSON replacement retains original image and removes incomplete image");
            var legacy=new MapWarpDocument();legacy.Points.Add(new MapWarpPoint{Id="old",MapKey="a",Name="旧場所",X=5,Y=6,Z=7,RotationX=0,RotationY=0,RotationZ=0,HasRotationCenter=true,CenterX=8,CenterY=0,CenterZ=0});
            var imported=HLocationDocument.Import(legacy).Presets[0];Check(imported.Legacy&&imported.Position[0]==5&&imported.Center[0]==8&&legacy.Points[0].X==5,"legacy import preserves location and center without invented camera or direction");
            File.WriteAllText(file,"broken");try{HLocationPresetStore.Load(file,"");throw new Exception("accepted corrupt file");}catch(System.Runtime.Serialization.SerializationException){}
            Check(File.ReadAllText(file)=="broken","failed load never overwrites existing data");
            var navPlaces=new HLocationDocument();navPlaces.Presets.Add(Sample("first","a"));navPlaces.Presets.Add(Sample("second","a"));navPlaces.Presets.Add(Sample("foreign","b"));
            Check(MapLocationNavigation.Next(legacy,navPlaces,"a","old",1)=="first","next arrow reaches screenshot H location after original registered place");
            Check(MapLocationNavigation.Next(legacy,navPlaces,"a","first",1)=="second","next arrow advances between added H locations");
            Check(MapLocationNavigation.Next(legacy,navPlaces,"a","second",1)=="old","next arrow wraps to original place");
            Check(MapLocationNavigation.Next(legacy,navPlaces,"a","old",-1)=="second","previous arrow wraps to last H location");
            Check(!MapLocationNavigation.Ids(legacy,navPlaces,"a").Contains("foreign"),"arrows exclude locations belonging to other maps");
            navPlaces.Presets.Add(Sample("old","a"));
            Check(MapLocationNavigation.Ids(legacy,navPlaces,"a").Distinct().Count()==MapLocationNavigation.Ids(legacy,navPlaces,"a").Count,"legacy-imported H locations do not create duplicate arrow entries");
            Check(MapLocationNavigation.Next(legacy,navPlaces,"missing",null,1)==null,"empty map navigation does nothing");
            var p=new Plugin{_pluginDir=Path.Combine(dir,"runtime")};Directory.CreateDirectory(p._pluginDir);
            p._femaleChara=new ChaControl();p._femaleChara.transform.position=new Vector3(1,0,2);
            p._femaleChara.objTop=new GameObject{transform=p._femaleChara.transform};
            var male=new ChaControl();male.transform.position=new Vector3(2,0,2);PluginSessionGate.Females.Clear();PluginSessionGate.Females.Add(p._femaleChara);PluginSessionGate.Male=male;
            p.root.position=new Vector3(7,-2,4);p.root.rotation=Quaternion.Euler(0,54,0);p.root.localScale=new Vector3(.1f,.1f,.1f);
            var camera=PluginSessionGate.HScene.flags.ctrlCamera;camera.Data=new BaseCameraControl_Ver2.CameraData{Pos=new Vector3(10,2,15),Dir=new Vector3(0,0,-5),Rot=new Vector3(0,20,0),Fov=47};camera.SetCameraData(camera.Data);
            var cameraOwner=MainGameCameraControl.Plugin.TestCreate(camera);
            BepInEx.Bootstrap.Chainloader.PluginInfos["com.kks.maingame.cameracontrol"]=new BepInEx.Bootstrap.PluginInfo{Instance=cameraOwner};
            cameraOwner.TestLease(1);p.LoadHLocations();p.BeginHPlaceCapture();
            Check(!p._hPlaceBusy&&p._hLocations.Presets.Count==0,"another camera owner prevents placement before any mutation");cameraOwner.TestLease(0);
            Vector3 originalFemale=p._femaleChara.transform.position,originalMale=male.transform.position,originalCamera=camera.thisCmaera.transform.position;
            var focus=camera.transBase.TransformPoint(camera.Data.Pos);p.LoadHLocations();p.BeginHPlaceCapture();
            Check(p._hPlaceBusy&&p.pending.MoveNext(),"single operation begins bounded placement and capture");
            Check(Near(p._femaleChara.transform.position,focus),"female moves to captured camera focus");
            Check(MathF.Abs((male.transform.position-p._femaleChara.transform.position).sqrMagnitude-1)<.0001f,"participants retain relative distance");
            Check(Near(camera.thisCmaera.transform.position,originalCamera)&&camera.thisCmaera.fieldOfView==47,"placement preserves camera world position and FOV across base synchronization");
            var facing=Vector3.ProjectOnPlane(camera.thisCmaera.transform.position-focus,Vector3.up).normalized;
            Check(Near(Vector3.ProjectOnPlane(p._femaleChara.transform.forward,Vector3.up).normalized,facing),"female faces camera horizontally without changing pose");
            Object.Canvases=new[]{new Canvas(),new Canvas{enabled=false}};Finish(p);
            Check(p._hLocations.Presets.Count==1&&p._hLocations.Presets[0].Name=="H場所001"&&!p._hPlaceBusy,"capture saves numbered preset without name input");
            Check(cameraOwner.TestAdopted,"camera plugin adopts native view and stops previous transition and bone follow without changing presets");
            Check(camera.thisCmaera.RenderedWithoutUi&&Object.Canvases[0].enabled&&!Object.Canvases[1].enabled&&RenderTexture.Borrowed==0,"capture excludes UI and restores UI and render resources");
            var created=p._hLocations.Presets[0];Check(Near(HV(created.Position),HV(created.Center)),"new location is also its rotation center");
            p.StopHLocations();Check(Near(p._femaleChara.transform.position,originalFemale)&&Near(male.transform.position,originalMale)&&Near(camera.thisCmaera.transform.position,originalCamera),"map/session cleanup restores actors and camera");
            p.RecallHPlace(created.Id);Check(Near(p._femaleChara.transform.position,focus)&&Near(camera.thisCmaera.transform.position,originalCamera),"image recall restores saved place and camera");
            var arrowView=camera.GetCameraData();arrowView.Pos+=Vector3.one;camera.SetCameraData(arrowView);var arrowCamera=p.ReadHPlaceCamera();
            p.RecallHPlace(created.Id,false);Check(Near(camera.thisCmaera.transform.position,arrowCamera.Position)&&Near(p._femaleChara.transform.position,focus),"arrow recall moves actors while preserving current camera view");
            p._femaleChara.transform.position=new Vector3(100,0,100);male.transform.position=new Vector3(101,0,100);p.ReapplyHPlaceAfterPose();
            Check(Near(p._femaleChara.transform.position,focus)&&MathF.Abs((male.transform.position-p._femaleChara.transform.position).sqrMagnitude-1)<.0001f,"pose event reapplies location without breaking participant offsets");
            p.StopHLocations();camera.thisCmaera.FailRender=true;p.BeginHPlaceCapture();Finish(p);
            Check(p._hLocations.Presets.Count==1&&!p._hPlaceBusy&&Near(p._femaleChara.transform.position,originalFemale),"render failure rolls back placement without partial preset");camera.thisCmaera.FailRender=false;
            string runtimeSaved=File.ReadAllText(p.HPlacePath);
            using(var locked=new FileStream(p.HPlacePath,FileMode.Open,FileAccess.Read,FileShare.None)){p.BeginHPlaceCapture();Finish(p);}
            Check(p._hLocations.Presets.Count==1&&File.ReadAllText(p.HPlacePath)==runtimeSaved&&!p._hPlaceBusy&&Near(p._femaleChara.transform.position,originalFemale),"runtime file failure restores placement and keeps previous document");
            p.BeginHPlaceCapture();p.pending.MoveNext();p.StopHLocations();Finish(p);
            Check(p._hLocations.Presets.Count==1&&Near(p._femaleChara.transform.position,originalFemale),"map change during pending capture invalidates save and restores actors");
            p.BeginHPlaceCapture();p.pending.MoveNext();PluginSessionGate.Allowed=false;Finish(p);
            Check(p._hLocations.Presets.Count==1&&Near(p._femaleChara.transform.position,originalFemale),"gate revocation before capture prevents commit and rolls back");PluginSessionGate.Allowed=true;
            p.StopHLocations();p.loading=true;p.BeginHPlaceCapture();Check(!p._hPlaceBusy,"loading blocks new placement");p.loading=false;
            p.BeginHPlaceEdit(created.Id);
            Check(p._hPlaceDraft!=null&&Near(p._femaleChara.transform.position,focus),"edit opens saved location in provisional view");
            string diskBeforeEdit=File.ReadAllText(p.HPlacePath);
            int sliderCount=GUILayout.SliderCalls;p.DrawHLocationEditor();
            Check(GUILayout.SliderCalls-sliderCount==9&&File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"editor provides nine sliders and merely displaying them never changes saved data");
            float sliderBase=p._hPlaceSliderCenters[0];
            GUILayout.SliderValues.Enqueue(sliderBase+2);Input.Held=true;GUILayout.AcquireSlider=true;p.DrawHLocationEditor();
            Check(p._hPlaceDraft.Position[0]==sliderBase+2&&p._hPlaceSliderCenters[0]==sliderBase&&MainGameUiInputCapture.UiInputCaptureApi.SliderActive,"drag previews coordinate while keeping fixed bounds and shared input capture");
            GUILayout.SliderValues.Enqueue(sliderBase+3);p.DrawHLocationEditor();
            Check(p._hPlaceDraft.Position[0]==sliderBase+3&&p._hPlaceSliderCenters[0]==sliderBase&&MainGameUiInputCapture.UiInputCaptureApi.SliderActive,"continued drag keeps original range and capture outside panel");
            Input.Held=false;p.DrawHLocationEditor();Check(!MainGameUiInputCapture.UiInputCaptureApi.SliderActive&&GUIUtility.hotControl==0,"mouse release frees only owned slider capture");
            p.SetHPlaceTypedNumber(0,"1000000");p.PreviewHPlaceEdit();
            Check(p._hPlaceDraft.Position[0]==1000000&&p._hPlaceSliderCenters[0]==1000000,"typing a large coordinate updates preview and recenters slider");
            Check(p.SetHPlaceSliderWidth(0,1000000)&&p._hPlaceSliderCenters[0]==1000000,"custom range supports large-coordinate coarse adjustment");
            p.SetHPlaceTypedNumber(0,"-");p.PreviewHPlaceEdit();
            Check(p._hPlaceEditNumbers[0]=="-"&&!p._hPlaceEditValid,"partial numeric input is retained without invalid preview");
            p.SetHPlaceTypedNumber(0,"1000000");p.PreviewHPlaceEdit();
            Check(!p.SetHPlaceSliderWidth(0,-1)&&!p.SetHPlaceSliderWidth(0,float.NaN),"invalid adjustment widths do not replace slider bounds");
            p.CloseHPlaceEdit();var editorCamera=p.ReadHPlaceCamera();p.BeginHPlaceEdit(created.Id);
            Check(Near(camera.thisCmaera.transform.position,editorCamera.Position),"opening editor preserves current world camera instead of recalling thumbnail view");
            var freeCamera=camera.GetCameraData();freeCamera.Pos+=Vector3.one;camera.SetCameraData(freeCamera);editorCamera=p.ReadHPlaceCamera();
            p._hPlaceDraft.Name="編集した名前";p._hPlaceEditNumbers[0]=(HV(p._hPlaceDraft.Position).x+5).ToString(System.Globalization.CultureInfo.InvariantCulture);p.PreviewHPlaceEdit();
            Check(!Near(p._femaleChara.transform.position,focus)&&File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"coordinate editing previews without writing stored preset");
            Check(Near(camera.thisCmaera.transform.position,editorCamera.Position),"coordinate preview preserves editor camera across character base changes");
            p.CloseHPlaceEdit();Check(p._hPlaceDraft==null&&Near(p._femaleChara.transform.position,originalFemale)&&File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"close discards draft and restores pre-edit actors and camera");
            p.root.localScale=new Vector3(.1f,.2f,.3f);p.BeginHPlaceEdit(created.Id);
            var editBefore=HV(p._hPlaceDraft.Position);var editCenter=editBefore+new Vector3(5,0,0);
            for(int i=0;i<3;i++)p._hPlaceEditNumbers[6+i]=HA(editCenter)[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            p.PreviewHPlaceEdit();var centerWorld=p.root.TransformPoint(editCenter);float radiusBefore=(p._femaleChara.transform.position-centerWorld).sqrMagnitude;
            p._hPlaceEditNumbers[4]=(p._hPlaceDraft.Rotation[1]+45).ToString(System.Globalization.CultureInfo.InvariantCulture);p.PreviewHPlaceEdit();
            Check(MathF.Abs((p._femaleChara.transform.position-centerWorld).sqrMagnitude-radiusBefore)<.0001f,"actual editor rotates about per-place center under nonuniform map scale");
            Check(File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"editing a center leaves saved presets unchanged until confirm");
            p.CloseHPlaceEdit();p.root.localScale=new Vector3(.1f,.1f,.1f);
            p.BeginHPlaceEdit(created.Id);p._hPlaceEditNumbers[0]="-";p.PreviewHPlaceEdit();
            Check(!p._hPlaceEditValid&&File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"incomplete numeric input cannot modify saved values");
            p._hPlaceEditNumbers[0]=p._hPlaceDraft.Position[0].ToString(System.Globalization.CultureInfo.InvariantCulture);p.PreviewHPlaceEdit();
            p._hPlaceDraft.Name="確定した名前";
            string confirmImage=p._hLocations.Presets[0].Image;
            var confirmBytes=File.ReadAllBytes(Path.Combine(p.HPlaceImageDir,confirmImage));
            var confirmCamera=p.ReadHPlaceCamera();camera.thisCmaera.FailRender=true;
            using(var locked=new FileStream(p.HPlacePath,FileMode.Open,FileAccess.Read,FileShare.None))p.SaveHPlaceEdit();
            Check(p._hPlaceDraft!=null&&File.ReadAllText(p.HPlacePath)==diskBeforeEdit,"failed placement-only save retains draft and original document");
            p.SaveHPlaceEdit();camera.thisCmaera.FailRender=false;
            var confirmed=HLocationPresetStore.Load(p.HPlacePath,p.MapWarpPath).Presets[0];
            Check(p._hPlaceDraft==null&&confirmed.Name=="確定した名前","confirm saves placement without requiring a render");
            Check(confirmed.Image==confirmImage&&File.ReadAllBytes(Path.Combine(p.HPlaceImageDir,confirmImage)).SequenceEqual(confirmBytes),"placement-only save preserves existing thumbnail bytes and identity");
            Check(Near(camera.thisCmaera.transform.position,confirmCamera.Position),"placement-only save preserves current editing camera");
            p.BeginHPlaceEdit(created.Id);
            var retakeData=camera.GetCameraData();retakeData.Pos+=new Vector3(3,1,-2);retakeData.Rot=new Vector3(10,70,0);retakeData.Fov=61;camera.SetCameraData(retakeData);
            var retakeCamera=p.ReadHPlaceCamera();string priorImage=p._hLocations.Presets[0].Image;
            GUILayout.Click="画像を撮って保存";Event.current.type=EventType.Repaint;p.DrawHLocationEditor();
            Check(!p._hPlaceBusy&&p._hPlaceUiAction!=null,"editor photo button queues capture without changing the layout during input");
            Event.current.type=EventType.Layout;p.ApplyPendingHPlaceUi();Finish(p);
            var retaken=HLocationPresetStore.Load(p.HPlacePath,p.MapWarpPath).Presets[0];
            Check(p._hPlaceDraft==null&&retaken.Id==created.Id&&retaken.Image!=priorImage&&retaken.Fov==61&&Near(HV(retaken.CameraPosition),p.root.InverseTransformPoint(retakeCamera.Position)),"editor photo button replaces same preset image and stores current camera instead of old saved view");
            Check(Near(camera.thisCmaera.transform.position,retakeCamera.Position)&&camera.thisCmaera.RenderedWithoutUi,"retake keeps current camera framing and excludes UI");
            p.BeginHPlaceEdit(created.Id);string beforeRetakeFailure=File.ReadAllText(p.HPlacePath);camera.thisCmaera.FailRender=true;
            p.BeginHPlaceCapture(p._hPlaceDraft,useCurrentCamera:true);Finish(p);camera.thisCmaera.FailRender=false;
            Check(p._hPlaceDraft!=null&&File.ReadAllText(p.HPlacePath)==beforeRetakeFailure&&!p._hPlaceBusy,"failed retake preserves saved photo and leaves editor available to retry");
            p.CloseHPlaceEdit();
            p.StopHLocations();Check(Near(p._femaleChara.transform.position,originalFemale),"confirmed edit retains original session rollback point");
            p.BeginHPlaceEdit(created.Id);p.DeleteHPlaceEdit();Check(p._hLocations.Presets.Count==0&&HLocationPresetStore.Load(p.HPlacePath,p.MapWarpPath).Presets.Count==0,"delete is committed through editor without leaving draft");
            p.BeginHPlaceCapture();var firstPending=p.pending;p.BeginHPlaceCapture();Check(ReferenceEquals(firstPending,p.pending),"repeated click while saving does not start second capture");Finish(p);
            Check(p._hLocations.Presets[0].Name=="H場所002","deleting and saving another preset never reuses previous number");
            p.StopHLocations();p.QueueHPlaceUi(()=>p.BeginHPlaceCapture());Event.current.type=EventType.Repaint;p.ApplyPendingHPlaceUi();
            Check(!p._hPlaceBusy,"GUI action does not change layout tree during repaint");
            Event.current.type=EventType.Layout;p.ApplyPendingHPlaceUi();
            Check(!p._hPlaceBusy,"stale unbound UI action is rejected");
            var rotated=HLocationGeometry.RotateAround(new Vector3(2,0,0),new Vector3(1,0,0),Quaternion.Euler(0,90,0));
            Check(Near(rotated,new Vector3(1,0,-1)),"edit rotation uses saved center rather than map origin");
            Console.WriteLine("TOTAL "+checks+" PASSED; native rendering and game interaction remain unverified");
        }
    }
}
