using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// Editor-only asset conversion. No new runtime component is used by either map.
public static class LegacyMapBuild
{
    [Serializable] public class TextureDef { public string id,name,file; public int width,height,format,mips,filter,aniso,wrap; public bool cube,linear; }
    [Serializable] public class FloatProp { public string key; public float value; }
    [Serializable] public class ColorProp { public string key; public float[] value; }
    [Serializable] public class TextureProp { public string key,id; public float[] scale,offset; }
    [Serializable] public class MaterialDef { public string id,name,shader,keywords; public int queue; public FloatProp[] floats; public ColorProp[] colors; public TextureProp[] textures; }
    [Serializable] public class MeshDef { public string id,file; }
    [Serializable] public class RendererDef { public string mesh; public string[] materials; public bool world,enabled,receive; public int cast,lightmap; public float[] lightmapST; }
    [Serializable] public class ColliderDef { public string kind,mesh; public float[] center,size; public bool enabled,trigger,convex; }
    [Serializable] public class ProbeDef { public string texture; public float[] center,size; public float intensity,blend; public int importance; public bool box; }
    [Serializable] public class Node { public string id,parent,name; public bool active; public float[] position,rotation,scale; public RendererDef[] renderers; public ColliderDef[] colliders; public ProbeDef probe; }
    [Serializable] public class LightmapDef { public string color,direction; }
    [Serializable] public class Hotel { public TextureDef[] textures; public MaterialDef[] materials; public MeshDef[] meshes; public Node[] nodes; public LightmapDef[] lightmaps; public float[] floor; }
    [Serializable] public class NativeTags { public string[] tags,layers; }
    [Serializable] public class Road { public FloatProp[] floats; public string lightColor,backgroundColor; }
    static readonly string Data="Assets/SourceData/", Gen="Assets/Generated/";
    static readonly List<AssetBundleBuild> Builds=new List<AssetBundleBuild>();
    static readonly Dictionary<string,Texture> Textures=new Dictionary<string,Texture>();
    static readonly Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
    static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
    static Vector3 V(float[] a) { return new Vector3(a[0],a[1],a[2]); }
    static Color C(float[] a) { return new Color(a[0],a[1],a[2],a.Length>3?a[3]:1); }
    static T Save<T>(T asset,string name) where T:UnityEngine.Object { AssetDatabase.CreateAsset(asset,Gen+name);return asset; }
    static void Log(string value) { Debug.Log("LEGACY_MAP_BUILD "+value); }
    static void Tags()
    {
        var native=JsonUtility.FromJson<NativeTags>(File.ReadAllText(Data+"native_tags.json"));
        var so=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags=so.FindProperty("tags");tags.arraySize=native.tags.Length;
        for(int i=0;i<native.tags.Length;i++)tags.GetArrayElementAtIndex(i).stringValue=native.tags[i];
        var layers=so.FindProperty("layers");for(int i=0;i<native.layers.Length;i++)layers.GetArrayElementAtIndex(i).stringValue=native.layers[i];
        so.ApplyModifiedProperties();
    }
    static int MipBytes(int size,TextureFormat format)
    {
        if(format==TextureFormat.DXT1)return Math.Max(1,(size+3)/4)*Math.Max(1,(size+3)/4)*8;
        if(format==TextureFormat.DXT5 || format==TextureFormat.BC6H || format==TextureFormat.BC7)return Math.Max(1,(size+3)/4)*Math.Max(1,(size+3)/4)*16;
        int bytes=format==TextureFormat.RGBAHalf?8:format==TextureFormat.RGBAFloat?16:format==TextureFormat.RGB24?3:4;
        return size*size*bytes;
    }
    static void LoadTextures(Hotel hotel)
    {
        foreach(var d in hotel.textures)
        {
            // Editor placeholders are replaced with the exact original serialized
            // textures by package.py. PNGs are only used to generate the list thumbnail.
            Texture texture;
            if(d.cube)
            {
                texture=new Cubemap(16,TextureFormat.RGBA32,false);
            }
            else
            {
                var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,d.linear);
                tex.LoadImage(File.ReadAllBytes(Data+d.id+".png"));texture=tex;
            }
            texture.name="OriginalVaM:"+d.id;texture.filterMode=(FilterMode)d.filter;texture.anisoLevel=d.aniso;texture.wrapMode=(TextureWrapMode)d.wrap;
            Save(texture,"tex_"+d.id+".asset");Textures.Add(d.id,texture);
        }
    }
    static void LoadMaterials(Hotel hotel)
    {
        foreach(var d in hotel.materials)
        {
            // The source uses the built-in Standard shaders; fail instead of silently substituting.
            Shader shader=Shader.Find(d.shader)??Shader.Find("Standard");
            var material=new Material(shader);material.name="OriginalVaM:"+d.id;
            foreach(var p in d.floats)if(material.HasProperty(p.key))material.SetFloat(p.key,p.value);
            foreach(var p in d.colors)if(material.HasProperty(p.key))material.SetColor(p.key,C(p.value));
            foreach(var p in d.textures)if(material.HasProperty(p.key))
            {
                if(!String.IsNullOrEmpty(p.id))material.SetTexture(p.key,Textures[p.id]);
                material.SetTextureScale(p.key,new Vector2(p.scale[0],p.scale[1]));material.SetTextureOffset(p.key,new Vector2(p.offset[0],p.offset[1]));
            }
            material.shaderKeywords=(d.keywords??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            material.renderQueue=d.queue;Save(material,"mat_"+d.id+".mat");Materials.Add(d.id,material);
        }
    }
    static void LoadMeshes(Hotel hotel)
    {
        foreach(var d in hotel.meshes)using(var r=new BinaryReader(File.OpenRead(Data+d.file)))
        {
            int count=r.ReadInt32(),subs=r.ReadInt32();var positions=new Vector3[count];var normals=new Vector3[count];var tangents=new Vector4[count];var uv=new Vector2[count];var uv2=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                positions[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());normals[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                tangents[i]=new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());uv2[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
            }
            var mesh=new Mesh();mesh.name=d.id;mesh.indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.vertices=positions;mesh.normals=normals;mesh.tangents=tangents;mesh.uv=uv;mesh.uv2=uv2;mesh.subMeshCount=subs;
            for(int s=0;s<subs;s++){int n=r.ReadInt32();var indices=new int[n];for(int i=0;i<n;i++)indices[i]=r.ReadInt32();mesh.SetTriangles(indices,s,false);}
            mesh.RecalculateBounds();Save(mesh,"mesh_"+d.id+".asset");Meshes.Add(d.id,mesh);
        }
    }
    static GameObject CreateRoot()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Map");var group=new GameObject("MapObjectGroup");group.transform.SetParent(root.transform,false);
        foreach(string name in new[]{"h_free","yuka00"}){var anchor=new GameObject(name);anchor.transform.SetParent(group.transform,false);anchor.transform.localPosition=new Vector3(0,.01f,0);anchor.transform.localEulerAngles=new Vector3(0,180,0);}
        return root;
    }
    static void Sun(GameObject root,Color ambient,bool road)
    {
        var go=new GameObject("Native time-of-day light");go.transform.SetParent(root.transform,false);go.transform.localEulerAngles=new Vector3(50,150,0);
        var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=road?0f:.45f;light.shadows=LightShadows.None;
        RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=ambient;RenderSettings.ambientEquatorColor=ambient;RenderSettings.ambientGroundColor=ambient;RenderSettings.skybox=null;
        var sun=root.AddComponent<SunLightInfo>();sun._targetLight=light;sun._infos=new SunLightInfo.Info[4];
        for(int i=0;i<4;i++)sun._infos[i]=new SunLightInfo.Info{type=(SunLightInfo.Info.Type)i,visibleList=new List<GameObject>(),angle=go.transform.eulerAngles,color=light.color,intensity=light.intensity,ambientSkyColor=ambient,ambientEquatorColor=ambient,ambientGroundColor=ambient,fogUse=road,fogColor=RenderSettings.fogColor,fogDensity=RenderSettings.fogDensity,fogStart=0,fogEnd=80,glFogDistanceFog=false,glFogHeightFog=false,aoeTint=Color.black,aoeRadius=.3f,aoePowerExponent=1,aoeBias=.1f,bafBloomThreshold=.8f,bafSepBlurSpread=1,dofFocalSize=1,dofAperture=.01f};
    }
    static void HotelScene(Hotel hotel)
    {
        Log("reconstruct hotel hierarchy and static batches");var root=CreateRoot();var geometry=new GameObject("VaM hotel original geometry");geometry.transform.SetParent(root.transform,false);
        var nodes=hotel.nodes.ToDictionary(n=>n.id,n=>new GameObject(n.name));
        foreach(var n in hotel.nodes)
        {
            var go=nodes[n.id];go.layer=LayerMask.NameToLayer("Map");go.transform.SetParent(String.IsNullOrEmpty(n.parent)?geometry.transform:nodes[n.parent].transform,false);
            go.transform.localPosition=V(n.position);go.transform.localRotation=new Quaternion(n.rotation[0],n.rotation[1],n.rotation[2],n.rotation[3]);go.transform.localScale=V(n.scale);
            foreach(var d in n.renderers)
            {
                var display=new GameObject(n.name+" display");display.layer=go.layer;display.transform.SetParent(d.world?geometry.transform:go.transform,false);
                display.AddComponent<MeshFilter>().sharedMesh=Meshes[d.mesh];var renderer=display.AddComponent<MeshRenderer>();renderer.sharedMaterials=d.materials.Select(k=>Materials[k]).ToArray();
                renderer.enabled=d.enabled;renderer.shadowCastingMode=(ShadowCastingMode)d.cast;renderer.receiveShadows=d.receive;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                renderer.lightmapIndex=d.lightmap;renderer.lightmapScaleOffset=new Vector4(d.lightmapST[0],d.lightmapST[1],d.lightmapST[2],d.lightmapST[3]);
                if(d.world && !n.active)display.SetActive(false);
            }
            foreach(var d in n.colliders)
            {
                Collider collider;
                if(d.kind=="mesh") {var m=go.AddComponent<MeshCollider>();m.sharedMesh=Meshes[d.mesh];m.convex=d.convex;collider=m;}
                else {var b=go.AddComponent<BoxCollider>();b.center=V(d.center);b.size=V(d.size);collider=b;}
                collider.enabled=d.enabled;collider.isTrigger=d.trigger;
            }
            if(n.probe!=null && !String.IsNullOrEmpty(n.probe.texture))
            {
                var p=n.probe;var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=Textures[p.texture];probe.center=V(p.center);probe.size=V(p.size);probe.intensity=p.intensity;probe.blendDistance=p.blend;probe.boxProjection=p.box;probe.importance=p.importance;
            }
            go.SetActive(n.active);
        }
        geometry.transform.localPosition=-V(hotel.floor);
        LightmapSettings.lightmapsMode=LightmapsMode.CombinedDirectional;
        LightmapSettings.lightmaps=hotel.lightmaps.Select(l=>new LightmapData{lightmapColor=(Texture2D)Textures[l.color],lightmapDir=(Texture2D)Textures[l.direction]}).ToArray();
        RenderSettings.fog=false;Sun(root,new Color(.10f,.10f,.10f),false);
        CameraCollision(root);
        Register(root,"canon_vam_hotel","CanonVamHotel",297603,"VaM ホテル（照明あり）",new Vector3(0,1.7f,-4),new Vector3(0,1.2f,1));
    }
    static void CameraCollision(GameObject root)
    {
        var instances=new List<CombineInstance>();
        foreach(var col in root.GetComponentsInChildren<MeshCollider>(true))if(col.enabled&&!col.isTrigger&&col.sharedMesh!=null)instances.Add(new CombineInstance{mesh=col.sharedMesh,transform=col.transform.localToWorldMatrix});
        if(instances.Count==0)return;
        var mesh=new Mesh();mesh.name="Native camera collision";mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(instances.ToArray(),true,true);Save(mesh,"camera_"+root.scene.handle+".asset");
        var go=new GameObject("CameraCollision");go.layer=LayerMask.NameToLayer("CameraCollision");go.transform.SetParent(root.transform,false);go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static Material RoadMaterial(string name,Color color)
    {
        var m=new Material(Shader.Find("Standard"));m.name=name;m.color=color;Save(m,name+".mat");return m;
    }
    static GameObject Part(GameObject root,PrimitiveType type,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=LayerMask.NameToLayer("Map");go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static void RoadScene()
    {
        Log("build streetlight from original overlay dimensions and settings");var root=CreateRoot();
        var cfg=JsonUtility.FromJson<Road>(File.ReadAllText(Data+"road.json"));var values=cfg.floats.ToDictionary(p=>p.key,p=>p.value);Func<string,float,float> f=(key,fallback)=>values.ContainsKey(key)?values[key]:fallback;
        Color lampColor,background;ColorUtility.TryParseHtmlString(cfg.lightColor,out lampColor);ColorUtility.TryParseHtmlString(cfg.backgroundColor,out background);
        var road=RoadMaterial("Streetlight asphalt",new Color(.72f,.72f,.72f));road.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Data+"StreetlightAsphalt.png");
        // LoadImage previously treated this generated normal map as a regular packed texture.
        road.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Data+"StreetlightAsphaltNormal.png"));road.EnableKeyword("_NORMALMAP");road.SetFloat("_BumpScale",f("NormalStrength",.65f));road.SetFloat("_Glossiness",.18f);
        var tile=new Vector2(f("TextureTileX",2.5f),f("TextureTileY",7));road.mainTextureScale=tile;road.SetTextureScale("_BumpMap",tile);
        Part(root,PrimitiveType.Plane,"Textured asphalt road",new Vector3(0,-.035f,0),new Vector3(f("RoadWidth",18)/10,1,f("RoadLength",50)/10),road);
        var metal=RoadMaterial("Streetlight dark metal",new Color(.13f,.14f,.15f));metal.EnableKeyword("_EMISSION");float visibility=f("PoleVisibility",.2f);metal.SetColor("_EmissionColor",new Color(visibility*.87f,visibility*.94f,visibility));
        var lens=RoadMaterial("Streetlight glowing lens",lampColor);lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",lampColor*1.2f);
        float height=f("LampHeight",5.8f),offset=f("PoleOffset",2.8f);
        Part(root,PrimitiveType.Cylinder,"Single streetlight pole",new Vector3(offset,height*.5f,0),new Vector3(.19f,height*.5f,.19f),metal);
        Part(root,PrimitiveType.Cube,"Streetlight arm",new Vector3(offset*.5f,height-.02f,0),new Vector3(offset,.12f,.13f),metal);
        Part(root,PrimitiveType.Cube,"Streetlight lamp housing",new Vector3(0,height-.08f,0),new Vector3(.85f,.18f,.48f),metal);
        Part(root,PrimitiveType.Cube,"Streetlight glowing lens",new Vector3(0,height-.20f,0),new Vector3(.68f,.04f,.34f),lens);
        var go=new GameObject("One streetlight spotlight");go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(0,height-.26f,0);go.transform.localEulerAngles=new Vector3(90,0,0);
        var light=go.AddComponent<Light>();light.type=LightType.Spot;light.color=lampColor;light.intensity=f("LightIntensity",1.15f);light.range=f("LightRange",11);light.spotAngle=f("LightAngle",66);light.shadows=LightShadows.Hard;light.shadowStrength=.85f;
        var backdrop=GameObject.CreatePrimitive(PrimitiveType.Sphere);backdrop.name="Streetlight black skydome";backdrop.transform.SetParent(root.transform,false);backdrop.transform.localScale=Vector3.one*200;UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        var skyMesh=UnityEngine.Object.Instantiate(backdrop.GetComponent<MeshFilter>().sharedMesh);skyMesh.name="Streetlight inward sky";var indices=skyMesh.triangles;for(int i=0;i<indices.Length;i+=3){int tmp=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=tmp;}skyMesh.triangles=indices;skyMesh.RecalculateNormals();Save(skyMesh,"road_sky.asset");backdrop.GetComponent<MeshFilter>().sharedMesh=skyMesh;
        var sky=Save(new Material(Shader.Find("Unlit/Color")),"road_sky.mat");sky.color=background;var renderer=backdrop.GetComponent<Renderer>();renderer.sharedMaterial=sky;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        LightmapSettings.lightmaps=new LightmapData[0];RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=f("FogDensity",.07f);RenderSettings.fogColor=background;
        Sun(root,Color.black,true);CameraCollision(root);
        Register(root,"canon_streetlight_road","CanonStreetlightRoad",297602,"街灯のある暗い道路",new Vector3(0,3.2f,-6),new Vector3(0,2.5f,0));
    }
    static string HPoints(int id,string suffix,int[] categories)
    {
        var root=new GameObject("HPoint_"+suffix+id);root.AddComponent<H.HPointOmitObject>();
        var location=new GameObject("yuka00");location.transform.SetParent(root.transform,false);location.transform.localPosition=new Vector3(0,.01f,0);location.transform.localEulerAngles=new Vector3(0,180,0);
        var data=location.AddComponent<H.HPointData>();data._categorys=categories;
        var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);marker.name="HPointMarker";marker.tag="H/HPoint";marker.transform.SetParent(location.transform,false);marker.transform.localPosition=new Vector3(0,.32f,0);marker.transform.localScale=new Vector3(.5f,.3f,.5f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());var collider=marker.AddComponent<CapsuleCollider>();collider.radius=.5f;collider.height=2;
        data._meshRender=marker.GetComponent<MeshRenderer>();data._meshRender.sharedMaterial=RoadMaterial("marker_"+suffix+id,new Color(.3f,.8f,.8f));
        string path=Gen+root.name+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return path;
    }
    static void Thumbnail(string key,Vector3 position,Vector3 target)
    {
        var cameraObject=new GameObject("Asset thumbnail camera");var camera=cameraObject.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=60;camera.farClipPlane=250;
        var render=new RenderTexture(512,288,24);camera.targetTexture=render;camera.Render();var active=RenderTexture.active;RenderTexture.active=render;
        var image=new Texture2D(512,288,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,512,288),0,0);image.Apply();File.WriteAllBytes(Gen+"sp_"+key+".png",image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=null;render.Release();UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(cameraObject);
        AssetDatabase.ImportAsset(Gen+"sp_"+key+".png");var importer=(TextureImporter)AssetImporter.GetAtPath(Gen+"sp_"+key+".png");importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();
    }
    static void Register(GameObject root,string key,string sceneName,int id,string title,Vector3 camera,Vector3 target)
    {
        Thumbnail(key,camera,target);
        string scene=Gen+sceneName+".unity";EditorSceneManager.SaveScene(root.scene,scene);
        var info=ScriptableObject.CreateInstance<MapInfo>();info.name=key;info.param.Add(new MapInfo.Param{MapName=sceneName,DisplayName=title,No=id,Sort=id,AssetBundleName="map/scene/"+key+".unity3d",AssetName=sceneName,LookFor=100,isFreeH=true,isH=true,isSky=true,isOutdoors=key.Contains("road"),ThumbnailMorningID=id,ThumbnailDayTimeID=id,ThumbnailEveningID=id,ThumbnailNightID=id});Save(info,key+"_mapinfo.asset");
        var thumb=ScriptableObject.CreateInstance<MapThumbnailInfo>();thumb.name=key+"_thumbnail";thumb.param.Add(new MapThumbnailInfo.Param{Name=title,ID=id,Bundle="map/thumbnail/"+key+".unity3d",Asset="sp_"+key});Save(thumb,key+"_thumbnailinfo.asset");
        var points=new[]{HPoints(id,"",new[]{0,1,2,3,8,9}),HPoints(id,"Add_",new[]{1010,1011,1012,1100,1101,1102}),HPoints(id,"3P_",new[]{3000,3001,3002})};
        Builds.Add(new AssetBundleBuild{assetBundleName=key+"_scene.unity3d",assetNames=new[]{scene}});
        Builds.Add(new AssetBundleBuild{assetBundleName=key+"_mapinfo.unity3d",assetNames=new[]{AssetDatabase.GetAssetPath(info)}});
        Builds.Add(new AssetBundleBuild{assetBundleName=key+"_thumbnailinfo.unity3d",assetNames=new[]{AssetDatabase.GetAssetPath(thumb)}});
        Builds.Add(new AssetBundleBuild{assetBundleName=key+"_thumbnail.unity3d",assetNames=new[]{Gen+"sp_"+key+".png"},addressableNames=new[]{"sp_"+key}});
        Builds.Add(new AssetBundleBuild{assetBundleName=key+"_hpoints.unity3d",assetNames=points,addressableNames=points.Select(Path.GetFileNameWithoutExtension).ToArray()});
        Log("registered "+title+" id="+id+" renderers="+root.GetComponentsInChildren<Renderer>(true).Length+" colliders="+root.GetComponentsInChildren<Collider>(true).Length);
    }
    public static void Build()
    {
        try
        {
            if(Application.unityVersion!="2019.4.9f1")throw new Exception("Requires KKS Unity version");
            Directory.CreateDirectory(Gen);Directory.CreateDirectory("Build");Tags();
            var hotel=JsonUtility.FromJson<Hotel>(File.ReadAllText(Data+"hotel.json"));LoadTextures(hotel);LoadMaterials(hotel);LoadMeshes(hotel);HotelScene(hotel);RoadScene();AssetDatabase.SaveAssets();
            var manifest=BuildPipeline.BuildAssetBundles("Build",Builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
            if(manifest==null)throw new Exception("AssetBundle build failed");
            Log("SUCCESS bundles="+Builds.Count);EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
