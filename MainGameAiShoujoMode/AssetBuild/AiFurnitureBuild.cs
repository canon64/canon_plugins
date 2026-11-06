using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MapEnvironmentRuntime;

// Offline reconstruction; original AI behaviours are never brought into the KKS bundle.
public static class AiFurnitureBuild
{
    [Serializable] public class Slot { public string id, path, pivot, recovery, action, sourcePoint, status, error; public Vector3 approach, position, rotation, exit; }
    [Serializable] public class ColorOverride { public string path; public int material; public bool isColor1,isColor2,isColor3,isEmission; public Color defColor1,defColor2,defColor3,defEmissionColor; }
    [Serializable] public class Item { public int id, category; public string name, nativeBundle, sourceBundle, sourceAsset, status, error, bundle, asset; public Slot[] slots; public ColorOverride[] colors; public Vector3 center, size; }
    [Serializable] public class Input { public string output, generated; public Item[] items; }
    private static readonly Dictionary<UnityEngine.Object,UnityEngine.Object> Assets = new Dictionary<UnityEngine.Object,UnityEngine.Object>();
    private static string _generated;
    public static void FinalizeAssets()
    {
        SandboxBuild.VerifyAudit();var p=SandboxBuild.Settings();
        var input=JsonUtility.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_FURNITURE_INPUT")));
        var built=JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(input.output,"catalog.json")));
        var builds=new List<AssetBundleBuild>();
        foreach(var group in built.items.Where(i=>i.status=="converted").GroupBy(i=>i.bundle))
        {
            var paths=new List<string>();var names=new List<string>();
            foreach(var item in group)
            {
                var source=input.items.Single(i=>i.id==item.id);item.slots=source.slots;item.colors=source.colors;
                var directory=input.generated+"/"+Path.GetFileNameWithoutExtension(item.sourceBundle);
                var path=directory+"/"+item.asset+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var color in source.colors??Array.Empty<ColorOverride>())
                    {
                        var node=string.IsNullOrEmpty(color.path)?root.transform:root.transform.Find(color.path);
                        var renderer=node==null?null:node.GetComponent<Renderer>();
                        if(renderer==null||color.material>=renderer.sharedMaterials.Length)throw new InvalidDataException("Native color renderer missing: "+item.id);
                        var materials=renderer.sharedMaterials;var material=new Material(materials[color.material]);
                        if(color.isColor1)material.color=color.defColor1;
                        if(color.isEmission){material.SetColor("_EmissionColor",color.defEmissionColor);material.EnableKeyword("_EMISSION");}
                        var materialPath=directory+"/color_"+item.id+"_"+paths.Count+"_"+Array.IndexOf(source.colors,color)+".mat";
                        AssetDatabase.CreateAsset(material,materialPath);materials[color.material]=material;renderer.sharedMaterials=materials;
                    }
                    foreach(var slot in item.slots??Array.Empty<Slot>())
                    {
                        var point=root.transform.Find(slot.path);
                        var pivot=point==null?null:point.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==slot.pivot);
                        var exit=point==null?null:point.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==slot.recovery);
                        if(point==null||pivot==null||exit==null){slot.status="unresolved";slot.error="Native sleep pivot or recovery missing";continue;}
                        slot.approach=root.transform.InverseTransformPoint(point.position);slot.position=root.transform.InverseTransformPoint(pivot.position);
                        slot.rotation=(Quaternion.Inverse(root.transform.rotation)*pivot.rotation).eulerAngles;slot.exit=root.transform.InverseTransformPoint(exit.position);slot.status="ready";slot.error="";
                    }
                    if(PrefabUtility.SaveAsPrefabAsset(root,path)==null)throw new IOException("Could not finalize furniture");
                    paths.Add(path);names.Add(item.asset);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            builds.Add(new AssetBundleBuild{assetBundleName=group.Key,assetNames=paths.ToArray(),addressableNames=names.ToArray()});
        }
        AssetDatabase.SaveAssets();
        if(BuildPipeline.BuildAssetBundles(input.output,builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)==null)throw new IOException("Final furniture bundle build failed");
        foreach(var group in built.items.Where(i=>i.status=="converted").GroupBy(i=>i.bundle))
        {
            var bundle=AssetBundle.LoadFromFile(Path.Combine(input.output,group.Key));
            try{foreach(var item in group){var prefab=bundle==null?null:bundle.LoadAsset<GameObject>(item.asset);if(prefab==null||prefab.GetComponentsInChildren<MonoBehaviour>(true).Any())throw new InvalidDataException("Packed furniture validation failed");}}
            finally{if(bundle!=null)bundle.Unload(true);}
        }
        File.WriteAllText(Path.Combine(input.output,"catalog.json"),JsonUtility.ToJson(built,true));
        if(!built.items.Any(i=>i.id==p.I("furniture.referenceBed")&&i.slots.Any(s=>s.status=="ready")))throw new InvalidDataException("Reference bed missing");
        Debug.Log("AI_FURNITURE_BUILD_PASS finalized="+built.items.Length+" slots="+built.items.Sum(i=>(i.slots??Array.Empty<Slot>()).Count(s=>s.status=="ready")));
    }
    private static GameObject Geometry(GameObject original)
    {
        var nodes = new Dictionary<Transform,Transform>();
        GameObject Copy(Transform source,Transform parent)
        {
            var value=new GameObject(source.name);value.transform.SetParent(parent,false);
            value.transform.localPosition=source.localPosition;value.transform.localRotation=source.localRotation;value.transform.localScale=source.localScale;
            value.SetActive(source.gameObject.activeSelf);nodes.Add(source,value.transform);
            foreach(Transform child in source)Copy(child,value.transform);
            return value;
        }
        var root=Copy(original.transform,null);
        foreach(var pair in nodes)
        {
            var source=pair.Key;var target=pair.Value.gameObject;
            var filter=source.GetComponent<MeshFilter>();if(filter!=null)target.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var renderer=source.GetComponent<MeshRenderer>();
            if(renderer!=null && renderer.sharedMaterials.All(m=>m!=null))EditorUtility.CopySerialized(renderer,target.AddComponent<MeshRenderer>());
            var skin=source.GetComponent<SkinnedMeshRenderer>();
            if(skin!=null && skin.sharedMaterials.All(m=>m!=null))
            {
                var copy=target.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(skin,copy);
                copy.bones=skin.bones.Select(b=>b!=null&&nodes.TryGetValue(b,out var mapped)?mapped:null).ToArray();
                copy.rootBone=skin.rootBone!=null&&nodes.TryGetValue(skin.rootBone,out var bone)?bone:null;
            }
            foreach(var collider in source.GetComponents<Collider>())
                if(collider is BoxCollider||collider is SphereCollider||collider is CapsuleCollider||collider is MeshCollider)
                    EditorUtility.CopySerialized(collider,target.AddComponent(collider.GetType()));
        }
        return root;
    }
    private static T Persist<T>(T original) where T:UnityEngine.Object
    {
        if (original == null) return null;
        if (Assets.TryGetValue(original, out var cached)) return (T)cached;
        var copy = UnityEngine.Object.Instantiate(original); copy.name = original.name;
        string path = _generated + "/asset_" + Assets.Count + ".asset";
        AssetDatabase.CreateAsset(copy, path); Assets.Add(original, copy); return copy;
    }
    private static Material Material(Material source)
    {
        if (source == null) throw new InvalidDataException("Missing furniture material");
        if (Assets.TryGetValue(source, out var old)) return (Material)old;
        var material = new Material(Shader.Find("Standard")) { name = source.name };
        if (source.HasProperty("_Color")) material.color = source.GetColor("_Color");
        foreach (var key in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
            if (source.HasProperty(key) && source.GetTexture(key) is Texture2D texture)
            {
                material.SetTexture(key, Persist(texture)); material.SetTextureOffset(key, source.GetTextureOffset(key)); material.SetTextureScale(key, source.GetTextureScale(key));
                if (key == "_BumpMap") material.EnableKeyword("_NORMALMAP");
                if (key == "_EmissionMap") material.EnableKeyword("_EMISSION");
            }
        if (source.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", source.GetFloat("_Glossiness"));
        if (source.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        string path = _generated + "/material_" + Assets.Count + ".mat"; AssetDatabase.CreateAsset(material,path); Assets.Add(source,material); return material;
    }
    public static void Build()
    {
        SandboxBuild.VerifyAudit(); var p = SandboxBuild.Settings();
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_FURNITURE_INPUT")));
        Directory.CreateDirectory(input.output); Directory.CreateDirectory(input.generated);
        var builds = new List<AssetBundleBuild>();
        foreach (var group in input.items.GroupBy(i => i.nativeBundle))
        {
            var bundle = AssetBundle.LoadFromFile(group.Key);
            var paths = new List<string>();var names=new List<string>();
            _generated = input.generated + "/" + Path.GetFileNameWithoutExtension(group.Key); Directory.CreateDirectory(_generated); Assets.Clear();
            try
            {
                foreach (var item in group)
                {
                    if (item.status == "unresolved") continue;
                    GameObject clone = null;
                    try
                    {
                        if (bundle == null) throw new IOException("Native furniture bundle could not be loaded");
                        var original = bundle.LoadAsset<GameObject>(item.sourceAsset);
                        if (original == null) throw new IOException("Native furniture prefab missing");
                        clone = Geometry(original); clone.name = "Furniture_" + item.id;
                        clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                        foreach (var node in clone.GetComponentsInChildren<Transform>(true))
                        {
                            node.gameObject.layer = p.I("math.zero"); node.gameObject.tag = "Untagged";
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
                        }
                        foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                        foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                        foreach (var animator in clone.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                        foreach (var animation in clone.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
                        foreach (var filter in clone.GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh = Persist(filter.sharedMesh);
                        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                        {
                            if (renderer is SkinnedMeshRenderer skin) skin.sharedMesh = Persist(skin.sharedMesh);
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(Material).ToArray();
                        }
                        foreach (var collider in clone.GetComponentsInChildren<MeshCollider>(true)) collider.sharedMesh = Persist(collider.sharedMesh);
                        var renderers = clone.GetComponentsInChildren<Renderer>(true);
                        if (!renderers.Any()) throw new InvalidDataException("Furniture has no renderer");
                        var bounds = renderers.First().bounds; foreach (var renderer in renderers.Skip(p.I("math.one"))) bounds.Encapsulate(renderer.bounds);
                        item.center = bounds.center; item.size = bounds.size;
                        if (!clone.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger))
                        { var collider = clone.AddComponent<BoxCollider>(); collider.center = item.center; collider.size = item.size; }
                        foreach (var slot in item.slots ?? Array.Empty<Slot>())
                        {
                            var point = clone.transform.Find(slot.path);
                            var pivot = point == null ? null : point.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == slot.pivot);
                            var exit = point == null ? null : point.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == slot.recovery);
                            if (point == null || pivot == null || exit == null) { slot.status = "unresolved"; slot.error = "Native sleep pivot or recovery missing"; continue; }
                            slot.approach = clone.transform.InverseTransformPoint(point.position); slot.position = clone.transform.InverseTransformPoint(pivot.position);
                            slot.rotation = (Quaternion.Inverse(clone.transform.rotation) * pivot.rotation).eulerAngles;
                            slot.exit = clone.transform.InverseTransformPoint(exit.position); slot.status = "ready";
                        }
                        string path = _generated + "/" + clone.name + ".prefab";
                        if(PrefabUtility.SaveAsPrefabAsset(clone,path)==null)throw new IOException("Converted prefab could not be saved");
                        item.bundle = "furniture_" + Path.GetFileNameWithoutExtension(group.Key) + ".bundle"; item.asset = clone.name; item.status = "converted";
                        paths.Add(path);names.Add(item.asset);
                    }
                    catch (Exception error) { item.status = "failed"; item.error = error.GetBaseException().Message; }
                    finally { if (clone != null) UnityEngine.Object.DestroyImmediate(clone); }
                }
                AssetDatabase.SaveAssets();
                if(paths.Any())builds.Add(new AssetBundleBuild{assetBundleName="furniture_"+Path.GetFileNameWithoutExtension(group.Key)+".bundle",assetNames=paths.ToArray(),addressableNames=names.ToArray()});
            }
            finally { if (bundle != null) bundle.Unload(true); Assets.Clear(); }
        }
        if (!builds.Any()) throw new InvalidDataException("No furniture converted");
        if(BuildPipeline.BuildAssetBundles(input.output,builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)==null)throw new IOException("Furniture bundle build failed");
        foreach (var group in input.items.Where(i => i.status == "converted").GroupBy(i=>i.bundle))
        {
            var bundle = AssetBundle.LoadFromFile(Path.Combine(input.output,group.Key));
            try {foreach(var item in group)if(bundle==null||bundle.LoadAsset<GameObject>(item.asset)==null){item.status="failed";item.error="Packed furniture verification failed";}}
            finally { if (bundle != null) bundle.Unload(true); }
        }
        File.WriteAllText(Path.Combine(input.output,"catalog.json"), JsonUtility.ToJson(input,true));
        if(!input.items.Any(i=>i.id==p.I("furniture.referenceBed")&&i.status=="converted"&&i.slots.Any(s=>s.status=="ready")))throw new InvalidDataException("Reference bed conversion/anchors failed");
        Debug.Log("AI_FURNITURE_BUILD_PASS converted=" + input.items.Count(i => i.status == "converted") + " failed=" + input.items.Count(i => i.status != "converted"));
    }
}
