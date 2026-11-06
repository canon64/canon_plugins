using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class DedicatedRigSnapshot : IDevelopmentCommand
{
    public string Name => "kks-actor-rig-snapshot";
    [Serializable] public sealed class Dump { public string utc; public int frame; public Actor[] actors; }
    [Serializable] public sealed class Actor { public int id, sex; public string name, scene, animationRoot; public Node[] nodes; public Skin[] skins; }
    [Serializable] public sealed class Node { public string path, actorPath; public Vector3 localPosition,localScale,worldPosition; public Quaternion localRotation,worldRotation; public int layer; public bool active; public ComponentInfo[] components; }
    [Serializable] public sealed class ComponentInfo { public string type, material, mesh; public bool enabled, trigger, convex, isKinematic, useGravity; public float radius,height,mass,drag,angularDrag; public int direction,constraints; public Vector3 center,size; public Scalar[] nativeFields; }
    [Serializable] public sealed class Scalar { public string name,value; }
    [Serializable] public sealed class Skin { public string path,mesh,rootBone; public int meshId; public bool enabled; public string[] bones; }
    static string PathOf(Transform root,Transform node)
    {
        if(node==null)return null;
        var parts=new List<string>();var current=node;
        while(current!=null && current!=root) {parts.Add(current.name);current=current.parent;}
        if(current!=root)parts.Add("<outside-root>");
        parts.Reverse();return string.Join("/",parts);
    }
    static ComponentInfo Describe(Component component)
    {
        if(component==null)return new ComponentInfo {type="<missing-script>"};
        var value=new ComponentInfo {type=component.GetType().AssemblyQualifiedName,enabled=!(component is Behaviour behavior)||behavior.enabled};
        if(component is Collider collider) {
            value.enabled=collider.enabled;value.trigger=collider.isTrigger;value.material=collider.sharedMaterial?.name;
            if(collider is SphereCollider sphere){value.center=sphere.center;value.radius=sphere.radius;}
            if(collider is CapsuleCollider capsule){value.center=capsule.center;value.radius=capsule.radius;value.height=capsule.height;value.direction=capsule.direction;}
            if(collider is BoxCollider box){value.center=box.center;value.size=box.size;}
            if(collider is MeshCollider mesh){value.mesh=mesh.sharedMesh?.name;value.convex=mesh.convex;}
        }
        if(component is Rigidbody body){value.isKinematic=body.isKinematic;value.useGravity=body.useGravity;value.mass=body.mass;value.drag=body.drag;value.angularDrag=body.angularDrag;value.constraints=(int)body.constraints;}
        if(component.GetType().Name.IndexOf("Collider",StringComparison.OrdinalIgnoreCase)>=default(int)) {
            value.nativeFields=component.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)
                .Where(f=>f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(Vector3))
                .Select(f=>new Scalar{name=f.Name,value=Convert.ToString(f.GetValue(component),System.Globalization.CultureInfo.InvariantCulture)}).ToArray();
        }
        return value;
    }
    public object Execute(Dictionary<string,object> arguments,DevelopmentContext context)
    {
        int requested=arguments.TryGetValue("actorId",out var id)?Convert.ToInt32(id):default(int);
        var actors=Resources.FindObjectsOfTypeAll<ChaControl>().Where(a=>a!=null&&a.gameObject.scene.IsValid()&&a.gameObject.scene.isLoaded&&a.animBody!=null&&(requested==default(int)||a.GetInstanceID()==requested)).ToArray();
        if(!actors.Any())throw new InvalidOperationException("No initialized real actor matches the requested instance.");
        var dump=new Dump {utc=DateTime.UtcNow.ToString("O"),frame=Time.frameCount,actors=actors.Select(a=>new Actor {
            id=a.GetInstanceID(),sex=a.sex,name=a.name,scene=a.gameObject.scene.name,animationRoot=PathOf(a.transform,a.animBody.transform),
            nodes=a.animBody.GetComponentsInChildren<Transform>(true).Select(t=>new Node {path=PathOf(a.animBody.transform,t),actorPath=PathOf(a.transform,t),localPosition=t.localPosition,localRotation=t.localRotation,localScale=t.localScale,worldPosition=t.position,worldRotation=t.rotation,layer=t.gameObject.layer,active=t.gameObject.activeInHierarchy,components=t.GetComponents<Component>().Select(Describe).ToArray()}).ToArray(),
            skins=a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>new Skin {path=PathOf(a.transform,r.transform),mesh=r.sharedMesh?.name,meshId=r.sharedMesh!=null?r.sharedMesh.GetInstanceID():default(int),enabled=r.enabled,rootBone=PathOf(a.transform,r.rootBone),bones=r.bones.Select(b=>PathOf(a.transform,b)).ToArray()}).ToArray()
        }).ToArray()};
        Directory.CreateDirectory(context.OutputDirectory);
        string file=System.IO.Path.Combine(context.OutputDirectory,"actor-rig-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".json");
        using (var stream = File.Create(file)) new DataContractJsonSerializer(typeof(Dump)).WriteObject(stream,dump);
        return new {path=file,frame=dump.frame,actors=dump.actors.Select(a=>new {a.id,a.name,a.sex,a.scene,a.animationRoot,nodes=a.nodes.Length,skins=a.skins.Length}).ToArray()};
    }
    public void Dispose() { }
}
