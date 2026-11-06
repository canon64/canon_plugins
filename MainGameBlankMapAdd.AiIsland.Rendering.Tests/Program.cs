using System.Reflection;
using MainGameBlankMapAdd;
using UnityEngine;

int checks=0;
void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
void Call(object obj,string method)
{
    obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);
}
bool Near(float a,float b)=>MathF.Abs(a-b)<.0001f;

var original=new TerrainData{size=new Vector3(2048,512,2048),treeInstances=new[]{new TreeInstance{widthScale=2,heightScale=3,position=new Vector3(.3f,.2f,.1f)}},
    detailPrototypes=new[]{new DetailPrototype{minWidth=1,maxWidth=2,minHeight=3,maxHeight=4}}};
var material=new Material(null);material.SetFloat("_Texture_1_Tiling",7);
var terrain=new Terrain{terrainData=original,materialTemplate=material};
var collider=new TerrainCollider{terrainData=original};terrain.Components.Add(collider);
var light=new Light{range=50,shadowNearPlane=1};
var flame=new ParticleSystem{isPlaying=true,Mode=ParticleSystemScalingMode.Local};
var idle=new ParticleSystem{isPlaying=false,Mode=ParticleSystemScalingMode.Shape};
var geometry=new AiIslandGeometry();geometry.Components.AddRange(new Component[]{terrain,light,flame,idle});geometry.ConvertUnits(.1f);
Check(Near(geometry.transform.localScale.x,.1f)&&Near(terrain.terrainData.size.x,204.8f),"mesh root and native terrain use 1/10 units");
Check(ReferenceEquals(collider.terrainData,terrain.terrainData),"terrain collision shares converted geometry");
Check(original.size.x==2048&&original.treeInstances[0].widthScale==2&&material.GetFloat("_Texture_1_Tiling")==7,"source terrain, vegetation and material remain unchanged");
Check(Near(terrain.terrainData.treeInstances[0].widthScale,.2f)&&Near(terrain.terrainData.detailPrototypes[0].maxHeight,.4f),"terrain trees and details follow corrected scale");
Check(terrain.terrainData.treeInstances[0].position.x==.3f,"normalized vegetation placement preserved");
Check(Near(terrain.materialTemplate.GetFloat("_Texture_1_Tiling"),.7f)&&Near(light.range,5),"terrain tiling and light ranges converted");
var converted=terrain.terrainData;Call(geometry,"OnDestroy");
Check(converted.Destroyed&&!original.Destroyed,"converted data released without destroying source");
Check(flame.Mode==ParticleSystemScalingMode.Hierarchy&&flame.isPlaying&&flame.Cleared,"playing flame uses converted parent scale and clears oversized particles");
Check(idle.Mode==ParticleSystemScalingMode.Hierarchy&&!idle.isPlaying&&idle.Cleared,"idle effects stay idle after scale conversion");
Check(flame.Culling==ParticleSystemCullingMode.Automatic,"offscreen effects use native automatic simulation culling");
var waterSource=new Material(new Shader{name="Lux Water/WaterSurface"}){name="m_ai_mi_umi01_00"};
waterSource.SetVector("_GerstnerVertexIntensity",new Vector4(.35f,.45f,.35f,7));
waterSource.SetVector("_GFinalFrequency",new Vector4(.9f,.8f,.7f,.6f));
waterSource.SetVector("_GFinalSpeed",new Vector4(-1.35f,.7695f,.7695f,1.5165f));
waterSource.SetFloat("_GGlobalSpeed",.45f);
var unrelated=new Material(new Shader{name="AQUAS/Desktop and Web"});
unrelated.SetVector("_GerstnerVertexIntensity",new Vector4(1,2,3,4));
var waterA=new Renderer{sharedMaterials=new[]{waterSource,unrelated,null,waterSource}};
var waterB=new Renderer{sharedMaterials=new[]{waterSource}};
var waterGeometry=new AiIslandGeometry();waterGeometry.Components.AddRange(new Component[]{waterA,waterB});
waterGeometry.ConvertUnits(.1f);
var waterConverted=waterA.sharedMaterials[0];var displacement=waterConverted.GetVector("_GerstnerVertexIntensity");
Check(Near(displacement.x,.035f)&&Near(displacement.y,.045f)&&Near(displacement.z,.035f)&&displacement.w==7,"Gerstner XYZ displacement follows map scale and preserves W");
var frequency=waterConverted.GetVector("_GFinalFrequency");
Check(Near(frequency.x,9)&&Near(frequency.y,8)&&Near(frequency.z,7)&&Near(frequency.w,6),"all four wave frequencies convert inversely to map scale");
Check(waterConverted.GetVector("_GFinalSpeed").Equals(waterSource.GetVector("_GFinalSpeed"))&&waterConverted.GetFloat("_GGlobalSpeed")==.45f,"wave time parameters remain unchanged");
Check(!ReferenceEquals(waterConverted,waterSource)&&waterSource.GetVector("_GerstnerVertexIntensity").x==.35f&&waterSource.GetVector("_GFinalFrequency").x==.9f,"native source water material is unchanged");
Check(ReferenceEquals(waterConverted,waterA.sharedMaterials[3])&&ReferenceEquals(waterConverted,waterB.sharedMaterials[0]),"shared water source is cloned once across renderers and material slots");
Check(ReferenceEquals(waterA.sharedMaterials[1],unrelated)&&waterA.sharedMaterials[2]==null,"unrelated water shader and empty material slots stay intact");
waterGeometry.ConvertUnits(.1f);
Check(ReferenceEquals(waterConverted,waterA.sharedMaterials[0])&&Near(waterGeometry.transform.localScale.x,.1f)&&Near(waterConverted.GetVector("_GFinalFrequency").x,9),"repeated conversion cannot multiply scale or waves twice");
var halfWater=new Renderer{sharedMaterials=new[]{waterSource}};var halfGeometry=new AiIslandGeometry();halfGeometry.Components.Add(halfWater);halfGeometry.ConvertUnits(.5f);
Check(Near(halfWater.sharedMaterials[0].GetVector("_GerstnerVertexIntensity").x,.175f)&&Near(halfWater.sharedMaterials[0].GetVector("_GFinalFrequency").x,1.8f),"wave conversion uses supplied MapScale rather than hardcoded 1/10");
Call(waterGeometry,"OnDestroy");Call(halfGeometry,"OnDestroy");
Check(waterConverted.Destroyed&&halfWater.sharedMaterials[0].Destroyed&&!waterSource.Destroyed&&!unrelated.Destroyed,"wave material clones released while native materials survive");
var cloudSky=new Material(null){name="existing KKS cloud sky"};
var reflection=new Cubemap();
RenderSettings.skybox=cloudSky;RenderSettings.sun=light;RenderSettings.customReflection=reflection;
RenderSettings.ambientIntensity=.7f;RenderSettings.fog=true;
var environment=AiIslandEnvironment.Capture();
RenderSettings.skybox=new Material(null);RenderSettings.sun=null;RenderSettings.ambientIntensity=2;RenderSettings.fog=false;
environment.RestoreKksEnvironment();
Check(ReferenceEquals(RenderSettings.skybox,cloudSky)&&ReferenceEquals(RenderSettings.sun,light),"original KKS cloud sky and sun restored after additive loading");
Check(ReferenceEquals(RenderSettings.customReflection,reflection)&&RenderSettings.fog&&RenderSettings.ambientIntensity==.7f,"KKS reflection, fog and environment strength preserved");
environment.Dispose();Check(!cloudSky.Destroyed&&!reflection.Destroyed,"existing KKS sky assets survive island cleanup");

var main=new Camera{farClipPlane=500};
var visibility=IslandLodTests.Run(Check,main);
var group=CullingGroup.Last;
var diagnosticFolder=System.IO.Path.Combine(@"J:\temp\ai-island-diagnostic-tests",Guid.NewGuid().ToString("N"));
UnityEngine.Profiling.Recorder.Get("Camera.Render").enabled=true;
var diagnostic=new AiIslandDiagnostics(diagnosticFolder,0,0);
Check(Camera.Subscribers==1&&UnityEngine.Profiling.Recorder.EnabledCount>1,"diagnostic start acquires native markers and camera callback");
Camera.Cull(main);diagnostic.Tick(visibility,main,terrain,"test=true");
Check(diagnostic.Completed,"time-bounded capture reports completion");
diagnostic.Dispose();
Check(Camera.Subscribers==0&&UnityEngine.Profiling.Recorder.EnabledCount==1,"logging off removes callback and restores only owned native markers");
string captured=System.IO.File.ReadAllText(diagnostic.Path);
Check(captured.Contains("simple=")&&captured.Contains("NATIVE Camera.Render")&&captured.Contains("gpuMs=unavailable")&&captured.Contains("STOP"),"log contains LOD counts, native CPU timing, unavailable GPU marker and stop record");
long length=new System.IO.FileInfo(diagnostic.Path).Length;
Camera.Cull(main);diagnostic.Tick(visibility,main,terrain,"must not write");diagnostic.Dispose();
Check(new System.IO.FileInfo(diagnostic.Path).Length==length,"stopped capture writes no further data and is idempotent");
try {new AiIslandDiagnostics(diagnostic.Path,10,1);throw new Exception("invalid output path accepted");}
catch(System.IO.IOException){}
Check(Camera.Subscribers==0&&UnityEngine.Profiling.Recorder.EnabledCount==1,"output failure does not leak camera hooks or profiler markers");
IslandLodTests.Cleanup(visibility,Check);
Console.WriteLine("TOTAL "+checks+" PASSED (native rendering remains untested)");
