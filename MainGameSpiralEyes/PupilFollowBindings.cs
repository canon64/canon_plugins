using System;
using System.Collections.Generic;
using UnityEngine;
namespace MainGameSpiralEyes
{
 // Uses the native material instance, so the game's UV/rotation writes reach
 // both passes without a frame callback, material copy or second renderer.
 internal sealed class PupilFollowBindings
 {
  sealed class Entry
  {
   public Renderer Renderer;public int Index;public Material Material;public Shader OriginalShader;
   public int Queue;public bool Suppressed;public float ExpressionPower;
  }
  readonly List<Entry> _entries=new List<Entry>();
  readonly Shader _shader,_utsShader;
  public PupilFollowBindings(Shader shader,Shader utsShader=null)
  {
   if(shader==null||!shader.isSupported||shader.name!="canon/PupilFollow")throw new InvalidOperationException("瞳追従シェーダーを使用できません。");
   if(utsShader!=null&&(!utsShader.isSupported||utsShader.name!="canon/PupilFollowKKUTSeye"))throw new InvalidOperationException("KKUTSeye用の瞳追従シェーダーを使用できません。");
   _shader=shader;_utsShader=utsShader;
  }
  bool IsFollowShader(Shader shader)=>shader==_shader||(_utsShader!=null&&shader==_utsShader);
  public int Count=>_entries.Count;
  public bool HasBinding(Renderer renderer)=>_entries.Exists(e=>e.Renderer==renderer);
  public void Apply(Renderer renderer,Texture texture,Vector2 position,float opacity,int side,bool suppressExpression)
  {
   if(renderer==null||texture==null)throw new InvalidOperationException("瞳追従に必要な目素材がありません。");
   var materials=renderer.sharedMaterials;
   for(int i=_entries.Count-1;i>=0;i--)
   {
    var entry=_entries[i];if(entry.Renderer!=renderer)continue;
    if(entry.Index>=materials.Length||materials[entry.Index]!=entry.Material||!IsFollowShader(entry.Material.shader)){Restore(entry);_entries.RemoveAt(i);}
   }
   bool bound=false;
   for(int index=0;index<materials.Length;index++)
   {
    var material=materials[index];if(material==null)continue;
    var entry=_entries.Find(e=>e.Renderer==renderer&&e.Index==index);
    if(entry==null)
    {
     if(material.shader==null)continue;
     var target=material.shader.name=="Koikano/main_eye"?_shader:material.shader.name=="KKUTSeye"?_utsShader:null;
     if(target==null)continue;
     if(!material.HasProperty("_MainTex")||!material.HasProperty("_expression")||!material.HasProperty("_rotation"))continue;
     entry=new Entry{Renderer=renderer,Index=index,Material=material,OriginalShader=material.shader,Queue=material.renderQueue};
     _entries.Add(entry);
     var keywords=material.shaderKeywords;
     material.shader=target;material.shaderKeywords=keywords;material.renderQueue=entry.Queue;
    }
    material.SetTexture("_CanonSpiral",texture);
    material.SetVector("_CanonSpiralPosition",new Vector4(position.x,position.y,0,0));
    material.SetFloat("_CanonSpiralOpacity",Mathf.Clamp01(opacity));material.SetFloat("_CanonSpiralSide",side);
    if(suppressExpression&&!entry.Suppressed){entry.ExpressionPower=material.GetFloat("_exppower");entry.Suppressed=true;material.SetFloat("_exppower",0);}
    else if(!suppressExpression&&entry.Suppressed){if(material.GetFloat("_exppower")==0)material.SetFloat("_exppower",entry.ExpressionPower);entry.Suppressed=false;}
    bound=true;
   }
   if(!bound)throw new InvalidOperationException("瞳追従に対応する目素材がありません（Koikano/main_eye・KKUTSeye）。詳細は SPIRAL_BINDING_FAILURE を確認してください。");
  }
  public void SetOpacity(Renderer renderer,float opacity)
  {foreach(var e in _entries)if(e.Renderer==renderer&&e.Material!=null&&IsFollowShader(e.Material.shader))e.Material.SetFloat("_CanonSpiralOpacity",Mathf.Clamp01(opacity));}
  public void SetOrbit(Renderer renderer,Vector4 orbit)
  {foreach(var e in _entries)if(e.Renderer==renderer&&e.Material!=null&&IsFollowShader(e.Material.shader))e.Material.SetVector("_CanonSpiralOrbit",orbit);}
  void Restore(Entry e)
  {
   var m=e.Material;if(m==null)return;
   // A later shader replacement belongs to the game or another mod.
   if(!IsFollowShader(m.shader))return;
   if(e.Suppressed&&m.GetFloat("_exppower")==0)m.SetFloat("_exppower",e.ExpressionPower);
   m.SetTexture("_CanonSpiral",null);m.SetFloat("_CanonSpiralOpacity",0);m.SetVector("_CanonSpiralOrbit",Vector4.zero);
   var keywords=m.shaderKeywords;int queue=m.renderQueue;m.shader=e.OriginalShader;m.shaderKeywords=keywords;m.renderQueue=queue;
  }
  public void Restore(Renderer renderer)
  {for(int i=_entries.Count-1;i>=0;i--)if(_entries[i].Renderer==renderer){Restore(_entries[i]);_entries.RemoveAt(i);}}
  public void Restore(){foreach(var e in _entries)Restore(e);_entries.Clear();}
 }
}
