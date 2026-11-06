using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class HotelThirdFloorBuild
{
    const string Data="Assets/Hotel3FData/", TextureDir="Assets/Hotel3FMaterials/", Gen="Assets/GeneratedHotel3F/", Output="BuildHotel3F";
    [Serializable] public class Mat { public string name,texture; public float[] color; public float roughness,metallic,emission; }
    [Serializable] public class Geo { public string name,file,material,group,weights,lodGroup; public int vertices,triangles,curtainSide,lod; }
    [Serializable] public class Lamp { public string name; public float[] position,forward,color; public float power; public bool spot; }
    [Serializable] public class Room { public float width,depth,height,wall; }
    [Serializable] public class Stage { public float radius,centerY,height,poleRadius; }
    [Serializable] public class Curtain { public float y,width,bottom,top,duration; public int bonesPerHalf; }
    [Serializable] public class Booth { public string name; public float x,y,yaw; }
    [Serializable] public class Config { public Room room; public Stage stage; public Curtain curtain; public Booth[] booths; }
    [Serializable] public class Manifest { public string scene,bundle; public Mat[] materials; public Geo[] meshes; public Lamp[] lights; public Config config; }
    static Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
    static Mesh Read(Geo d)
    {
        using(var r=new BinaryReader(File.OpenRead(Data+d.file)))
        {
            int count=r.ReadInt32(),indexCount=r.ReadInt32();var v=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];
            for(int i=0;i<count;i++){v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());}
            var indices=new int[indexCount];for(int i=0;i<indexCount;i++)indices[i]=r.ReadInt32();
            for(int i=0;i<indexCount;i+=3){int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}
            var mesh=new Mesh{name=d.name,indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh,Gen+"mesh_"+d.name+".asset");return mesh;
        }
    }
    static Texture2D Texture(string file,bool normal=false,bool data=false)
    {
        string path=TextureDir+file;var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null)throw new InvalidDataException(path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.convertToNormalmap=false;
        importer.sRGBTexture=!normal&&!data;importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=normal||data?2048:4096;
        importer.mipmapEnabled=true;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static GameObject Empty(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static BoxCollider Box(string name,Transform parent,Vector3 pos,Vector3 size)
    {var o=Empty(name,parent);o.layer=LayerMask.NameToLayer("Map");o.transform.localPosition=pos;var c=o.AddComponent<BoxCollider>();c.size=size;return c;}
    static void Collisions(Transform root,Config cfg)
    {
        var group=Empty("3F_Collision",root).transform;
        Box("3F_Ground",group,new Vector3(0,-.12f,10),new Vector3(14,.24f,20));
        foreach(int sign in new[]{-1,1})Box("3F_Wall",group,new Vector3(sign*7.09f,2.1f,10),new Vector3(.18f,4.2f,20.36f));
        Box("3F_BackWall",group,new Vector3(0,2.1f,20.09f),new Vector3(14.36f,4.2f,.18f));
        Box("3F_Ceiling",group,new Vector3(0,4.29f,10),new Vector3(14.36f,.18f,20.36f));
        foreach(float x in new[]{-4.1f,4.1f})Box("3F_FrontWall",group,new Vector3(x,2.1f,-.09f),new Vector3(5.8f,4.2f,.18f));
        // Seal the walking map at the vestibule; the interaction returns to 2F.
        Box("3F_EntryBoundary",group,new Vector3(0,1.4f,-.16f),new Vector3(2.4f,2.8f,.05f));
        Box("3F_BackstageDeck",group,new Vector3(0,.225f,18.25f),new Vector3(6.4f,.45f,3.5f));
        foreach(float x in new[]{-5.1f,5.1f})Box("3F_BackstageWall",group,new Vector3(x,2.1f,16.65f),new Vector3(3.8f,4.2f,.18f));
        // Stage and runway collision use the actual mesh, with a separate gentle step ramp.
        var platform=Empty("3F_PlatformCollision",group);var filter=GameObject.Find("Stage__Obsidian").GetComponent<MeshFilter>();
        platform.layer=LayerMask.NameToLayer("Map");platform.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        var ramp=new Mesh{name="3F_AccessRamp"};ramp.vertices=new[]{new Vector3(-.72f,0,2.84f),new Vector3(.72f,0,2.84f),new Vector3(-.72f,.45f,3.62f),new Vector3(.72f,.45f,3.62f)};
        ramp.triangles=new[]{0,2,1,1,2,3};ramp.RecalculateNormals();AssetDatabase.CreateAsset(ramp,Gen+"access_ramp.asset");
        Empty("3F_AccessRamp",group).AddComponent<MeshCollider>().sharedMesh=ramp;
        Box("3F_ReturnElevator",group,new Vector3(2.15f,1.35f,.055f),new Vector3(1.7f,2.7f,.12f));
        foreach(var booth in cfg.booths)
        {
            float targetY=booth.y<10?6.5f:booth.y;float yaw=Mathf.Atan2(-booth.x,-(targetY-booth.y))*Mathf.Rad2Deg;float scale=booth.y>=10?.78f:1;
            var holder=Empty("3F_SeatCollision_"+booth.name,group).transform;holder.localPosition=new Vector3(booth.x,0,booth.y);holder.localRotation=Quaternion.Euler(0,-yaw,0);holder.localScale=new Vector3(scale,1,scale);
            for(int segment=0;segment<7;segment++)
            {
                float angle=(-66+(segment+.5f)*132/7)*Mathf.Deg2Rad;
                var c=Box("Upholstery",holder,new Vector3(Mathf.Sin(angle)*1.03f,.54f,Mathf.Cos(angle)*1.03f),new Vector3(.42f,1.08f,.95f));c.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
            }
            Box("Table",holder,new Vector3(0,.35f,-.25f),new Vector3(.82f,.70f,.82f));
        }
        foreach(int side in new[]{-1,1})for(int i=0;i<16;i++)
        {
            float start=side>0?22:196;float angle=(start+(i+.5f)*142/16)*Mathf.Deg2Rad;
            var c=Box("3F_RailCollision",group,new Vector3(2.92f*Mathf.Sin(angle),.775f,6.5f+2.92f*Mathf.Cos(angle)),new Vector3(.46f,.65f,.045f));c.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
        }
    }
    static void BuildCurtain(GameObject go,Mesh mesh,Geo d,Config cfg)
    {
        int count=cfg.curtain.bonesPerHalf;var bones=new Transform[count];var bind=new Matrix4x4[count];var clip=new AnimationClip{name="CurtainTravel",legacy=true,frameRate=24};
        for(int i=0;i<count;i++)
        {
            float x=d.curtainSide*cfg.curtain.width*.5f*i/(count-1);var bone=Empty("Column_"+i,go.transform).transform;bone.localPosition=new Vector3(x,cfg.curtain.bottom,cfg.curtain.y);bones[i]=bone;bind[i]=bone.worldToLocalMatrix*go.transform.localToWorldMatrix;
            float end=d.curtainSide*(2.63f+Mathf.Abs(x)/3.2f*.57f);
            clip.SetCurve(bone.name,typeof(Transform),"localPosition.x",AnimationCurve.EaseInOut(0,x,cfg.curtain.duration,end));
            clip.SetCurve(bone.name,typeof(Transform),"localScale.x",AnimationCurve.EaseInOut(0,1,cfg.curtain.duration,.18f));
        }
        var weights=new BoneWeight[mesh.vertexCount];
        using(var r=new BinaryReader(File.OpenRead(Data+d.weights)))for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=(int)r.ReadSingle(),boneIndex1=(int)r.ReadSingle(),weight0=r.ReadSingle(),weight1=r.ReadSingle()};
        mesh.boneWeights=weights;mesh.bindposes=bind;EditorUtility.SetDirty(mesh);
        var skinned=go.AddComponent<SkinnedMeshRenderer>();skinned.sharedMesh=mesh;skinned.sharedMaterial=mats[d.material];skinned.bones=bones;skinned.rootBone=go.transform;skinned.localBounds=new Bounds(new Vector3(0,2.2f,16.55f),new Vector3(7,4,1));skinned.updateWhenOffscreen=false;
        var clipFolder=Gen+d.name;
        if(!AssetDatabase.IsValidFolder(clipFolder))AssetDatabase.CreateFolder(Gen.TrimEnd('/'),d.name);
        AssetDatabase.CreateAsset(clip,clipFolder+"/CurtainTravel.anim");
        var animation=go.AddComponent<Animation>();animation.playAutomatically=false;animation.AddClip(clip,"CurtainTravel");animation.enabled=false;
    }
    static void NativeEnvironment(GameObject root)
    {
        var sun=Empty("3F_NativeSun",root.transform).AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=0;sun.enabled=true;
        var info=root.AddComponent<SunLightInfo>();info._targetLight=sun;info._infos=new SunLightInfo.Info[4];
        for(int i=0;i<4;i++)info._infos[i]=new SunLightInfo.Info{type=(SunLightInfo.Info.Type)i,visibleList=new List<GameObject>(),intensity=0,color=Color.white,ambientSkyColor=RenderSettings.ambientSkyColor,ambientEquatorColor=RenderSettings.ambientEquatorColor,ambientGroundColor=RenderSettings.ambientGroundColor,fogUse=false,aoeTint=Color.black,aoeRadius=.3f,aoePowerExponent=1,aoeBias=.1f,bafBloomThreshold=.8f,bafSepBlurSpread=1,dofFocalSize=1,dofAperture=.01f};
        var anchors=Empty("MapObjectGroup",root.transform);Empty("h_free",root.transform).transform.localPosition=new Vector3(0,.45f,5.5f);Empty("yuka00",anchors.transform).transform.localPosition=new Vector3(0,.45f,5.5f);
        RenderSettings.sun=sun;
    }
    static void ReflectionStates(Transform root)
    {
        var active=Empty("3F_ReflectionProbes",root);var states=Empty("3F_ReflectionStates",root);
        var positions=new[]{new Vector3(0,1.15f,6.5f),new Vector3(0,1.15f,13),new Vector3(0,1.6f,3)};
        var sizes=new[]{new Vector3(8,4.2f,8),new Vector3(4,4.2f,9),new Vector3(14,4.2f,20)};
        var resolutions=new[]{512,256,128};var colors=new[]{new Color(1,.035f,.018f),new Color(.38f,.035f,1),new Color(1,.28f,.055f)};
        var animations=root.GetComponentsInChildren<Animation>(true);
        for(int open=0;open<2;open++)
        {
            foreach(var a in animations){a.enabled=true;a.Play("CurtainTravel");a["CurtainTravel"].time=open*4;a.Sample();a.Stop();a.enabled=false;}
            for(int preset=0;preset<3;preset++)
            {
                mats["RedLED"].SetColor("_EmissionColor",colors[preset]*3);var state=Empty((open==0?"closed_":"open_")+preset,states.transform);
                for(int i=0;i<3;i++)
                {
                    var probe=Empty("Probe_"+i,state.transform).AddComponent<ReflectionProbe>();probe.transform.localPosition=positions[i];probe.size=sizes[i];probe.center=Vector3.zero;probe.boxProjection=true;probe.mode=ReflectionProbeMode.Custom;probe.importance=i==2?0:2;probe.blendDistance=.7f;
                    var cube=new Cubemap(resolutions[i],TextureFormat.RGBAHalf,true){name=state.name+"_"+i};
                    var camera=Empty("ReflectionBakeCamera",root).AddComponent<Camera>();camera.transform.position=positions[i];camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.03f);camera.nearClipPlane=.05f;camera.farClipPlane=40;camera.allowHDR=true;camera.enabled=false;
                    if(!camera.RenderToCubemap(cube))throw new Exception("Reflection bake failed");UnityEngine.Object.DestroyImmediate(camera.gameObject);
                    AssetDatabase.CreateAsset(cube,Gen+cube.name+".cubemap");probe.customBakedTexture=cube;probe.enabled=false;
                    if(open==0&&preset==0)
                    {
                        var target=Empty("Probe_"+i,active.transform).AddComponent<ReflectionProbe>();EditorUtility.CopySerialized(probe,target);target.enabled=false;target.transform.localPosition=positions[i];
                    }
                }
                state.SetActive(false);
            }
        }
        foreach(var a in animations){a.enabled=true;a.Play("CurtainTravel");a["CurtainTravel"].time=0;a.Sample();a.Stop();a.enabled=false;}
        mats["RedLED"].SetColor("_EmissionColor",colors[0]*3);EditorUtility.SetDirty(mats["RedLED"]);
        foreach(var probe in active.GetComponentsInChildren<ReflectionProbe>())probe.enabled=true;
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
        RenderSettings.customReflection=active.GetComponentsInChildren<ReflectionProbe>().Last().customBakedTexture as Cubemap;
    }
    static void Mist(Transform root)
    {
        var mat=new Material(Shader.Find("Particles/Standard Unlit")){name="3F_ThinMist"};mat.mainTexture=Texture("Smoke_Wisp_RGBA.png");mat.SetFloat("_Mode",2);mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.EnableKeyword("_ALPHABLEND_ON");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,Gen+"mist.mat");
        var points=new[]{new Vector3(-1.8f,4.0f,5.2f),new Vector3(1.8f,4.0f,5.2f),new Vector3(-1.8f,4.0f,8),new Vector3(1.8f,4.0f,8)};
        for(int i=0;i<points.Length;i++)
        {
            var go=Empty("3F_Mist_"+i,root);go.transform.localPosition=points[i];go.transform.localRotation=Quaternion.Euler(65,90,0);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=6;main.startLifetime=4;main.startSpeed=.18f;main.startSize=new ParticleSystem.MinMaxCurve(.8f,1.5f);main.startColor=new Color(.8f,.82f,.85f,.055f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=3;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.025f;
            var fade=ps.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(0,1)});fade.color=g;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
    static void SpatialChecks(Transform root)
    {
        Physics.SyncTransforms();var points=new[]{new Vector3(0,.05f,1.4f),new Vector3(-3.9f,.05f,6.5f),new Vector3(3.9f,.05f,6.5f),new Vector3(0,.50f,6.0f),new Vector3(0,.50f,12),new Vector3(0,.50f,18.5f)};
        foreach(var point in points){RaycastHit hit;if(!Physics.Raycast(point+Vector3.up*.7f,Vector3.down,out hit,1)||Mathf.Abs(hit.point.y-(point.y-.05f))>.07f)throw new Exception("Missing floor "+point);}
        if(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=2)throw new Exception("Curtain skins missing");
        if(root.GetComponentsInChildren<ParticleSystem>(true).Length!=4)throw new Exception("Nozzles missing");
        if(root.GetComponentsInChildren<ReflectionProbe>(true).Count(p=>p.enabled)!=3)throw new Exception("Active reflection probe count");
    }
    public static void Build()
    {
        try
        {
            if(Application.unityVersion!="2019.4.9f1")throw new Exception("Unity version mismatch");
            Directory.CreateDirectory(Gen);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            foreach(string guid in AssetDatabase.FindAssets("",new[]{Gen.TrimEnd('/')})){string p=AssetDatabase.GUIDToAssetPath(guid);if(!AssetDatabase.IsValidFolder(p))AssetDatabase.DeleteAsset(p);}
            var cfg=JsonUtility.FromJson<Manifest>(File.ReadAllText(Data+"scene.json"));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);scene.name=cfg.scene;var root=new GameObject("Map");root.layer=LayerMask.NameToLayer("Map");
            mats.Clear();
            foreach(var d in cfg.materials)
            {
                var m=new Material(Shader.Find("Standard")){name=d.name,color=new Color(d.color[0],d.color[1],d.color[2],d.color[3])};m.SetFloat("_Glossiness",1-d.roughness);m.SetFloat("_Metallic",d.metallic);
                if(!string.IsNullOrEmpty(d.texture))
                {
                    m.color=Color.white;m.mainTexture=Texture(d.texture+"_BaseColor.png");m.SetTexture("_BumpMap",Texture(d.texture+"_NormalGL.png",true));m.EnableKeyword("_NORMALMAP");m.SetTexture("_MetallicGlossMap",Texture(d.texture+"_MetallicSmoothness.png",false,true));m.EnableKeyword("_METALLICGLOSSMAP");m.SetFloat("_GlossMapScale",1);
                }
                if(d.emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(d.color[0],d.color[1],d.color[2])*d.emission);}
                if(d.name=="Glass")
                {
                    m.SetFloat("_Mode",3);m.color=new Color(.65f,.60f,.53f,.18f);m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;
                }
                m.globalIlluminationFlags=d.name=="RedLED"?MaterialGlobalIlluminationFlags.None:MaterialGlobalIlluminationFlags.BakedEmissive;
                AssetDatabase.CreateAsset(m,Gen+d.name+".mat");mats.Add(d.name,m);
            }
            var lodGroups=new Dictionary<string,Transform>();var lodRenderers=new Dictionary<string,List<Renderer>[]>();
            foreach(var d in cfg.meshes)
            {
                Transform parent=root.transform;
                if(!string.IsNullOrEmpty(d.lodGroup))
                {
                    if(!lodGroups.ContainsKey(d.lodGroup)){lodGroups.Add(d.lodGroup,Empty(d.lodGroup+"_LOD",root.transform).transform);lodRenderers.Add(d.lodGroup,new[]{new List<Renderer>(),new List<Renderer>(),new List<Renderer>()});}
                    parent=lodGroups[d.lodGroup];
                }
                var go=Empty(d.name,parent);go.layer=root.layer;var mesh=Read(d);
                if(!string.IsNullOrEmpty(d.weights)){BuildCurtain(go,mesh,d,cfg.config);continue;}
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mats[d.material];renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;
                if(!string.IsNullOrEmpty(d.lodGroup))lodRenderers[d.lodGroup][d.lod].Add(renderer);
                var flags=StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic;
                bool lightmapped=d.group=="Shell"||d.group=="Stage"||d.group=="Backstage"||d.group.StartsWith("WallBay_");
                if(d.lod==0&&d.material!="Glass")
                {
                    if(lightmapped&&mats[d.material].GetFloat("_Metallic")<.5f)
                    {
                        UnwrapParam unwrap;UnwrapParam.SetDefaults(out unwrap);unwrap.packMargin=12;
                        Unwrapping.GenerateSecondaryUVSet(mesh,unwrap);EditorUtility.SetDirty(mesh);flags|=StaticEditorFlags.ContributeGI;
                    }
                    else if(!string.IsNullOrEmpty(d.lodGroup))
                    {flags|=StaticEditorFlags.ContributeGI;renderer.receiveGI=ReceiveGI.LightProbes;}
                }
                GameObjectUtility.SetStaticEditorFlags(go,flags);
            }
            foreach(var entry in lodGroups)
            {
                var levels=lodRenderers[entry.Key];var lod=entry.Value.gameObject.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
                lod.SetLODs(new[]{new LOD(.14f,levels[0].ToArray()),new LOD(.065f,levels[1].ToArray()),new LOD(.008f,levels[2].ToArray())});lod.RecalculateBounds();
            }
            foreach(var d in cfg.lights)
            {
                var go=Empty(d.name,root.transform);go.transform.localPosition=V(d.position);go.transform.rotation=Quaternion.LookRotation(V(d.forward));var l=go.AddComponent<Light>();l.color=new Color(d.color[0],d.color[1],d.color[2]);l.type=d.spot?LightType.Spot:LightType.Point;l.range=d.spot?13:5;l.intensity=d.spot?3.4f:(d.power>90?1.0f:.65f);l.shadows=d.spot?LightShadows.Soft:LightShadows.None;l.spotAngle=50;l.shadowBias=.035f;l.shadowNormalBias=.12f;l.renderMode=LightRenderMode.Auto;
                l.lightmapBakeType=d.spot?LightmapBakeType.Mixed:LightmapBakeType.Baked;
            }
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.11f,.105f,.12f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.8f;RenderSettings.skybox=null;RenderSettings.fog=false;
            var lightProbes=Empty("3F_LightProbes",root.transform).AddComponent<LightProbeGroup>();var probePositions=new List<Vector3>();
            foreach(float x in new[]{-5.8f,-2.8f,0,2.8f,5.8f})foreach(float z in new[]{1.2f,4,7,10,13,16,19})foreach(float y in new[]{.8f,2.2f,3.7f})probePositions.Add(new Vector3(x,y,z));
            lightProbes.probePositions=probePositions.ToArray();
            NativeEnvironment(root);Collisions(root.transform,cfg.config);Mist(root.transform);
            var scenePath=Gen+cfg.scene+".unity";EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
            foreach(var animation in root.GetComponentsInChildren<Animation>(true))
                if(animation.GetClip("CurtainTravel")==null||animation["CurtainTravel"]==null)throw new Exception("CurtainTravel was lost during asset serialization");
            Lightmapping.giWorkflowMode=Lightmapping.GIWorkflowMode.OnDemand;LightmapEditorSettings.lightmapper=LightmapEditorSettings.Lightmapper.ProgressiveCPU;
            LightmapEditorSettings.bakeResolution=20;LightmapEditorSettings.maxAtlasSize=2048;LightmapEditorSettings.directSampleCount=32;LightmapEditorSettings.indirectSampleCount=128;LightmapEditorSettings.environmentSampleCount=64;
            Lightmapping.realtimeGI=false;Lightmapping.bakedGI=true;
            if(!Lightmapping.Bake())throw new Exception("Static lighting bake failed");
            ReflectionStates(root.transform);SpatialChecks(root.transform);EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
            var report="{\"scene\":\""+cfg.scene+"\",\"renderers\":"+root.GetComponentsInChildren<Renderer>(true).Length+",\"materials\":"+mats.Count+",\"colliders\":"+root.GetComponentsInChildren<Collider>(true).Length+",\"curtainSkins\":2,\"reflectionStates\":6,\"gameRuntimeVerified\":false}";
            var builds=new[]{new AssetBundleBuild{assetBundleName=cfg.bundle,assetNames=new[]{scenePath}}};
            if(BuildPipeline.BuildAssetBundles(Output,builds,BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64)==null)throw new Exception("Bundle build failed");
            File.WriteAllText(Output+"/validation.json",report);Debug.Log("HOTEL_THIRD_FLOOR_BUILD_OK "+report);EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
