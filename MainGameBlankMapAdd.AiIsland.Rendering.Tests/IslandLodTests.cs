using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MainGameBlankMapAdd;
using UnityEngine;
using UnityEngine.Rendering;

internal static class IslandLodTests
{
    private static MeshRenderer high, low;
    private static LODGroup lod;
    private static Material originalMaterial;
    private static Mesh originalMesh, simplified;
    private static MeshFilter filter;
    internal static AiIslandVisibility Run(Action<bool,string> check, Camera camera)
    {
        MeshRenderer Make(float x)
        {
            var material=new Material(new Shader{name="Nature/SpeedTree"});
            material.SetTexture("_MainTex",new Texture2D(2,2,TextureFormat.RGB24,false));
            return new MeshRenderer{bounds=new Bounds{center=new Vector3(x,0,0),extents=Vector3.one},sharedMaterials=new[]{material}};
        }
        high=Make(5);low=Make(5);low.enabled=false;
        var whiteCard=new MeshRenderer{bounds=high.bounds,sharedMaterials=new[]{new Material(new Shader{name="Standard"})}};
        lod=new LODGroup{Lods=new[]{new LOD{renderers=new[]{high}},new LOD{renderers=new[]{low}},new LOD{renderers=new[]{whiteCard}},new LOD{renderers=Array.Empty<Renderer>()}}};
        originalMaterial=low.sharedMaterials[0];
        var standalone=Make(30);filter=new MeshFilter();standalone.Components.Add(filter);
        using(var input=new BinaryReader(typeof(AiIslandVisibility).Assembly.GetManifestResourceStream("MainGameBlankMapAdd.AiIslandLod.bin")))
        {
            input.ReadBytes(8);input.ReadInt32();string key=Encoding.UTF8.GetString(input.ReadBytes(input.ReadInt32()));
            var fields=key.Split('\n');var bytes=fields[3].Split('-').Select(s=>Convert.ToByte(s,16)).ToArray();
            float F(int i)=>BitConverter.ToSingle(bytes,i*4);
            originalMesh=new Mesh{name=fields[0],VertexCountOverride=int.Parse(fields[1]),subMeshCount=int.Parse(fields[2]),
                bounds=new Bounds{center=new Vector3(F(0),F(1),F(2)),extents=new Vector3(F(3),F(4),F(5))}};
        }
        filter.sharedMesh=originalMesh;
        var waterMaterial=new Material(new Shader{name="Lux Water/WaterSurface"}){name="m_ai_mi_umi01_00"};
        waterMaterial.SetVector("_GerstnerVertexIntensity",new Vector4(.035f,.045f,.035f,7));
        var water=Make(50);water.sharedMaterials=new[]{waterMaterial};
        var waterFilter=new MeshFilter{sharedMesh=originalMesh};water.Components.Add(waterFilter);
        var waterHigh=Make(60);waterHigh.sharedMaterials=new[]{waterMaterial};
        var waterLow=Make(60);var waterLod=new LODGroup{Lods=new[]{new LOD{renderers=new[]{waterHigh}},new LOD{renderers=new[]{waterLow}}}};
        check(!waterMaterial.HasProperty("_ReflectionTex")&&AiIslandVisibility.IsWaterMaterial(waterMaterial),"Lux Water without reflection property is still recognized as water");
        var gerstner=new Material(new Shader{name="Custom/VertexWaves"});gerstner.SetVector("_GFinalFrequency",new Vector4(1,2,3,4));
        check(AiIslandVisibility.IsWaterMaterial(gerstner)&&AiIslandVisibility.IsWaterMaterial(new Material(new Shader{name="AQUAS/Desktop and Web"}))&&
            AiIslandVisibility.IsWaterMaterial(new Material(new Shader{name="NatureManufacture Shaders/Water/Water River Tesseled Vertex Color Flow"})),"wave properties, AQUAS and river shaders are protected");
        check(!AiIslandVisibility.HasLodTexture(whiteCard.sharedMaterials[0]),"untextured white card is not a valid distant LOD");
        var alternative=new Material(new Shader{name="Custom/Albedo"});alternative.SetTexture("_BaseMap",high.sharedMaterials[0].GetTexture("_MainTex"));
        check(AiIslandVisibility.HasLodTexture(alternative),"native texture binding outside MainTex remains usable without shader replacement");
        var owner=new AiIslandVisibility();owner.Components.AddRange(new Component[]{high,low,whiteCard,lod,standalone,water,waterHigh,waterLow,waterLod});
        owner.Initialize(camera);var group=CullingGroup.Last;
        check(ReferenceEquals(waterFilter.sharedMesh,originalMesh)&&ReferenceEquals(water.sharedMaterials[0],waterMaterial)&&water.receiveShadows,
            "water keeps original dense mesh, shader and shadow settings even when a matching simplified mesh exists");
        check(waterLod.ForceCalls==0&&waterLod.ForcedLevel==-1&&owner.Snapshot().Contains("groups=2 "),"entire water LOD group is excluded and cannot re-enter standalone processing");
        check(lod.ForcedLevel==0,"near geometry selects authored detailed LOD");
        simplified=filter.sharedMesh;
        check(owner.PreparedMeshCount==1&&simplified!=originalMesh,"static object uses offline simplified geometry without reading source vertices at runtime");
        check(simplified.Indices.Values.All(indices=>indices.Length>0&&indices.All(i=>i>=0&&i<simplified.vertexCount)),"prepared geometry keeps valid indices and nonempty material surfaces");
        group.onStateChanged(new CullingGroupEvent{index=0,currentDistance=2,isVisible=true});
        check(lod.ForcedLevel==1,"10m skips white terminal card and empty cull level in favor of textured polygon LOD");
        check(!low.forceRenderingOff&&!high.forceRenderingOff&&!low.enabled,"LOD selection leaves renderer visibility flags untouched");
        check(low.shadowCastingMode==ShadowCastingMode.Off&&!low.receiveShadows,"distant LOD removes dynamic shadow work");
        check(ReferenceEquals(low.sharedMaterials[0],originalMaterial)&&originalMaterial.shader.name=="Nature/SpeedTree"&&originalMaterial.GetTexture("_MainTex")!=null,
            "distant foliage retains original specialized shader and alpha texture instead of opaque Diffuse replacement");
        int calls=lod.ForceCalls;
        group.onStateChanged(new CullingGroupEvent{index=0,currentDistance=1,isVisible=false});
        check(lod.ForcedLevel==1&&lod.ForceCalls==calls,"9 to 10m hysteresis avoids repeated switching");
        group.onStateChanged(new CullingGroupEvent{index=0,currentDistance=2,isVisible=false});
        check(lod.ForcedLevel==1&&lod.ForceCalls==calls&&!low.forceRenderingOff,"offscreen event never hides scenery or repeats unchanged assignment");
        check(waterLod.ForceCalls==0&&ReferenceEquals(waterFilter.sharedMesh,originalMesh)&&
            waterMaterial.GetVector("_GerstnerVertexIntensity").Equals(new Vector4(.035f,.045f,.035f,7)),"distance and visibility notifications never alter water tessellation or wave parameters");
        group.onStateChanged(new CullingGroupEvent{index=0,currentDistance=0,isVisible=true});
        check(lod.ForcedLevel==0&&ReferenceEquals(low.sharedMaterials[0],originalMaterial)&&low.receiveShadows,"inside 9m restores detail, original material and shadows");
        group.onStateChanged(new CullingGroupEvent{index=1,currentDistance=0,isVisible=true});
        check(ReferenceEquals(filter.sharedMesh,originalMesh),"approaching restores exact original standalone mesh");
        check(Physics.Rays==0&&owner.PendingJobs==0&&owner.Snapshot().Contains("managedScansPerFrame=0"),"distance LOD uses no rays or scan coroutine");
        owner.RequestRefresh();check(lod.ForceCalls==calls+1,"unchanged map placement does no renderer traversal");
        return owner;
    }
    internal static void Cleanup(AiIslandVisibility owner,Action<bool,string> check)
    {
        typeof(AiIslandVisibility).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
        check(CullingGroup.Last.Disposed&&lod.ForcedLevel==-1,"cleanup releases native distance notifications and restores automatic authored LOD");
        check(ReferenceEquals(filter.sharedMesh,originalMesh)&&!originalMesh.Destroyed&&simplified.Destroyed,"cleanup restores source mesh and destroys only generated copy");
        check(ReferenceEquals(low.sharedMaterials[0],originalMaterial)&&!originalMaterial.Destroyed,"cleanup restores source material without destroying shared assets");
    }
}
