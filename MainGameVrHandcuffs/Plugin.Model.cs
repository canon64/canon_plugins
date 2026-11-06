using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        private readonly List<UnityEngine.Object> _modelResources=new List<UnityEngine.Object>();
        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        private void CreateRig()
        {
            _rig=new GameObject("Handcuffs_AB_ThreeLinks");
            var shader=Shader.Find("Standard");
            if (shader==null) throw new InvalidOperationException("Standard shader is unavailable");
            var metal=new Material(shader) { name="Handcuff silver",color=new Color(.55f,.57f,.60f,1) };
            metal.SetFloat("_Metallic",1); metal.SetFloat("_Glossiness",.72f); _modelResources.Add(metal);
            using (var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Handcuffs.Model"))
            using (var reader=new BinaryReader(stream ?? throw new InvalidOperationException("Handcuff mesh resource missing")))
            {
                if (reader.ReadInt32()!=0x48434631 || reader.ReadInt32()!=5) throw new InvalidDataException("Handcuff resource version");
                int count=reader.ReadInt32();
                if (count<5 || count>100) throw new InvalidDataException("Handcuff resource object count");
                for (int i=0;i<5;i++)
                {
                    var go=new GameObject(i<2?"Handcuff_"+(i==0?"A":"B"):"ChainLink_"+(i-1));
                    go.transform.SetParent(_rig.transform,false); go.transform.localPosition=ReadVector(reader);
                    _parts[i]=go.transform;
                }
                for (int i=0;i<count;i++)
                {
                    int group=reader.ReadInt32(),length=reader.ReadInt32();
                    if (group<0 || group>4 || length<1 || length>200) throw new InvalidDataException("Handcuff mesh header");
                    string name=Encoding.UTF8.GetString(reader.ReadBytes(length)); int size=reader.ReadInt32();
                    if (size<3 || size>1000000 || size%3!=0) throw new InvalidDataException("Handcuff mesh vertices");
                    var verts=new Vector3[size]; var normals=new Vector3[size]; var indices=new int[size];
                    for (int j=0;j<size;j++) { verts[j]=ReadVector(reader); normals[j]=ReadVector(reader); indices[j]=j; }
                    var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32,vertices=verts,normals=normals,triangles=indices };
                    mesh.RecalculateBounds(); _modelResources.Add(mesh);
                    var go=new GameObject(name); go.transform.SetParent(_parts[group],false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=metal;
                }
                if (stream.Position!=stream.Length) throw new InvalidDataException("Handcuff resource trailing data");
            }
            for (int i=0;i<2;i++)
            {
                var eye=new GameObject("ChainEye_"+(i==0?"A":"B")).transform;
                eye.SetParent(_parts[i],false);eye.position=new Vector3(i==0?-.025f:.025f,0,.040f);
                _chainEyes[i]=eye;
            }
            for (int i=0;i<2;i++)
            {
                var target=new GameObject("WristIKTarget_"+(i==0?"A":"B"));
                target.transform.SetParent(_parts[i],false); _wristTargets[i]=target.transform;
            }
            CreateAttachmentAudio();
        }
        private void DestroyModelResources()
        {
            foreach (var resource in _modelResources) if (resource!=null) Destroy(resource);
            _modelResources.Clear();
        }
    }
}
