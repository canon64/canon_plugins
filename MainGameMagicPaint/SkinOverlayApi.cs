using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameMagicPaint
{
    // One compositor owns _MainTex. Paint and wax are independent inputs, never nested
    // snapshots of each other's output, so removal and draw order cannot erase the other.
    public static class SkinOverlayApi
    {
        public const int PaintOrder=100, WaxOrder=200;
        internal sealed class Binding
        {
            internal int Index;
            internal Texture Source;
            internal RenderTexture Target;
            internal MaterialPropertyBlock Saved;
        }
        sealed class Layer {internal string Owner;internal Texture Texture;internal int Order;}
        sealed class Entry {internal Renderer Renderer;internal readonly List<Layer> Layers=new List<Layer>();internal readonly List<Binding> Bindings=new List<Binding>();}
        static readonly Dictionary<Renderer,Entry> Entries=new Dictionary<Renderer,Entry>();
        static readonly int MainTex=Shader.PropertyToID("_MainTex");
        static Material _blend;
        public static bool SetLayer(string owner,Renderer renderer,Texture texture,int order)
        {
            if(string.IsNullOrEmpty(owner)||renderer==null||texture==null||!CanonGate.GateApi.CanRun(owner))return false;
            if(!Entries.TryGetValue(renderer,out var entry)){entry=new Entry{Renderer=renderer};Entries.Add(renderer,entry);}
            var layer=entry.Layers.Find(l=>l.Owner==owner);
            if(layer==null){layer=new Layer{Owner=owner};entry.Layers.Add(layer);}
            layer.Texture=texture;layer.Order=order;
            entry.Layers.Sort((a,b)=>{int n=a.Order.CompareTo(b.Order);return n!=0?n:string.CompareOrdinal(a.Owner,b.Owner);});
            RefreshBindings(entry);Compose(entry);return entry.Bindings.Count>0;
        }
        public static void RemoveLayer(string owner,Renderer renderer)
        {
            if(ReferenceEquals(renderer,null)||!Entries.TryGetValue(renderer,out var entry))return;
            entry.Layers.RemoveAll(l=>l.Owner==owner);
            if(entry.Layers.Count>0&&renderer!=null){RefreshBindings(entry);Compose(entry);return;}
            Release(entry);Entries.Remove(renderer);
            if(Entries.Count==0&&_blend!=null){UnityEngine.Object.Destroy(_blend);_blend=null;}
        }
        public static void Refresh(Renderer renderer)
        {
            if(renderer!=null&&Entries.TryGetValue(renderer,out var entry)){RefreshBindings(entry);Compose(entry);}
        }
        internal static IEnumerable<Binding> GetBindings(Renderer renderer)
        {
            return renderer!=null&&Entries.TryGetValue(renderer,out var entry)?entry.Bindings:(IEnumerable<Binding>)Array.Empty<Binding>();
        }
        static void RefreshBindings(Entry entry)
        {
            var renderer=entry.Renderer;var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var material=materials[i];if(material==null||!material.HasProperty(MainTex))continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                var common=new MaterialPropertyBlock();renderer.GetPropertyBlock(common);
                var binding=entry.Bindings.Find(b=>b.Index==i);
                Texture current=block.GetTexture(MainTex)??common.GetTexture(MainTex)??material.GetTexture(MainTex);
                if(binding!=null&&current==binding.Target)
                    current=binding.Saved.GetTexture(MainTex)??common.GetTexture(MainTex)??material.GetTexture(MainTex);
                else if(binding!=null)binding.Saved=block;
                if(current==null)continue;
                if(binding==null){binding=new Binding{Index=i,Saved=block};entry.Bindings.Add(binding);}
                binding.Source=current;
                int width=Mathf.Min(4096,current.width),height=Mathf.Min(4096,current.height);
                if(binding.Target!=null&&(binding.Target.width!=width||binding.Target.height!=height))
                {binding.Target.Release();UnityEngine.Object.Destroy(binding.Target);binding.Target=null;}
                if(binding.Target==null)
                {
                    binding.Target=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="Skin paint + wax",wrapMode=TextureWrapMode.Clamp};
                    if(!binding.Target.Create())throw new InvalidOperationException("肌の重ね描画先を作成できません");
                }
            }
        }
        static void Compose(Entry entry)
        {
            if(entry.Renderer==null)return;
            if(_blend==null)
            {
                var shader=Shader.Find("UI/Default")??Shader.Find("Unlit/Transparent");
                if(shader==null)throw new InvalidOperationException("肌の重ね描画用シェーダーがありません");
                _blend=new Material(shader){name="Skin overlay compositor",color=Color.white};
                if(_blend.HasProperty("unity_GUIZTestMode"))_blend.SetInt("unity_GUIZTestMode",(int)CompareFunction.Always);
            }
            var previous=RenderTexture.active;
            try
            {
                foreach(var binding in entry.Bindings)
                {
                    if(binding.Source==null||binding.Target==null)continue;
                    Graphics.Blit(binding.Source,binding.Target);RenderTexture.active=binding.Target;
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadOrtho();
                        foreach(var layer in entry.Layers)
                        {
                            if(layer.Texture==null)continue;_blend.mainTexture=layer.Texture;
                            if(!_blend.SetPass(0))throw new InvalidOperationException("肌の重ね描画パスを使用できません");
                            GL.Begin(GL.QUADS);GL.Color(Color.white);
                            GL.TexCoord2(0,0);GL.Vertex3(0,0,.1f);GL.TexCoord2(1,0);GL.Vertex3(1,0,.1f);
                            GL.TexCoord2(1,1);GL.Vertex3(1,1,.1f);GL.TexCoord2(0,1);GL.Vertex3(0,1,.1f);GL.End();
                        }
                    }
                    finally{GL.PopMatrix();}
                    var block=new MaterialPropertyBlock();entry.Renderer.GetPropertyBlock(block,binding.Index);
                    if(block.isEmpty)entry.Renderer.GetPropertyBlock(block);
                    block.SetTexture(MainTex,binding.Target);entry.Renderer.SetPropertyBlock(block,binding.Index);
                }
            }
            finally{RenderTexture.active=previous;}
        }
        static void Release(Entry entry)
        {
            foreach(var binding in entry.Bindings)
            {
                if(entry.Renderer!=null&&binding.Index<entry.Renderer.sharedMaterials.Length)
                {
                    var now=new MaterialPropertyBlock();entry.Renderer.GetPropertyBlock(now,binding.Index);
                    if(now.GetTexture(MainTex)==binding.Target)entry.Renderer.SetPropertyBlock(binding.Saved.isEmpty?null:binding.Saved,binding.Index);
                }
                if(binding.Target!=null){binding.Target.Release();UnityEngine.Object.Destroy(binding.Target);}
            }
            entry.Bindings.Clear();entry.Layers.Clear();
        }
    }
}
