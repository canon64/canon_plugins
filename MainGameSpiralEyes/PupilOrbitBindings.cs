using System;
using System.Collections.Generic;
using UnityEngine;
namespace MainGameSpiralEyes
{
 internal sealed class PupilOrbitBindings:IDisposable
 {
  sealed class Slot {public Renderer Renderer;public int Index,Property,Side;public float Direction,Phase;public Texture Original;public Material Material;public CustomRenderTexture Texture;public Vector2 Scale;}
  sealed class Saved {public Renderer Renderer;public int Index;public MaterialPropertyBlock Block;}
  readonly List<Slot> _slots=new List<Slot>();readonly List<Saved> _saved=new List<Saved>();readonly Shader _shader;
  float _radius,_speed=.5f,_phase,_epoch=Time.time;
  public Vector4 Orbit=>new Vector4(_radius,_speed,_phase,_epoch);
  static readonly string[] Properties={"_MainTex","_overtex1","_overtex2","_expression"};
  public PupilOrbitBindings(Shader shader){if(shader==null||!shader.isSupported)throw new InvalidOperationException("瞳円運動シェーダーを使用できません。");_shader=shader;}
  public void Set(float radius,float speed)
  {
   if(speed!=_speed){_phase=(_phase+(Time.time-_epoch)*_speed)%1;_epoch=Time.time;_speed=speed;}
   _radius=radius;foreach(var slot in _slots)Configure(slot);
  }
  void Configure(Slot slot)
  {
   slot.Material.SetVector("_Orbit",new Vector4(_radius,_speed,_phase,_epoch));
   slot.Material.SetVector("_Mapping",new Vector4(slot.Scale.x,slot.Scale.y,slot.Direction,slot.Phase));
  }
  public void Apply(Renderer renderer,int side)
  {
   if(renderer==null)return;var materials=renderer.sharedMaterials;
   for(int index=0;index<materials.Length;index++)
   {
    var mat=materials[index];if(mat==null)continue;
    if(!_saved.Exists(s=>s.Renderer==renderer&&s.Index==index)){var b=new MaterialPropertyBlock();renderer.GetPropertyBlock(b,index);_saved.Add(new Saved{Renderer=renderer,Index=index,Block=b});}
    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,index);var common=new MaterialPropertyBlock();renderer.GetPropertyBlock(common);if(block.isEmpty)renderer.GetPropertyBlock(block);
    var pupil=mat.HasProperty("_MainTex")?mat.GetTextureScale("_MainTex"):Vector2.one;
    foreach(var name in Properties)
    {
     if(!mat.HasProperty(name))continue;int property=Shader.PropertyToID(name);var slot=_slots.Find(s=>s.Renderer==renderer&&s.Index==index&&s.Property==property);
     var source=block.GetTexture(property)??common.GetTexture(property)??mat.GetTexture(property);if(slot!=null&&source==slot.Texture)source=slot.Original;if(source==null)continue;
     if(slot==null)
     {
      slot=new Slot{Renderer=renderer,Index=index,Property=property,Side=side};_slots.Add(slot);slot.Material=new Material(_shader){name="canon pupil orbit"};
      slot.Texture=new CustomRenderTexture(Mathf.Clamp(source.width,64,512),Mathf.Clamp(source.height,64,512),RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
      {name="canon orbit "+name,material=slot.Material,updateMode=CustomRenderTextureUpdateMode.Realtime,initializationMode=CustomRenderTextureUpdateMode.OnLoad,doubleBuffered=false,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
      if(!slot.Texture.Create())throw new InvalidOperationException("瞳円運動テクスチャを作成できません。");slot.Texture.Initialize();
     }
     var scale=mat.GetTextureScale(name);
     PupilOrbitMotion.Mapping(side,scale.x,scale.y,pupil.x,pupil.y,out var x,out var y,out var direction,out var phase);
     slot.Scale=new Vector2(x,y);slot.Direction=direction;slot.Phase=phase;
     slot.Original=source;slot.Material.SetTexture("_Source",source);Configure(slot);slot.Texture.Update();block.SetTexture(property,slot.Texture);
    }
    renderer.SetPropertyBlock(block,index);
   }
  }
  public bool HasPupil(Renderer renderer)=>_slots.Exists(s=>s.Renderer==renderer&&s.Property==Shader.PropertyToID("_MainTex"));
  public void Unbind(bool disposeResources=true)
  {
   foreach(var saved in _saved)if(saved.Renderer!=null&&saved.Index<saved.Renderer.sharedMaterials.Length)saved.Renderer.SetPropertyBlock(saved.Block.isEmpty?null:saved.Block,saved.Index);_saved.Clear();
   if(!disposeResources)return;
   foreach(var slot in _slots){if(slot.Texture!=null){slot.Texture.Release();Release(slot.Texture);}if(slot.Material!=null)Release(slot.Material);}_slots.Clear();
  }
  static void Release(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
  public void Dispose()=>Unbind();
 }
}
