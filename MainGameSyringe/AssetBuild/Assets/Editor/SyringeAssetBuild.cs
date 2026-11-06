using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SyringeAssetBuild
{
 [Serializable]class Fluid {public float[][] profile;public int width,height;public float minimum,maximum,initialFill,volumeMl;}
 static readonly List<string> Assets=new List<string>();
 static Shader Liquid;
 static string Text(BinaryReader r){return Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));}
 static float[] Floats(BinaryReader r){var a=new float[r.ReadInt32()];for(int i=0;i<a.Length;i++)a[i]=r.ReadSingle();return a;}
 static Vector3[] Vectors(float[] a){var v=new Vector3[a.Length/3];for(int i=0;i<v.Length;i++)v[i]=new Vector3(a[i*3],a[i*3+1],a[i*3+2]);return v;}
 static Vector2[] Uvs(float[] a){var v=new Vector2[a.Length/2];for(int i=0;i<v.Length;i++)v[i]=new Vector2(a[i*2],a[i*2+1]);return v;}
 static T Save<T>(T asset,string name)where T:UnityEngine.Object {string path="Assets/Generated/"+name+".asset";AssetDatabase.CreateAsset(asset,path);Assets.Add(path);return asset;}
 static Material Transparent(string name,Color color,int queue,bool depth,int cull=2)
 {
  var m=new Material(Liquid){name=name,renderQueue=queue};m.SetColor("_Color",color);m.SetFloat("_ZWrite",depth?1:0);m.SetFloat("_Cull",cull);return Save(m,name);
 }
 static GameObject Model(string kind)
 {
  var root=new GameObject(kind+"Prefab");var nodes=new Dictionary<string,GameObject>();var parents=new Dictionary<string,string>();
  using(var reader=new BinaryReader(File.OpenRead("Assets/Source/"+kind+".bytes")))
  {
   if(reader.ReadInt32()!=1)throw new InvalidDataException();int count=reader.ReadInt32();
   for(int i=0;i<count;i++)
   {
    string name=Text(reader),parent=Text(reader);var obj=new GameObject(name);nodes.Add(name,obj);parents.Add(name,parent);
    obj.transform.localPosition=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    obj.transform.localRotation=new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    obj.transform.localScale=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    if(!reader.ReadBoolean())continue;
    var mesh=new Mesh{name=kind+"_"+name,indexFormat=IndexFormat.UInt32};mesh.vertices=Vectors(Floats(reader));mesh.normals=Vectors(Floats(reader));mesh.uv=Uvs(Floats(reader));
    var indices=new int[reader.ReadInt32()];for(int j=0;j<indices.Length;j++)indices[j]=reader.ReadInt32();
    for(int j=0;j<indices.Length;j+=3){int swap=indices[j+1];indices[j+1]=indices[j+2];indices[j+2]=swap;}
    mesh.triangles=indices;mesh.RecalculateBounds();Save(mesh,mesh.name);
    Color color=new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),1);float metal=reader.ReadSingle(),rough=reader.ReadSingle();string map=Text(reader);bool cutout=reader.ReadBoolean();
    var material=new Material(Shader.Find("Standard")){name=kind+"_"+name+"_Material"};material.color=color;material.SetFloat("_Metallic",metal);material.SetFloat("_Glossiness",1-rough);
    bool glass=name=="GlassBarrel"||name=="GlassBottle",fluid=name=="PurpleLiquid"||name=="NozzleLiquid";
    if(glass||fluid)
    {
     UnityEngine.Object.DestroyImmediate(material);
     material=Transparent(kind+"_"+name+"_Material",fluid?new Color(.1f,.006f,.36f,.55f):new Color(.985f,.99f,1,.14f),glass?3030:3020,fluid);
    }
    else
    {
     if(!string.IsNullOrEmpty(map)){material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Source/"+map);if(material.mainTexture==null)throw new Exception("Missing texture: "+map);}
     if(cutout){material.SetFloat("_Mode",1);material.SetFloat("_Cutoff",.1f);material.EnableKeyword("_ALPHATEST_ON");material.renderQueue=2450;}
     Save(material,material.name);
    }
    obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
    if(glass)
    {
     var back=new GameObject(name+"_back");back.transform.SetParent(obj.transform,false);back.AddComponent<MeshFilter>().sharedMesh=mesh;
     back.AddComponent<MeshRenderer>().sharedMaterial=Transparent(kind+"_"+name+"_Back",new Color(.985f,.99f,1,.055f),3010,false,1);
    }
   }
  }
  foreach(var pair in nodes){var obj=pair.Value;var p=obj.transform.localPosition;var q=obj.transform.localRotation;var s=obj.transform.localScale;obj.transform.SetParent(nodes.ContainsKey(parents[pair.Key])?nodes[parents[pair.Key]].transform:root.transform,false);obj.transform.localPosition=p;obj.transform.localRotation=q;obj.transform.localScale=s;}
  if(kind=="Syringe")
  {
   nodes["Syringe_ROOT"].name="Assembly";nodes["Pusher_ROOT"].name="Pusher";nodes["Syringe_ROOT"].transform.localScale=new Vector3(2f/3,2f/3,1);
  }
  else BottleLiquid(nodes["PurpleLiquid"]);
  string prefab="Assets/Generated/"+kind+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);Assets.Add(prefab);UnityEngine.Object.DestroyImmediate(root);return null;
 }
 static void BottleLiquid(GameObject obj)
 {
  // JsonUtility cannot read jagged arrays; the binary builder writes a separate flat profile.
  var values=File.ReadAllLines("Assets/Source/FluidProfile.txt");var profile=new Vector2[values.Length];
  for(int i=0;i<values.Length;i++){var parts=values[i].Split(',');profile[i]=new Vector2(float.Parse(parts[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture));}
  var vectors=new Vector4[24];for(int i=0;i<24;i++){var p=profile[Math.Min(i,profile.Length-1)];vectors[i]=new Vector4(p.x,p.y,0,0);}
  var table=new Texture2D(129,129,TextureFormat.RGBA32,false,true){name="FluidVolumeTable",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};table.LoadRawTextureData(File.ReadAllBytes("Assets/Source/FluidTable.bytes"));table.Apply(false,true);Save(table,table.name);
  var positions=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();const int segments=80;
  for(int row=0;row<profile.Length;row++)for(int i=0;i<=segments;i++)
  {float angle=i*Mathf.PI*2/segments;var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);positions.Add(radial*profile[row].x+Vector3.forward*profile[row].y);normals.Add(radial);}
  for(int row=0;row<profile.Length-1;row++)for(int i=0;i<segments;i++){int a=row*(segments+1)+i,b=a+segments+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
  var mesh=new Mesh{name="BottleFluidContainer"};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();Save(mesh,mesh.name);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
  var body=obj.GetComponent<MeshRenderer>().sharedMaterial;body.SetFloat("_Kind",1);body.SetFloat("_Fill",.72945555f);body.SetTexture("_Table",table);body.SetVectorArray("_Profile",vectors);EditorUtility.SetDirty(body);
  positions.Clear();normals.Clear();triangles.Clear();const int grid=64;
  for(int y=0;y<=grid;y++)for(int x=0;x<=grid;x++){positions.Add(new Vector3((x/(float)grid-.5f)*.11f,(y/(float)grid-.5f)*.11f,0));normals.Add(Vector3.forward);}
  for(int y=0;y<grid;y++)for(int x=0;x<grid;x++){int a=y*(grid+1)+x,b=a+grid+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
  var capMesh=new Mesh{name="BottleFluidSurface"};capMesh.SetVertices(positions);capMesh.SetNormals(normals);capMesh.SetTriangles(triangles,0);capMesh.bounds=mesh.bounds;Save(capMesh,capMesh.name);
  var cap=new GameObject("FreeSurface");cap.transform.SetParent(obj.transform,false);cap.AddComponent<MeshFilter>().sharedMesh=capMesh;
  var capMaterial=new Material(body){name="BottleFluidSurfaceMaterial",renderQueue=3022};capMaterial.SetFloat("_Kind",2);capMaterial.SetFloat("_Cull",0);Save(capMaterial,capMaterial.name);cap.AddComponent<MeshRenderer>().sharedMaterial=capMaterial;
 }
 static void VerifyRefill(AssetBundle bundle)
 {
  var source=bundle.LoadAsset<GameObject>("assets/generated/syringe.prefab");if(source==null)throw new Exception("Bundled syringe prefab missing");var root=UnityEngine.Object.Instantiate(source);
  var assembly=root.transform.Find("Assembly");var rig=new GameObject("RefillRig");rig.transform.SetParent(assembly,false);
  var bottle=UnityEngine.Object.Instantiate(bundle.LoadAsset<GameObject>("assets/generated/bottle.prefab"));bottle.name="Bottle";bottle.transform.SetParent(rig.transform,false);
  if(bottle.transform.Find("MedicineBottle_ROOT/SilverFoilLabel").GetComponent<MeshRenderer>().sharedMaterial.mainTexture==null)throw new Exception("Bottle spiral label texture missing from bundle");
  if(assembly.Find("Graduation_Print").GetComponent<MeshRenderer>().sharedMaterial.mainTexture==null)throw new Exception("Syringe graduation texture missing from bundle");
  var liquid=bottle.transform.Find("MedicineBottle_ROOT/PurpleLiquid").GetComponent<MeshRenderer>();var cap=liquid.transform.Find("FreeSurface").GetComponent<MeshRenderer>();var material=liquid.material;var capMaterial=cap.material;
  var pusher=assembly.Find("Pusher");var syringeLiquid=assembly.Find("LiquidPivot");
  var clip=MainGameSyringe.SyringeRefillMotion.Create(new Vector3(0,0,.0405f),Vector3.zero,.02f,Quaternion.identity,Quaternion.Euler(-90,0,0),new Vector3(0,0,.23f),new Vector3(0,0,.11f));
  try
  {
   float[] times={0,1.3f,2.8f,4.3f,5.6f};float[] pusherZ={.0405f,.0405f,.02025f,0,0};float[] fills={.72945555f,.72945555f,.36472777f,0,0};
   for(int i=0;i<times.Length;i++)
   {
    clip.SampleAnimation(root,times[i]);
    if(Mathf.Abs(pusher.localPosition.z-pusherZ[i])>.00002f)throw new Exception("Pusher curve binding failed at "+times[i]);
    if(Mathf.Abs(AnimatedFill(liquid)-fills[i])>.0001f||Mathf.Abs(AnimatedFill(cap)-fills[i])>.0001f)throw new Exception("Bottle fill binding failed at "+times[i]+" actual="+AnimatedFill(liquid)+","+AnimatedFill(cap));
   }
   if(Mathf.Abs(syringeLiquid.localScale.z-1)>.0001f)throw new Exception("Syringe does not fill");
   var profiles=File.ReadAllLines("Assets/Source/FluidProfile.txt");if(profiles.Length!=17)throw new Exception("Fluid profile count");
   File.WriteAllText(Path.GetFullPath("../Resources/asset-verification.json"),"{\"ok\":true,\"bundleLoadConfirmed\":true,\"injectionPusherSeconds\":3,\"refillPusherSeconds\":3,\"refillTotalSeconds\":5.6,\"bottleFinalFill\":0,\"realUnityAnimationSamples\":5}");
   Debug.Log("SYRINGE_REAL_ANIMATION_BINDINGS_OK");
  }
  finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(capMaterial);UnityEngine.Object.DestroyImmediate(clip);}
 }
 static float AnimatedFill(Renderer renderer)
 {if(!renderer.HasPropertyBlock())return renderer.sharedMaterial.GetFloat("_Fill");var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);return block.GetFloat("_Fill");}
 public static void Build()
 {
  try
  {
   if(!File.ReadAllText("Packages/manifest.json").Contains("\"com.unity.modules.assetbundle\""))throw new Exception("AssetBundle build module is required");
   Directory.CreateDirectory("Assets/Generated");AssetDatabase.Refresh();
   foreach(string texture in Directory.GetFiles("Assets/Source","*.png")){var importer=(TextureImporter)AssetImporter.GetAtPath(texture);importer.maxTextureSize=4096;importer.anisoLevel=4;importer.SaveAndReimport();}
   Liquid=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SyringeLiquid.shader");
   if(Liquid==null)throw new Exception("Liquid shader missing");Model("Syringe");Model("Bottle");AssetDatabase.SaveAssets();
   foreach(var message in ShaderUtil.GetShaderMessages(Liquid))if(message.severity.ToString()=="Error")throw new Exception(message.message);
   string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),output=Path.Combine(root,"Resources");Directory.CreateDirectory(output);
   var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="syringe-models.bundle",assetNames=new[]{"Assets/Generated/Syringe.prefab","Assets/Generated/Bottle.prefab"}}},BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
   if(manifest==null)throw new Exception("Bundle build failed");
   var bundle=AssetBundle.LoadFromMemory(File.ReadAllBytes(Path.Combine(output,"syringe-models.bundle")));
   if(bundle==null)throw new Exception("Built AssetBundle cannot be loaded by Unity "+Application.unityVersion);
   try{VerifyRefill(bundle);}finally{bundle.Unload(true);}
   foreach(var message in ShaderUtil.GetShaderMessages(Liquid))if(message.severity.ToString()=="Error")throw new Exception(message.message);
   Debug.Log("SYRINGE_ASSET_BUILD_OK");EditorApplication.Exit(0);
  }
  catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
