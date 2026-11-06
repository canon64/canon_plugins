using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Hs2MapBuild {
 [Serializable] public class Mat { public string id,name,shader,main,normal,metallicMap,specularMap,occlusion,emissionMap;public float[] color,emission,scale,offset;public float metallic,smoothness,cutoff,bumpScale;public int mode,queue;public bool twoSided; }
 [Serializable] public class Geo {public string name,file,material;public bool collider;}
 [Serializable] public class Lamp {public string name;public int type;public float[] position,direction,color;public float intensity,range,spotAngle;}
 [Serializable] public class MapDef {public string id,label,scene,pack,thumbnail;public int mapId;public Geo[] meshes;public Lamp[] lights;public float[] bounds;}
 [Serializable] public class Config {public Mat[] materials;public MapDef[] maps;}
 [Serializable] public class Report {public string id,label,pack,scene;public int mapId,renderers,colliders,lights;public float[] spawn;}
 [Serializable] public class Reports {public Report[] maps;}
 static string data="Assets/ImportData/",gen="Assets/Generated/";
 static Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
 static Vector3 V(float[] a){return new Vector3(a[0],a[1],a[2]);}
 static Color C(float[] a){return new Color(a[0],a[1],a[2],a.Length>3?a[3]:1);}
 static Texture2D Texture(string file,bool normal=false,bool linear=false){
  if(String.IsNullOrEmpty(file))return null;
  if(textures.ContainsKey(file))return textures[file];
  var ti=(TextureImporter)AssetImporter.GetAtPath(data+file);
  if(ti==null)throw new Exception("Texture importer missing: "+file);
  ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
  ti.sRGBTexture=!(normal||linear);ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Compressed;ti.SaveAndReimport();
  var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(data+file);textures[file]=tex;return tex;
 }
 static void SetTex(Material mat,string prop,string file,string keyword=null,bool normal=false,bool linear=false){
  if(String.IsNullOrEmpty(file))return;mat.SetTexture(prop,Texture(file,normal,linear));if(keyword!=null)mat.EnableKeyword(keyword);
 }
 static Mesh ReadMesh(string file){
  string path=gen+Path.GetFileNameWithoutExtension(file)+".asset";
  using(var r=new BinaryReader(File.OpenRead(data+file))){
   int nv=r.ReadInt32(),ni=r.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];
   for(int i=0;i<nv;i++){v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());}
   var t=new int[ni];for(int i=0;i<ni;i++)t[i]=r.ReadInt32();
   var m=new Mesh{name=Path.GetFileNameWithoutExtension(file),indexFormat=nv>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.vertices=v;m.triangles=t;m.normals=n;m.uv=uv;m.RecalculateBounds();m.RecalculateTangents();AssetDatabase.CreateAsset(m,path);return m;
  }
 }
 static Vector3 FindSpawn(Bounds bounds){
  Physics.SyncTransforms();var candidates=new List<Vector3>();
  // Prefer the original source floor plane near the origin, then search outward.
  int maxRing=Mathf.CeilToInt(Mathf.Max(bounds.size.x,bounds.size.z)/.65f)+2;
  for(int ring=0;ring<maxRing;ring++)for(int x=-ring;x<=ring;x++)for(int z=-ring;z<=ring;z++){
   if(Math.Abs(x)!=ring&&Math.Abs(z)!=ring)continue;
   Vector3 top=new Vector3(x*.65f,bounds.max.y+2,z*.65f);
   foreach(var hit in Physics.RaycastAll(top,Vector3.down,bounds.size.y+5,1<<LayerMask.NameToLayer("Map")).OrderBy(h=>Mathf.Abs(h.point.y))){
    if(hit.normal.y<.75f||hit.point.y>bounds.max.y-1.65f)continue;
    Vector3 p=hit.point+Vector3.up*.04f;
    if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.4f,.25f,1<<LayerMask.NameToLayer("Map")))continue;
    return p;
   }
  }
  throw new Exception("No clear standing/H floor position; source floor placement must be corrected");
 }
 static string HPoints(MapDef map,string suffix,Vector3 spawn,int[] cats){
  var root=new GameObject("HPoint_"+suffix+map.mapId);root.AddComponent<H.HPointOmitObject>();
  var point=new GameObject("yuka00");point.transform.SetParent(root.transform,false);point.transform.position=spawn;var d=point.AddComponent<H.HPointData>();d._categorys=cats;
  var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);marker.name="HPointMarker";marker.transform.SetParent(point.transform,false);marker.transform.localPosition=new Vector3(0,.32f,0);marker.transform.localScale=new Vector3(.5f,.3f,.5f);marker.tag="H/HPoint";
  UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.AddComponent<CapsuleCollider>();d._meshRender=marker.GetComponent<MeshRenderer>();
  string path=gen+root.name+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return path;
 }
 public static void Build(){try{
  if(Application.unityVersion!="2019.4.9f1")throw new Exception("Requires target KKS Unity version");
  if(AssetDatabase.IsValidFolder(gen.TrimEnd('/')))AssetDatabase.DeleteAsset(gen.TrimEnd('/'));
  textures.Clear();
  Directory.CreateDirectory(gen);Directory.CreateDirectory("Build");
  if(LayerMask.NameToLayer("Map")!=11||LayerMask.NameToLayer("CameraCollision")!=12)throw new Exception("Native layer table mismatch");
  var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(data+"imports.json"));var mats=new Dictionary<string,Material>();
  foreach(var def in cfg.materials){
   string shader=def.shader.Contains("Specular")?"Standard (Specular setup)":"Standard";
   var m=new Material(Shader.Find(shader)){name=def.name,color=C(def.color)};
   m.mainTexture=Texture(def.main);m.mainTextureScale=new Vector2(def.scale[0],def.scale[1]);m.mainTextureOffset=new Vector2(def.offset[0],def.offset[1]);
   m.SetFloat("_Metallic",def.metallic);m.SetFloat("_Glossiness",def.smoothness);m.SetFloat("_Cutoff",def.cutoff);m.SetFloat("_BumpScale",def.bumpScale);
   SetTex(m,"_BumpMap",def.normal,"_NORMALMAP",true);SetTex(m,"_MetallicGlossMap",def.metallicMap,"_METALLICGLOSSMAP",false,true);SetTex(m,"_SpecGlossMap",def.specularMap,"_SPECGLOSSMAP",false,true);SetTex(m,"_OcclusionMap",def.occlusion,null,false,true);SetTex(m,"_EmissionMap",def.emissionMap,"_EMISSION");
   if(def.emission.Take(3).Any(v=>v>0)){m.SetColor("_EmissionColor",C(def.emission));m.EnableKeyword("_EMISSION");}
   int mode=def.mode;
   if(def.shader.IndexOf("Refraction",StringComparison.OrdinalIgnoreCase)>=0){mode=3;m.color=new Color(m.color.r,m.color.g,m.color.b,.18f);}
   m.SetFloat("_Mode",mode);
   if(mode==1){m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}
   else if(mode>=2){m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHABLEND_ON");m.renderQueue=3000;}
   AssetDatabase.CreateAsset(m,gen+def.id+".mat");mats.Add(def.id,m);
  }
  var builds=new List<AssetBundleBuild>();var reports=new List<Report>();
  foreach(var def in cfg.maps){
   Debug.Log("HS2_BUILD_MAP "+def.id);
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Map");var group=new GameObject("MapObjectGroup");group.transform.SetParent(root.transform,false);
   Bounds bounds=new Bounds();bool first=true;var collision=new List<CombineInstance>();
   foreach(var g in def.meshes){
    var ob=new GameObject(g.name);ob.layer=LayerMask.NameToLayer("Map");ob.transform.SetParent(root.transform,false);var mesh=ReadMesh(g.file);ob.AddComponent<MeshFilter>().sharedMesh=mesh;ob.AddComponent<MeshRenderer>().sharedMaterial=mats[g.material];
    if(first){bounds=mesh.bounds;first=false;}else bounds.Encapsulate(mesh.bounds);
    if(g.collider){ob.AddComponent<MeshCollider>().sharedMesh=mesh;collision.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});}
   }
   if(collision.Count==0)throw new Exception("Map has no solid collision: "+def.id);
   var cm=new Mesh{name=def.id+" camera collision",indexFormat=IndexFormat.UInt32};cm.CombineMeshes(collision.ToArray(),true,false);AssetDatabase.CreateAsset(cm,gen+def.id+"_camera_collision.asset");
   var cameraCol=new GameObject("CameraCollision");cameraCol.layer=LayerMask.NameToLayer("CameraCollision");cameraCol.transform.SetParent(root.transform,false);cameraCol.AddComponent<MeshCollider>().sharedMesh=cm;
   foreach(var l in def.lights){
    var ob=new GameObject(l.name);ob.transform.SetParent(root.transform,false);ob.transform.position=V(l.position);Vector3 dir=V(l.direction);if(dir.sqrMagnitude>1e-8)ob.transform.rotation=Quaternion.LookRotation(dir);
    var light=ob.AddComponent<Light>();light.type=l.type==1?LightType.Directional:l.type==0?LightType.Spot:LightType.Point;light.color=C(l.color);light.intensity=l.intensity;light.range=l.range;light.spotAngle=l.spotAngle;light.shadows=LightShadows.None;
   }
   var sun=root.GetComponentsInChildren<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
   if(sun==null){var ob=new GameObject("KKS map daylight");ob.transform.SetParent(root.transform,false);ob.transform.rotation=Quaternion.Euler(50,-35,0);sun=ob.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.65f;sun.color=new Color(1,.94f,.85f);}
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.42f,.46f,.53f);RenderSettings.ambientEquatorColor=new Color(.34f,.32f,.30f);RenderSettings.ambientGroundColor=new Color(.22f,.21f,.20f);RenderSettings.fog=false;
   var si=root.AddComponent<SunLightInfo>();si._targetLight=sun;si._infos=new SunLightInfo.Info[4];
   for(int i=0;i<4;i++)si._infos[i]=new SunLightInfo.Info{type=(SunLightInfo.Info.Type)i,visibleList=new List<GameObject>(),angle=sun.transform.eulerAngles,color=sun.color,intensity=sun.intensity,ambientSkyColor=RenderSettings.ambientSkyColor,ambientEquatorColor=RenderSettings.ambientEquatorColor,ambientGroundColor=RenderSettings.ambientGroundColor,fogUse=false,glFogDistanceFog=false,glFogHeightFog=false,aoeRadius=.3f,aoePowerExponent=1,aoeBias=.1f,bafBloomThreshold=.8f,bafSepBlurSpread=1,dofFocalSize=1,dofAperture=.01f};
   Vector3 spawn=FindSpawn(bounds);
   foreach(string name in new[]{"h_free","yuka00"}){var p=new GameObject(name);p.transform.SetParent(group.transform,false);p.transform.position=spawn;}
   string scenePath=gen+def.scene+".unity";EditorSceneManager.SaveScene(scene,scenePath);
   string prefix="canon_hs2_"+def.id;
   builds.Add(new AssetBundleBuild{assetBundleName=prefix+"_scene.unity3d",assetNames=new[]{scenePath}});
   var info=ScriptableObject.CreateInstance<MapInfo>();info.name=prefix;info.param.Add(new MapInfo.Param{MapName=def.scene,DisplayName=def.label,No=def.mapId,Sort=def.mapId,AssetBundleName="map/scene/"+prefix+".unity3d",AssetName=def.scene,LookFor=100,isFreeH=true,isH=true,isSky=false,ThumbnailMorningID=def.mapId,ThumbnailDayTimeID=def.mapId,ThumbnailEveningID=def.mapId,ThumbnailNightID=def.mapId});
   AssetDatabase.CreateAsset(info,gen+prefix+"_info.asset");builds.Add(new AssetBundleBuild{assetBundleName=prefix+"_mapinfo.unity3d",assetNames=new[]{gen+prefix+"_info.asset"}});
   var ti=(TextureImporter)AssetImporter.GetAtPath(data+def.thumbnail);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.alphaIsTransparency=true;ti.npotScale=TextureImporterNPOTScale.None;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.mipmapEnabled=false;ti.SaveAndReimport();
   if(AssetDatabase.LoadAssetAtPath<Sprite>(data+def.thumbnail)==null)throw new Exception("Thumbnail Sprite missing: "+def.id);
   string thumbnailAsset=Path.GetFileNameWithoutExtension(def.thumbnail);
   var thumb=ScriptableObject.CreateInstance<MapThumbnailInfo>();thumb.name=prefix;thumb.param.Add(new MapThumbnailInfo.Param{Name=def.label,ID=def.mapId,Bundle="map/thumbnail/"+prefix+".unity3d",Asset=thumbnailAsset});
   AssetDatabase.CreateAsset(thumb,gen+prefix+"_thumbnailinfo.asset");builds.Add(new AssetBundleBuild{assetBundleName=prefix+"_thumbnailinfo.unity3d",assetNames=new[]{gen+prefix+"_thumbnailinfo.asset"}});
   builds.Add(new AssetBundleBuild{assetBundleName=prefix+"_thumbnail.unity3d",assetNames=new[]{data+def.thumbnail},addressableNames=new[]{thumbnailAsset}});
   string hp=HPoints(def,"",spawn,new[]{0,1,2,3,8,9});string add=HPoints(def,"Add_",spawn,new[]{1010,1011,1012,1100,1101,1102});string hp3=HPoints(def,"3P_",spawn,new[]{3000,3001,3002});
   builds.Add(new AssetBundleBuild{assetBundleName=prefix+"_hpoints.unity3d",assetNames=new[]{hp,add,hp3},addressableNames=new[]{"HPoint_"+def.mapId,"HPoint_Add_"+def.mapId,"HPoint_3P_"+def.mapId}});
   reports.Add(new Report{id=def.id,label=def.label,pack=def.pack,scene=def.scene,mapId=def.mapId,renderers=def.meshes.Length,colliders=collision.Count+1,lights=root.GetComponentsInChildren<Light>().Length,spawn=new[]{spawn.x,spawn.y,spawn.z}});
  }
  AssetDatabase.SaveAssets();var manifest=BuildPipeline.BuildAssetBundles("Build",builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
  if(manifest==null)throw new Exception("Map AssetBundle build failed");
  File.WriteAllText("Build/build_report.json",JsonUtility.ToJson(new Reports{maps=reports.ToArray()},true));Debug.Log("HS2_ZIPMOD_BUILD_OK "+reports.Count+" maps");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
